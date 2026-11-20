"""Coletor de Cost Reports (bucket Oracle "bling") — 1.38.0.

Baixa os relatórios de custo OFICIAIS que a Oracle publica num bucket
próprio (namespace `bling`, bucket = OCID da tenancy do cliente) e grava
APENAS AGREGADOS no JSON — nunca linhas cruas. Dois formatos, validados em
campo (20-07-2026, serviceitoci23) e na doc oficial:

- FOCUS (FinOps Open Cost & Usage Specification): particionado por dia de
  uso em `FOCUS Reports/AAAA/MM/DD/`. Traz `ListCost`/`BilledCost` → o
  DESCONTO MEDIDO (não inferido). A última partição pode atrasar bem mais
  que as 24h documentadas (campo: 9 dias) — o coletor usa a ÚLTIMA
  disponível e grava o atraso como proveniência.
- Proprietário OCI: `reports/cost-csv/`, arquivos sequenciais (~6h de uso
  cada). Traz `cost/myCostOverage`/`usage/billedQuantityOverage` → o
  OVERAGE DISCRIMINADO por linha, que a Usage API não expõe.

CUSTO ZERO (análise registrada em references.json): a policy IAM é
gratuita; no modelo de cobrança do Object Storage da OCI, armazenamento,
requests e transferência são faturados na tenancy DONA do bucket — a da
Oracle. Não existe "requester pays" na OCI. Download típico < 1 MB.

PRÉ-REQUISITO (manual/permissions.md): policy no root
  define tenancy usage-report as <OCID fixo da Oracle>
  endorse group <grupo> to read objects in tenancy usage-report
Sem a policy, o erro é NÃO-FATAL com mensagem clara.
"""

from __future__ import annotations

import csv
import datetime as dt
import gzip
import io
from typing import Optional

import oci

from ..auth import AuthContext
from ..util import ErrorSink

# OCID fixo da tenancy da Oracle que hospeda os cost reports (doc oficial:
# Billing > Cost Reports > Required IAM Policy). Não é um segredo.
BLING_NAMESPACE = "bling"

# Quantos meses para trás procurar a última partição FOCUS disponível.
_FOCUS_LOOKBACK_MONTHS = 3
# Top N SKUs em overage gravados no JSON (agregados, não linhas).
_TOP_OVERAGE_SKUS = 10


def _f(v) -> float:
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def _month_str(d: dt.date) -> str:
    return f"{d.year:04d}/{d.month:02d}"


def _prev_month(d: dt.date) -> dt.date:
    return (d.replace(day=1) - dt.timedelta(days=1)).replace(day=1)


def _list_names(client, bucket: str, prefix: str) -> list:
    """Lista nomes de objetos (paginado, fields=name)."""
    names: list = []
    start = None
    while True:
        resp = client.list_objects(BLING_NAMESPACE, bucket, prefix=prefix,
                                   fields="name", start=start, limit=1000)
        data = resp.data
        names.extend(o.name for o in (data.objects or []))
        start = getattr(data, "next_start_with", None)
        if not start:
            break
    return names


def _get_csv_rows(client, bucket: str, name: str) -> list:
    """Baixa um objeto .csv.gz e devolve as linhas como dicts."""
    body = client.get_object(BLING_NAMESPACE, bucket, name).data.content
    raw = gzip.decompress(body)
    return list(csv.DictReader(io.StringIO(raw.decode("utf-8", errors="replace"))))


