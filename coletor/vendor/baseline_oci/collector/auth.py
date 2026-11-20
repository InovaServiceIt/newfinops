"""Autenticação do coletor (somente leitura).

Detecção automática, na ordem de prioridade definida para o produto:

1. OCI Cloud Shell  -> delegation token (/etc/oci/delegation_token)
2. Config file      -> ~/.oci/config  (API key)
3. Instance principal (execução dentro de uma instância OCI com dynamic group)

Nenhum método aqui modifica a tenancy. O signer/clients resultantes só serão
usados para chamadas List/Get.

Referências oficiais:
- SDK Authentication Methods:
  https://docs.oracle.com/en-us/iaas/Content/API/Concepts/sdk_authentication_methods.htm
- O delegation token do Cloud Shell é lido de /etc/oci/delegation_token e
  passado a InstancePrincipalsDelegationTokenSigner; o signer expõe `region` e
  `tenancy_id` derivados da metadata da instância.
"""

from __future__ import annotations

import base64
import json
import os
from dataclasses import dataclass
from typing import Optional

import oci

DELEGATION_TOKEN_PATH = os.environ.get(
    "OCI_DELEGATION_TOKEN_FILE", "/etc/oci/delegation_token"
)

# --- Resiliência de rede (1.39.0) -----------------------------------------
# Caso de campo (20-07-2026, Cloud Shell em "OCI service network"): chamadas
# para região sem rota penduravam por minutos — o retry DEFAULT do SDK tem
# backoff longo e mascarava a falha. Todos os clients passam a usar:
# - timeout explícito: 5s para conectar, 60s de leitura (folga para a
#   Usage API; conexão pendurada falha em segundos);
# - retry LIMITADO: até 3 tentativas / 60s no total, apenas para 429 e 5xx.
# Falha de rede vira erro não-fatal VISÍVEL no relatório, nunca coleta parada.
CLIENT_TIMEOUT = (5, 60)  # (connect, read) em segundos
_RETRY_LIMITADO = oci.retry.RetryStrategyBuilder(
    max_attempts_check=True, max_attempts=3,
    total_elapsed_time_check=True, total_elapsed_time_seconds=60,
    retry_max_wait_between_calls_seconds=10,
    service_error_check=True,
    service_error_retry_on_any_5xx=True,
    service_error_retry_config={429: []},
).get_retry_strategy()


@dataclass
class AuthContext:
    """Resultado da autenticação, consumido pela camada de descoberta/coleta."""

    method: str  # "cloud_shell" | "config_file" | "instance_principal"
    config: dict
    signer: Optional[object]  # signer do SDK, ou None quando usa só config file
    tenancy_id: str
    home_region: str
    user_id: Optional[str] = None  # OCID do usuário autenticado (quando resolvível)

    def client_kwargs(self, region: Optional[str] = None) -> dict:
        """kwargs para instanciar um client do SDK na região desejada.

        Inclui timeout explícito e retry limitado (1.39.0) — ver constantes
        no topo do módulo. Ponto único: todos os coletores herdam."""
        cfg = dict(self.config)
        if region:
            cfg["region"] = region
        kwargs = {"config": cfg, "timeout": CLIENT_TIMEOUT,
                  "retry_strategy": _RETRY_LIMITADO}
        if self.signer is not None:
            kwargs["signer"] = self.signer
        return kwargs


def _detect_region_from_env() -> Optional[str]:
    for var in ("OCI_REGION", "OCI_CLI_REGION", "OCI_REGION_NAME"):
        value = os.environ.get(var)
        if value:
            return value
    return None


def _detect_tenancy_from_env() -> Optional[str]:
    # No Cloud Shell, o delegation token reflete o tenancy da infraestrutura da
    # Oracle, não o do usuário. O tenancy do usuário vem destas variáveis.
    for var in ("OCI_TENANCY", "OCI_CLI_TENANCY"):
        value = os.environ.get(var)
        if value:
            return value
    return None


