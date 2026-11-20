# Service IT · Baseline OCI

Ferramenta que coleta o **baseline de uma tenancy Oracle Cloud Infrastructure
(OCI)** com **custo zero** e **100% somente leitura**, e gera uma visão humana e
executiva (relatório HTML). Mesmo espírito do produto de baseline AWS da Service
IT, porém **nativo de OCI** (sem assumir paridade entre as nuvens).

Versão **1.69.0** — validada em tenancy real.

## Princípios inegociáveis

- **Custo zero absoluto:** apenas operações de leitura `List`/`Get`. Nunca cria,
  altera ou remove recursos. A análise de custo é refeita por API OCI (ver
  `collector/references.json`), não herdada do produto AWS.
- **100% read-only:** a coleta jamais modifica a tenancy.
- **JSON auto-contido:** o coletor grava tudo que o relatório precisa, incluindo
  a proveniência de custo por operação.
- **Gerador puro:** o relatório é função do JSON; roda offline. A única exceção
  é a descoberta opcional do logo do cliente (acesso à web, isolado em
  `generator/logo.py`).
- **Dados de cliente nunca versionados** (`.gitignore`; inclui `coletas/` e
  qualquer `*.baseline.json`).

## Serviços cobertos

IAM (compartments, users, groups, policies), Compute (instâncias com S.O.,
CPU/memória, disco boot e de dados, e marcação de worker OKE), Networking (VCNs,
subnets, gateways, route tables, security lists, NSGs), conectividade híbrida
(VPN IPSec Site-to-Site, FastConnect virtual circuits, CPEs, Remote Peering) e DRG
com seus attachments (qual VCN liga a qual DRG),
Balanceadores (Load Balancers e Network Load Balancers), Block Storage (volumes,
boot volumes e seus backups), File Storage (FSS: file systems e mount targets),
Object Storage, Database (DB Systems/DBCS, Exadata Cloud Service, Exadata
Cloud@Customer, Autonomous e os bancos/CDBs com a versão de cada um), OKE
(clusters, node pools managed e virtuais),
Segurança (KMS: vaults e chaves com algoritmo e proteção; Bastion) e Serverless
(Functions com applications e imagem, API Gateway com deployments, Container
Instances e seus containers/imagens).

OCI Monitoring fica **fora** do baseline: a ingestão de métricas é gratuita, mas
o *retrieval* é cobrado, o que violaria a regra de custo zero.

## Estrutura do repositório

```
collector/        Coleta read-only (Python, SDK oci)
  auth.py            Cloud Shell -> config file -> instance principal
  discovery.py       Regiões subscritas + árvore de compartments
  collectors/        iam, compute, network, block_storage, object_storage,
                     database, oke, loadbalancer, filestorage, security,
                     serverless
  collect.py         Orquestrador -> grava 1 JSON
  references.json    Proveniência de custo (dados, não hardcoded na lógica)
generator/        Geração offline (funções do JSON)
  report_html.py     Relatório executivo (identidade Service IT)
  insights.py        Destaques do inventário (achados determinísticos)
  diagram.py         Diagrama Hub DRG (HTML interativo + .drawio editável):
                     subnet→gateway (route table), VCN→DRG, IPSec/FastConnect/
                     Remote Peering→DRG, LB→subnets — só relações comprovadas
  oci_icons.py       Ícones oficiais OCI (asset PNG base64; gerado por tools/)
  tools/             extract_icons.py: regenera oci_icons.py da biblioteca Oracle
  logo.py            Descoberta do logo do cliente (única parte que acessa a web)
  generate.py        CLI: JSON -> relatório HTML + diagrama HTML + .drawio
  qa/                Validação de HTML via jsdom (relatório e diagrama)
manual/           Execução (Cloud Shell) e permissões mínimas
exemplos/         baseline_exemplo.json (sanitizado)
scripts/          build_pacote_cliente.sh (empacota só o collector/ p/ o cliente)
```

## Início rápido

```bash
# 1) Coletar (no OCI Cloud Shell, recomendado)
python3 -m collector.collect --cliente "Nome do Cliente" --site cliente.com

# 2) Gerar o relatório (offline, na máquina da Service IT)
python3 -m generator.generate ./output/<arquivo>.baseline.json
#    --logo ./logo.png   força um logo específico (override)
```

Passo a passo completo e permissões em [`manual/`](manual/README.md).

## Decisões de arquitetura específicas de OCI

- **Hierarquia nativa:** o "org-wide" do AWS é coberto pela varredura recursiva
  de compartments e iteração por região subscrita. IAM é global.
- **Tenancy no Cloud Shell:** resolvido via `OCI_TENANCY` (o delegation token
  reflete o tenancy da infraestrutura, não o do usuário).
- **Specs de shape vêm da API** (`list_shapes`), não de tabela hardcoded.

## Versionamento

SemVer. A tag do repositório é a versão do produto. Versões `1.x` trazem
melhorias compatíveis; `2.0` fica reservado para mudanças incompatíveis (por
exemplo, alteração do schema do JSON). Constantes por componente em
`collector/version.py` e `generator/version.py`; histórico no
[CHANGELOG](CHANGELOG.md).

## Empacotamento para o cliente

O cliente recebe **apenas** o `collector/` (o gerador é interno da Service IT):

```bash
./scripts/build_pacote_cliente.sh   # gera dist/ServiceIT-Baseline-OCI-Coletor-v<versao>.zip
```
