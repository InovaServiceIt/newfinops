"""Coletor de Database (por região). Apenas List.

Tipos cobertos (cada um identificado no relatório):
- DB Systems            -> DBCS (Base Database / DB System)
- Cloud VM Clusters     -> Exadata Cloud Service (ExaCS)
- VM Clusters           -> Exadata Cloud@Customer (ExaCC)
- Autonomous Databases  -> Autonomous (ADB)
- DB Homes              -> trazem a versão do banco (db_version)
- Databases             -> bancos (CDBs); a versão vem do DB Home (db_home_id)

Nota validada em coleta real: `list_databases` exige `db_home_id` (ou
`system_id`); só `compartment_id` retorna HTTP 400 MissingParameter. Por isso os
databases são coletados iterando pelos DB Homes já listados.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_database(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    db = oci.database.DatabaseClient(**auth.client_kwargs(region))
    result: dict = {
        "db_systems": [],
        "cloud_vm_clusters": [],
        "vm_clusters": [],
        "autonomous_databases": [],
        "databases": [],
        "db_homes": [],
    }
    ops = {
        "db_systems": db.list_db_systems,
        "cloud_vm_clusters": db.list_cloud_vm_clusters,
        "vm_clusters": db.list_vm_clusters,
        "autonomous_databases": db.list_autonomous_databases,
        "db_homes": db.list_db_homes,
    }
    for cid in compartment_ids:
        for key, fn in ops.items():
            try:
                result[key].extend(to_dict(x) for x in paginate(fn, compartment_id=cid))
            except Exception as exc:  # noqa: BLE001
                errors.record(scope=f"database.{key}", region=region, compartment_id=cid, error=exc)

    # Databases (CDBs): a API exige db_home_id; iteramos pelos DB Homes coletados.
    for dh in result["db_homes"]:
        cid, hid = dh.get("compartment_id"), dh.get("id")
        if not (cid and hid):
            continue
        try:
            result["databases"].extend(
                to_dict(x) for x in paginate(db.list_databases, compartment_id=cid, db_home_id=hid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="database.databases", region=region, compartment_id=cid, error=exc)

    return result
