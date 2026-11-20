"""Coletor Serverless (por região): Functions (applications + functions), API
Gateway (gateways + deployments) e Container Instances. Apenas List.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_serverless(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    fn = oci.functions.FunctionsManagementClient(**auth.client_kwargs(region))
    gw = oci.apigateway.GatewayClient(**auth.client_kwargs(region))
    dep = oci.apigateway.DeploymentClient(**auth.client_kwargs(region))
    ci = oci.container_instances.ContainerInstanceClient(**auth.client_kwargs(region))
    result: dict = {
        "applications": [],
        "functions": [],
        "gateways": [],
        "api_deployments": [],
        "container_instances": [],
        "containers": [],
    }

    for cid in compartment_ids:
        # --- Functions: applications e, por application, as functions ---
        try:
            apps = list(paginate(fn.list_applications, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="serverless.applications", region=region, compartment_id=cid, error=exc)
            apps = []
        for a in apps:
            ad = to_dict(a)
            result["applications"].append(ad)
            try:
                result["functions"].extend(
                    to_dict(f) for f in paginate(fn.list_functions, application_id=ad.get("id"))
                )
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="serverless.functions", region=region, compartment_id=cid, error=exc)

        # --- API Gateway: gateways e deployments ---
        try:
            result["gateways"].extend(to_dict(g) for g in paginate(gw.list_gateways, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="serverless.gateways", region=region, compartment_id=cid, error=exc)
        try:
            result["api_deployments"].extend(to_dict(d) for d in paginate(dep.list_deployments, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="serverless.api_deployments", region=region, compartment_id=cid, error=exc)

        # --- Container Instances (e os containers de cada uma, p/ imagem/versão) ---
        try:
            result["container_instances"].extend(
                to_dict(c) for c in paginate(ci.list_container_instances, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="serverless.container_instances", region=region, compartment_id=cid, error=exc)
        try:
            result["containers"].extend(
                to_dict(c) for c in paginate(ci.list_containers, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="serverless.containers", region=region, compartment_id=cid, error=exc)

    return result
