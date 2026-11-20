"""Fluxo do job com conexão FALSA (sem banco) e credencial cifrada de verdade."""
import contextlib
import json
import logging

import pytest

from cloudvista_coletor import regras, sql
from cloudvista_coletor.cripto import ChavesMestras, aad_credencial, aad_dek
from cloudvista_coletor.servico import Job, Servico
from conftest import cifrar_v1, kek_b64

KEK = bytes(range(32))
DEK = bytes([7] * 32)


class Resultado:
    def __init__(self, linha):
        self._linha = linha

    def fetchone(self):
        return self._linha


class ConexaoFalsa:
    """Registra os comandos; responde a CREDENCIAL/PARAMETRO/NOME_CLIENTE."""

    def __init__(self, credencial=None):
        self.credencial = credencial
        self.comandos = []
        self.closed = False

    def execute(self, texto, params=None):
        self.comandos.append((texto, params or {}))
        if texto == sql.CREDENCIAL:
            return Resultado(self.credencial)
        if texto == sql.NOME_CLIENTE:
            return Resultado(("Ultragaz",))
        return Resultado(None)

    @contextlib.contextmanager
    def transaction(self):
        yield

    def com(self, texto):
        return [p for t, p in self.comandos if t == texto]


def _linha_credencial(tipo, provedor, dados, conta=11, cliente=5, status="PENDENTE_TESTE", versao_formato=1):
    dek_cifrada = cifrar_v1(KEK, DEK, aad_dek(cliente, 1))
    conteudo = cifrar_v1(DEK, json.dumps(dados).encode(), aad_credencial(conta, tipo))
    return (99, tipo, versao_formato, status, conteudo, 1, dek_cifrada, cliente, provedor, "id-da-conta")


@pytest.fixture
def servico():
    s = Servico("postgresql://ignorado", ChavesMestras.de_ambiente({"CV_MASTER_KEY_V1": kek_b64(KEK), "CV_MASTER_KEY_ATUAL": "1"}))
    s.parametro = lambda conn, chave: regras.PARAMETROS_PADRAO[chave][0]
    return s


def _job(tipo="TESTE_CREDENCIAL"):
    return Job(id=1, tipo_job=tipo, cliente_id=5, cloud_conta_id=11, escopo={})


def _tudo_gravado(conn) -> str:
    return json.dumps([p for _, p in conn.comandos], default=str)


def test_teste_de_credencial_com_falha_grava_falha_teste_auditoria_e_erro(servico, caplog, monkeypatch):
    from cloudvista_coletor import testes_credencial as t

    class Recusado(Exception):
        status = 401

    def recusa(dados, timeout):
        raise Recusado(f"NotAuthenticated para {dados['secret_access_key']}")

    monkeypatch.setitem(t.TESTES, "AWS_ACCESS_KEY", recusa)
    dados = {"access_key_id": "EXEMPLO-FALSO", "secret_access_key": "SEGREDO_TESTE_aws"}
    conn = ConexaoFalsa(_linha_credencial("AWS_ACCESS_KEY", "AWS", dados))

    with caplog.at_level(logging.INFO):
        servico.processar(conn, _job())

    teste = conn.com(sql.RESULTADO_TESTE)[0]
    assert teste["status"] == "FALHA_TESTE" and teste["resultado"] == "Falha: autenticação recusada (401)"
    aud = conn.com(sql.AUDITORIA)[0]
    assert aud["acao"] == "TESTADA_FALHA"
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "ERRO" and fim["versao"].startswith("cv-")
    assert "SEGREDO_TESTE" not in _tudo_gravado(conn)
    assert all("SEGREDO_TESTE" not in r.getMessage() for r in caplog.records)


def test_teste_de_credencial_ok(servico, monkeypatch):
    from cloudvista_coletor import testes_credencial as t

    monkeypatch.setitem(t.TESTES, "OCI_API_KEY", lambda dados, timeout: "OK: tenancy ultragaz, 4 região(ões)")
    conn = ConexaoFalsa(_linha_credencial("OCI_API_KEY", "OCI", {"private_key_pem": "SEGREDO_TESTE_pem"}))
    servico.processar(conn, _job())

    assert conn.com(sql.RESULTADO_TESTE)[0]["status"] == "ATIVA"
    assert conn.com(sql.AUDITORIA)[0]["acao"] == "TESTADA_OK"
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "CONCLUIDO" and fim["erro"] is None
    assert "SEGREDO_TESTE" not in _tudo_gravado(conn)


@pytest.mark.parametrize("linha", [None, "desativada"])
def test_conta_sem_credencial_ativa(servico, linha):
    credencial = _linha_credencial("AWS_ACCESS_KEY", "AWS", {}, status="DESATIVADA") if linha else None
    conn = ConexaoFalsa(credencial)
    servico.processar(conn, _job("COLETA_CUSTO"))
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "ERRO" and fim["erro"] == "Conta sem credencial ativa"


