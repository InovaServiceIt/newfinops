"""Versão do componente coletor.

A versão do produto (tag do repositório) sobe no mesmo commit que altera estas
constantes. Mantenha sincronizado com generator/version.py e o CHANGELOG.
"""

COLLECTOR_VERSION = "1.48.0"

# Versão do schema do JSON de saída. O gerador valida compatibilidade contra
# este valor. Mudanças incompatíveis exigem incremento de MAJOR.
# 1.9.0: bloco opcional "cost" (Usage API) — aditivo, compatível com major 1.
# 1.10.0: bloco "governance" (tag namespaces/tags, budgets, alert rules) — aditivo.
# 1.11.0: cost.monthly_by_resource (custo por recurso/hostname) — aditivo.
# 1.18.0: governance.rate_cards (preços contratados por SKU) — aditivo.
# 1.19.0: cost.monthly_by_sku (consumo mensal por skuPartNumber) — aditivo.
# 1.20.0: cost.monthly_by_region (distribuição regional do custo) — aditivo.
# 1.21.0: cost.reports (agregados dos cost reports oficiais) — aditivo.
# 1.22.0: cost.daily_by_region (diário por região) — aditivo.
# 1.23.0: network.private_ips + security.certificates — aditivo.
# 1.24.0: collection.started_at_utc/duration_seconds + _cert_parsed nos
#         certificados embutidos dos LBs (derivado local do PEM) — aditivo.
# 1.25.0 (Fase B): NSG._rules/_vnics, ip_sec_connections._tunnels,
#         network.local_peering_gateways/dhcp_options, compute.images_catalog,
#         governance.tag_defaults — aditivo.
# 1.26.0: object_storage.buckets[].lifecycle_rules (regras de ciclo de vida);
#         versioning do bucket já vinha do get_bucket — aditivo.
SCHEMA_VERSION = "1.26.0"

# Carimbo de build do PACOTE distribuído. No repositório fica "dev"; o
# scripts/build_pacote_cliente.sh injeta data/hora UTC ao empacotar, para
# distinguir builds pre-release com o MESMO número de versão.
BUILD_STAMP = "dev"

PRODUCT_NAME = "service-it-baseline-oci"
