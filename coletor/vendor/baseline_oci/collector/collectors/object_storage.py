"""Coletor de Object Storage (por região).

Apenas metadata: namespace, listagem de buckets por compartment, get_bucket com
campos aproximados (contagem/tamanho) e versioning, e as regras de lifecycle
(get_object_lifecycle_policy). NÃO lista objetos nem lê dados, evitando
'requests' de dados cobráveis. List/Get de control-plane.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_object_storage(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    obj = oci.object_storage.ObjectStorageClient(**auth.client_kwargs(region))
    result: dict = {"namespace": None, "buckets": []}

    try:
        namespace = obj.get_namespace().data
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="object_storage.namespace", region=region, compartment_id=auth.tenancy_id, error=exc)
        return result
    result["namespace"] = namespace

    for cid in compartment_ids:
        try:
            buckets = paginate(obj.list_buckets, namespace, cid)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="object_storage.buckets", region=region, compartment_id=cid, error=exc)
            continue

        for b in buckets:
            entry = to_dict(b)
            # Enriquecimento com metadata aproximada (sem listar objetos).
            try:
                detail = obj.get_bucket(
                    namespace,
                    b.name,
                    fields=["approximateCount", "approximateSize"],
                ).data
                entry.update(to_dict(detail))
            except Exception as exc:  # noqa: BLE001
                errors.record(
                    scope="object_storage.get_bucket", region=region, compartment_id=cid, error=exc
                )
            # Lifecycle (ciclo de vida) do bucket: regras de arquivamento/exclusão.
            # HTTP 404 = bucket sem política (situação normal, não é erro). Apenas
            # metadados de control-plane; custo zero.
            try:
                pol = obj.get_object_lifecycle_policy(namespace, b.name).data
                entry["lifecycle_rules"] = [to_dict(r) for r in (pol.items or [])]
            except oci.exceptions.ServiceError as exc:
                if exc.status == 404:
                    entry["lifecycle_rules"] = []
                else:
                    errors.record(scope="object_storage.lifecycle", region=region,
                                  compartment_id=cid, error=exc)
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="object_storage.lifecycle", region=region,
                              compartment_id=cid, error=exc)
            # destino da replicação cross-region (GET só quando o
            # bucket é origem de replicação — raro; policy traz região/bucket
            # de destino, status e último sync).
            if entry.get("replication_enabled"):
                try:
                    entry["replication_policies"] = [
                        to_dict(p) for p in paginate(
                            obj.list_replication_policies, namespace, b.name)
                    ]
                except Exception as exc:  # noqa: BLE001
                    errors.record(scope="object_storage.replication_policies",
                                  region=region, compartment_id=cid, error=exc)
            result["buckets"].append(entry)

    return result