@pytest.mark.parametrize("provedor,tipo", [("AWS", "AWS_ACCESS_KEY"), ("AZURE", "AZURE_SERVICE_PRINCIPAL"), ("GCP", "GCP_SERVICE_ACCOUNT")])
def test_coleta_de_provedor_sem_coletor_vira_erro(servico, provedor, tipo):
    conn = ConexaoFalsa(_linha_credencial(tipo, provedor, {"x": "SEGREDO_TESTE_y"}))
    servico.processar(conn, _job("COLETA_COMPLETA"))
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "ERRO" and fim["erro"] == regras.MENSAGEM_SEM_COLETA
    assert not conn.com(sql.COLETA_BRUTA)


def test_coleta_oci_grava_coleta_bruta_gzip_e_coletado(servico, monkeypatch):
    from cloudvista_coletor.adaptadores import oci as adaptador

    chamadas = {}

    def coletar_falso(config, *, com_custo, dias_custo, nome_cliente):
        chamadas.update(config=dict(config), com_custo=com_custo, dias=dias_custo, cliente=nome_cliente)
        return {"collection": {"regions_scanned": ["sa-saopaulo-1"]}, "resources_by_region": {"sa-saopaulo-1": {"compute": [1, 2]}},
                "errors": [{"scope": "cost"}]}

    monkeypatch.setattr(adaptador, "coletar", coletar_falso)
    dados = {"user_ocid": "u", "tenancy_ocid": "t", "fingerprint": "f", "region": "sa-saopaulo-1", "private_key_pem": "SEGREDO_TESTE_pem"}
    conn = ConexaoFalsa(_linha_credencial("OCI_API_KEY", "OCI", dados))
    servico.processar(conn, Job(id=1, tipo_job="COLETA_INVENTARIO", cliente_id=5, cloud_conta_id=11, escopo={"cost_days": 30}))

    assert chamadas["com_custo"] is False and chamadas["dias"] == 30 and chamadas["cliente"] == "Ultragaz"
    assert chamadas["config"]["key_content"] == "SEGREDO_TESTE_pem"
    bruta = conn.com(sql.COLETA_BRUTA)[0]
    assert bruta["tamanho"] == len(bruta["conteudo"]) and len(bruta["sha256"]) == 64
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "COLETADO" and fim["coletado"] is True
    resumo = json.loads(fim["resumo"])
    assert resumo == {"recursos": 2, "regioes": 1, "meses_custo": 0, "erros_nao_fatais": 1,
                      "duracao_seg": resumo["duracao_seg"], "avisos_amostra": resumo["avisos_amostra"]}
    assert len(resumo["avisos_amostra"]) == 1 and "custo" not in resumo  # inventário: sem diagnóstico de custo
    # O conteúdo gravado é o documento da coleta (sem a credencial).
    assert "SEGREDO_TESTE" not in json.dumps({k: v for k, v in bruta.items() if k != "conteudo"})
    import gzip
    assert b"SEGREDO_TESTE" not in gzip.decompress(bruta["conteudo"])


def test_falha_fatal_na_coleta_vira_erro_sanitizado(servico, monkeypatch, caplog):
    from cloudvista_coletor.adaptadores import oci as adaptador

    def explode(*a, **k):
        raise RuntimeError("SDK: key_content inválido SEGREDO_TESTE_pem")

    monkeypatch.setattr(adaptador, "coletar", explode)
    dados = {"user_ocid": "u", "tenancy_ocid": "t", "fingerprint": "f", "region": "r", "private_key_pem": "SEGREDO_TESTE_pem"}
    conn = ConexaoFalsa(_linha_credencial("OCI_API_KEY", "OCI", dados))
    with caplog.at_level(logging.INFO):
        servico.processar(conn, _job("COLETA_COMPLETA"))
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "ERRO" and fim["erro"].startswith("RuntimeError")
    assert "SEGREDO_TESTE" not in _tudo_gravado(conn)
    assert all("SEGREDO_TESTE" not in r.getMessage() for r in caplog.records)


def test_aad_errado_nao_decifra_e_vira_erro(servico):
    linha = list(_linha_credencial("AWS_ACCESS_KEY", "AWS", {"a": "b"}, conta=12))  # cifrada para outra conta
    conn = ConexaoFalsa(tuple(linha))
    servico.processar(conn, _job())  # job da conta 11
    fim = conn.com(sql.FINALIZAR)[0]
    assert fim["status"] == "ERRO" and fim["erro"].startswith("Não foi possível decifrar")


def test_sigterm_antes_de_iniciar_devolve_o_job(servico):
    conn = ConexaoFalsa(_linha_credencial("OCI_API_KEY", "OCI", {"private_key_pem": "x"}))
    servico.parar.set()
    servico.processar(conn, _job())
    assert conn.com(sql.DEVOLVER) == [{"id": 1}]
    assert not conn.com(sql.FINALIZAR)
