"""Coletor de IAM (global / home region).

Usuários e grupos vivem na raiz da tenancy; policies existem por compartment e
são varridas em toda a árvore. Apenas List/Get.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_iam(auth: AuthContext, compartment_ids: List[str], errors: ErrorSink) -> dict:
    identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))
    region = auth.home_region
    result: dict = {"users": [], "groups": [], "policies": []}

    try:
        result["users"] = [to_dict(u) for u in paginate(identity.list_users, compartment_id=auth.tenancy_id)]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="iam.users", region=region, compartment_id=auth.tenancy_id, error=exc)

    try:
        result["groups"] = [to_dict(g) for g in paginate(identity.list_groups, compartment_id=auth.tenancy_id)]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="iam.groups", region=region, compartment_id=auth.tenancy_id, error=exc)

    # Policies por compartment (inclui a raiz).
    for cid in compartment_ids:
        try:
            policies = paginate(identity.list_policies, compartment_id=cid)
            result["policies"].extend(to_dict(p) for p in policies)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="iam.policies", region=region, compartment_id=cid, error=exc)

    return result
