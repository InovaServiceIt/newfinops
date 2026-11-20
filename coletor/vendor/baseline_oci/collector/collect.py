"""Orquestrador do coletor Baseline OCI (custo zero, 100% read-only).

Fluxo:
  1. Autentica (Cloud Shell -> config file -> instance principal).
  2. Descobre regiões subscritas e a árvore de compartments.
  3. Coleta IAM (global) e, por região, Compute/Networking/Block/Object Storage.
  4. Grava um único JSON auto-contido (inclui referências de proveniência de
     custo e a lista de erros não-fatais).

Uso:
  python -m collector.collect [--out CAMINHO] [--profile DEFAULT]
                              [--regions r1,r2] [--include-deleted]

NUNCA cria, altera ou apaga recursos. Não acessa OCI Monitoring (ver
references.json -> monitoring).
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import sys
import unicodedata

import oci

# Suporta execução como módulo (-m collector.collect) e como script direto.
if __package__ in (None, ""):
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from collector import auth as auth_mod
    from collector import discovery
    from collector.util import ErrorSink
    from collector.version import BUILD_STAMP, COLLECTOR_VERSION, PRODUCT_NAME, SCHEMA_VERSION
    from collector.collectors.iam import collect_iam
    from collector.collectors.compute import collect_compute
    from collector.collectors.network import collect_network
    from collector.collectors.block_storage import collect_block_storage
    from collector.collectors.object_storage import collect_object_storage
    from collector.collectors.database import collect_database
    from collector.collectors.oke import collect_oke
    from collector.collectors.loadbalancer import collect_load_balancers
    from collector.collectors.filestorage import collect_file_storage
    from collector.collectors.security import collect_security
    from collector.collectors.serverless import collect_serverless
    from collector.collectors.scheduler import collect_scheduler
    from collector.collectors.cost import collect_cost
    from collector.collectors.cost_reports import collect_cost_reports
    from collector.collectors.governance import collect_governance
else:
    from . import auth as auth_mod
    from . import discovery
    from .util import ErrorSink
    from .version import BUILD_STAMP, COLLECTOR_VERSION, PRODUCT_NAME, SCHEMA_VERSION
    from .collectors.iam import collect_iam
    from .collectors.compute import collect_compute
    from .collectors.network import collect_network
    from .collectors.block_storage import collect_block_storage
    from .collectors.object_storage import collect_object_storage
    from .collectors.database import collect_database
    from .collectors.oke import collect_oke
    from .collectors.loadbalancer import collect_load_balancers
    from .collectors.filestorage import collect_file_storage
    from .collectors.security import collect_security
    from .collectors.serverless import collect_serverless
    from .collectors.scheduler import collect_scheduler
    from .collectors.cost import collect_cost
    from .collectors.cost_reports import collect_cost_reports
    from .collectors.governance import collect_governance

try:
    from zoneinfo import ZoneInfo
    _BR_TZ = ZoneInfo("America/Sao_Paulo")
except Exception:  # UTC-3 fixo (Brasília sem horário de verão desde 2019)
    _BR_TZ = dt.timezone(dt.timedelta(hours=-3))

REFERENCES_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "references.json")


def _now_utc() -> str:
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _slug(text) -> str:
    """Normaliza para uso em nome de arquivo: ASCII, sem espaços/acentos, hifens."""
    t = unicodedata.normalize("NFKD", str(text)).encode("ascii", "ignore").decode()
    return re.sub(r"[^A-Za-z0-9]+", "-", t).strip("-").lower() or "x"


def _clean_site(s):
    """Extrai só o domínio de entradas tortas: '[txt](url)', 'https://x/p', 'www.x/'."""
    if not s:
        return None
    s = str(s).strip()
    m = re.search(r"\]\(([^)]+)\)", s)  # markdown [texto](url)
    if m:
        s = m.group(1)
    s = re.sub(r"^https?://", "", s, flags=re.I).strip().strip("/")
    s = s.split("/")[0].split("?")[0].strip()
    return s or None


def _load_references() -> dict:
    with open(REFERENCES_PATH, "r", encoding="utf-8") as fh:
        return json.load(fh)


def _preflight_regions(auth, region_names: list, errors, strict: bool = False) -> list:
    """Testa alcance de rede de cada região ANTES da varredura (1.39.0).

    Caso de campo (20-07-2026): Cloud Shell em "OCI service network" só alcança
    a região da sessão; regiões remotas penduravam a coleta em silêncio.
    Uma chamada leve por região, timeout curto e SEM retry: região inalcançável
    é PULADA com aviso no terminal e vira erro não-fatal DECLARADO no relatório
    (escopo preflight.region). Com strict=True, aborta em vez de pular.
    """
    alcancaveis: list = []
    for rn in region_names:
        try:
            kw = auth.client_kwargs(rn)
            kw["timeout"] = (5, 10)
            kw["retry_strategy"] = oci.retry.NoneRetryStrategy()
            oci.identity.IdentityClient(**kw).list_availability_domains(auth.tenancy_id)
            alcancaveis.append(rn)
        except Exception as exc:  # noqa: BLE001
            msg = (f"região {rn} inalcançável desta rede (teste de ~5s) — região PULADA. "
                   "Causa provável: Cloud Shell em 'OCI service network' (alcança só a "
                   "região da sessão) ou bloqueio de rede. Soluções: Public network "
                   "(Actions > Network; requer policy 'use cloud-shell-public-network'), "
                   "rodar com --regions só nas regiões alcançáveis, ou coletar de máquina "
                   "com rota usando config file.")
            print(f"      ⚠  {msg}")
            if strict:
                print("      --strict-regions ativo: abortando sem coletar.")
                raise SystemExit(2)
            e2 = Exception(f"{msg} [{type(exc).__name__}: {str(exc)[:120]}]")
            errors.record(scope="preflight.region", region=rn,
                          compartment_id=auth.tenancy_id, error=e2)
    return alcancaveis


class _Progress:
    """Barra de progresso da coleta, em % de etapas concluídas.

    - TTY (Cloud Shell/terminal): uma única linha atualizada com \\r e limpeza
      ANSI, no formato `[████░░░░]  42% · sa-vinhedo-1 · database`.
    - Sem TTY (pipe/log): uma linha por etapa `[ 7/24]  29% · ...` (sem ANSI).

    O total é determinístico: IAM + (regiões × serviços) + blocos de custo
    (quando --cost) + montagem do JSON. `step(label)` desenha o rótulo do que
    COMEÇOU com o % do que já CONCLUIU; `finish()` fecha em 100%.
    """

    WIDTH = 26

    def __init__(self, total: int) -> None:
        self.total = max(1, int(total))
        self.done_n = 0
        self.tty = sys.stdout.isatty()
        self._open_line = False

    def step(self, label: str) -> None:
        self._draw(label)
        self.done_n = min(self.done_n + 1, self.total)

    def pulse(self, label: str) -> None:
        """Sub-progresso: redesenha o rótulo SEM avançar o contador (1.39.0).

        Só em TTY — em pipe/log viraria uma linha por compartment (ruído)."""
        if self.tty:
            self._draw(label)

    def finish(self) -> None:
        self.done_n = self.total
        self._draw("concluído")
        if self.tty:
            sys.stdout.write("\n")
            sys.stdout.flush()
        self._open_line = False

    def abort(self) -> None:
        """Garante quebra de linha se a coleta morrer com a barra aberta (TTY)."""
        if self.tty and self._open_line:
            sys.stdout.write("\n")
            sys.stdout.flush()
            self._open_line = False

    def _draw(self, label: str) -> None:
        pct = int(round(100.0 * self.done_n / self.total))
        if self.tty:
            fill = int(self.WIDTH * self.done_n / self.total)
            bar = "█" * fill + "░" * (self.WIDTH - fill)
            sys.stdout.write(f"\r\033[2K[{bar}] {pct:3d}% · {label[:60]}")
            sys.stdout.flush()
            self._open_line = True
        else:
            print(f"[{self.done_n:>3}/{self.total}] {pct:3d}% · {label}")


def run(out_path: str | None, profile: str, regions_filter: list[str] | None,
        include_deleted: bool, discover_only: bool = False,
        client_name: str | None = None, client_site: str | None = None,
        executed_by: str | None = None, with_cost: bool = False,
        cost_days: int = 90, cost_tags: list[str] | None = None,
        strict_regions: bool = False) -> dict:
    errors = ErrorSink()
    branding = {"client_name": client_name, "client_site": client_site}

    # 1.46.0 (pedido do time): início/término/duração da execução — no
    # terminal e no JSON (collection.started_at_utc / duration_seconds).
    _inicio_utc = dt.datetime.now(dt.timezone.utc)
    _inicio_br = dt.datetime.now(_BR_TZ)

    print(f"Coletor Baseline OCI v{COLLECTOR_VERSION} · build {BUILD_STAMP}")
    print(f"Início: {_inicio_br.strftime('%d/%m/%Y %H:%M:%S')} (horário de Brasília)")
    print("[1/4] Autenticando (read-only)...")
    auth = auth_mod.authenticate(prefer_config_profile=profile)
    if not executed_by:
        executed_by = auth_mod.resolve_user_name(auth)
    print(f"      método={auth.method}  home_region={auth.home_region}  executor={executed_by or '—'}")
    if auth.method == "cloud_shell":
        print("      dica: em tenancy multi-região, use o Cloud Shell em Public network "
              "(Actions > Network) — na 'OCI service network' só a região da sessão é alcançável.")

    print("[2/4] Descobrindo regiões e compartments...")
    regions = discovery.discover_regions(auth)
    compartments = discovery.discover_compartments(auth)
    comp_ids = (
        [c["id"] for c in compartments]
        if include_deleted
        else discovery.active_compartment_ids(compartments)
    )

    region_names = [r["region_name"] for r in regions]
    if regions_filter:
        region_names = [r for r in region_names if r in regions_filter]
    print(f"      regiões={len(region_names)}  compartments={len(comp_ids)}")

    # Preflight de alcance de rede por região (1.39.0) — pula inalcançáveis
    # com aviso e erro não-fatal declarado; --strict-regions aborta.
    region_names = _preflight_regions(auth, region_names, errors, strict=strict_regions)

    tenancy_name = next((c.get("name") for c in compartments if c.get("is_root")), None)

    if discover_only:
        document = {
            "schema_version": SCHEMA_VERSION,
            "product": PRODUCT_NAME,
            "collector_version": COLLECTOR_VERSION,
            "generated_at_utc": _now_utc(),
            "collection": {
                "auth_method": auth.method,
                "executed_by": executed_by,
                "read_only": True,
                "zero_cost": True,
                "mode": "discover-only",
                "regions_scanned": region_names,
                "compartments_scanned": len(comp_ids),
            },
            "tenancy": {"id": auth.tenancy_id, "name": tenancy_name, "home_region": auth.home_region},
            "regions_subscribed": regions,
            "compartments": compartments,
            "references": _load_references(),
            "errors": errors.as_list(),
            "branding": branding,
        }
        path = out_path or os.path.join(os.getcwd(), "output", "discover-only.json")
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(document, fh, ensure_ascii=False, indent=2, default=str)
        print(f"\nDescoberta concluída (sem coletar recursos). Resumo salvo em: {path}")
        print(f"  Tenancy: {tenancy_name} | Regiões: {len(region_names)} | Compartments: {len(comp_ids)}")
        return document

    service_collectors = [
        ("compute", collect_compute),
        ("network", collect_network),
        ("block_storage", collect_block_storage),
        ("object_storage", collect_object_storage),
        ("database", collect_database),
        ("oke", collect_oke),
        ("load_balancers", collect_load_balancers),
        ("file_storage", collect_file_storage),
        ("security", collect_security),
        ("serverless", collect_serverless),
        ("scheduler", collect_scheduler),
    ]
    # Total de etapas: IAM + governança + regiões×serviços + 7 blocos de custo + JSON.
    total_steps = 2 + len(region_names) * len(service_collectors) + (13 if with_cost else 0) + 1

    print("[3/4] Coletando IAM (global) e recursos por região...")
    prog = _Progress(total_steps)
    cost = None
    resources_by_region: dict = {}
    try:
        prog.step("IAM (global)")
        iam = collect_iam(auth, comp_ids, errors)

        prog.step("governança (tags & budgets)")
        governance = collect_governance(auth, errors)

        for region in region_names:
            region_data: dict = {}
            for key, collect_fn in service_collectors:
                prog.step(f"{region} · {key}")
                if key in ("compute", "block_storage"):
                    # 1.39.0: sub-progresso por compartment nos 2 serviços mais
                    # longos — a barra mostra vida em varreduras grandes.
                    region_data[key] = collect_fn(
                        auth, region, comp_ids, errors,
                        on_pulse=lambda i, n, _k=key, _r=region:
                            prog.pulse(f"{_r} · {_k} · compartment {i}/{n}"))
                else:
                    region_data[key] = collect_fn(auth, region, comp_ids, errors)
            resources_by_region[region] = region_data

        if with_cost:
            cost = collect_cost(auth, compartments, errors, days=cost_days,
                                tag_overrides=cost_tags,
                                on_step=lambda name: prog.step(f"custos · {name}"))
            # 1.38.0: cost reports oficiais (bucket Oracle) — agregados.
            # Falha (ex.: policy ausente) é não-fatal; o bloco só entra
            # no JSON quando algo foi lido.
            reports = collect_cost_reports(
                auth, errors, on_step=lambda name: prog.step(name))
            if cost is not None and reports:
                cost["reports"] = reports

        prog.step("montando JSON")
        prog.finish()
    finally:
        prog.abort()

    print("[4/4] Montando JSON...")
    document = {
        "schema_version": SCHEMA_VERSION,
        "product": PRODUCT_NAME,
        "collector_version": COLLECTOR_VERSION,
        "generated_at_utc": _now_utc(),
        "collection": {
            "auth_method": auth.method,
            "executed_by": executed_by,
            "collector_build": BUILD_STAMP,
            "started_at_utc": _inicio_utc.isoformat(),
            "duration_seconds": round(
                (dt.datetime.now(dt.timezone.utc) - _inicio_utc).total_seconds(), 1),
            "read_only": True,
            "zero_cost": True,
            "utilization_collected": False,
            "cost_collected": bool(cost),
            "regions_scanned": region_names,
            "compartments_scanned": len(comp_ids),
            "include_deleted_compartments": include_deleted,
            "notes": [
                "Apenas operações List/Get. Nenhum recurso criado/alterado/removido.",
                "OCI Monitoring NÃO consultado (retrieval de métricas é cobrado).",
            ] + ([
                "Custos coletados via Usage API (RequestSummarizedUsages): POST de consulta, "
                "read-only e sem SKU de cobrança — exceção documentada em references.usage_api.",
            ] if cost else []),
        },
        "tenancy": {
            "id": auth.tenancy_id,
            "name": tenancy_name,
            "home_region": auth.home_region,
        },
        "regions_subscribed": regions,
        "compartments": compartments,
        "iam": iam,
        "governance": governance,
        "resources_by_region": resources_by_region,
        **({"cost": cost} if cost else {}),
        "references": _load_references(),
        "errors": errors.as_list(),
        "branding": branding,
    }

    if out_path is None:
        out_dir = os.path.join(os.getcwd(), "output")
        os.makedirs(out_dir, exist_ok=True)
        # Nome: baseline_<cliente>_<tenancy>_<dd-mm-aaaa-HHhMM>.json (cliente omitido
        # se não informado). A hora (de Brasília) evita sobrescrever/confundir
        # execuções do mesmo dia (ex.: testar dois pacotes na mesma tenancy).
        date_str = dt.datetime.now(_BR_TZ).strftime("%d-%m-%Y-%Hh%M")
        parts = ["baseline"]
        if client_name:
            parts.append(_slug(client_name))
        parts.append(_slug(tenancy_name or "tenancy"))
        parts.append(date_str)
        out_path = os.path.join(out_dir, "_".join(parts) + ".json")

    with open(out_path, "w", encoding="utf-8") as fh:
        json.dump(document, fh, ensure_ascii=False, indent=2, default=str)

    print(f"\nOK. Baseline salvo em: {out_path}")
    _fim_br = dt.datetime.now(_BR_TZ)
    _dur = _fim_br - _inicio_br
    _h, _resto = divmod(int(_dur.total_seconds()), 3600)
    _m, _s = divmod(_resto, 60)
    print(f"Início:   {_inicio_br.strftime('%d/%m/%Y %H:%M:%S')} (Brasília)")
    print(f"Término:  {_fim_br.strftime('%d/%m/%Y %H:%M:%S')} (Brasília)")
    print(f"Duração:  {_h:02d}:{_m:02d}:{_s:02d}")
    errs = errors.as_list()
    if errs:
        scopes = sorted({str(e.get("scope") or "?") for e in errs})
        print(f"Atenção: {len(errs)} erro(s) não-fatal(is) registrado(s) no JSON.")
        print(f"  Escopos com falha: {', '.join(scopes)}")
        print("  Detalhes no JSON (chave 'errors') e na seção Erros do relatório.")
    return document


def _resolve_branding(name: str | None, site: str | None) -> tuple[str | None, str | None]:
    """Nome do cliente e site. Pergunta interativamente se em TTY e não informados."""
    interactive = sys.stdin.isatty()
    if name is None and interactive:
        try:
            name = input("Nome do cliente (enter para pular): ").strip() or None
        except EOFError:
            name = None
    if site is None and interactive:
        try:
            site = input("Site do cliente p/ logo, ex: cliente.com (enter para pular): ").strip() or None
        except EOFError:
            site = None
    return name, _clean_site(site)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Coletor Baseline OCI (custo zero, read-only).")
    parser.add_argument("--out", default=None, help="Caminho do JSON de saída.")
    parser.add_argument("--profile", default="DEFAULT", help="Profile do config file.")
    parser.add_argument("--regions", default=None, help="Filtro de regiões (csv).")
    parser.add_argument("--strict-regions", dest="strict_regions", action="store_true",
                        help="Aborta se alguma região subscrita estiver inalcançável no preflight "
                             "(padrão: pula a região com aviso e registra erro não-fatal).")
    parser.add_argument("--include-deleted", action="store_true",
                        help="Inclui compartments DELETED na varredura.")
    parser.add_argument("--discover-only", action="store_true",
                        help="Apenas autentica e descobre regiões/compartments (não coleta recursos).")
    parser.add_argument("--cost", action="store_true",
                        help="OPT-IN: coleta custos via Usage API (mesma fonte do Cost Analysis). "
                             "Exige policy 'read usage-report in tenancy'. Consulta read-only, sem cobrança.")
    parser.add_argument("--cost-days", type=int, default=90,
                        help="Janela do grão diário de custos, em dias (padrão 90; máx. 92 = limite da API).")
    parser.add_argument("--cost-tag", action="append", default=None, metavar="NAMESPACE.CHAVE",
                        help="Tag para agrupar custos (repetível). Sem a flag, usa as cost-tracking "
                             "tags da tenancy (até 3).")
    parser.add_argument("--cliente", default=None, help="Nome do cliente (exibido no relatório).")
    parser.add_argument("--site", default=None, help="Site do cliente, ex: cliente.com (usado para o logo no relatório).")
    parser.add_argument("--executado-por", dest="executado_por", default=None,
                        help="Nome de quem executou a coleta (padrão: nome IAM do usuário autenticado).")
    args = parser.parse_args(argv)

    regions_filter = [r.strip() for r in args.regions.split(",")] if args.regions else None
    client_name, client_site = _resolve_branding(args.cliente, args.site)
    try:
        run(args.out, args.profile, regions_filter, args.include_deleted, args.discover_only,
            client_name=client_name, client_site=client_site, executed_by=args.executado_por,
            with_cost=args.cost, cost_days=args.cost_days, cost_tags=args.cost_tag,
            strict_regions=args.strict_regions)
        return 0
    except Exception as exc:  # noqa: BLE001
        print(f"ERRO fatal: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
