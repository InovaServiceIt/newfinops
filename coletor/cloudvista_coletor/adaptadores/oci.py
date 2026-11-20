"""Adaptador do coletor OCI do Maurício (coletor/vendor/baseline_oci — NÃO editado).

- Monta o config do SDK EM MEMÓRIA a partir da credencial decifrada (key_content = PEM;
  nenhuma chave em disco).
- Substitui, só durante a coleta, `collector.auth.authenticate` por uma função que devolve
  esse AuthContext (o `collect.run()` chama `auth_mod.authenticate(...)`).
- O JSON de saída do `run()` vai para um arquivo temporário apagado no `finally`
  (é dado de cliente; não fica no container). A saída de terminal do coletor é descartada.
"""
from __future__ import annotations

import contextlib
import io
import os
import sys
import tempfile
import threading
from pathlib import Path

VENDOR = Path(__file__).resolve().parents[2] / "vendor" / "baseline_oci"

_trava_vendor = threading.Lock()  # o patch de authenticate é global ao módulo vendorizado


def _importar_vendor():
    if str(VENDOR) not in sys.path:
        sys.path.insert(0, str(VENDOR))
    from collector import auth as auth_mod  # noqa: PLC0415
    from collector import collect as collect_mod  # noqa: PLC0415
    from collector.version import COLLECTOR_VERSION  # noqa: PLC0415
    return auth_mod, collect_mod, COLLECTOR_VERSION


def versao_coletor_oci() -> str:
    try:
        return _importar_vendor()[2]
    except Exception:  # noqa: BLE001
        return "desconhecida"


def montar_config(dados: dict) -> dict:
    """Config do SDK OCI a partir do JSON da credencial OCI_API_KEY (função pura)."""
    obrigatorios = ("user_ocid", "tenancy_ocid", "fingerprint", "region", "private_key_pem")
    faltando = [c for c in obrigatorios if not dados.get(c)]
    if faltando:
        raise ValueError(f"credencial OCI incompleta: faltam {', '.join(faltando)}")
    config = {
        "user": dados["user_ocid"],
        "tenancy": dados["tenancy_ocid"],
        "fingerprint": dados["fingerprint"],
        "region": dados["region"],
        "key_content": dados["private_key_pem"],
    }
    if dados.get("passphrase"):
        config["pass_phrase"] = dados["passphrase"]
    return config


def regiao_home(config: dict) -> str:
    """Região home da tenancy (list_region_subscriptions); se não der, a região do config."""
    import oci  # noqa: PLC0415

    try:
        cliente = oci.identity.IdentityClient(config, timeout=(5, 30))
        for r in cliente.list_region_subscriptions(config["tenancy"]).data:
            if getattr(r, "is_home_region", False):
                return r.region_name
    except Exception:  # noqa: BLE001 - a coleta registra o erro real depois
        pass
    return config["region"]


def contexto_auth(config: dict):
    """AuthContext do vendor a partir do config em memória."""
    auth_mod, _, _ = _importar_vendor()
    import oci  # noqa: PLC0415

    oci.config.validate_config(config)
    return auth_mod.AuthContext(
        method="cloudvista_api_key",
        config=config,
        signer=None,
        tenancy_id=config["tenancy"],
        home_region=regiao_home(config),
        user_id=config.get("user"),
    )


@contextlib.contextmanager
def _authenticate_substituido(auth_mod, contexto):
    original = auth_mod.authenticate
    auth_mod.authenticate = lambda prefer_config_profile="DEFAULT": contexto  # noqa: ARG005
    try:
        yield
    finally:
        auth_mod.authenticate = original


def coletar(config: dict, *, com_custo: bool, dias_custo: int, nome_cliente: str | None) -> dict:
    """Roda o coletor do Maurício e devolve o documento (dict)."""
    auth_mod, collect_mod, _ = _importar_vendor()
    contexto = contexto_auth(config)

    fd, caminho = tempfile.mkstemp(prefix="cv-coleta-", suffix=".json")
    os.close(fd)
    try:
        with _trava_vendor, _authenticate_substituido(auth_mod, contexto), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return collect_mod.run(
                caminho, "DEFAULT", None, False,
                discover_only=False,
                client_name=nome_cliente,
                client_site=None,
                executed_by="CloudVista",
                with_cost=com_custo,
                cost_days=dias_custo,
                cost_tags=None,
                strict_regions=False,
            )
    finally:
        with contextlib.suppress(OSError):
            os.remove(caminho)
