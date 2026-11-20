"""Coletor de Segurança (por região): KMS (vaults + keys) e Bastion.

KMS exige duas etapas: listar os vaults (KmsVaultClient, endpoint regional) e,
para cada vault ativo, listar as chaves no seu endpoint de gerenciamento
(KmsManagementClient apontado para o management_endpoint do vault). Apenas List.
Coleta só metadados (nome, algoritmo, proteção); nunca acessa o crypto endpoint.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_security(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    vault_cli = oci.key_management.KmsVaultClient(**auth.client_kwargs(region))
    bastion_cli = oci.bastion.BastionClient(**auth.client_kwargs(region))
    cert_cli = oci.certificates_management.CertificatesManagementClient(
        **auth.client_kwargs(region))
    result: dict = {"vaults": [], "keys": [], "bastions": [], "certificates": []}

    for cid in compartment_ids:
        # --- KMS: vaults e, por vault ativo, as chaves (metadados) ---
        try:
            vaults = list(paginate(vault_cli.list_vaults, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="security.vaults", region=region, compartment_id=cid, error=exc)
            vaults = []
        for v in vaults:
            vd = to_dict(v)
            result["vaults"].append(vd)
            me = vd.get("management_endpoint")
            if vd.get("lifecycle_state") == "ACTIVE" and me:
                try:
                    mgmt = oci.key_management.KmsManagementClient(
                        **auth.client_kwargs(region), service_endpoint=me
                    )
                    result["keys"].extend(to_dict(k) for k in paginate(mgmt.list_keys, compartment_id=cid))
                except Exception as exc:  # noqa: BLE001
                    errors.record(scope="security.keys", region=region, compartment_id=cid, error=exc)

        # --- Bastion ---
        try:
            result["bastions"].extend(to_dict(b) for b in paginate(bastion_cli.list_bastions, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="security.bastions", region=region, compartment_id=cid, error=exc)

        # --- Certificados (1.45.0, feedback item 2c) — GET /certificates -----
        # Validade em current_version_summary.validity (validado no SDK); o
        # relatório destaca vencidos e a vencer. Campos filtrados (sem tags).
        try:
            for c in paginate(cert_cli.list_certificates, compartment_id=cid):
                cdict = to_dict(c)
                val = ((cdict.get("current_version_summary") or {}).get("validity") or {})
                result["certificates"].append({
                    "id": cdict.get("id"), "name": cdict.get("name"),
                    "config_type": cdict.get("config_type"),
                    "certificate_profile_type": cdict.get("certificate_profile_type"),
                    "lifecycle_state": cdict.get("lifecycle_state"),
                    "time_created": cdict.get("time_created"),
                    "not_valid_before": val.get("time_of_validity_not_before"),
                    "not_valid_after": val.get("time_of_validity_not_after"),
                    "compartment_id": cdict.get("compartment_id"),
                })
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="security.certificates", region=region, compartment_id=cid, error=exc)

    return result
