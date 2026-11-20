"""Coletor de Networking (por região).

VCNs, subnets, gateways (Internet/NAT/Service/DRG), DRG attachments (qual VCN
está ligada a qual DRG), route tables, security lists, NSGs, conectividade
híbrida (IPSec VPN, FastConnect virtual circuits, CPEs, Remote Peering) e IPs
públicos reservados (scope REGION). Tudo listado por compartment. Apenas List/Get.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_network(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    vnc = oci.core.VirtualNetworkClient(**auth.client_kwargs(region))
    funcs = {
        "vcns": vnc.list_vcns,
        "subnets": vnc.list_subnets,
        "internet_gateways": vnc.list_internet_gateways,
        "nat_gateways": vnc.list_nat_gateways,
        "service_gateways": vnc.list_service_gateways,
        "drgs": vnc.list_drgs,
        "drg_attachments": vnc.list_drg_attachments,
        "route_tables": vnc.list_route_tables,
        "security_lists": vnc.list_security_lists,
        "network_security_groups": vnc.list_network_security_groups,
        "ip_sec_connections": vnc.list_ip_sec_connections,
        "virtual_circuits": vnc.list_virtual_circuits,
        "cpes": vnc.list_cpes,
        "remote_peering_connections": vnc.list_remote_peering_connections,
        # 1.47.0 (Fase B): peering local e DHCP options — List, gratuitas.
        "local_peering_gateways": vnc.list_local_peering_gateways,
        "dhcp_options": vnc.list_dhcp_options,
    }
    result: dict = {k: [] for k in funcs}
    result["public_ips"] = []

    for cid in compartment_ids:
        for key, func in funcs.items():
            try:
                result[key].extend(to_dict(x) for x in paginate(func, compartment_id=cid))
            except Exception as exc:  # noqa: BLE001
                errors.record(scope=f"network.{key}", region=region, compartment_id=cid, error=exc)
        # IPs públicos RESERVADOS (scope REGION). Efêmeros (AD) seguem o ciclo da
        # instância e não entram aqui. Apenas inventário; sem inferência de custo.
        try:
            result["public_ips"].extend(
                to_dict(x) for x in paginate(vnc.list_public_ips, scope="REGION", compartment_id=cid)
            )
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="network.public_ips", region=region, compartment_id=cid, error=exc)

    # --- Private IPs por subnet (1.45.0, feedback item 2b) --------------------
    # GET /privateIps filtrado por subnet_id (validado no SDK). Um GET leve por
    # subnet já coletada; a correlação IP → vNIC → instância/LB é do GERADOR.
    # Falhas por subnet são agregadas num único erro não-fatal (padrão 1.31.2).
    result["private_ips"] = []
    falhas_pip = 0
    amostra_pip = None
    for sn in result.get("subnets") or []:
        sid = sn.get("id")
        if not sid:
            continue
        try:
            for p in paginate(vnc.list_private_ips, subnet_id=sid):
                d = to_dict(p)
                result["private_ips"].append({
                    "id": d.get("id"), "ip_address": d.get("ip_address"),
                    "vnic_id": d.get("vnic_id"), "subnet_id": d.get("subnet_id"),
                    "display_name": d.get("display_name"),
                    "is_primary": d.get("is_primary"),
                    "hostname_label": d.get("hostname_label"),
                    "time_created": d.get("time_created"),
                })
        except Exception as exc:  # noqa: BLE001
            falhas_pip += 1
            if amostra_pip is None:
                amostra_pip = exc
    if falhas_pip:
        agg = Exception(f"{falhas_pip} subnet(s) sem resposta de private IPs; amostra do 1º "
                        f"erro: {type(amostra_pip).__name__}: {str(amostra_pip)[:180]}")
        agg.status = getattr(amostra_pip, "status", None)
        errors.record(scope="network.private_ips", region=region,
                      compartment_id=auth.tenancy_id, error=agg)

    # --- 1.47.0 (Fase B): regras e VNICs de cada NSG -------------------------
    # As regras NÃO vêm no list_network_security_groups: exigem 2 Lists por
    # NSG (gratuitas; NSGs são poucos). Embutidas no próprio NSG (_rules /
    # _vnics), como os demais derivados. Falhas agregadas (padrão 1.31.2).
    falhas_nsg = 0
    amostra_nsg = None
    for nsg in result.get("network_security_groups") or []:
        nid = nsg.get("id")
        if not nid:
            continue
        try:
            nsg["_rules"] = [to_dict(r) for r in paginate(
                vnc.list_network_security_group_security_rules,
                network_security_group_id=nid)]
            nsg["_vnics"] = [to_dict(v) for v in paginate(
                vnc.list_network_security_group_vnics,
                network_security_group_id=nid)]
        except Exception as exc:  # noqa: BLE001
            falhas_nsg += 1
            if amostra_nsg is None:
                amostra_nsg = exc
    if falhas_nsg:
        agg_n = Exception(f"{falhas_nsg} NSG(s) sem regras/vnics; amostra do 1º erro: "
                          f"{type(amostra_nsg).__name__}: {str(amostra_nsg)[:180]}")
        agg_n.status = getattr(amostra_nsg, "status", None)
        errors.record(scope="network.nsg_details", region=region,
                      compartment_id=auth.tenancy_id, error=agg_n)

    # --- 1.47.0 (Fase B): túneis de cada conexão IPSec (status/BGP) ----------
    falhas_tun = 0
    amostra_tun = None
    for ipsc in result.get("ip_sec_connections") or []:
        iid = ipsc.get("id")
        if not iid:
            continue
        try:
            ipsc["_tunnels"] = [to_dict(t) for t in paginate(
                vnc.list_ip_sec_connection_tunnels, ipsc_id=iid)]
        except Exception as exc:  # noqa: BLE001
            falhas_tun += 1
            if amostra_tun is None:
                amostra_tun = exc
    if falhas_tun:
        agg_t = Exception(f"{falhas_tun} conexão(ões) IPSec sem túneis; amostra do 1º erro: "
                          f"{type(amostra_tun).__name__}: {str(amostra_tun)[:180]}")
        agg_t.status = getattr(amostra_tun, "status", None)
        errors.record(scope="network.ipsec_tunnels", region=region,
                      compartment_id=auth.tenancy_id, error=agg_t)

    return result
