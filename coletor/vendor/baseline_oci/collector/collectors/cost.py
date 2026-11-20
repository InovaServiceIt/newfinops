"""Coleta de custos (opt-in via --cost) — OCI Usage API, a MESMA fonte do
Cost Analysis do console (menu Billing & Cost Management).

EXCEÇÃO DOCUMENTADA à regra "só List/Get": `RequestSummarizedUsages` é um POST
de CONSULTA (query read-only sobre o usage store de billing). Não cria, altera
nem remove recursos e não há SKU de cobrança associado à chamada (proveniência
e racional em `references.json -> usage_api`, com `must_revalidate`). Por isso
o módulo é OPT-IN: só roda quando o operador passa `--cost`.

Permissão IAM adicional (statement oficial da doc de Cost Analysis):
    Allow group <grupo> to read usage-report in tenancy

Janelas validadas na doc oficial (Cost Analysis / Usage API):
- DAILY:   > 24h e <= 3 meses por consulta  -> --cost-days (padrão 90, teto 92)
- MONTHLY: <= 12 meses por consulta         -> últimos 12 meses calendário
- Histórico disponível: até 12 meses; atraso embutido de ~24h (as últimas 24h
  nunca estão completas — o relatório declara isso).
- Forecast nativo (exponential smoothing): exige >= 10 dias contínuos de
  histórico no grão diário; o mês corrente é sempre parcialmente projetado.

Tudo que o relatório precisa é gravado no JSON (linhas agregadas + proveniência
de cada consulta em `queries`). Falha em um bloco não derruba os demais: vira
erro não-fatal com escopo `cost.*`.
"""

from __future__ import annotations

import datetime as dt
from typing import List, Optional

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict

UTC = dt.timezone.utc

# Guarda-corpos de paginação (defensivos; consultas agregadas raramente paginam).
PAGE_LIMIT = 500
MAX_PAGES = 60

# Máximo de cost-tracking tags consultadas quando não vier override por flag.
MAX_TAGS = 3

# Teto do grão diário: a doc limita a 3 meses por consulta.
MAX_DAILY_DAYS = 92

# Profundidade máxima de compartment aceita no groupBy compartmentPath.
# O console expõe até o nível 5; degradamos automaticamente em caso de 400.
MAX_COMPARTMENT_DEPTH = 5


# ---------------------------------------------------------------------------
# Datas (sempre UTC — o usage store é indexado em UTC)
# ---------------------------------------------------------------------------
def _today_utc() -> dt.datetime:
    now = dt.datetime.now(UTC)
    return dt.datetime(now.year, now.month, now.day, tzinfo=UTC)


def _month_floor(d: dt.datetime) -> dt.datetime:
    return dt.datetime(d.year, d.month, 1, tzinfo=UTC)


def _months_back(d: dt.datetime, n: int) -> dt.datetime:
    """Primeiro dia do mês, n meses antes de d."""
    y, m = d.year, d.month - n
    while m <= 0:
        m += 12
        y -= 1
    return dt.datetime(y, m, 1, tzinfo=UTC)


