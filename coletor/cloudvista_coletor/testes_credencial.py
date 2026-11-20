"""TESTE_CREDENCIAL: chamadas mínimas e somente leitura por tipo de credencial.

Cada teste devolve (ok, mensagem curta). Mensagens de falha são sanitizadas
(nunca trazem o segredo nem trechos do request).
"""
from __future__ import annotations

import concurrent.futures
from typing import Callable

from .seguranca import LIMITE_TESTE, sanitizar

ESCOPO_GCP = "https://www.googleapis.com/auth/cloud-platform.read-only"
ESCOPO_AZURE = "https://management.azure.com/.default"


def _ultimos(s: str, n: int = 4) -> str:
    return s[-n:] if s else "?"


def testar_oci(dados: dict, timeout: int) -> str:
    import oci  # noqa: PLC0415

    from .adaptadores.oci import montar_config  # noqa: PLC0415

    config = montar_config(dados)
    oci.config.validate_config(config)
    identidade = oci.identity.IdentityClient(config, timeout=(5, timeout))
    tenancy = identidade.get_tenancy(config["tenancy"]).data
    regioes = identidade.list_region_subscriptions(config["tenancy"]).data
    return f"OK: tenancy {tenancy.name}, {len(regioes)} região(ões)"


def _cliente_sts(chave: str, segredo: str, timeout: int, token: str | None = None):
    import boto3  # noqa: PLC0415
    from botocore.config import Config  # noqa: PLC0415

    return boto3.client(
        "sts",
        aws_access_key_id=chave,
        aws_secret_access_key=segredo,
        aws_session_token=token,
        region_name="us-east-1",
        config=Config(connect_timeout=5, read_timeout=timeout, retries={"max_attempts": 2}),
    )


def testar_aws_chave(dados: dict, timeout: int) -> str:
    ident = _cliente_sts(dados["access_key_id"], dados["secret_access_key"], timeout).get_caller_identity()
    return f"OK: conta {ident['Account']}"


def testar_aws_role(dados: dict, timeout: int) -> str:
    sts = _cliente_sts(dados["access_key_id"], dados["secret_access_key"], timeout)
    parametros = {"RoleArn": dados["role_arn"], "RoleSessionName": "cloudvista-teste", "DurationSeconds": 900}
    if dados.get("external_id"):
        parametros["ExternalId"] = dados["external_id"]
    cred = sts.assume_role(**parametros)["Credentials"]
    ident = _cliente_sts(cred["AccessKeyId"], cred["SecretAccessKey"], timeout, cred["SessionToken"]).get_caller_identity()
    papel = dados["role_arn"].rsplit("/", 1)[-1]
    return f"OK: role {papel} assumido na conta {ident['Account']}"


def testar_azure(dados: dict, timeout: int) -> str:  # noqa: ARG001 - timeout aplicado pelo executor
    from azure.identity import ClientSecretCredential  # noqa: PLC0415

    cred = ClientSecretCredential(dados["tenant_id"], dados["client_id"], dados["client_secret"])
    try:
        cred.get_token(ESCOPO_AZURE)
    finally:
        cred.close()
    return f"OK: token emitido para o app …{_ultimos(dados['client_id'])}"


def testar_gcp(dados: dict, timeout: int) -> str:  # noqa: ARG001
    import json  # noqa: PLC0415

    from google.auth.transport.requests import Request  # noqa: PLC0415
    from google.oauth2 import service_account  # noqa: PLC0415

    info = json.loads(dados["service_account_json"])
    cred = service_account.Credentials.from_service_account_info(info, scopes=[ESCOPO_GCP])
    cred.refresh(Request())
    return f"OK: service account {info.get('client_email', '?')}"


TESTES: dict[str, Callable[[dict, int], str]] = {
    "OCI_API_KEY": testar_oci,
    "AWS_ACCESS_KEY": testar_aws_chave,
    "AWS_ASSUME_ROLE": testar_aws_role,
    "AZURE_SERVICE_PRINCIPAL": testar_azure,
    "GCP_SERVICE_ACCOUNT": testar_gcp,
}


def mensagem_de_falha(exc: BaseException) -> str:
    """Classifica a falha em uma frase curta e segura."""
    status = getattr(exc, "status", None)
    resposta = getattr(exc, "response", None)
    if status is None and isinstance(resposta, dict):  # botocore ClientError
        status = (resposta.get("ResponseMetadata") or {}).get("HTTPStatusCode")
        codigo = (resposta.get("Error") or {}).get("Code")
    else:
        codigo = getattr(exc, "code", None)
    nome = type(exc).__name__
    if status in (401, 403) or nome in ("ClientAuthenticationError", "RefreshError") or codigo in (
        "InvalidClientTokenId", "SignatureDoesNotMatch", "AccessDenied", "NotAuthenticated", "UnrecognizedClientException",
    ):
        rotulo = f" ({status})" if status else ""
        return f"Falha: autenticação recusada{rotulo}"
    if nome in ("InvalidPrivateKey", "InvalidKeyFilePath", "MissingPrivateKeyPassphrase", "InvalidConfig", "ValueError", "JSONDecodeError"):
        return "Falha: credencial inválida ou incompleta"
    if nome in ("ConnectTimeout", "ReadTimeout", "ConnectTimeoutError", "ReadTimeoutError", "TimeoutError", "RequestException"):
        return "Falha: sem resposta do provedor (rede/timeout)"
    return sanitizar(f"Falha: {nome}" + (f" ({status})" if status else ""), LIMITE_TESTE)


def executar_teste(tipo: str, dados: dict, timeout: int) -> tuple[bool, str]:
    """Roda o teste do tipo com limite de tempo. Nunca levanta exceção."""
    teste = TESTES.get(tipo)
    if teste is None:
        return False, "Falha: tipo de credencial desconhecido"
    executor = concurrent.futures.ThreadPoolExecutor(max_workers=1, thread_name_prefix="teste-credencial")
    try:
        futuro = executor.submit(teste, dados, timeout)
        try:
            return True, sanitizar(futuro.result(timeout=timeout), LIMITE_TESTE)
        except concurrent.futures.TimeoutError:
            return False, f"Falha: tempo esgotado ({timeout}s)"
        except Exception as exc:  # noqa: BLE001
            return False, mensagem_de_falha(exc)
    finally:
        executor.shutdown(wait=False, cancel_futures=True)
