"""Coletor de Governança (global / home region): Tagging, Budgets e Contrato.

Escopo (1.12.0): tag namespaces + tags definidas (com is_cost_tracking e
is_retired) e budgets com alert rules. Tudo operação **List (HTTP GET)** de
control-plane, custo zero — dentro da regra original do produto, sem exceção.

Escopo (1.33.0): rate card do contrato via OneSubscription
(`RatecardClient.list_rate_cards`, GET /ratecards por organization
subscription). Preços líquidos contratados por SKU. Validado em campo
(20-07-2026, serviceitoci23): 3.443 entradas, BRL, com tiers e vigências.

Escopo (1.20.0): subscriptions da tenancy via Organizations API
(`tenant_manager_control_plane.SubscriptionClient.list_subscriptions`, GET no
root) — vigência do contrato (start/end), modelo de pagamento (ex.: ANNUAL no
Universal Credits), tier e estado. Policy: `read organizations-subscriptions
in tenancy`. Validado em campo (16-07-2026, serviceitoci23): a chamada
retornou o contrato CLOUDCM/ANNUAL com vigência, além de entradas de serviço
(IDCS) e registros históricos DELETED.

Pontos de projeto validados por introspecção do SDK:
- `list_tag_namespaces(compartment_id, include_subcompartments=True)` no root
  cobre a tenancy INTEIRA numa chamada (namespaces vivem em compartments).
- `BudgetSummary` já traz `actual_spend`/`forecasted_spend` no próprio List —
  o gasto atual do budget vem de graça, sem consultar billing.
- Alert rules são listadas por budget (`list_alert_rules(budget_id)`).
- `Subscription` da Organizations API é polimórfico (Classic/Cloud); os campos
  usados aqui existem em ambos (attribute_map validado no SDK 2.182.0).
"""

from __future__ import annotations

import oci

from ..auth import AuthContext
from ..util import ErrorSink, paginate, to_dict


