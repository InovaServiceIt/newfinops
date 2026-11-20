"""Ajuste MD 0.5: diagnóstico sanitizado da falha de conexão + backoff."""
import logging

import pytest

from cloudvista_coletor import regras
from cloudvista_coletor.cripto import ChavesMestras
from cloudvista_coletor.seguranca import FiltroSanitizador, sanitizar, sanitizar_conexao
from cloudvista_coletor.servico import Servico

URL = "postgresql://svc_cv_collector:SEGREDO_TESTE_X@10.0.0.5:5432/cloudvista?sslmode=require"


class OperationalError(Exception):
    """Simula psycopg.OperationalError (mesmo nome de tipo)."""


def test_url_com_senha_vira_asteriscos():
    s = sanitizar_conexao(f'invalid URI query parameter in "{URL}"')
    assert "SEGREDO_TESTE_X" not in s and "***" in s


def test_valor_literal_de_cv_db_url_e_removido():
    s = sanitizar_conexao(f"erro de parse em {URL}", URL)
    assert "SEGREDO_TESTE_X" not in s and "[CV_DB_URL]" in s


@pytest.mark.parametrize("mensagem", [
    'connection to server at "10.0.0.5", port 5432 failed: FATAL:  password authentication failed for user "svc_cv_collector"',
    'connection to server at "10.0.0.5", port 5432 failed: timeout expired',
    "server does not support SSL, but SSL was required",
    'invalid connection option "sslmod"',
    'missing "=" after "postgresql:/x" in connection info string',
])
def test_diagnostico_legivel_e_preservado(mensagem):
    assert sanitizar_conexao(mensagem) == " ".join(mensagem.split())  # só normaliza espaços
    # o filtro de log também não corta mais "password authentication failed"
    assert "password authentication failed" in sanitizar(mensagem) if "password" in mensagem else True


def test_dsn_password_e_limite():
    s = sanitizar_conexao("host=x password=SEGREDO_TESTE_DSN user=y")
    assert "SEGREDO_TESTE_DSN" not in s and "password=***" in s
    assert len(sanitizar_conexao("x" * 1000)) <= 300


def test_gatilho_generico_ainda_corta_atribuicoes_de_senha():
    assert "SEGREDO_TESTE" not in sanitizar("falhou password=SEGREDO_TESTE_a")
    assert "SEGREDO_TESTE" not in sanitizar("falhou senha: SEGREDO_TESTE_b")


def test_backoff_2_5_15_30_60_com_teto():
    assert [regras.atraso_reconexao(n) for n in range(1, 9)] == [2, 5, 15, 30, 60, 60, 60, 60]


def test_falha_de_conexao_loga_diagnostico_sem_senha_e_com_backoff(caplog):
    servico = Servico(URL, ChavesMestras())
    esperas = []

    def conectar_falha():
        raise OperationalError(
            f'connection to server failed: password authentication failed for user "svc_cv_collector" ({URL})')

    def esperar(segundos):
        esperas.append(segundos)
        if len(esperas) >= 3:
            servico.parar.set()
        return servico.parar.is_set()

    servico.conectar = conectar_falha
    servico.parar.wait = esperar

    logger = logging.getLogger("cloudvista.coletor")
    filtro = FiltroSanitizador()
    logger.addFilter(filtro)
    try:
        with caplog.at_level(logging.INFO, logger="cloudvista.coletor"):
            assert servico.conectar_com_backoff() is False
    finally:
        logger.removeFilter(filtro)

    assert esperas == [2, 5, 15]
    mensagens = [r.getMessage() for r in caplog.records]
    assert len(mensagens) == 3
    assert all("SEGREDO_TESTE_X" not in m for m in mensagens)
    assert all("OperationalError" in m and "password authentication failed" in m for m in mensagens)
    assert any("[CV_DB_URL]" in m or "***" in m for m in mensagens)
