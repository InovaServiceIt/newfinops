"""Diagnóstico de custo e amostra de avisos no resumo (sanitização incluída)."""
from cloudvista_coletor import regras
from cloudvista_coletor.diagnostico import avisos_amostra, diagnostico_custo, mascarar_ocids, mensagem_segura

# Formato real do str(oci.exceptions.ServiceError): dict com endpoint, headers e request id.
SERVICE_ERROR_404 = (
    "{'target_service': 'usage_api', 'status': 404, 'code': 'NotAuthorizedOrNotFound', "
    "'opc-request-id': 'ABCDEF0123456789/ABCDEF0123456789/ABCDEF0123456789', "
    "'message': 'Authorization failed or requested resource not found.', "
    "'operation_name': 'request_summarized_usages', 'timestamp': '2026-11-20T21:00:00.000000+00:00', "
    "'client_version': 'Oracle-PythonSDK/2.150.0', 'request_endpoint': "
    "'POST https://usageapi.sa-saopaulo-1.oci.oraclecloud.com/20200107/usage', "
    "'logging_tips': 'To get more info on the failing request, refer to https://docs.oracle.com/x', "
    "'troubleshooting_tips': 'See https://docs.oracle.com/iaas/Content/API/References/apierrors.htm'}"
)

OCID_USER = "ocid1.user.oc1.." + "a" * 60
OCID_COMP = "ocid1.compartment.oc1.." + "b" * 56 + "wxyz"


def _erro(scope, message, code="404", tipo="ServiceError"):
    return {"scope": scope, "region": "sa-saopaulo-1", "compartment_id": OCID_COMP,
            "error_type": tipo, "error_code": code, "message": message}


def _doc_custo_vazio():
    return {
        "cost": {
            "monthly_by_service": [], "daily_by_service": [], "monthly_by_compartment": [],
            "daily_by_compartment": [], "monthly_by_resource": [], "monthly_by_tag": [],
            "forecast": None, "credits": None,
            "queries": [
                {"name": "monthly_by_service", "ok": False, "truncated": False, "error": SERVICE_ERROR_404},
                {"name": "daily_by_service", "ok": False, "truncated": False, "error": SERVICE_ERROR_404},
            ],
            "notes": ["Fonte idêntica à do Cost Analysis do console."],
        },
        "errors": [_erro("cost.monthly_by_service", SERVICE_ERROR_404), _erro("compute", "timeout", code=None, tipo="RequestException")],
    }


def test_custo_ausente_com_motivo_da_usage_api():
    d = diagnostico_custo(_doc_custo_vazio())
    assert d["status"] == "ausente"
    assert "monthly_by_service" in d["motivo"]
    assert "Authorization failed or requested resource not found." in d["motivo"]
    assert d["blocos"]["monthly_by_service"] == 0
    assert d["blocos"]["forecast"] == 0
    assert [q["name"] for q in d["queries_falhas"]] == ["monthly_by_service", "daily_by_service"]
    assert d["queries_falhas"][0]["ok"] is False


def test_sem_bloco_cost_usa_o_erro_de_escopo_custo():
    doc = {"errors": [_erro("usage_api", SERVICE_ERROR_404)]}
    d = diagnostico_custo(doc)
    assert d["status"] == "ausente"
    assert d["motivo"].startswith("usage_api (HTTP 404): Authorization failed")
    assert d["blocos"] == {} and d["queries_falhas"] == []


def test_sem_bloco_e_sem_erro():
    assert diagnostico_custo({})["motivo"] == "Bloco de custo não retornado pelo coletor."


def test_custo_parcial_com_truncada_e_ultima_ocorrencia():
    doc = {"cost": {
        "monthly_by_service": [{"month": "2026-11"}], "daily_by_compartment": [{"day": "2026-11-01"}],
        "forecast": {"daily": [{"day": "2026-11-30"}]}, "credits": {"monthly": []},
        "queries": [
            {"name": "monthly_by_resource", "ok": False, "truncated": True},
            {"name": "monthly_by_resource", "ok": True, "truncated": False},   # retentativa venceu
            {"name": "daily_by_compartment", "ok": True, "truncated": True},
        ],
    }}
    d = diagnostico_custo(doc)
    assert d["status"] == "parcial"
    assert d["queries_falhas"] == [{"name": "daily_by_compartment", "ok": True, "truncated": True}]
    assert d["blocos"]["forecast"] == 1
    assert d["motivo"] == "1 consulta(s) com falha ou truncada(s)."


def test_custo_ok():
    d = diagnostico_custo({"cost": {"monthly_by_service": [{"month": "2026-11"}], "queries": [{"name": "monthly_by_service", "ok": True}]}})
    assert (d["status"], d["motivo"]) == ("ok", "")


def test_amostra_sanitizada_sem_request_nem_headers():
    erros = [_erro("cost.monthly_by_service", SERVICE_ERROR_404)]
    a = avisos_amostra(erros)[0]
    assert a == {"area": "cost.monthly_by_service", "operacao": "request_summarized_usages",
                 "status_http": 404, "mensagem": "Authorization failed or requested resource not found."}


def test_amostra_limita_a_20_e_200_caracteres():
    erros = [_erro("compute", "x " * 500, code=None, tipo="RequestException") for _ in range(30)]
    a = avisos_amostra(erros)
    assert len(a) == 20
    assert all(len(i["mensagem"]) <= 200 for i in a)
    assert a[0]["operacao"] == "RequestException" and a[0]["status_http"] is None


def test_amostra_mascara_ocids_e_segredos():
    msg = f"user {OCID_USER} sem acesso ao compartment {OCID_COMP}; key_content=-----BEGIN PRIVATE KEY-----abc"
    a = avisos_amostra([_erro("iam", msg, code="NotAuthenticated")])[0]
    assert "a" * 20 not in a["mensagem"] and "ocid1.user" not in a["mensagem"]
    assert "ocid1.compartment...wxyz" in a["mensagem"]
    assert "BEGIN" not in a["mensagem"] and "[conteúdo omitido]" in a["mensagem"]
    assert a["operacao"] == "ServiceError (NotAuthenticated)"


def test_mascarar_ocids():
    assert mascarar_ocids(f"x {OCID_COMP}, y") == "x ocid1.compartment...wxyz, y"
    assert mascarar_ocids(OCID_USER) == "ocid1.[omitido]"


def test_mensagem_segura_tira_url_e_header_de_assinatura():
    s = mensagem_segura("falha em https://usageapi.example.com/20200107/usage?x=1 Authorization: Signature version=\"1\",keyId=\"abc\"")
    assert "usageapi.example.com" in s and "/20200107" not in s
    assert "keyId" not in s


def test_resumo_com_custo_e_meses_so_do_monthly_by_service():
    doc = _doc_custo_vazio()
    doc["cost"]["monthly_by_compartment"] = [{"month": "2026-10"}]
    r = regras.resumo_coleta(doc, 1.0, com_custo=True)
    assert r["meses_custo"] == 0
    assert r["custo"]["status"] == "parcial"            # compartment tem linha, service falhou
    assert len(r["avisos_amostra"]) == 2
    assert "custo" not in regras.resumo_coleta(doc, 1.0)  # inventário: sem diagnóstico de custo