def _next_month(d: dt.datetime) -> dt.datetime:
    return dt.datetime(d.year + (d.month // 12), (d.month % 12) + 1, 1, tzinfo=UTC)


def _iso(d: dt.datetime) -> str:
    return d.strftime("%Y-%m-%dT%H:%M:%SZ")


# ---------------------------------------------------------------------------
# Cliente e execução de consultas
# ---------------------------------------------------------------------------
def _client(auth: AuthContext) -> "oci.usage_api.UsageapiClient":
    kwargs = auth.client_kwargs(auth.home_region)
    # 429/5xx: retry exponencial oficial do SDK (a chamada continua read-only).
    kwargs["retry_strategy"] = oci.retry.DEFAULT_RETRY_STRATEGY
    return oci.usage_api.UsageapiClient(**kwargs)


def _run(client, details) -> tuple[list[dict], bool]:
    """Executa RequestSummarizedUsages paginando por opc-next-page.

    Retorna (itens, truncado). Truncado=True apenas se MAX_PAGES for atingido
    com mais páginas pendentes (registrado na proveniência da consulta).
    """
    items: list[dict] = []
    page: Optional[str] = None
    pages = 0
    while True:
        kw = {"limit": PAGE_LIMIT}
        if page:
            kw["page"] = page
        resp = client.request_summarized_usages(details, **kw)
        data = to_dict(resp.data) or {}
        items.extend(data.get("items") or [])
        page = resp.headers.get("opc-next-page")
        pages += 1
        if not page or pages >= MAX_PAGES:
            break
    return items, bool(page)


def _details(tenant_id: str, granularity: str, start: dt.datetime, end: dt.datetime,
             group_by: Optional[List[str]] = None, query_type: str = "COST",
             compartment_depth: Optional[int] = None, group_by_tag: Optional[list] = None,
             forecast=None):
    models = oci.usage_api.models
    kwargs = dict(
        tenant_id=tenant_id,
        granularity=granularity,
        time_usage_started=start,
        time_usage_ended=end,
        query_type=query_type,
    )
    if group_by is not None:
        kwargs["group_by"] = group_by
    if compartment_depth is not None:
        kwargs["compartment_depth"] = float(compartment_depth)
    if group_by_tag is not None:
        kwargs["group_by_tag"] = group_by_tag
    if forecast is not None:
        kwargs["forecast"] = forecast
    return models.RequestSummarizedUsagesDetails(**kwargs)


# ---------------------------------------------------------------------------
# Redução de linhas (o JSON guarda o que o relatório usa, com proveniência)
# ---------------------------------------------------------------------------
def _bucket(item: dict, granularity: str) -> Optional[str]:
    ts = str(item.get("time_usage_started") or "")
    if not ts:
        return None
    return ts[:7] if granularity == "MONTHLY" else ts[:10]


def _agg_rows(items: list[dict], granularity: str, label_key: str,
              label_fn=None, keep_forecast: bool = False) -> list[dict]:
    """Agrega computed_amount por (bucket, rótulo, moeda[, is_forecast]).

    Linhas com amount nulo/zero são descartadas (não carregam informação e
    incham o JSON). A soma é determinística: mesmo insumo, mesmo resultado.
    """
    acc: dict = {}
    for it in items:
        amt = it.get("computed_amount")
        if amt is None:
            continue
        try:
            amt = float(amt)
        except (TypeError, ValueError):
            continue
        if abs(amt) < 0.005:
            continue
        b = _bucket(it, granularity)
        if not b:
            continue
        label = label_fn(it) if label_fn else it.get(label_key)
        cur = (it.get("currency") or "").strip() or None
        isf = bool(it.get("is_forecast")) if keep_forecast else False
        key = (b, label, cur, isf)
        acc[key] = acc.get(key, 0.0) + amt
    bkey = "month" if granularity == "MONTHLY" else "day"
    out = []
    for (b, label, cur, isf), total in sorted(acc.items(), key=lambda kv: (kv[0][0], str(kv[0][1]))):
        row = {bkey: b, label_key: label, "amount": round(total, 2), "currency": cur}
        if keep_forecast:
            row["is_forecast"] = isf
        out.append(row)
    return out


def _compartment_label(item: dict) -> str:
    return item.get("compartment_path") or item.get("compartment_name") or "(sem compartment)"


def _tag_label(item: dict) -> str:
    for t in item.get("tags") or []:
        if isinstance(t, dict) and (t.get("value") or t.get("key")):
            return t.get("value") or "(sem valor)"
    return "(sem tag)"


def _max_depth(compartments: list[dict]) -> int:
    depth = 1
    for c in compartments or []:
        if c.get("lifecycle_state", "ACTIVE") != "ACTIVE":
            continue
        path = c.get("path") or ""
        depth = max(depth, len([p for p in path.split(" / ") if p]))
    return max(1, min(depth, MAX_COMPARTMENT_DEPTH))


def _parse_tag_overrides(raw: Optional[List[str]], errors: ErrorSink,
                         region: str, tenancy_id: str) -> list[dict]:
    tags = []
    for spec in raw or []:
        if "." in spec:
            ns, key = spec.split(".", 1)
            tags.append({"namespace": ns.strip(), "key": key.strip()})
        else:
            errors.record(scope="cost.tag_override", region=region, compartment_id=tenancy_id,
                          error=ValueError(f"--cost-tag inválido (esperado Namespace.Chave): {spec!r}"))
    return tags


def _discover_cost_tags(auth: AuthContext, errors: ErrorSink) -> list[dict]:
    """Cost-tracking tags da tenancy via IAM (List, custo zero). Melhor esforço."""
    try:
        identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))
        raw = [to_dict(t) for t in paginate(identity.list_cost_tracking_tags, auth.tenancy_id)]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="cost.cost_tracking_tags", region=auth.home_region,
                      compartment_id=auth.tenancy_id, error=exc)
        return []
    tags = []
    for t in raw:
        ns = t.get("tag_namespace_name") or t.get("tag_namespace_id")
        key = t.get("name")
        if ns and key and not str(ns).startswith("ocid1."):
            tags.append({"namespace": str(ns), "key": str(key)})
    return tags