def collect_cost_reports(auth: AuthContext, errors: ErrorSink,
                         on_step=None) -> Optional[dict]:
    """Agregados dos cost reports oficiais; None se nada acessível."""
    def _tick(name: str) -> None:
        if on_step:
            try:
                on_step(name)
            except Exception:  # noqa: BLE001
                pass

    region = auth.home_region
    bucket = auth.tenancy_id
    client = oci.object_storage.ObjectStorageClient(**auth.client_kwargs(region))
    hoje = dt.datetime.now(dt.timezone.utc).date()
    result: dict = {
        "source": f"objectstorage {BLING_NAMESPACE}/<tenancy_ocid> (cost reports oficiais)",
        "collected_at_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "notes": [
            "Agregados calculados na coleta a partir dos CSVs oficiais; linhas cruas não são gravadas.",
            "FOCUS: a última partição disponível pode atrasar dias (campo: 9d) — data e atraso declarados.",
            "Proprietário: cada arquivo cobre ~6h de uso; o mais recente é usado.",
            "Requer policy 'define tenancy usage-report ... / endorse group ... to read objects' (manual).",
        ],
    }
    algum_ok = False

    # ---- FOCUS: última partição disponível ----------------------------------
    _tick("cost reports · FOCUS")
    try:
        particao = None
        arquivos: list = []
        mes = hoje.replace(day=1)
        for _ in range(_FOCUS_LOOKBACK_MONTHS):
            nomes = _list_names(client, bucket, f"FOCUS Reports/{_month_str(mes)}/")
            if nomes:
                # partição = maior dia presente no mês
                dias = sorted({n.split("/")[3] for n in nomes if len(n.split("/")) >= 5})
                particao = f"{_month_str(mes)}/{dias[-1]}"
                arquivos = sorted(n for n in nomes
                                  if n.startswith(f"FOCUS Reports/{particao}/"))
                break
            mes = _prev_month(mes)
        if not particao:
            raise Exception(
                f"nenhuma partição FOCUS nos últimos {_FOCUS_LOOKBACK_MONTHS} meses "
                "(tenancy sem FOCUS reports ou policy sem efeito)")
        billed_total = list_total = 0.0
        rows_n = 0
        moeda = ""
        by_service: dict = {}
        by_category: dict = {}
        for nome in arquivos:
            for r in _get_csv_rows(client, bucket, nome):
                rows_n += 1
                b = _f(r.get("BilledCost"))
                l_ = _f(r.get("ListCost"))
                billed_total += b
                list_total += l_
                moeda = r.get("BillingCurrency") or moeda
                svc = r.get("ServiceName") or "—"
                e = by_service.setdefault(svc, {"billed": 0.0, "list": 0.0})
                e["billed"] += b
                e["list"] += l_
                cat = r.get("ChargeCategory") or "—"
                c = by_category.setdefault(cat, {"billed": 0.0, "rows": 0})
                c["billed"] += b
                c["rows"] += 1
        try:
            part_date = dt.date(*[int(x) for x in particao.split("/")])
            atraso = (hoje - part_date).days
            part_iso = part_date.isoformat()
        except (TypeError, ValueError):
            atraso = None
            part_iso = particao
        desconto_pct = (100.0 * (1 - billed_total / list_total)) if list_total > 0 else None
        result["focus"] = {
            "partition_date": part_iso,
            "lag_days": atraso,
            "files": len(arquivos),
            "rows": rows_n,
            "currency": moeda,
            "billed_total": round(billed_total, 2),
            "list_total": round(list_total, 2),
            "discount_measured_pct": round(desconto_pct, 1) if desconto_pct is not None else None,
            "by_service": [
                {"service": s, "billed": round(v["billed"], 2), "list": round(v["list"], 2)}
                for s, v in sorted(by_service.items(), key=lambda kv: -kv[1]["billed"])
            ],
            "by_charge_category": [
                {"category": c, "billed": round(v["billed"], 2), "rows": v["rows"]}
                for c, v in sorted(by_category.items(), key=lambda kv: -kv[1]["billed"])
            ],
        }
        algum_ok = True
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="cost.reports.focus", region=region,
                      compartment_id=auth.tenancy_id, error=exc)

    # ---- Proprietário: arquivo mais recente ---------------------------------
    _tick("cost reports · nativo")
    try:
        nomes = _list_names(client, bucket, "reports/cost-csv/")
        if not nomes:
            raise Exception("nenhum objeto em reports/cost-csv/ "
                            "(tenancy sem cost reports ou policy sem efeito)")
        ultimo = sorted(nomes)[-1]  # IDs sequenciais com padding fixo → último = mais recente
        rows = _get_csv_rows(client, bucket, ultimo)
        cost_total = ov_cost = 0.0
        moeda = ""
        int_ini = int_fim = None
        ov_by_sku: dict = {}
        for r in rows:
            c = _f(r.get("cost/myCost"))
            o = _f(r.get("cost/myCostOverage"))
            cost_total += c
            ov_cost += o
            moeda = r.get("cost/currencyCode") or moeda
            ini = r.get("lineItem/intervalUsageStart")
            fim = r.get("lineItem/intervalUsageEnd")
            if ini and (int_ini is None or ini < int_ini):
                int_ini = ini
            if fim and (int_fim is None or fim > int_fim):
                int_fim = fim
            if o:
                sku = r.get("cost/productSku") or "—"
                e = ov_by_sku.setdefault(sku, {"overage_cost": 0.0, "overage_qty": 0.0,
                                               "description": ""})
                e["overage_cost"] += o
                e["overage_qty"] += _f(r.get("usage/billedQuantityOverage"))
                if r.get("product/Description") and not e["description"]:
                    e["description"] = r.get("product/Description")
                if r.get("product/description") and not e["description"]:
                    e["description"] = r.get("product/description")
        top = sorted(ov_by_sku.items(), key=lambda kv: -kv[1]["overage_cost"])
        result["native"] = {
            "file": ultimo,
            "rows": len(rows),
            "interval_start": int_ini,
            "interval_end": int_fim,
            "currency": moeda,
            "cost_total": round(cost_total, 2),
            "overage_cost_total": round(ov_cost, 2),
            "overage_pct": round(100.0 * ov_cost / cost_total, 1) if cost_total > 0 else None,
            "overage_by_sku": [
                {"sku": s, "overage_cost": round(v["overage_cost"], 4),
                 "overage_qty": round(v["overage_qty"], 4),
                 "description": v["description"]}
                for s, v in top[:_TOP_OVERAGE_SKUS]
            ],
            "overage_skus_n": len(ov_by_sku),
        }
        algum_ok = True
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="cost.reports.native", region=region,
                      compartment_id=auth.tenancy_id, error=exc)

    return result if algum_ok else None
