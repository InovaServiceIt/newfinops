"""Coletor de Block Storage (por região).

Volumes de bloco (por compartment) e boot volumes (por AD + compartment, pois a
API exige availability_domain). Também os backups (volume e boot), ambos por
compartment. ADs resolvidos via Identity regional. List/Get.

1.23.0: políticas de backup e suas atribuições. `list_volume_backup_policies`
sem compartment retorna as Oracle-defined (bronze/silver/gold); com
compartment, as user-defined. A associação recurso→política vem de
`get_volume_backup_policy_asset_assignment` (GET por volume/boot volume;
resposta vazia = sem política). Retenção fica em schedules[].retention_seconds.
"""

from __future__ import annotations

import time
from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_block_storage(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink,
    on_pulse=None,
) -> dict:
    block = oci.core.BlockstorageClient(**auth.client_kwargs(region))
    identity = oci.identity.IdentityClient(**auth.client_kwargs(region))
    result: dict = {
        "volumes": [],
        "boot_volumes": [],
        "volume_backups": [],
        "boot_volume_backups": [],
        "volume_backup_policies": [],
        "volume_backup_policy_assignments": [],
    }

    # ADs da região (a partir da raiz da tenancy).
    try:
        ads = [ad.name for ad in identity.list_availability_domains(auth.tenancy_id).data]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="block.availability_domains", region=region, compartment_id=auth.tenancy_id, error=exc)
        ads = []

    for _i, cid in enumerate(compartment_ids):
        if on_pulse:
            try:
                on_pulse(_i + 1, len(compartment_ids))
            except Exception:  # noqa: BLE001 — progresso nunca derruba a coleta
                pass
        try:
            result["volumes"].extend(to_dict(v) for v in paginate(block.list_volumes, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="block.volumes", region=region, compartment_id=cid, error=exc)

        try:
            result["volume_backups"].extend(
                to_dict(b) for b in paginate(block.list_volume_backups, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="block.volume_backups", region=region, compartment_id=cid, error=exc)

        try:
            result["boot_volume_backups"].extend(
                to_dict(b) for b in paginate(block.list_boot_volume_backups, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="block.boot_volume_backups", region=region, compartment_id=cid, error=exc)

        for ad in ads:
            try:
                boot = paginate(
                    block.list_boot_volumes, availability_domain=ad, compartment_id=cid
                )
                result["boot_volumes"].extend(to_dict(b) for b in boot)
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="block.boot_volumes", region=region, compartment_id=cid, error=exc)

    # --- Políticas de backup (Oracle-defined + user-defined) -----------------
    vistos: set = set()
    try:
        for p in paginate(block.list_volume_backup_policies):  # sem compartment: bronze/silver/gold
            d = to_dict(p)
            if d.get("id") not in vistos:
                vistos.add(d.get("id"))
                d["defined_by"] = "oracle"
                result["volume_backup_policies"].append(d)
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="block.backup_policies_oracle", region=region,
                      compartment_id=auth.tenancy_id, error=exc)
    for cid in compartment_ids:
        try:
            for p in paginate(block.list_volume_backup_policies, compartment_id=cid):
                d = to_dict(p)
                if d.get("id") not in vistos:
                    vistos.add(d.get("id"))
                    d["defined_by"] = "user"
                    result["volume_backup_policies"].append(d)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="block.backup_policies", region=region, compartment_id=cid, error=exc)

    # --- Atribuição recurso -> política (GET por volume/boot volume) ---------
    # Resposta vazia = recurso SEM política (informação válida, não erro).
    # Falhas por asset são agregadas num único erro não-fatal ao final.
    # 1.35.0: retry com backoff (2s, depois 5s) quando o serviço responde 429
    # TooManyRequests — throttling recorrente confirmado em campo (20-07:
    # 3 coletas no dia, 2/28 assets com 429; a causa raiz ficou visível
    # graças à amostra introduzida na 1.31.2).
    falhas_assign = 0
    amostra_falha = None
    for it in result["volumes"] + result["boot_volumes"]:
        asset_id = it.get("id")
        if not asset_id:
            continue
        try:
            data = None
            for espera in (0, 2, 5):
                if espera:
                    time.sleep(espera)
                try:
                    data = block.get_volume_backup_policy_asset_assignment(asset_id).data
                    break
                except Exception as exc:  # noqa: BLE001
                    if getattr(exc, "status", None) == 429:
                        continue  # throttle: espera e tenta de novo
                    raise
            else:
                raise Exception("429 TooManyRequests persistiu após 2 retries (2s+5s)")
            for a in (data or []):
                result["volume_backup_policy_assignments"].append({
                    "asset_id": asset_id,
                    "policy_id": getattr(a, "policy_id", None),
                    "id": getattr(a, "id", None),
                })
        except Exception as exc:  # noqa: BLE001
            falhas_assign += 1
            if amostra_falha is None:
                amostra_falha = exc
    if falhas_assign:
        # Erro agregado COM amostra do 1º motivo (diagnóstico de campo 20-07:
        # a mensagem sem causa raiz não permitia distinguir throttle de 404).
        agg = Exception(
            f"{falhas_assign} asset(s) sem resposta de assignment; amostra do 1º erro: "
            f"{type(amostra_falha).__name__}: {str(amostra_falha)[:180]}")
        agg.status = getattr(amostra_falha, "status", None)
        agg.code = getattr(amostra_falha, "code", None)
        errors.record(scope="block.backup_policy_assignments", region=region,
                      compartment_id=auth.tenancy_id, error=agg)

    return result
