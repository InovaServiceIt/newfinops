"""Descoberta de regiões subscritas e da árvore de compartments.

O "org-wide" do produto AWS é nativo na OCI: a varredura recursiva de
compartments a partir da raiz da tenancy cobre toda a organização. IAM é global
e consultado na home region.

Apenas operações List/Get (read-only).
"""

from __future__ import annotations

from typing import Dict, List

import oci

from .auth import AuthContext
from .util import paginate, to_dict


def discover_regions(auth: AuthContext) -> List[dict]:
    """Lista as regiões subscritas pela tenancy (operação global)."""
    identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))
    subs = paginate(identity.list_region_subscriptions, auth.tenancy_id)
    regions = [to_dict(s) for s in subs]
    # Garante a home region marcada de forma explícita.
    for r in regions:
        r["is_home_region"] = bool(r.get("is_home_region")) or (
            r.get("region_name") == auth.home_region
        )
    return regions


def discover_compartments(auth: AuthContext) -> List[dict]:
    """Retorna a árvore achatada de compartments, incluindo a raiz (tenancy).

    Cada item recebe um campo 'path' legível (ex.: 'root / Prod / Rede').
    Compartments DELETED são incluídos com seu lifecycle_state para fidelidade,
    mas o relatório pode filtrá-los.
    """
    identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))

    # Raiz = a própria tenancy. get_compartment traz time_created (get_tenancy não tem).
    try:
        root = to_dict(identity.get_compartment(auth.tenancy_id).data)
    except Exception:  # noqa: BLE001 - fallback sem time_created
        tenancy = identity.get_tenancy(auth.tenancy_id).data
        root = {
            "id": auth.tenancy_id,
            "name": getattr(tenancy, "name", "root"),
            "description": getattr(tenancy, "description", None),
            "compartment_id": None,
            "lifecycle_state": "ACTIVE",
        }
    root["is_root"] = True

    children = paginate(
        identity.list_compartments,
        auth.tenancy_id,
        compartment_id_in_subtree=True,
        access_level="ANY",
    )
    flat = [root] + [to_dict(c) for c in children]

    # Indexa e calcula o caminho hierárquico.
    by_id: Dict[str, dict] = {c["id"]: c for c in flat}
    for comp in flat:
        comp["path"] = _build_path(comp, by_id)
    return flat


def _build_path(comp: dict, by_id: Dict[str, dict]) -> str:
    parts = [comp.get("name", "?")]
    parent_id = comp.get("compartment_id")
    seen = {comp["id"]}
    while parent_id and parent_id in by_id and parent_id not in seen:
        seen.add(parent_id)
        parent = by_id[parent_id]
        parts.append(parent.get("name", "?"))
        parent_id = parent.get("compartment_id")
    return " / ".join(reversed(parts))


def active_compartment_ids(compartments: List[dict]) -> List[str]:
    """IDs de compartments ativos (úteis para varrer recursos)."""
    return [
        c["id"]
        for c in compartments
        if c.get("lifecycle_state", "ACTIVE") == "ACTIVE"
    ]
