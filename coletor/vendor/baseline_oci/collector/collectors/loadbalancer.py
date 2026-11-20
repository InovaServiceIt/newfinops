"""Coletor de balanceadores (por região): Load Balancers (LBaaS) e Network Load
Balancers (NLB). Apenas List.

1.46.0: os certificados LEGADOS embutidos no LB (criados direto no recurso,
fora do serviço Certificates) já vêm com o PEM público no próprio
list_load_balancers — nenhuma chamada extra. A VALIDADE não é campo da API:
é extraída localmente do PEM com o pacote `cryptography` (dependência do
próprio SDK oci — presente no Cloud Shell). Parse falho nunca derruba a
coleta: os campos simplesmente não são gravados.
"""

from __future__ import annotations

from typing import List

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def _parse_cert_pem(pem: str) -> dict | None:
    """Extrai validade/CN/emissor do PRIMEIRO bloco PEM (certificado folha).

    Local e offline: nenhuma chamada de rede. Retorna None em qualquer falha
    (cryptography ausente, PEM malformado) — coleta segue sem o derivado."""
    try:
        from cryptography import x509  # dependência transitiva do SDK oci
        bloco = pem.split("-----END CERTIFICATE-----")[0] + "-----END CERTIFICATE-----"
        cert = x509.load_pem_x509_certificate(bloco.encode())
        # cryptography >= 42 usa *_utc; versões antigas só o atributo naive
        nva = getattr(cert, "not_valid_after_utc", None) or cert.not_valid_after
        nvb = getattr(cert, "not_valid_before_utc", None) or cert.not_valid_before

        def _cn(nome) -> str | None:
            try:
                return next((a.value for a in nome
                             if getattr(a.oid, "_name", "") == "commonName"), None)
            except Exception:  # noqa: BLE001
                return None

        return {
            "not_valid_after": nva.isoformat(),
            "not_valid_before": nvb.isoformat(),
            "subject_cn": _cn(cert.subject),
            "issuer_cn": _cn(cert.issuer),
        }
    except Exception:  # noqa: BLE001 — derivado é bônus, nunca requisito
        return None


def _enriquece_certs_lb(lb_dict: dict) -> None:
    """Anota cada certificado embutido do LB com `_cert_parsed` (derivado)."""
    for _nome, cert in (lb_dict.get("certificates") or {}).items():
        if isinstance(cert, dict) and cert.get("public_certificate"):
            parsed = _parse_cert_pem(cert["public_certificate"])
            if parsed:
                cert["_cert_parsed"] = parsed


def collect_load_balancers(
    auth: AuthContext, region: str, compartment_ids: List[str], errors: ErrorSink
) -> dict:
    lb = oci.load_balancer.LoadBalancerClient(**auth.client_kwargs(region))
    nlb = oci.network_load_balancer.NetworkLoadBalancerClient(**auth.client_kwargs(region))
    result: dict = {"load_balancers": [], "network_load_balancers": []}
    for cid in compartment_ids:
        try:
            for x in paginate(lb.list_load_balancers, compartment_id=cid):
                d = to_dict(x)
                _enriquece_certs_lb(d)
                result["load_balancers"].append(d)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="loadbalancer.load_balancers", region=region, compartment_id=cid, error=exc)
        try:
            result["network_load_balancers"].extend(to_dict(x) for x in paginate(nlb.list_network_load_balancers, compartment_id=cid))
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="loadbalancer.network_load_balancers", region=region, compartment_id=cid, error=exc)
    return result
