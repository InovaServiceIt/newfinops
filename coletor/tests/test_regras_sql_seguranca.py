import gzip
import hashlib
import json
import logging

import pytest

from cloudvista_coletor import regras, sql
from cloudvista_coletor.adaptadores.oci import montar_config
from cloudvista_coletor.seguranca import FiltroSanitizador, descrever_excecao, sanitizar
from cloudvista_coletor.testes_credencial import executar_teste, mensagem_de_falha

PEM = "-----BEGIN PRIVATE KEY-----\nSEGREDO_TESTE_pem\n-----END PRIVATE KEY-----"
DADOS_OCI = {
    "user_ocid": "ocid1.user.oc1..aaaa", "tenancy_ocid": "ocid1.tenancy.oc1..bbbb",
    "fingerprint": "ab:cd:ef:01:23:45:67:89:ab:cd:ef:01:23:45:67:89", "region": "sa-saopaulo-1",
    "private_key_pem": PEM,
}


# ── Config OCI em memória ────────────────────────────────────────────────────

def test_config_oci_em_memoria_com_key_content():
    cfg = montar_config(DADOS_OCI)
    assert cfg == {
        "user": "ocid1.user.oc1..aaaa", "tenancy": "ocid1.tenancy.oc1..bbbb",
        "fingerprint": DADOS_OCI["fingerprint"], "region": "sa-saopaulo-1", "key_content": PEM,
    }
    assert "key_file" not in cfg  # nenhuma chave em disco


def test_config_oci_passphrase_opcional_e_obrigatorios():
    assert montar_config({**DADOS_OCI, "passphrase": "SEGREDO_TESTE_pass"})["pass_phrase"] == "SEGREDO_TESTE_pass"
    with pytest.raises(ValueError) as e:
        montar_config({**DADOS_OCI, "private_key_pem": ""})
    assert "private_key_pem" in str(e.value) and "SEGREDO_TESTE" not in str(e.value)


# ── Regras ───────────────────────────────────────────────────────────────────

def test_tipo_job_para_with_cost():
    assert regras.com_custo("COLETA_CUSTO") is True
    assert regras.com_custo("COLETA_COMPLETA") is True
    assert regras.com_custo("COLETA_INVENTARIO") is False
    with pytest.raises(ValueError):
        regras.com_custo("TESTE_CREDENCIAL")


@pytest.mark.parametrize("escopo,esperado", [
    (None, 90), ({}, 90), ({"cost_days": 30}, 30), ({"cost_days": "45"}, 45),
    ({"cost_days": 0}, 90), ({"cost_days": 93}, 90), ({"cost_days": "x"}, 90),
])
def test_dias_de_custo(escopo, esperado):
    assert regras.dias_de_custo(escopo) == esperado


def test_gzip_e_sha256_do_conteudo_gravado():
    doc = {"tenancy": {"name": "ultragaz"}, "acentuação": "ção"}
    gz, sha = regras.compactar_documento(doc)
    assert sha == hashlib.sha256(gz).hexdigest()
    assert json.loads(gzip.decompress(gz).decode("utf-8")) == doc
    assert regras.compactar_documento(doc) == (gz, sha)  # determinístico (mtime=0)


def test_resumo_so_contagens():
    doc = {
        "collection": {"regions_scanned": ["sa-saopaulo-1", "sa-vinhedo-1"]},
        "resources_by_region": {"sa-saopaulo-1": {"compute": [1, 2, 3], "network": {"vcns": [1], "subnets": [1, 2]}}},
        "cost": {"monthly_by_service": [{"month": "2026-09"}, {"month": "2026-10"}, {"month": "2026-10"}],
                 "daily_by_service": [{"month": "2026-11"}]},
        "errors": [{"scope": "cost"}],
    }
    r = regras.resumo_coleta(doc, 12.34)
    assert r == {"recursos": 6, "regioes": 2, "meses_custo": 2, "erros_nao_fatais": 1, "duracao_seg": 12.3,
                 "avisos_amostra": [{"area": "cost", "operacao": "", "status_http": None, "mensagem": "erro sem descrição"}]}


@pytest.mark.parametrize("chave,valor,esperado", [
    ("coletor.intervalo_poll_seg", None, 5), ("coletor.intervalo_poll_seg", "10", 10),
    ("coletor.heartbeat_seg", "abc", 30), ("coletor.timeout_teste_seg", "9999", 60),
])
def test_parametros_com_default(chave, valor, esperado):
    assert regras.parametro_inteiro(chave, valor) == esperado


