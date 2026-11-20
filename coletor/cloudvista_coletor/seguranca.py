"""Sanitização de mensagens (equivalente ao LogSeguro da API).

Mensagens de exceção do SDK podem trazer trechos do request. Antes de gravar em
`erro`/`teste_resultado`/`resumo` ou logar: corta tudo a partir de palavras sensíveis,
remove blocos PEM, mascara sequências longas parecidas com segredo e limita o tamanho.
"""
from __future__ import annotations

import logging
import re

LIMITE_TESTE = 300
LIMITE_ERRO = 1000

# A partir da primeira ocorrência destas palavras (sem diferenciar maiúsculas), o resto é descartado.
# "password"/"senha" só disparam como atribuição (password=..., senha: ...): assim mensagens de
# diagnóstico como 'password authentication failed for user "x"' continuam legíveis.
_GATILHOS = re.compile(
    r"(key_content|pass_?phrase|secret|password\s*[=:]|passwd\s*[=:]|senha\s*[=:]|private|-----BEGIN|authorization|"
    r"x-amz-security-token|signature|credential|token=|client_secret|access_key)",
    re.IGNORECASE,
)

# Credenciais embutidas em URL (postgresql://usuario:senha@host) e em DSN (password=...).
_URL_COM_SENHA = re.compile(r"([a-z][a-z0-9+.-]*://[^\s:/@]+:)[^\s@]*@", re.IGNORECASE)
_DSN_SENHA = re.compile(r"(password\s*=\s*)('[^']*'|\S+)", re.IGNORECASE)
# Sequências longas (base64/hex) que podem ser material de chave.
_LONGO = re.compile(r"[A-Za-z0-9+/=_-]{40,}")


def sanitizar(texto: object, limite: int = LIMITE_ERRO) -> str:
    """Devolve uma versão segura e curta de `texto` (nunca levanta exceção)."""
    try:
        s = str(texto)
    except Exception:  # noqa: BLE001
        return "erro sem descrição"
    m = _GATILHOS.search(s)
    if m:
        s = s[: m.start()].rstrip(" :=,;\"'([{") + " [conteúdo omitido]"
    s = _LONGO.sub("[omitido]", s)
    s = " ".join(s.split())  # sem quebras de linha
    if len(s) > limite:
        s = s[: max(limite - 1, 0)] + "…"
    return s or "erro sem descrição"


def descrever_excecao(exc: BaseException, limite: int = LIMITE_ERRO) -> str:
    """Tipo + mensagem sanitizada (status HTTP quando o SDK expõe)."""
    status = getattr(exc, "status", None) or getattr(getattr(exc, "response", None), "status_code", None)
    base = type(exc).__name__ + (f" ({status})" if status else "")
    msg = sanitizar(exc, limite)
    return sanitizar(f"{base}: {msg}", limite)


class FiltroSanitizador(logging.Filter):
    """Defesa em profundidade: sanitiza toda mensagem de log e descarta tracebacks."""

    def filter(self, record: logging.LogRecord) -> bool:
        try:
            mensagem = record.getMessage()
        except Exception:  # noqa: BLE001
            mensagem = str(record.msg)
        record.msg = sanitizar(mensagem, LIMITE_ERRO)
        record.args = ()
        if record.exc_info:
            record.msg += f" | {type(record.exc_info[1]).__name__}"
            record.exc_info = None
            record.exc_text = None
        return True


def sanitizar_conexao(texto: object, url: str | None = None, limite: int = LIMITE_TESTE) -> str:
    """Mensagem de falha de conexão com diagnóstico, sem segredo.

    A mensagem do libpq não traz a senha, mas pode ecoar a URL em erro de parse: remove o valor
    literal de CV_DB_URL, troca a senha de qualquer `scheme://user:senha@` e de `password=` por ***
    e corta no limite. Mantém textos como 'password authentication failed for user "x"'.
    """
    try:
        s = str(texto)
    except Exception:  # noqa: BLE001
        return "erro sem descrição"
    if url:
        s = s.replace(url, "[CV_DB_URL]")
    s = _URL_COM_SENHA.sub(r"\1***@", s)
    s = _DSN_SENHA.sub(r"\1***", s)
    s = " ".join(s.split())
    if len(s) > limite:
        s = s[: max(limite - 1, 0)] + "…"
    return s or "erro sem descrição"
