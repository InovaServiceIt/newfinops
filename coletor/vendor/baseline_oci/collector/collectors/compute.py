"""Coletor de Compute (por região).

Inventário de instâncias (com S.O. resolvido via get_image), catálogo de shapes
(OCPU/memória da API) e attachments de volumes (block e boot) para mapear disco e
o que está anexado a cada instância. Apenas List/Get.

1.20.0: capacity reservations (`list_compute_capacity_reservations` +
`get_compute_capacity_reservation` para as configs por shape). O List/Get é
custo zero; a RESERVA em si é que cobra — capacidade reservada e não usada é
faturada a 85% do preço da instância (doc oficial), por isso o relatório
destaca reserva ociosa como achado FinOps.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_compute(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink,
    on_pulse=None,
) -> dict:
    compute = oci.core.ComputeClient(**auth.client_kwargs(region))
    identity = oci.identity.IdentityClient(**auth.client_kwargs(region))
    result: dict = {"instances": [], "shapes": [], "volume_attachments": [], "boot_volume_attachments": [], "vnic_attachments": [], "capacity_reservations": []}
    image_cache: dict = {}  # image_id -> {"os":, "os_version":}

    try:
        ads = [ad.name for ad in identity.list_availability_domains(auth.tenancy_id).data]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="compute.availability_domains", region=region, compartment_id=auth.tenancy_id, error=exc)
        ads = []

    # 1.47.0 (Fase B, sugestão 12): CATÁLOGO de imagens de plataforma da
    # região — 1 List paginada no root (as platform images aparecem em
    # qualquer compartment). Campos enxutos: o gerador só precisa de
    # S.O./versão para o indício de ciclo de vida ("a Oracle ainda publica
    # imagens deste S.O.?"). Imagens custom do root entram e são separáveis
    # pelo operating_system == "Custom"/base_image_id.
    result["images_catalog"] = []
    try:
        for im in paginate(compute.list_images, compartment_id=auth.tenancy_id):
            result["images_catalog"].append({
                "id": im.id,
                "display_name": im.display_name,
                "operating_system": im.operating_system,
                "operating_system_version": im.operating_system_version,
                "time_created": im.time_created,
            })
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="compute.images_catalog", region=region,
                      compartment_id=auth.tenancy_id, error=exc)

    for _i, cid in enumerate(compartment_ids):
        if on_pulse:
            try:
                on_pulse(_i + 1, len(compartment_ids))
            except Exception:  # noqa: BLE001 — progresso nunca derruba a coleta
                pass
        # Instâncias + sistema operacional (via imagem)
        try:
            for inst in paginate(compute.list_instances, compartment_id=cid):
                d = to_dict(inst)
                img_id = getattr(inst, "image_id", None)
                if img_id:
                    if img_id not in image_cache:
                        try:
                            im = compute.get_image(img_id).data
                            image_cache[img_id] = {"os": im.operating_system,
                                                   "os_version": im.operating_system_version,
                                                   "image_name": im.display_name}
                        except Exception:  # noqa: BLE001 - imagem inacessível/apagada
                            image_cache[img_id] = {"os": None, "os_version": None, "image_name": None}
                    d["os"] = image_cache[img_id]["os"]
                    d["os_version"] = image_cache[img_id]["os_version"]
                    d["image_name"] = image_cache[img_id].get("image_name")
                result["instances"].append(d)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="compute.instances", region=region, compartment_id=cid, error=exc)

        # Attachments de block volumes (por compartment)
        try:
            result["volume_attachments"].extend(
                to_dict(x) for x in paginate(compute.list_volume_attachments, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="compute.volume_attachments", region=region, compartment_id=cid, error=exc)

        # VNIC attachments: ligam a instância à subnet (base do diagrama de rede)
        try:
            result["vnic_attachments"].extend(
                to_dict(x) for x in paginate(compute.list_vnic_attachments, compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="compute.vnic_attachments", region=region, compartment_id=cid, error=exc)

        # Capacity reservations: summary já traz reservado × usado; o get
        # complementa com as configs por shape (raras por tenancy — get barato).
        try:
            for cr in paginate(compute.list_compute_capacity_reservations, compartment_id=cid):
                d = to_dict(cr)
                try:
                    full = compute.get_compute_capacity_reservation(d.get("id")).data
                    d["instance_reservation_configs"] = [
                        to_dict(c) for c in (full.instance_reservation_configs or [])
                    ]
                except Exception:  # noqa: BLE001 - detalhe é complemento, não requisito
                    d["instance_reservation_configs"] = []
                result["capacity_reservations"].append(d)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="compute.capacity_reservations", region=region, compartment_id=cid, error=exc)

        # Attachments de boot volumes (exigem availability_domain)
        for ad in ads:
            try:
                result["boot_volume_attachments"].extend(
                    to_dict(x) for x in paginate(
                        compute.list_boot_volume_attachments, availability_domain=ad, compartment_id=cid
                    )
                )
            except Exception as exc:  # noqa: BLE001
                errors.record(scope="compute.boot_volume_attachments", region=region, compartment_id=cid, error=exc)

    # --- OS Management Hub: S.O. REAL reportado pelo agente (1.29.0) --------
    # Imagem custom/exportada não revela o S.O. pela API de imagem (metadado
    # declarativo). Quando a tenancy usa OSMH, o managed instance (OCID = o da
    # própria instância) traz os_name/os_version/kernel verificados pelo
    # agente. Tenancy sem OSMH: primeiro erro encerra a varredura da região
    # (sem ruído de erro por compartment).
    result["osmh_managed_instances"] = []
    inst_ids = {i.get("id") for i in result["instances"] if i.get("id")}
    if inst_ids:
        try:
            osmh = oci.os_management_hub.ManagedInstanceClient(**auth.client_kwargs(region))
            for cid in compartment_ids:
                try:
                    summaries = paginate(osmh.list_managed_instances, compartment_id=cid)
                except Exception as exc:  # noqa: BLE001 - sem OSMH/permissão: 1 erro e para
                    errors.record(scope="compute.osmh", region=region, compartment_id=cid, error=exc)
                    break
                for ms in summaries:
                    mid = getattr(ms, "id", None)
                    if mid not in inst_ids:
                        continue
                    try:
                        full = osmh.get_managed_instance(mid).data
                        result["osmh_managed_instances"].append({
                            "id": mid,
                            "os_name": getattr(full, "os_name", None),
                            "os_version": getattr(full, "os_version", None),
                            "os_kernel_version": getattr(full, "os_kernel_version", None),
                            "os_family": getattr(full, "os_family", None),
                            "status": getattr(full, "status", None),
                            "agent_version": getattr(full, "agent_version", None),
                            "time_last_checkin": (full.time_last_checkin.isoformat()
                                                  if getattr(full, "time_last_checkin", None) else None),
                        })
                    except Exception:  # noqa: BLE001 - detalhe é complemento
                        continue
        except Exception as exc:  # noqa: BLE001 - client indisponível na região
            errors.record(scope="compute.osmh", region=region, compartment_id=auth.tenancy_id, error=exc)

    # Catálogo de shapes (specs de OCPU/memória vêm da API).
    try:
        seen = set()
        for s in paginate(compute.list_shapes, compartment_id=auth.tenancy_id):
            d = to_dict(s)
            name = d.get("shape")
            if name and name not in seen:
                seen.add(name)
                result["shapes"].append(d)
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="compute.shapes", region=region, compartment_id=auth.tenancy_id, error=exc)

    return result