def _user_ocid_from_delegation_token(token: str) -> Optional[str]:
    """Extrai o OCID do usuário do delegation token (JWT do Cloud Shell).

    Decodifica apenas o payload (sem verificar assinatura: é leitura local do
    próprio token) e procura recursivamente um claim cujo valor seja um OCID de
    usuário. Robusto ao nome exato do claim.
    """
    try:
        parts = token.strip().split(".")
        if len(parts) < 2:
            return None
        payload = parts[1] + "=" * (-len(parts[1]) % 4)
        data = json.loads(base64.urlsafe_b64decode(payload).decode("utf-8", "ignore"))
    except Exception:  # noqa: BLE001
        return None

    def _find(obj):
        if isinstance(obj, str):
            return obj if obj.startswith("ocid1.user.") else None
        if isinstance(obj, dict):
            for v in obj.values():
                found = _find(v)
                if found:
                    return found
        if isinstance(obj, list):
            for v in obj:
                found = _find(v)
                if found:
                    return found
        return None

    return _find(data)


def resolve_user_name(auth: "AuthContext") -> Optional[str]:
    """Nome IAM do usuário autenticado (get_user). None se indisponível.

    Read-only: uma chamada Get à API de Identity.
    """
    if not auth.user_id:
        return None
    try:
        identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))
        return identity.get_user(auth.user_id).data.name
    except Exception:  # noqa: BLE001
        return None


def authenticate(prefer_config_profile: str = "DEFAULT") -> AuthContext:
    """Resolve credenciais read-only seguindo a ordem de prioridade do produto.

    Levanta RuntimeError com mensagem acionável se nenhum método funcionar.
    """
    # --- 1) Cloud Shell: delegation token ---------------------------------
    if os.path.exists(DELEGATION_TOKEN_PATH):
        try:
            with open(DELEGATION_TOKEN_PATH, "r", encoding="utf-8") as fh:
                token = fh.read()
            signer = oci.auth.signers.InstancePrincipalsDelegationTokenSigner(
                delegation_token=token
            )
            region = _detect_region_from_env() or getattr(signer, "region", None)
            # No Cloud Shell, prioriza OCI_TENANCY (tenancy do usuário); o
            # signer.tenancy_id apontaria para o tenancy da infraestrutura.
            tenancy_id = _detect_tenancy_from_env() or getattr(signer, "tenancy_id", None)
            if not region or not tenancy_id:
                raise RuntimeError(
                    "Delegation token lido, mas região/tenancy não resolvidas. "
                    "Defina OCI_REGION e OCI_TENANCY no ambiente do Cloud Shell."
                )
            return AuthContext(
                method="cloud_shell",
                config={"region": region},
                signer=signer,
                tenancy_id=tenancy_id,
                home_region=region,
                user_id=_user_ocid_from_delegation_token(token),
            )
        except Exception as exc:  # noqa: BLE001 - degrada para o próximo método
            _warn(f"Cloud Shell detectado mas falhou ({exc}); tentando config file.")

    # --- 2) Config file (API key) -----------------------------------------
    config_path = os.environ.get(
        "OCI_CONFIG_FILE", os.path.expanduser("~/.oci/config")
    )
    if os.path.exists(config_path):
        try:
            config = oci.config.from_file(
                file_location=config_path, profile_name=prefer_config_profile
            )
            oci.config.validate_config(config)
            return AuthContext(
                method="config_file",
                config=config,
                signer=None,
                tenancy_id=config["tenancy"],
                home_region=config["region"],
                user_id=config.get("user"),
            )
        except Exception as exc:  # noqa: BLE001
            _warn(f"Config file encontrado mas inválido ({exc}); tentando instance principal.")

    # --- 3) Instance principal --------------------------------------------
    try:
        signer = oci.auth.signers.InstancePrincipalsSecurityTokenSigner()
        region = _detect_region_from_env() or getattr(signer, "region", None)
        tenancy_id = getattr(signer, "tenancy_id", None)
        if not region or not tenancy_id:
            raise RuntimeError("Instance principal sem região/tenancy resolvidas.")
        return AuthContext(
            method="instance_principal",
            config={"region": region},
            signer=signer,
            tenancy_id=tenancy_id,
            home_region=region,
        )
    except Exception as exc:  # noqa: BLE001
        raise RuntimeError(
            "Nenhum método de autenticação funcionou. Opções:\n"
            "  - Rodar no OCI Cloud Shell (recomendado), ou\n"
            "  - Configurar ~/.oci/config (oci setup config), ou\n"
            "  - Executar dentro de instância com dynamic group/policy.\n"
            f"Último erro: {exc}"
        ) from exc


def _warn(msg: str) -> None:
    print(f"[auth][aviso] {msg}")
