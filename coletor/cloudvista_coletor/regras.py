"""Regras puras do coletor (sem banco, sem rede)."""
from __future__ import annotations

import gzip
import hashlib
import json

PROVEDORES_COM_COLETA = {"OCI"}
MENSAGEM_SEM_COLETA = "Coleta deste provedor ainda não disponível (próximas fases)"
MENSAGEM_SEM_CREDENCIAL = "Conta sem credencial ativa"

TIPOS_COLETA = {"COLETA_CUSTO", "COLETA_INVENTARIO", "COLETA_COMPLETA"}
COST_DAYS_PADRAO = 90
COST_DAYS_MAX = 92  # limite da Usage API (ver vendor/collect.py)

# Defaults dos parâmetros (usados se a chave não existir em `parametro`).
PARAMETROS_PADRAO = {
    "coletor.intervalo_poll_seg": (5, 1, 300),
    "coletor.heartbeat_seg": (30, 5, 600),
    "coletor.timeout_teste_seg": (60, 5, 600),
}


def parametro_inteiro(chave: str, valor: object) -> int:
    padrao, minimo, maximo = PARAMETROS_PADRAO[chave]
    try:
        n = int(str(valor).strip())
    except (TypeError, ValueError):
        return padrao
    return n if minimo <= n <= maximo else padrao


def com_custo(tipo_job: str) -> bool:
    """COLETA_INVENTARIO → sem custo; COLETA_CUSTO e COLETA_COMPLETA → com custo."""
    if tipo_job not in TIPOS_COLETA:
        raise ValueError(f"tipo de job sem coleta: {tipo_job}")
    return tipo_job != "COLETA_INVENTARIO"


def dias_de_custo(escopo: dict | None) -> int:
    try:
        n = int((escopo or {}).get("cost_days", COST_DAYS_PADRAO))
    except (TypeError, ValueError):
        return COST_DAYS_PADRAO
    return n if 1 <= n <= COST_DAYS_MAX else COST_DAYS_PADRAO


def compactar_documento(documento: dict) -> tuple[bytes, str]:
    """JSON UTF-8 → gzip. Devolve (conteúdo gzip, sha256 hex do conteúdo gzip gravado)."""
    bruto = json.dumps(documento, ensure_ascii=False, default=str, separators=(",", ":")).encode("utf-8")
    gz = gzip.compress(bruto, compresslevel=6, mtime=0)
    return gz, hashlib.sha256(gz).hexdigest()


def resumo_coleta(documento: dict, duracao_seg: float) -> dict:
    """Só contagens (nada sensível)."""
    recursos = 0
    for regiao in (documento.get("resources_by_region") or {}).values():
        if isinstance(regiao, dict):
            for itens in regiao.values():
                if isinstance(itens, list):
                    recursos += len(itens)
                elif isinstance(itens, dict):
                    recursos += sum(len(v) for v in itens.values() if isinstance(v, list))
    return {
        "recursos": recursos,
        "regioes": len((documento.get("collection") or {}).get("regions_scanned") or []),
        "meses_custo": meses_de_custo(documento.get("cost")),
        "erros_nao_fatais": len(documento.get("errors") or []),
        "duracao_seg": round(float(duracao_seg), 1),
    }


def meses_de_custo(custo: dict | None) -> int:
    """Meses distintos nas séries mensais do coletor (campos month / time_usage_started)."""
    meses: set[str] = set()
    for chave, linhas in (custo or {}).items():
        if not chave.startswith("monthly_by_") or not isinstance(linhas, list):
            continue
        for linha in linhas:
            if isinstance(linha, dict):
                mes = str(linha.get("month") or linha.get("time_usage_started") or "")[:7]
                if mes:
                    meses.add(mes)
    return len(meses)


# Backoff das tentativas de conexão ao banco (segundos): 2 → 5 → 15 → 30 → 60 (teto).
BACKOFF_CONEXAO = (2, 5, 15, 30, 60)


def atraso_reconexao(tentativa: int) -> int:
    """Atraso antes da tentativa N (1 = primeira falha)."""
    if tentativa < 1:
        return BACKOFF_CONEXAO[0]
    return BACKOFF_CONEXAO[min(tentativa, len(BACKOFF_CONEXAO)) - 1]