def test_provedores_sem_coleta():
    assert regras.PROVEDORES_COM_COLETA == {"OCI"}
    assert regras.MENSAGEM_SEM_COLETA == "Coleta deste provedor ainda não disponível (próximas fases)"


# ── SQL ──────────────────────────────────────────────────────────────────────

def test_sql_do_claim():
    assert "FOR UPDATE SKIP LOCKED" in sql.CLAIM
    assert "ORDER BY prioridade, criado_em" in sql.CLAIM
    assert "status = 'PENDENTE'" in sql.CLAIM
    assert "tentativas = tentativas + 1" in sql.CLAIM
    assert "%(executor)s" in sql.CLAIM


def test_sql_sem_select_estrela_e_parametrizado():
    for nome in dir(sql):
        texto = getattr(sql, nome)
        if isinstance(texto, str) and not nome.startswith("_"):
            assert "SELECT *" not in texto.upper(), nome
            assert "{" not in texto, nome  # nada de f-string/format
    assert "'COLETOR'" in sql.AUDITORIA and "NULL" in sql.AUDITORIA
    assert "'JSON_GZIP'" in sql.COLETA_BRUTA
    assert "AND status = 'EXECUTANDO'" in sql.FINALIZAR and "AND status = 'EXECUTANDO'" in sql.HEARTBEAT


# ── Sanitização ──────────────────────────────────────────────────────────────

@pytest.mark.parametrize("texto", [
    "Falha: config key_content=SEGREDO_TESTE_a",
    "erro no request Authorization: Signature SEGREDO_TESTE_b",
    f"chave inválida {PEM}",
    "client_secret=SEGREDO_TESTE_c inválido",
    "conexão recusada password=SEGREDO_TESTE_d",
    "token=SEGREDO_TESTE_e expirado",
    "aws_secret SEGREDO_TESTE_f",
])
def test_sanitizar_remove_trechos_sensiveis(texto):
    s = sanitizar(texto)
    assert "SEGREDO_TESTE" not in s and "BEGIN" not in s


def test_sanitizar_limita_tamanho_e_sequencias_longas():
    assert len(sanitizar("x " * 1000, 300)) <= 300
    assert "[omitido]" in sanitizar("valor " + "A" * 64)


def test_filtro_de_log_sanitiza_e_descarta_traceback(caplog):
    logger = logging.getLogger("teste.sanitizacao")
    logger.addFilter(FiltroSanitizador())
    with caplog.at_level(logging.INFO, logger="teste.sanitizacao"):
        try:
            raise RuntimeError("falhou com secret=SEGREDO_TESTE_log")
        except RuntimeError:
            logger.exception("Erro: %s", "key_content SEGREDO_TESTE_arg")
    assert caplog.records and all("SEGREDO_TESTE" not in r.getMessage() for r in caplog.records)
    assert all(r.exc_info is None for r in caplog.records)


class _ErroSdk(Exception):
    def __init__(self, msg, status=None):
        super().__init__(msg)
        self.status = status


def test_mensagens_de_falha_do_teste_nao_vazam_segredo():
    assert mensagem_de_falha(_ErroSdk("NotAuthenticated SEGREDO_TESTE_x", 401)) == "Falha: autenticação recusada (401)"
    assert "SEGREDO_TESTE" not in mensagem_de_falha(_ErroSdk("boom SEGREDO_TESTE_y", 500))
    assert "SEGREDO_TESTE" not in descrever_excecao(_ErroSdk("detalhe private_key SEGREDO_TESTE_z", 400))


def test_executar_teste_captura_excecao_e_timeout(monkeypatch):
    from cloudvista_coletor import testes_credencial as t

    def explode(dados, timeout):
        raise _ErroSdk(f"request falhou com {dados['client_secret']}", 401)

    monkeypatch.setitem(t.TESTES, "AZURE_SERVICE_PRINCIPAL", explode)
    ok, msg = executar_teste("AZURE_SERVICE_PRINCIPAL", {"client_secret": "SEGREDO_TESTE_azure"}, 5)
    assert not ok and msg == "Falha: autenticação recusada (401)"

    def lento(dados, timeout):
        import time
        time.sleep(3)
        return "OK"

    monkeypatch.setitem(t.TESTES, "GCP_SERVICE_ACCOUNT", lento)
    ok, msg = executar_teste("GCP_SERVICE_ACCOUNT", {}, 1)
    assert not ok and msg == "Falha: tempo esgotado (1s)"

    assert executar_teste("SENHA", {}, 1) == (False, "Falha: tipo de credencial desconhecido")
