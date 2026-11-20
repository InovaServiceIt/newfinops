"""Coletor de File Storage (FSS, por região): file systems e mount targets.
Ambos exigem availability_domain. Apenas List.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_file_storage(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    fss = oci.file_storage.FileStorageClient(**auth.client_kwargs(region))
    identity = oci.identity.IdentityClient(**auth.client_kwargs(region))
    result: dict = {"file_systems": [], "mount_targets": []}

    try:
        ads = [ad.name for ad in identity.list_availability_domains(auth.tenancy_id).data]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="file_storage.availability_domains", region=region, compartment_id=auth.tenancy_id, error=exc)
        ads = []

    for cid in compartment_ids:
        for ad in ads:
            try:
                result["file_systems"].extend(
                    to_dict(x) for x in paginate(fss.list_file_systems, compartment_id=cid, availability_domain=ad)
                )
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="file_storage.file_systems", region=region, compartment_id=cid, error=exc)
            try:
                result["mount_targets"].extend(
                    to_dict(x) for x in paginate(fss.list_mount_targets, compartment_id=cid, availability_domain=ad)
                )
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="file_storage.mount_targets", region=region, compartment_id=cid, error=exc)
    return result