# ---------------------------------------------------------------------------
# Coleta principal
# ---------------------------------------------------------------------------
def collect_cost(auth: AuthContext, compartments: list[dict], errors: ErrorSink,
                 days: int = 90, tag_overrides: Optional[List[str]] = None,
                 on_step=None) -> dict:
    """Coleta a visão de custos da tenancy (estilo Cost Analysis).

    Consultas (todas tenancy-wide, endpoint da home region, queryType COST):
      1. MONTHLY x service           (últimos 12 meses calendário — cobre o YTD)
      2. MONTHLY x compartmentPath   (idem, com compartmentDepth)
      3. DAILY   x service           (janela --cost-days, <= 92 dias)
      4. DAILY   x compartmentPath   (idem)
      5. DAILY   total + forecast até o fim do mês corrente (forecast BASIC)
      6. MONTHLY créditos (queryType ALLCREDIT), quando o contrato tiver
      7. MONTHLY x tag de cost-tracking (até 3 tags, ou --cost-tag Namespace.Chave)
      8. MONTHLY x recurso (resourceId; nomes cruzados com o inventário no relatório)
      9. MONTHLY x SKU (skuPartNumber + skuName; cruza com governance.rate_cards
         no relatório — probe de campo 20-07-2026: part numbers B* casam 1:1)
      10. MONTHLY x região (probe de campo 20-07-2026: total do mês bateu com
          a soma por SKU — sanidade cruzada; 1 região na tenancy de validação)
      11. DAILY x região (feedback de cliente item 5b — gráfico Diário por
          região; mesma dimensão validada da consulta 10, no grão diário)

    `on_step(nome_do_bloco)` é um callback opcional de progresso (barra do
    orquestrador); chamado uma vez no início de cada um dos 11 blocos acima.
    """

    def _tick(name: str) -> None:
        if on_step:
            try:
                on_step(name)
            except Exception:  # noqa: BLE001 - progresso nunca derruba a coleta
                pass

    client = _client(auth)
    region = auth.home_region
    tenancy_id = auth.tenancy_id

    today = _today_utc()
    tomorrow = today + dt.timedelta(days=1)
    days = max(2, min(int(days or 90), MAX_DAILY_DAYS))

    monthly_start = _months_back(today, 11)           # 12 meses calendário, incl. o corrente
    daily_start = today - dt.timedelta(days=days)
    forecast_hist_start = today - dt.timedelta(days=31)
    forecast_end = _next_month(today)                 # projeta até o fim do mês corrente
    depth = _max_depth(compartments)

    result: dict = {
        "source": "usage_api.request_summarized_usages",
        "endpoint_region": region,
        "collected_at_utc": _iso(dt.datetime.now(UTC)),
        "query_type_default": "COST",
        "compartment_depth": depth,
        "windows": {
            "monthly": {"start": _iso(monthly_start), "end": _iso(tomorrow)},
            "daily": {"start": _iso(daily_start), "end": _iso(tomorrow), "days": days},
            "ytd_start": _iso(dt.datetime(today.year, 1, 1, tzinfo=UTC)),
        },
        "currencies": [],
        "monthly_by_service": [],
        "monthly_by_compartment": [],
        "daily_by_service": [],
        "daily_by_compartment": [],
        "forecast": None,
        "credits": None,
        "cost_tracking_tags": [],
        "monthly_by_tag": [],
        "monthly_by_resource": [],
        "monthly_by_sku": [],
        "monthly_by_region": [],
        "daily_by_region": [],
        "queries": [],
        "notes": [
            "Fonte idêntica à do Cost Analysis do console (Usage API / RequestSummarizedUsages).",
            "Atraso embutido de ~24h: as últimas 24h nunca estão completas no usage store.",
            "Valores refletem o rating da assinatura (descontos aplicados), na moeda do contrato.",
            "Consulta read-only: POST de query, sem criação/alteração de recursos e sem SKU de cobrança (ver references.usage_api).",
        ],
    }

    def _log_query(name: str, granularity: str, start: dt.datetime, end: dt.datetime,
                   group_by=None, query_type: str = "COST", extra: Optional[dict] = None,
                   rows: int = 0, truncated: bool = False, ok: bool = True, error: str = None):
        q = {"name": name, "operation": "RequestSummarizedUsages", "granularity": granularity,
             "time_usage_started": _iso(start), "time_usage_ended": _iso(end),
             "group_by": group_by, "query_type": query_type, "rows_kept": rows,
             "truncated": truncated, "ok": ok}
        if extra:
            q.update(extra)
        if error:
            q["error"] = error[:300]
        result["queries"].append(q)

    def _query_block(name: str, granularity: str, start: dt.datetime, end: dt.datetime,
                     group_by=None, query_type: str = "COST", compartment_depth=None,
                     group_by_tag=None, forecast=None, extra: Optional[dict] = None):
        """Executa uma consulta com degradação graciosa; devolve itens crus ou None."""
        depths = [compartment_depth] if compartment_depth is None else \
            sorted({d for d in (compartment_depth, 4, 2) if d <= compartment_depth}, reverse=True)
        last_exc: Optional[Exception] = None
        for d in depths:
            try:
                details = _details(tenancy_id, granularity, start, end, group_by=group_by,
                                   query_type=query_type, compartment_depth=d,
                                   group_by_tag=group_by_tag, forecast=forecast)
                items, truncated = _run(client, details)
                info = dict(extra or {})
                if d is not None:
                    info["compartment_depth"] = d
                    result["compartment_depth"] = d
                _log_query(name, granularity, start, end, group_by, query_type,
                           info, rows=len(items), truncated=truncated)
                return items
            except oci.exceptions.ServiceError as exc:
                last_exc = exc
                # 400 pode ser profundidade não suportada: tenta mais raso.
                if exc.status == 400 and d is not None and d != depths[-1]:
                    continue
                break
            except Exception as exc:  # noqa: BLE001
                last_exc = exc
                break
        errors.record(scope=f"cost.{name}", region=region, compartment_id=tenancy_id, error=last_exc)
        _log_query(name, granularity, start, end, group_by, query_type, extra,
                   ok=False, error=str(last_exc))
        return None

    # --- 1) Mensal por serviço (12 meses; o YTD é derivado destes meses) ----
    _tick("mensal por serviço")
    items = _query_block("monthly_by_service", "MONTHLY", monthly_start, tomorrow, ["service"])
    if items is not None:
        result["monthly_by_service"] = _agg_rows(items, "MONTHLY", "service")

    # --- 2) Mensal por compartment (caminho hierárquico) ---------------------
    _tick("mensal por compartment")
    items = _query_block("monthly_by_compartment", "MONTHLY", monthly_start, tomorrow,
                         ["compartmentPath"], compartment_depth=depth)
    if items is not None:
        result["monthly_by_compartment"] = _agg_rows(
            items, "MONTHLY", "compartment_path", label_fn=_compartment_label)

    # --- 3) Diário por serviço ------------------------------------------------
    _tick("diário por serviço")
    items = _query_block("daily_by_service", "DAILY", daily_start, tomorrow, ["service"])
    if items is not None:
        result["daily_by_service"] = _agg_rows(items, "DAILY", "service")

    # --- 4) Diário por compartment --------------------------------------------
    _tick("diário por compartment")
    items = _query_block("daily_by_compartment", "DAILY", daily_start, tomorrow,
                         ["compartmentPath"], compartment_depth=result["compartment_depth"])
    if items is not None:
        result["daily_by_compartment"] = _agg_rows(
            items, "DAILY", "compartment_path", label_fn=_compartment_label)

    # --- 5) Forecast do mês corrente (grão diário, série única) ---------------
    # CONTRATO DO ENDPOINT (descoberto em campo, 16-07-2026, probe A-G do
    # scripts/diagnose_forecast.py): time_forecast_started DEVE ser IGUAL a
    # time_usage_ended — a própria API revela a regra no 400 da variante F
    # ("usage end time should be equal to the forecast start time") — e o ponto
    # de junção precisa ser ONTEM: com usage_ended=hoje o serviço devolve o 400
    # ENGANOSO "date does not have the right precision: hours" (coerente com o
    # atraso ~24h do usage store); com usage_ended=ontem aceita (variante D
    # validada: 47 linhas, 17 projetadas). Uma única tentativa, exatamente a
    # variante validada; se falhar, o relatório usa a projeção linear rotulada.
    _tick("forecast do mês")
    yesterday = today - dt.timedelta(days=1)
    fc_end = max(forecast_end, today + dt.timedelta(days=1))
    fc_variant = "started_igual_usage_ended_ontem"
    fc_model = oci.usage_api.models.Forecast(
        time_forecast_started=yesterday, time_forecast_ended=fc_end,
        forecast_type="BASIC")
    items = None
    fc_err: Optional[Exception] = None
    try:
        details = _details(tenancy_id, "DAILY", forecast_hist_start, yesterday,
                           query_type="COST", forecast=fc_model)
        items, truncated = _run(client, details)
        _log_query("forecast_daily_total", "DAILY", forecast_hist_start, yesterday,
                   None, "COST",
                   {"forecast_type": "BASIC", "forecast_variant": fc_variant,
                    "time_forecast_ended": _iso(fc_end)},
                   rows=len(items), truncated=truncated)
    except Exception as exc:  # noqa: BLE001
        fc_err = exc
        items = None
    if items is None:
        errors.record(scope="cost.forecast_daily_total", region=region,
                      compartment_id=tenancy_id, error=fc_err)
        _log_query("forecast_daily_total", "DAILY", forecast_hist_start, yesterday,
                   None, "COST", {"forecast_type": "BASIC", "forecast_variant": fc_variant},
                   ok=False, error=str(fc_err))
    else:
        rows = _agg_rows(items, "DAILY", "total", label_fn=lambda _it: "total", keep_forecast=True)
        for r in rows:
            r.pop("total", None)
        result["forecast"] = {
            "forecast_type": "BASIC",
            "forecast_variant": fc_variant,
            "time_forecast_ended": _iso(fc_end),
            "history_days": 31,
            "daily": rows,
            "note": "Exponential smoothing nativo da Usage API; exige >=10 dias contínuos de histórico. "
                    "A projeção começa no fim dos dados reais (started omitido ou = amanhã).",
        }

    # --- 6) Créditos / Universal Credits (ALLCREDIT) ---------------------------
    _tick("créditos")
    items = _query_block("credits_monthly", "MONTHLY", monthly_start, tomorrow,
                         query_type="ALLCREDIT")
    if items is not None:
        rows = _agg_rows(items, "MONTHLY", "total", label_fn=lambda _it: "total")
        for r in rows:
            r.pop("total", None)
        result["credits"] = {
            "query_type": "ALLCREDIT",
            "monthly": rows,
            "note": "Ajustes de crédito + créditos expirados (queryType ALLCREDIT). "
                    "Vazio quando o contrato não tem créditos no período.",
        }

    # --- 7) Tags de cost-tracking ----------------------------------------------
    _tick("tags de custo")
    tags = _parse_tag_overrides(tag_overrides, errors, region, tenancy_id) \
        or _discover_cost_tags(auth, errors)[:MAX_TAGS]
    result["cost_tracking_tags"] = tags
    for tag in tags:
        tag_model = oci.usage_api.models.Tag(namespace=tag["namespace"], key=tag["key"])
        items = _query_block(f"monthly_by_tag.{tag['namespace']}.{tag['key']}", "MONTHLY",
                             monthly_start, tomorrow, group_by_tag=[tag_model],
                             extra={"tag": f"{tag['namespace']}.{tag['key']}"})
        if items is not None:
            rows = _agg_rows(items, "MONTHLY", "tag_value", label_fn=_tag_label)
            for r in rows:
                r["tag_namespace"] = tag["namespace"]
                r["tag_key"] = tag["key"]
            result["monthly_by_tag"].extend(rows)


    # --- 8) Mensal por recurso (visão "hostname") ------------------------------
    # groupBy ["service","resourceId"] traz o serviço junto; fallback para só
    # ["resourceId"] se a combinação for rejeitada (padrão: tentar e degradar).
    _tick("por recurso")
    r_items = None
    r_group = None
    r_err: Optional[Exception] = None
    for group in (["service", "resourceId"], ["resourceId"]):
        try:
            details = _details(tenancy_id, "MONTHLY", monthly_start, tomorrow,
                               group_by=group, query_type="COST")
            r_items, truncated = _run(client, details)
            r_group = group
            _log_query("monthly_by_resource", "MONTHLY", monthly_start, tomorrow,
                       group, "COST", rows=len(r_items), truncated=truncated)
            break
        except Exception as exc:  # noqa: BLE001
            r_err = exc
            r_items = None
    if r_items is None:
        errors.record(scope="cost.monthly_by_resource", region=region,
                      compartment_id=tenancy_id, error=r_err)
        _log_query("monthly_by_resource", "MONTHLY", monthly_start, tomorrow,
                   ["resourceId"], "COST", ok=False, error=str(r_err))
    else:
        acc: dict = {}
        rnames: dict = {}
        rsvcs: dict = {}
        for it in r_items:
            amt = it.get("computed_amount")
            if amt is None:
                continue
            try:
                amt = float(amt)
            except (TypeError, ValueError):
                continue
            if abs(amt) < 0.005:
                continue
            month = str(it.get("time_usage_started") or "")[:7]
            if not month:
                continue
            rid = it.get("resource_id")
            cur_ = (it.get("currency") or "").strip() or None
            acc[(month, rid, cur_)] = acc.get((month, rid, cur_), 0.0) + amt
            if it.get("resource_name") and rid:
                rnames[rid] = it["resource_name"]
            if it.get("service") and rid:
                rsvcs[rid] = it["service"]
        rows = []
        for (month, rid, cur_), total in sorted(acc.items(), key=lambda kv: (kv[0][0], str(kv[0][1]))):
            row = {"month": month, "resource_id": rid, "amount": round(total, 2), "currency": cur_}
            if rid in rsvcs:
                row["service"] = rsvcs[rid]
            if rid in rnames:
                row["resource_name"] = rnames[rid]
            rows.append(row)
        result["monthly_by_resource"] = rows
        result["monthly_by_resource_group_by"] = r_group

    # --- 9) Mensal por SKU (skuPartNumber + skuName) ---------------------------
    # Ponte consumo ↔ rate card: o part number retornado (B*) casa 1:1 com
    # governance.rate_cards (probe de campo 20-07-2026, serviceitoci23:
    # B91962 "Block Volume - Performance Units"). computed_quantity é o
    # consumo FÍSICO na unidade do SKU — vai junto para o relatório.
    _tick("por SKU")
    items = _query_block("monthly_by_sku", "MONTHLY", monthly_start, tomorrow,
                         ["skuPartNumber", "skuName"])
    if items is not None:
        acc_s: dict = {}
        snames: dict = {}
        for it in items:
            amt = it.get("computed_amount")
            qty = it.get("computed_quantity")
            try:
                amt = float(amt) if amt is not None else 0.0
            except (TypeError, ValueError):
                amt = 0.0
            try:
                qty = float(qty) if qty is not None else 0.0
            except (TypeError, ValueError):
                qty = 0.0
            # mantém custo relevante OU uso físico com custo 0 (SKU free/incluído)
            if abs(amt) < 0.005 and qty <= 0:
                continue
            month = str(it.get("time_usage_started") or "")[:7]
            pn = it.get("sku_part_number")
            if not month or not pn:
                continue
            cur_ = (it.get("currency") or "").strip() or None
            k = (month, pn, cur_)
            a, q = acc_s.get(k, (0.0, 0.0))
            acc_s[k] = (a + amt, q + qty)
            if it.get("sku_name"):
                snames[pn] = it["sku_name"]
        result["monthly_by_sku"] = [
            {"month": m, "sku_part_number": pn, "amount": round(a, 2),
             "quantity": round(q, 4), "currency": cur_,
             **({"sku_name": snames[pn]} if pn in snames else {})}
            for (m, pn, cur_), (a, q) in sorted(acc_s.items(),
                                                key=lambda kv: (kv[0][0], str(kv[0][1])))
        ]

    # --- 10) Mensal por região -------------------------------------------------
    # Distribuição regional do custo (multi-region) e sanidade cruzada: no
    # probe de campo (20-07-2026) o total por região do mês fechado bateu com
    # a soma por SKU. Em tenancy de 1 região, o relatório lê como
    # concentração regional (insumo de DR), não como problema.
    _tick("por região")
    items = _query_block("monthly_by_region", "MONTHLY", monthly_start, tomorrow, ["region"])
    if items is not None:
        result["monthly_by_region"] = _agg_rows(items, "MONTHLY", "region")

    # --- 11) Diário por região (feedback de cliente, item 5b) ------------------
    _tick("diário por região")
    items = _query_block("daily_by_region", "DAILY", daily_start, tomorrow, ["region"])
    if items is not None:
        result["daily_by_region"] = _agg_rows(items, "DAILY", "region")

    # Moedas efetivamente observadas (normalmente uma: a do contrato).
    curs = set()
    for coll in ("monthly_by_service", "monthly_by_compartment", "daily_by_service",
                 "daily_by_compartment", "monthly_by_tag", "monthly_by_resource"):
        curs.update(r.get("currency") for r in result[coll] if r.get("currency"))
    if result.get("forecast"):
        curs.update(r.get("currency") for r in result["forecast"]["daily"] if r.get("currency"))
    if result.get("credits"):
        curs.update(r.get("currency") for r in result["credits"]["monthly"] if r.get("currency"))
    result["currencies"] = sorted(c for c in curs if c)

    return result
