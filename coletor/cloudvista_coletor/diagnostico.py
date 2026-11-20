"""Diagnóstico do custo e amostra de avisos para o `resumo` do job (regras puras).

Tudo que sai daqui vai para `job_execucao.resumo` (visível na tela Coletas): só contagens,
nomes de consulta e mensagens curtas sanitizadas. Sem trechos de request/headers, sem OCID de
usuário; OCIDs de compartment/recurso aparecem truncados (ocid1.compartment...abcd).
"""
from __future__ import annotations

import re

from .seguranca import sanitizar

LIMITE_AVISOS = 20
LIMITE_MENSAGEM = 200

# Blocos de custo que o importador usa (nome → como contar as linhas).
BLOCOS_CUSTO = (
    "monthly_by_service", "daily_by_service", "monthly_by_compartment", "daily_by_compartment",
    "monthly_by_resource", "monthly_by_tag",
)
# Escopos de erro (ErrorSink do vendor) que dizem respeito a custo.
_ESCOPOS_CUSTO = ("cost", "usage_api", "cost_reports")

_OCID = re.compile(r"ocid1\.([a-z0-9_-]+)\.[^\s'\",;)\]}]*", re.IGNORECASE)
_URL = re.compile(r"https?://([^/\s'\"]+)[^\s'\"]*", re.IGNORECASE)
# ServiceError do SDK OCI: str() é um dict com status, code, message, request_endpoint, headers...
_SE_MENSAGEM = re.compile(r"""['"]message['"]\s*:\s*(['"])(.*?)\1""", re.DOTALL)
_SE_OPERACAO = re.compile(r"""['"]operation_name['"]\s*:\s*['"]([A-Za-z0-9_]+)['"]""")
_SE_STATUS = re.compile(r"""['"]status['"]\s*:\s*(\d{3})""")
_SE_CODIGO = re.compile(r"""['"]code['"]\s*:\s*['"]([A-Za-z0-9_.-]+)['"]""")


def mascarar_ocids(texto: str) -> str:
    """OCID de usuário some; os demais ficam truncados nos 4 últimos caracteres."""
    def troca(m: re.Match) -> str:
        tipo = m.group(1).lower()
        if tipo in ("user", "apikey", "credential", "authtoken", "customersecretkey"):
            return "ocid1.[omitido]"  # nem o tipo (evita disparar gatilhos do sanitizador)
        return f"ocid1.{tipo}...{m.group(0)[-4:]}"
    return _OCID.sub(troca, texto)


def mensagem_segura(texto: object, limite: int = LIMITE_MENSAGEM) -> str:
    """Extrai só a mensagem de um ServiceError, sem endpoint/headers; mascara OCIDs e URLs."""
    s = str(texto or "")
    m = _SE_MENSAGEM.search(s)
    if m:
        s = m.group(2)
    s = _URL.sub(lambda u: u.group(1), s)
    return sanitizar(mascarar_ocids(s), limite)


def _status_http(erro: dict) -> int | None:
    for valor in (erro.get("error_code"), _primeiro(_SE_STATUS, erro.get("message"))):
        if valor is not None and str(valor).isdigit() and 100 <= int(valor) <= 599:
            return int(valor)
    return None


def _primeiro(regex: re.Pattern, texto: object) -> str | None:
    m = regex.search(str(texto or ""))
    return m.group(1) if m else None


def avisos_amostra(erros: list | None, limite: int = LIMITE_AVISOS) -> list[dict]:
    """Até `limite` avisos do coletor, sanitizados: {area, operacao, status_http, mensagem}."""
    amostra = []
    for erro in (erros or [])[:limite]:
        if not isinstance(erro, dict):
            continue
        mensagem = erro.get("message")
        codigo = erro.get("error_code")
        operacao = _primeiro(_SE_OPERACAO, mensagem) or erro.get("error_type") or ""
        if codigo and not str(codigo).isdigit():
            operacao = f"{operacao} ({codigo})" if operacao else str(codigo)
        elif not codigo and (c := _primeiro(_SE_CODIGO, mensagem)):
            operacao = f"{operacao} ({c})" if operacao else c
        amostra.append({
            "area": sanitizar(erro.get("scope") or "", 80),
            "operacao": sanitizar(operacao, 80) if operacao else "",
            "status_http": _status_http(erro),
            "mensagem": mensagem_segura(mensagem),
        })
    return amostra


def _eh_de_custo(erro: dict) -> bool:
    escopo = str(erro.get("scope") or "").lower()
    return any(escopo == e or escopo.startswith(e + ".") for e in _ESCOPOS_CUSTO)


def _linhas(custo: dict, bloco: str) -> int:
    v = custo.get(bloco)
    return len(v) if isinstance(v, list) else 0


def diagnostico_custo(documento: dict) -> dict:
    """
    status: ok (linhas e nenhuma consulta com falha) | parcial (linhas, mas há falha/truncamento)
            | ausente (nenhuma linha de custo). motivo: texto curto sanitizado.
    """
    custo = documento.get("cost") if isinstance(documento.get("cost"), dict) else None
    erros = [e for e in (documento.get("errors") or []) if isinstance(e, dict) and _eh_de_custo(e)]

    blocos: dict[str, int] = {}
    falhas: list[dict] = []
    if custo:
        blocos = {b: _linhas(custo, b) for b in BLOCOS_CUSTO}
        previsao = custo.get("forecast") if isinstance(custo.get("forecast"), dict) else {}
        creditos = custo.get("credits") if isinstance(custo.get("credits"), dict) else {}
        blocos["forecast"] = len(previsao.get("daily") or [])
        blocos["credits"] = len(creditos.get("monthly") or [])
        # Última ocorrência de cada consulta vale (o vendor re-tenta com profundidade menor).
        ultimas: dict[str, dict] = {}
        for q in custo.get("queries") or []:
            if isinstance(q, dict) and q.get("name"):
                ultimas[str(q["name"])] = q
        for nome, q in ultimas.items():
            if not q.get("ok", True) or q.get("truncated"):
                item = {"name": sanitizar(nome, 120), "ok": bool(q.get("ok", True)), "truncated": bool(q.get("truncated"))}
                if q.get("error"):
                    item["erro"] = mensagem_segura(q["error"])
                falhas.append(item)

    total = sum(blocos.values())
    if total == 0:
        status = "ausente"
        if falhas and falhas[0].get("erro"):
            motivo = f"{falhas[0]['name']}: {falhas[0]['erro']}"
        elif erros:
            e = erros[0]
            http = _status_http(e)
            motivo = f"{e.get('scope')}{f' (HTTP {http})' if http else ''}: {mensagem_segura(e.get('message'))}"
        elif not custo:
            motivo = "Bloco de custo não retornado pelo coletor."
        else:
            motivo = "A Usage API respondeu sem linhas no período."
    elif falhas:
        status = "parcial"
        motivo = f"{len(falhas)} consulta(s) com falha ou truncada(s)."
    else:
        status = "ok"
        motivo = ""

    return {"status": status, "motivo": sanitizar(motivo, LIMITE_MENSAGEM) if motivo else "",
            "blocos": blocos, "queries_falhas": falhas}