def collect_governance(auth: AuthContext, errors: ErrorSink) -> dict:
    identity = oci.identity.IdentityClient(**auth.client_kwargs(auth.home_region))
    budget_client = oci.budget.BudgetClient(**auth.client_kwargs(auth.home_region))
    result: dict = {"tag_namespaces": [], "tags": [], "budgets": [], "alert_rules": []}

    # --- Tag namespaces (root + toda a subárvore de compartments) -----------
    try:
        result["tag_namespaces"] = [
            to_dict(n) for n in paginate(
                identity.list_tag_namespaces, auth.tenancy_id, include_subcompartments=True)
        ]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="governance.tag_namespaces", region=auth.home_region,
                      compartment_id=auth.tenancy_id, error=exc)

    # --- Tags definidas em cada namespace ------------------------------------
    for ns in result["tag_namespaces"]:
        try:
            for t in paginate(identity.list_tags, ns.get("id")):
                td = to_dict(t)
                td["tag_namespace_id"] = ns.get("id")
                td["tag_namespace_name"] = ns.get("name")
                result["tags"].append(td)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="governance.tags", region=auth.home_region,
                          compartment_id=ns.get("id") or auth.tenancy_id, error=exc)

    # --- 1.47.0 (Fase B): tag DEFAULTS do root (auto-aplicação na criação) ---
    # List gratuita, home region. Permite ao gerador afirmar (fato, não
    # indício) se a tag default de CreatedBy está configurada. Coletamos só o
    # ROOT: defaults do root herdam para toda a subárvore; defaults locais de
    # sub-compartments existem mas o caso de governança relevante é o root.
    result["tag_defaults"] = []
    try:
        result["tag_defaults"] = [
            to_dict(td) for td in paginate(
                identity.list_tag_defaults, compartment_id=auth.tenancy_id)
        ]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="governance.tag_defaults", region=auth.home_region,
                      compartment_id=auth.tenancy_id, error=exc)

    # --- Budgets (tenancy; ALL inclui budgets de compartment e de tag) -------
    try:
        result["budgets"] = [
            to_dict(b) for b in paginate(
                budget_client.list_budgets, auth.tenancy_id, target_type="ALL")
        ]
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="governance.budgets", region=auth.home_region,
                      compartment_id=auth.tenancy_id, error=exc)

    # --- Alert rules de cada budget -------------------------------------------
    for b in result["budgets"]:
        try:
            for a in paginate(budget_client.list_alert_rules, b.get("id")):
                ad = to_dict(a)
                ad["budget_id"] = b.get("id")
                ad["budget_name"] = b.get("display_name")
                result["alert_rules"].append(ad)
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="governance.alert_rules", region=auth.home_region,
                          compartment_id=auth.tenancy_id, error=exc)

    # --- Subscriptions / contrato (Organizations API, GET no root) -----------
    # Vigência e modelo do contrato (ex.: Universal Credits ANNUAL). O end_date
    # de um registro V1 pode não refletir renovação: o relatório reporta o dado
    # como está e sinaliza a validação — nunca inventa a data nova.
    try:
        sub_client = oci.tenant_manager_control_plane.SubscriptionClient(
            **auth.client_kwargs(auth.home_region))
        result["subscriptions"] = [
            to_dict(s) for s in paginate(
                sub_client.list_subscriptions, compartment_id=auth.tenancy_id)
        ]
    except Exception as exc:  # noqa: BLE001
        result["subscriptions"] = []
        errors.record(scope="governance.subscriptions", region=auth.home_region,
                      compartment_id=auth.tenancy_id, error=exc)

    # --- Queima do contrato (OneSubscription, GET no root) — 1.21.0 ----------
    # Validado em campo (16-07-2026, serviceitoci23): list_organization_subscriptions
    # traz o CABEÇALHO do contrato (total_value, vigência completa, moeda) e
    # list_subscribed_services as LINHAS de crédito (used/available/funded por
    # período). A rota list_subscriptions do onesubscription retornou 404 na
    # mesma tenancy — não é usada. PRIVACIDADE: o summary traz endereços e
    # contatos de billing; gravamos APENAS campos financeiros/técnicos.
    result["organization_subscriptions"] = []
    result["subscribed_services"] = []
    try:
        org_client = oci.onesubscription.OrganizationSubscriptionClient(
            **auth.client_kwargs(auth.home_region))
        for s in paginate(org_client.list_organization_subscriptions,
                          compartment_id=auth.tenancy_id):
            cur = getattr(s, "currency", None)
            result["organization_subscriptions"].append({
                "id": getattr(s, "id", None),
                "service_name": getattr(s, "service_name", None),
                "type": getattr(s, "type", None),
                "status": getattr(s, "status", None),
                "time_start": _iso_or_none(getattr(s, "time_start", None)),
                "time_end": _iso_or_none(getattr(s, "time_end", None)),
                "total_value": getattr(s, "total_value", None),
                "currency": getattr(cur, "iso_code", None) if cur else None,
            })
    except Exception as exc:  # noqa: BLE001
        errors.record(scope="governance.organization_subscriptions",
                      region=auth.home_region, compartment_id=auth.tenancy_id, error=exc)

    if result["organization_subscriptions"]:
        try:
            svc_client = oci.onesubscription.SubscribedServiceClient(
                **auth.client_kwargs(auth.home_region))
            for org in result["organization_subscriptions"]:
                sid = org.get("id")
                if not sid:
                    continue
                for it in paginate(svc_client.list_subscribed_services,
                                   compartment_id=auth.tenancy_id, subscription_id=sid):
                    prod = getattr(it, "product", None)
                    result["subscribed_services"].append({
                        "id": getattr(it, "id", None),
                        "subscription_id": sid,
                        "product_name": getattr(prod, "name", None) if prod else None,
                        "part_number": getattr(prod, "part_number", None) if prod else None,
                        "status": getattr(it, "status", None),
                        "time_start": _iso_or_none(getattr(it, "time_start", None)),
                        "time_end": _iso_or_none(getattr(it, "time_end", None)),
                        "total_value": getattr(it, "total_value", None),
                        "used_amount": getattr(it, "used_amount", None),
                        "available_amount": getattr(it, "available_amount", None),
                        "funded_allocation_value": getattr(it, "funded_allocation_value", None),
                        "is_payg": getattr(it, "is_payg", None),
                        "pricing_model": getattr(it, "pricing_model", None),
                        "quantity": getattr(it, "quantity", None),
                    })
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="governance.subscribed_services",
                          region=auth.home_region, compartment_id=auth.tenancy_id, error=exc)

    # --- Rate card do contrato (OneSubscription, GET /ratecards) — 1.33.0 ----
    # Preço unitário LÍQUIDO contratado por SKU (net), preço de excedente
    # (overage), desconto discricionário, tiers e vigência POR ENTRADA — o
    # dump de campo (20-07-2026, serviceitoci23) trouxe 3.443 entradas em 5
    # linhas de contrato, com o mesmo part_number repetido entre vigências:
    # a escolha da entrada "vigente" é do GERADOR (função pura do JSON).
    # Gravamos APENAS preço/produto/vigência — nenhum dado de contato.
    result["rate_cards"] = []
    if result["organization_subscriptions"]:
        try:
            rc_client = oci.onesubscription.RatecardClient(
                **auth.client_kwargs(auth.home_region))
            for org in result["organization_subscriptions"]:
                sid = org.get("id")
                if not sid:
                    continue
                for rc in paginate(rc_client.list_rate_cards,
                                   subscription_id=sid, compartment_id=auth.tenancy_id):
                    prod = getattr(rc, "product", None)
                    cur = getattr(rc, "currency", None)
                    tiers = [{
                        "up_to_quantity": getattr(t, "up_to_quantity", None),
                        "net_unit_price": getattr(t, "net_unit_price", None),
                        "overage_price": getattr(t, "overage_price", None),
                    } for t in (getattr(rc, "rate_card_tiers", None) or [])]
                    result["rate_cards"].append({
                        "subscription_id": sid,
                        "subscribed_service_id": getattr(rc, "subscribed_service_id", None),
                        "part_number": getattr(prod, "part_number", None) if prod else None,
                        "product_name": getattr(prod, "name", None) if prod else None,
                        "unit_of_measure": getattr(prod, "unit_of_measure", None) if prod else None,
                        "billing_category": getattr(prod, "billing_category", None) if prod else None,
                        "net_unit_price": getattr(rc, "net_unit_price", None),
                        "overage_price": getattr(rc, "overage_price", None),
                        "discretionary_discount_percentage": getattr(
                            rc, "discretionary_discount_percentage", None),
                        "is_tier": getattr(rc, "is_tier", None),
                        "rate_card_tiers": tiers,
                        "currency": getattr(cur, "iso_code", None) if cur else None,
                        "time_start": _iso_or_none(getattr(rc, "time_start", None)),
                        "time_end": _iso_or_none(getattr(rc, "time_end", None)),
                    })
        except Exception as exc:  # noqa: BLE001
            errors.record(scope="governance.rate_cards",
                          region=auth.home_region, compartment_id=auth.tenancy_id, error=exc)

    return result


def _iso_or_none(v):
    """datetime -> ISO 8601; passa adiante strings/None (fidelidade ao dado)."""
    try:
        return v.isoformat() if hasattr(v, "isoformat") else v
    except Exception:  # noqa: BLE001
        return None
