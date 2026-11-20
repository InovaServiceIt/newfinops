"""Coletor de OKE / Kubernetes (por região). Apenas List/Get.

Coleta clusters, node pools MANAGED (workers são instâncias Compute) e node
pools VIRTUAIS (serverless, sem instância Compute). Para cada managed node pool,
resolve os nodes (get_node_pool) e o tamanho desejado (node_config_details.size),
permitindo distinguir cluster x node pool managed x virtual x worker x VM avulsa.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_oke(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    ce = oci.container_engine.ContainerEngineClient(**auth.client_kwargs(region))
    result: dict = {"clusters": [], "node_pools": [], "virtual_node_pools": [], "worker_instance_ids": []}

    for cid in compartment_ids:
        try:
            for c in paginate(ce.list_clusters, compartment_id=cid):
                result["clusters"].append(to_dict(c))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="oke.clusters", region=region, compartment_id=cid, error=exc)

        # Managed node pools (workers = instâncias Compute)
        try:
            node_pools = paginate(ce.list_node_pools, compartment_id=cid)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="oke.node_pools", region=region, compartment_id=cid, error=exc)
            node_pools = []
        for np in node_pools:
            d = to_dict(np)
            workers: list = []
            try:
                full = ce.get_node_pool(np.id).data
                workers = [n.id for n in (getattr(full, "nodes", None) or []) if getattr(n, "id", None)]
                ncd = getattr(full, "node_config_details", None)
                d["_size"] = getattr(ncd, "size", None) if ncd is not None else None
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="oke.get_node_pool", region=region, compartment_id=cid, error=exc)
            d["worker_instance_ids"] = workers
            result["worker_instance_ids"].extend(workers)
            result["node_pools"].append(d)

        # Virtual node pools (serverless; nós NÃO são instâncias Compute)
        try:
            for vnp in paginate(ce.list_virtual_node_pools, compartment_id=cid):
                result["virtual_node_pools"].append(to_dict(vnp))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="oke.virtual_node_pools", region=region, compartment_id=cid, error=exc)

    return result
