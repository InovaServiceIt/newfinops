# Changelog — Service IT · Baseline OCI

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
A versão do produto = tag do repositório, subida no mesmo commit que altera as
constantes de versão de cada componente (`collector/version.py`,
`generator/version.py`).

## [Não lançado]

## [1.69.1] — 2026-10-26 (Correção: navegação da sidebar)
### Corrigido
- **Clicar num serviço no menu lateral agora rola até ele.** Duas causas somadas:
  (1) a topbar fixa (`appbar`, 56px) cobria o topo da seção — faltava
  `scroll-padding-top`/`scroll-margin-top`; e (2) um serviço **ocultado pelo
  filtro de baseline** (`display:none`) não tinha para onde rolar. Agora o link
  reexibe o tipo oculto antes de rolar e posiciona o título abaixo da barra.
- Só gerador (**1.69.1**); coletor e schema inalterados.

## [1.69.0] — 2026-10-23 (Object Storage: versioning e lifecycle)
### Adicionado
- **Buckets: duas colunas novas no relatório.**
  - **Versioning**: Habilitado / Suspenso / Desabilitado (já vinha do `get_bucket`).
  - **Lifecycle**: "Habilitado · N regras" com **cada regra detalhada** (ação
    Arquivar / Apagar / Acesso infrequente · prazo em dias/anos · alvo: objetos,
    versões anteriores, uploads incompletos; marca a regra desabilitada). Mostra
    "Não" quando o bucket não tem política.
- **Coletor:** `get_object_lifecycle_policy` por bucket (HTTP 404 = bucket sem
  política, tratado como normal e não erro). Apenas metadados de control-plane,
  custo zero. Entrada correspondente em `references.json` (`billable: false`).

### Nota
- Coletor **1.48.0**, schema **1.26.0** (`lifecycle_rules` aditivo), gerador
  **1.69.0**. O **versioning** aparece ao apenas **regerar** o relatório (campo já
  coletado); o **lifecycle detalhado** exige **recoletar** com o coletor 1.48.0.
  Coletas anteriores mostram "n/d" na coluna Lifecycle.

## [1.68.0] — 2026-09-25 (Ponte report ↔ cost na topbar)
### Adicionado
- **Botão "Custos OCI →" na topbar do report** (pill vermelho na
  identidade Service IT, padrão do produto Baseline AWS): atalho direto
  para o relatório de custos, ao lado do logo do cliente. Renderizado
  apenas quando o JSON tem bloco `cost` (via `_outputs`); abre em nova
  aba. O atalho textual da sidebar permanece.
- **Botão "Baseline OCI →" na topbar do cost** (simetria da ponte): o
  caminho de volta, antes só como link no corpo, agora também na topbar.
- QA: `validate_html.js` exige o botão quando há cost (e a ausência
  quando não há); `validate_cost.js` exige o botão de volta.

## [1.67.0] — 2026-09-25 (Consolidado de N tenancies)
### Adicionado
- **Relatório consolidado de N tenancies** (novo módulo
  `generator/consolidado.py`; espelha as decisões do consolidado AWS da
  Service IT, adaptadas ao motor OCI): `generate.py a.json b.json ...`
  gera `baseline_<cliente>_consolidado_<N>-tenancies_<data>.report.html`
  e `.cost.html` (data = a da coleta mais recente). O consolidado REUSA
  os renders standalone sobre um documento MESCLADO: seletor "Tenancy"
  troca KPIs e destaques (painéis pré-calculados em Python, do mesmo
  pipeline do individual) e filtra as tabelas pela coluna "Tenancy",
  presente em todas as vistas do inventário.
- **Regras da mescla** (consolidar não é somar): contagens concatenam;
  percentuais (tags, CreatedBy, MFA) recalculam sobre o conjunto;
  contrato, assinaturas, rate card, forecast nativo, cost reports e
  créditos NÃO consolidam (semântica por tenancy — exclusão declarada no
  relatório); custos só consolidam quando TODAS as tenancies têm bloco
  cost na MESMA moeda (senão o cost consolidado não sai e o motivo é
  impresso). Chave desconhecida no JSON ABORTA a mescla com instrução de
  classificação — bloco novo do coletor nunca some em silêncio.
- **Cost consolidado**: nota de escopo no resumo, donut do mês "por
  tenancy" e seção "Custo por tenancy" (gráfico mensal empilhado +
  tabela YTD/MTD). Advisor, anomalias e demais análises rodam sobre o
  conjunto. Sem diagrama consolidado (topologia mesclada de tenancies
  distintas afirmaria uma rede que não existe; diagramas seguem nos
  individuais).
- QA: checks novos nos dois validadores, incluindo runtime do seletor.
  Só gerador (**1.67.0**); funciona com as coletas existentes.

## [1.66.0] — 2026-08-14 (Fase B do coletor: rede profunda, catálogo de S.O. e tag defaults)
### Adicionado
- **Coletor 1.47.0** (todas List gratuitas, validadas no SDK; custo zero
  preservado): regras e VNICs de cada NSG (`_rules`/`_vnics`, 2 chamadas
  por NSG, falhas agregadas), túneis de cada conexão IPSec com status e
  BGP (`_tunnels`), LPGs e DHCP options por compartment, catálogo de
  imagens da região (campos enxutos de S.O./versão) e tag defaults do
  root. Schema **1.25.0** (aditivo).
- **Gerador 1.66.0** (tudo condicional a coletas 1.47.0+; coletas antigas
  mostram n/d): colunas "Regras"/"VNICs associadas" nos NSGs e "Túneis"
  (status por túnel, DOWN em destaque) nas VPNs IPSec; tipos novos "LPGs
  (Local Peering)" e "DHCP Options" no inventário e no filtro (Network);
  destaques novos — "Túneis IPSec fora de UP" e "VPN IPSec com um único
  túnel operacional" (Resiliência) e "S.O. sem imagem de plataforma atual
  na região" (Governança — indício de geração antiga pelo catálogo vivo
  da Oracle, nunca afirmação de EOL); o card de CreatedBy passa a usar os
  tag defaults coletados para afirmar como FATO se a default existe (e,
  existindo, explica a cobertura baixa como recursos anteriores a ela).
- Manual de permissões: `instance-images` e `tag-defaults` adicionados à
  opção granular; references.json com as 7 operações novas.
### Observação
- **Validação de campo pendente**: release testada com sintético fiel aos
  modelos do SDK; a primeira coleta com o zip 1.47.0 confirma em campo
  (usuário sem janela de recoleta no momento — combinado "aplicar agora,
  testar e ajustar depois").

## [1.65.0] — 2026-08-14 (Segurança: MFA cruzado com federação)
### Alterado
- **O destaque "Usuários IAM sem MFA" agora separa locais de federados**:
  usuário LOCAL sem MFA é risco direto (senha do IAM sem segundo fator);
  usuário FEDERADO autentica no identity provider, onde o MFA pode estar
  aplicado sem o IAM enxergar — o card pede validação da política do IdP
  em vez de acusar. Na lista expansível, cada usuário indica o caso.
  Complementa a coluna Federado da 1.64.0.
  Só gerador (**1.65.0**); basta regerar.

## [1.64.0] — 2026-08-14 (IAM: coluna Federado)
### Adicionado
- **Coluna "Federado" (Sim/Não) na tabela de Usuários IAM** (documento de
  melhorias do time, 14/08): Sim quando a conta é gerida por identity
  provider — `identity_provider_id` (SAML legado) ou `external_identifier`
  (Identity Domain/IDCS) presentes no usuário; Não = usuário local do IAM.
  Fonte declarada no tooltip de cada célula. Dados já coletados (nenhuma
  API nova). Rótulos em pt-BR (Sim/Não), seguindo o padrão do relatório.
  Só gerador (**1.64.0**); basta regerar.

## [1.63.0] — 2026-07-29 (Governança: rastreabilidade de criador)
### Adicionado
- **Destaque "Rastreabilidade de criador (CreatedBy) baixa"** (Governança):
  quando menos de 50% (critério declarado) dos recursos do inventário têm
  a tag automática `Oracle-Tags.CreatedBy`, o card informa a cobertura e a
  ação (ativar a tag default no root compartment — sem custo). Indício por
  cobertura, nunca afirmação sobre a configuração: tag defaults não são
  coletadas (candidata a futura release de coletor), e o card ressalva que
  uma default ativada recentemente também gera cobertura baixa (só vale
  para recursos criados depois dela). Complementa a coluna Responsável da
  1.62.0. Só gerador (**1.63.0**); basta regerar.

## [1.62.0] — 2026-07-29 (Filtro por categoria + coluna Responsável)
### Adicionado
- **Filtro de baseline agrupado por categoria** (pedido do time): os tipos
  do inventário agora aparecem em 7 grupos (Compute, Containers &
  Serverless, Database, Storage, Network, Segurança, IAM & Governança; um
  "Outros" automático acolhe tipos futuros). Cada categoria tem um
  checkbox-mestre que marca/desmarca o grupo inteiro (estado intermediário
  quando misto); a persistência por tipo no navegador continua a mesma.
- **Coluna "Responsável"** em todas as tabelas do inventário (por tipo,
  por região e por compartment): o criador do recurso segundo a tag
  automática `Oracle-Tags.CreatedBy` (exibido encurtado; o valor completo
  no tooltip). Honestidade do método: a coluna só aparece quando a coleta
  tem ao menos um recurso com a tag (senão seria 100% vazia); recursos
  criados antes da tag default ou por automação mostram "—" com o motivo
  no tooltip. Nenhuma API nova: o dado já estava nos defined_tags.
  Só gerador (**1.62.0**); basta regerar.

## [1.61.0] — 2026-07-29 (Gráficos na seção "Por tag de custo")
### Adicionado
- **Gráfico mensal empilhado por VALOR da tag** em cada bloco da seção
  "Por tag de custo" (pedido do time): mesma mecânica do "Mensal por
  serviço" (top 9 valores + Outros, hover por segmento, legenda), com
  "(sem tag)" como série própria em cinza — o buraco de alocação fica
  visível na própria barra. Tabelas YTD e tendência permanecem abaixo do
  gráfico. Só gerador (**1.61.0**); basta regerar.

## [1.60.0] — 2026-07-28 (Duração da execução + validade dos certificados de LB)
### Adicionado
- **Coletor 1.46.0 — tempo de execução** (pedido do time): o terminal agora
  imprime início, término e duração (HH:MM:SS, horário de Brasília), e o
  JSON grava `collection.started_at_utc` e `collection.duration_seconds`.
  O painel ⓘ dos relatórios exibe início e duração da coleta (coletas
  1.46.0+).
- **Coletor 1.46.0 — certificados importados nos Load Balancers**: os
  certificados LEGADOS embutidos no LB (criados direto no recurso, fora do
  serviço Certificates) já vinham com o PEM público no list_load_balancers;
  agora o coletor extrai LOCALMENTE validade, CN e emissor
  (`_cert_parsed`, via pacote cryptography — dependência do próprio SDK
  oci; nenhuma chamada de API nova, custo zero preservado; parse falho
  nunca derruba a coleta). Schema **1.24.0** (aditivo).
- **Gerador 1.60.0**: coluna "Certificado (validade)" na tabela de Load
  Balancers (VENCIDO em destaque) e certificados de LB incluídos nos
  destaques de certificados vencidos/a vencer ("embutido no LB X").
  Caso real que motivou: cert *.ultragaz.com.br embutido em LB, expirado
  desde 2025, invisível ao serviço Certificates.
### Observação
- Coletas anteriores continuam funcionando (campos condicionais); os novos
  dados aparecem a partir da primeira coleta com o pacote 1.46.0.

## [1.59.0] — 2026-07-24 (Correção: BYOL de Windows na OCI não exige dedicated host)
### Corrigido
- **Removido o card "Windows BYOL sem dedicated VM host" (introduzido na
  1.57.0) — a premissa estava errada** (apontado pelo usuário em campo): a
  exigência de host dedicado vale para os "Listed Providers" da Microsoft
  (AWS, Google, Alibaba, Azure de terceiros); a **OCI não é Listed
  Provider**, e o Flexible Virtualization Benefit (Product Terms 2022)
  permite BYOL de Windows Server em infraestrutura compartilhada para
  licenças com Software Assurance ou por assinatura. O card era falso
  positivo estrutural.
- **Advisor A11 corrigido no mesmo ponto** (o texto errado existia desde a
  1.40.0): as ações agora citam a elegibilidade correta (SA/assinatura via
  Flexible Virtualization Benefit; OCI fora dos Listed Providers) em vez
  de "normalmente exige dedicated VM host".
### Removido
- **Enxugamento dos destaques a pedido do time** (revisão de campo): saem
  "Padronização tecnológica (shapes)" (1.57.0), "Recursos de banco com
  licença BYOL" (histórico — a coluna Licença das tabelas de banco segue
  destacando BYOL por recurso), "Security lists sem associação", "Route
  tables vazias associadas a subnets", "Regras liberando SSH/RDP da
  internet" e "Regras liberando todos os protocolos da internet" (1.58.0).
  As regras de rede completas permanecem nos JSONs coletados — insumo
  disponível para um eventual relatório de postura de rede à parte. Dos
  cards de rede permanecem "VCNs com CIDRs sobrepostos" e "FastConnect
  com BGP fora de UP".
- QA: checks novos garantem a AUSÊNCIA dos sete cards removidos e da
  menção a dedicated host como exigência.
  Só gerador (**1.59.0**); basta regerar.

## [1.58.0] — 2026-07-24 (Postura de rede, Fase A — dados já coletados)
### Adicionado
- **Seis destaques de rede** derivados de dados que JÁ estavam nos JSONs
  (o List de security lists devolve as regras completas; nenhuma API nova):
  "Regras liberando SSH/RDP da internet" (ingress 0.0.0.0/0 alcançando
  22/3389 em SL ASSOCIADA a subnet), "Regras liberando todos os protocolos
  da internet" (protocol all de 0.0.0.0/0 — sem dupla contagem com o card
  anterior), "FastConnect com BGP fora de UP" (IPv4; IPv6 DOWN sem IPv6 é
  esperado e excluído), "Security lists sem associação" (higiene), "Route
  tables vazias associadas a subnets" (confirmar isolamento intencional) e
  "VCNs com CIDRs sobrepostos" (pares cross-região via ipaddress; impede
  peering — ressalva de isolamento proposital). Todos com lista expansível.
- **Rejeitados com justificativa** (pedidos da sugestão original): "egress
  irrestrito" (default da OCI — ruído sem sinal), "subnet pública sem
  necessidade aparente" (julgamento que os dados não sustentam) e
  "prefixos BGP anunciados" (a API de inventário não os expõe).
- **Fase B registrada** (release de coletor futura, custo zero, gate de
  recoleta): NSG rules + membros, túneis IPSec com status/BGP, LPG/RPC,
  DHCP options — junto com list_images (sugestão 12).
  Só gerador (**1.58.0**); basta regerar.

## [1.57.0] — 2026-07-24 (Padronização tecnológica de shapes + BYOL sem dedicated host)
### Adicionado
- **"Padronização tecnológica (shapes)"** (sugestão do time, Fase A):
  distribuição dos shapes em uso com o processador REAL do catálogo de
  shapes coletado na região (processor_description), gerações coexistindo
  na mesma família (regra sintática sobre o nome; comparação só dentro da
  família — Standard/DenseIO/Optimized/HPC; GPU fora, pois o sufixo é
  modelo do acelerador) e shapes em uso fora do catálogo atual da região.
  Card informativo de padronização, explicitamente NÃO rotulado como
  vulnerabilidade. A "matriz de EOL de S.O." sugerida foi rejeitada por
  violar o design (nada curado/hardcoded); a alternativa fiel — catálogo
  de imagens de plataforma via list_images — fica para a Fase B (release
  de coletor com gate de recoleta).
- **"Windows BYOL sem dedicated VM host"**: cruza licensing_configs (BYOL
  declarado) com dedicated_vm_host_id ausente — indício de conformidade
  Microsoft a validar com o licenciador, nunca afirmação.
  Só gerador (**1.57.0**); basta regerar.

## [1.56.0] — 2026-07-24 (Reservas de capacidade vazias)
### Adicionado
- **"Reservas de capacidade ativas sem capacidade reservada"** (sugestão do
  time): capacity reservation ACTIVE com 0 instância reservada não gera
  custo, mas também não garante nada — e se existia para garantir
  capacidade (ex.: DR), a garantia não está ativa (risco declarado no
  card). Grupo Governança (sem custo, não é otimização). No detalhe do
  item, o shape configurado na reserva (shape · OCPU / GB · 0 reservado),
  que dá o contexto do que ela deveria proteger. O card de reservas com
  capacidade OCIOSA (que custa 85% do preço) segue separado.
  Só gerador (**1.56.0**); basta regerar.

## [1.55.0] — 2026-07-24 (Backups manuais antigos sem expiração)
### Adicionado
- **"Backups manuais com mais de 365 dias e sem expiração"** (sugestão do
  time): o caso acionável de acúmulo — backup manual não expira sozinho e
  cobra storage até ser removido. O card mostra o espaço ÚNICO consumido
  (efetivamente cobrado) e o referenciado; backups SCHEDULED antigos com
  expiração programada são citados como contraste (retenção de policy,
  intencional, fora da contagem). Na lista expansível: block/boot, data de
  criação, ~GB único e o sinal **"origem já removida"** quando o volume
  protegido não existe mais no inventário — o candidato mais forte a
  limpeza. Campos `source_type`/`expiration_time`/`unique_size_in_gbs` já
  estavam nos JSONs coletados (nenhuma API nova); limiar de 365 dias como
  constante declarada.
  Só gerador (**1.55.0**); basta regerar.

## [1.54.0] — 2026-07-24 (Destaques de bancos: ADBs parados e backup automático)
### Adicionado
- **"Autonomous Databases parados (STOPPED)"** (sugestão do time): conta os
  ADBs parados com o storage provisionado no agregado e por item (workload
  + ~GB) — parado não cobra compute, mas o storage segue cobrado. Wording
  orienta validar clones/testes temporários (os nomes costumam indicar).
  **Standby de Data Guard fica fora da contagem** (role STANDBY é DR
  intencional, não desperdício — exclusão declarada no card).
- **"Databases sem backup automático"**: databases de DB System com
  `auto_backup_enabled` desabilitado explicitamente; confirmar proteção
  externa (RMAN/Data Guard) ou ausência real de recorrência. ADBs não
  entram (backup embutido, não desabilitável). Ambos com lista expansível.
- QA: checks condicionais dos dois cards (exclusão do standby declarada;
  validação de proteção externa citada).
  Só gerador (**1.54.0**); basta regerar.

## [1.53.0] — 2026-07-24 (Instâncias paradas com idade e armazenamento)
### Alterado
- **O destaque "Instâncias paradas (STOPPED)" agora quantifica** (sugestão
  do time): quantas foram criadas há mais de 180 dias (limiar declarado) e
  quantos GB de boot + block as paradas mantêm provisionados — que seguem
  cobrados. Na lista expansível, cada instância mostra shape, idade em dias
  e ~GB associado, priorizando a conversa de terminação/limpeza. Limite
  declarado no próprio card: a idade é da CRIAÇÃO da instância; o
  inventário não registra há quanto tempo está parada (o tempo exato em
  STOPPED exigiria varrer o OCI Audit — API gratuita, porém sem filtro
  server-side por evento e com retenção de 365 dias: varredura client-side
  inviabilizaria o tempo de coleta; fica documentado como possível opt-in
  futuro, fora desta versão).
  Só gerador (**1.53.0**); basta regerar.

## [1.52.0] — 2026-07-24 (Destaques de Resource Scheduler + nomenclatura)
### Adicionado
- **Dois destaques novos do Resource Scheduler** (sugestão do time, validada
  nos dados já coletados — sem API nova): "Schedules com falha na última
  execução" (ativos com last_run_status FAILED; STOP falho = recursos
  ligados gerando custo, START falho = ambiente fora do ar no horário
  esperado) e "Schedules inativos" (validar se a desativação é intencional;
  par start/stop com um lado só ativo deixa recursos permanentemente
  ligados ou desligados). Ambos com a lista expansível (ação + data da
  falha por schedule).
### Alterado
- **Nomenclatura**: "Agendas" vira "Schedules" em todo o relatório — tipo
  do inventário "Schedules (Resource Scheduler)", coluna "Schedule
  start/stop" nas instâncias (termo do produto Oracle).
  Só gerador (**1.52.0**); basta regerar.

## [1.51.0] — 2026-07-24 (Destaques com a lista dos recursos)
### Adicionado
- **Cada destaque enumerável agora expande a lista dos recursos** (sugestão
  do time: "4 boot volumes não anexados — quais?"). Clique em "ver os N
  recursos" abre tabela inline com Nome, Região, Compartment e um detalhe
  específico do achado (GB, shape, IP, validade do certificado, tipo).
  Cobre 16 destaques: paradas, volumes/boots não anexados, sem backup e
  sem política, clusters OKE vazios, LBs sem backend, IPs ociosos,
  reservas ociosas, buckets públicos, usuários sem MFA, chaves em
  software, BYOL de banco e certificados vencidos/a vencer. Teto de 150
  linhas por destaque (o restante segue no inventário/CSV). Elemento
  `<details>` nativo: sem JS novo, offline. Destaques agregados
  (cobertura de tags, variação mensal, budgets) seguem como estão.
- QA: checks de coerência (nº de linhas confere com o rótulo do expansor,
  cabeçalho presente).
  Só gerador (**1.51.0**); basta regerar.

## [1.50.0] — 2026-07-24 (Licença de appliance pelo nome da imagem)
### Adicionado
- **Appliances de marketplace (FortiGate etc.) agora mostram o modelo de
  licenciamento na coluna Licença S.O.** (feedback de campo: FortiGate BYOL
  aparecia como "—"). O modelo consta no NOME da imagem da listagem
  ("...BYOL...", "...PAYG...", "license included") e vira rótulo
  "(imagem)": **BYOL (imagem)** em destaque, PAYG (imagem) ou
  license-included (imagem) — indício forte com fonte declarada no
  tooltip, nunca afirmação (BYOL de appliance = licença do FABRICANTE,
  não faturada pela OCI). Para Windows, o nível "imagem" entra ENTRE o
  declarado (licensing_configs) e a heurística de consumo.
- **Advisor A12**: card "Appliances com BYOL no nome da imagem" com ação
  de conformidade (inventariar licenças/subscrições do fabricante;
  appliance sem subscrição ativa fica sem updates de segurança). Não
  conta quem já declara BYOL em licensing_configs (sem duplicidade).
- QA: checks condicionais novos nos dois validadores.
  Só gerador (**1.50.0**); basta regerar.

## [1.49.0] — 2026-07-23 (Licença Windows declarada por instância + fix do falso "possível BYOL")
### Corrigido
- **Falso positivo de campo (Ultragaz): instâncias Windows PARADAS eram
  rotuladas "possível BYOL — validar"**. Instância não ligada não fatura
  licença, então a ausência de SKU no consumo não diz nada sobre ela —
  paradas agora aparecem como "n/d — parada, sem sinal de consumo".
- **A coluna Licença S.O. agora usa `licensing_configs` da instância**,
  licença DECLARADA pela plataforma (BYOL / license-included / partner,
  rotuladas "(declarado)"). A premissa de 1.42.0 de que "a API não expõe
  licença por instância" estava desatualizada — o campo existe no modelo
  Instance e o coletor (to_dict) SEMPRE o gravou nos JSONs; era o gerador
  que não o lia. **Benefício imediato nas coletas existentes, sem
  recoleta.** A heurística por consumo vira fallback, restrita a Windows
  ligadas sem declaração.
### Alterado
- **Advisor A11 reformulado**: card novo "Instâncias Windows com BYOL
  declarado" (afirmação da plataforma); o indício "sem SKU no consumo"
  passa a contar só as ligadas sem declaração; caso todas as sem
  declaração estejam paradas, o card explica que não há sinal (não é
  indício de BYOL).
- QA: checks novos nos dois validadores (rótulos "(declarado)", nenhuma
  parada com "possível BYOL", ramos novos do A11).
  Só gerador (**1.49.0**); basta regerar.

## [1.48.0] — 2026-07-23 (Legenda dos donuts acima + seleção múltipla com Shift+clique)
### Alterado
- **Legenda dos donuts agora fica ACIMA do gráfico** (como nos gráficos de
  barras), horizontal, com o donut centrado no card.
- **Seleção múltipla na legenda**: clique isola um item; **Shift+clique
  acumula/remove itens do filtro**; novo clique volta ao total. Vale para o
  gráfico **Mensal por serviço** e para os **3 donuts**. A técnica de
  variantes pré-renderizadas de 1.42.0 (que não escala para combinações)
  foi substituída por um motor de redesenho em **JS puro embutido** — dados
  do gráfico em JSON inline, mesma geometria dos SVGs do gerador, 100%
  offline e sem bibliotecas; voltar ao total restaura o SVG original.
  Nos donuts filtrados, o centro mostra a soma da seleção e os tooltips
  mantêm o % referido ao total do mês (sem inflar participação).
- QA: runtime com cliques reais incl. `shiftKey` (isola, acumula 2 séries,
  remove do filtro, restaura o original) no mensal e nos donuts; checks de
  JSON embutido válido e de legenda posicionada antes do SVG.
  Só gerador (**1.48.0**); basta regerar.

## [1.47.0] — 2026-07-23 (Donuts por serviço/região/compartment + leitura em celular)
### Alterado
- **Donut de custo sai do resumo e vira fileira própria** logo abaixo dele
  (feedback: ao lado dos KPIs ficava mal formatado). A fileira traz até
  **3 donuts do mês corrente**: por serviço, **por região** (novo; coletas
  1.37+) e **por compartment** (novo) — mesmo estilo, top 6 + Outros,
  hover por segmento.
### Adicionado
- **Leitura em celular (responsivo essencial)** nos dois relatórios, feita
  100% via `@media (max-width: 900px/640px)`: no desktop nada muda, por
  construção. Em telas pequenas o menu começa recolhido e abre no fluxo,
  KPIs e donuts empilham, abas rolam na horizontal, appbar compacta e
  tabelas mantêm rolagem horizontal. O foco do produto segue sendo o
  navegador do computador; o celular ganha legibilidade, não paridade.
- QA: novos checks nos dois validadores (donut fora do resumo, fileira
  posicionada após o resumo, media queries e auto-recolhimento presentes).
  Só gerador (**1.47.0**); basta regerar.

## [1.46.0] — 2026-07-23 (Inventário por região com detalhe completo)
### Alterado
- **"Inventário por região" agora tem o mesmo nível de detalhe do "Por
  compartment"** (pedido do usuário ao ver a matriz): abaixo do resumo de
  contagens, um bloco colapsável por região com as tabelas completas de
  cada tipo — todas as colunas, OCID, Criado em e o **Compartment** de
  cada item (o análogo da região na visão por compartment). Estado
  aberto/fechado persistido no navegador, filtros/ordenação/CSV herdados.
  Só gerador (**1.46.0**); basta regerar.


## [1.45.0] — 2026-07-23 (Novos pedidos — Fase B: private IPs e certificados)
### Adicionado
- **Coletor: private IPs** (`network.private_ips`) — um GET leve por subnet
  já coletada (rota validada no SDK), com falhas agregadas num único erro
  não-fatal com amostra da causa. **Certificados**
  (`security.certificates`) — GET por compartment no serviço Certificates,
  com **validade** (not_valid_before/after da versão corrente), tipo,
  perfil e estado; campos filtrados. Coletor **1.45.0**, schema **1.23.0**
  (aditivo).
- **Inventário: "IPs Privados"** com a correlação completa IP → vNIC →
  **instância** (via attachments) ou **Load Balancer** (por IP), e a
  subnet resolvida por nome. Os **IPs Públicos** atribuídos a IP privado
  agora resolvem até o recurso final (fecha o limite declarado na 1.44.0).
- **Inventário: "Certificados"** com badge de **VENCIDO** e **"vence em
  Nd"** (≤30 dias, calculado na data da coleta) + dois achados novos no
  grupo Resiliência dos Destaques (vencidos; a vencer em 30d).

## [1.44.0] — 2026-07-23 (Novos pedidos — Fase A)
### Adicionado
- **Inventário por região**: matriz tipos × regiões subscritas com totais,
  após a seção Por compartment, com escopo declarado (recursos regionais;
  IAM e globais ficam de fora).
- **IPs Públicos como tipo do inventário**: IP, Reservado/Efêmero, estado e
  **"Atribuído a"** com o nome resolvido quando a entidade está no
  inventário (NAT Gateway, Load Balancer); IP AVAILABLE ganha badge
  "ocioso (sem associação)" — na 1ª geração real, 18 dos 33 IPs da tenancy
  de validação estavam ociosos. A correlação fina IP privado → instância
  chega com a coleta de private IPs (1.45.0+, limite declarado na célula).
- **Chip do cliente na cor dominante do logo** (quando o relatório é gerado
  com logo): extração via **Pillow, dependência OPCIONAL do gerador** —
  sem a lib (ou logo SVG), o chip permanece vermelho Service IT e nada
  quebra (aviso amigável no terminal com o comando de instalação).
  Filtros de sanidade (ignora fundo branco/cinza/transparente) e texto em
  branco ou preto pela luminância. Só gerador (**1.44.0**); basta regerar.

## [1.43.0] — 2026-07-23 (Feedbacks de cliente — Fase 3: Diário por região)
### Adicionado
- **Coletor: diário por região** (`cost.daily_by_region`) — 11ª consulta da
  Usage API (DAILY × region, mesma janela dos demais diários; dimensão já
  validada em campo na consulta mensal). Coletor **1.43.0**, schema
  **1.22.0** (aditivo).
- **Cost.html: gráfico "Diário por região"** (item 5b, último do documento
  de melhorias) — após o Diário por compartment, com a mesma infra dos
  diários (janelas 30/60/90d e hover enxuto). Condicional a coletas
  1.43.0+; coletas antigas seguem sem o gráfico.

## [1.42.0] — 2026-07-23 (Feedbacks de cliente — Fase 2: funcionalidades)
### Adicionado
- **Coluna "Projeção do mês" na YTD por serviço** (item 7): pro-rata linear
  por serviço (mês corrente ÷ dias completos × dias do mês), calculada na
  data da coleta, com o método declarado no rodapé — o forecast nativo
  segue valendo apenas para o total. Só aparece com 3+ dias completos.
- **Ordenação do inventário no menu lateral** (item 8): botão no grupo
  Inventário cicla padrão → A-Z → quantidade, escolha salva no navegador.
- **Coluna "Licença S.O." nas Instâncias como INDÍCIO rotulado** (item 9):
  a API não expõe licença por instância (decisão registrada) — Windows
  recebe "embutida (indício)" quando a tenancy fatura SKU de licença no
  mês fechado, ou "possível BYOL — validar" quando não; dedicated host é
  anotado; tooltip declara o método e o limite. Demais S.O.: "—".
- **Coluna "Capacity type" nas Instâncias** (item 10): On-demand /
  Preemptible / Capacity reservation / Dedicated host, derivada de campos
  que a instância já traz.
- **Legenda clicável no Mensal por serviço** (item 5c): clique numa série
  a ISOLA (variantes SVG pré-renderizadas por série, fiel à arquitetura
  offline); novo clique volta ao total. Seleção múltipla fica fora por
  exigir motor de gráfico no navegador — limite registrado.
  Só gerador (**1.42.0**); basta regerar.

## [1.41.0] — 2026-07-22 (Feedbacks de cliente — Fase 1: formatação e leitura)
### Alterado
- **Moeda sem redundância** (feedback item 1): com uma única moeda na
  coleta, os KPIs do Resumo perdem o sufixo (o título vira "Resumo —
  valores em BRL") e as tabelas YTD/Drivers levam a moeda para o HEADER
  das colunas, com células limpas. Coleta multi-moeda mantém o sufixo
  (honestidade).
- **KPI longo sem estouro** (item 2): valores acima de 13 caracteres
  (caso real: projeção do ano na casa dos milhões) reduzem a fonte
  automaticamente no card.
- **"Maior driver" → "Maior destaque"** na frase-resumo (item 3).
- **Médias diárias** (item 4): sparkline removido; entram os KPIs "Média
  diária (30d)" e "Média diária (mês corrente)", com o último dia da
  série descartado (potencialmente parcial) e o critério declarado.
- **Ordem da Visão geral** (itens 5a/5e): Mensal por serviço → Diário por
  serviço → Diário por compartment; Budgets, Contrato & assinaturas e
  Queima do contrato foram para o fim da página.
- **Hover enxuto nos gráficos** (item 5d): tooltip mostra série · valor;
  o rótulo do eixo já está visível sob a barra.
- **Linha de total imune a ordenação e filtro** (item 6): em TODAS as
  tabelas com total (Drivers, YTD, Evolução do inventário), a linha fica
  fora do sort e é re-anexada sempre ao fim; o filtro nunca a esconde.
  Só gerador (**1.41.0**); basta regerar.

## [1.40.0] — 2026-07-22 (Licenciamento Windows: inventário × consumo)
### Adicionado
- **Advisor — regra A11 "licenciamento Windows"**: a API de compute não
  expõe licença de instância (BYOL não é atributo do recurso), então o
  cruzamento honesto é: instâncias **Windows do inventário** (campo `os`
  embutido pelo coletor) × **SKU de licença Windows** no consumo por SKU
  do mês fechado. Com SKU presente: card informativo com o **custo
  observado da licença embutida** (por OCPU/hora ligada) e a avaliação de
  BYOL como ação. Sem SKU: card "indício de BYOL/imagem própria — validar
  conformidade", nunca afirmação (instância parada ou criada após o mês
  de referência também não gera o SKU — limite declarado no card). Caso
  real da 1ª coleta Ultragaz: 6 instâncias Windows, zero SKU de licença
  no mês — pergunta de conformidade pronta para a reunião. Só gerador
  (**1.40.0**); basta regerar.

## [1.39.1] — 2026-07-22 (Runway sem dado de saldo — sem alarme falso)
### Corrigido
- **Runway não é calculado quando o contrato não informa saldo** — caso de
  campo na 1ª coleta da Ultragaz: contrato recém-registrado com linhas
  futuras e `available` nulo em todas; o runway tratava nulo como zero e
  diria **"saldo esgotado"** (alarme falso). Agora, sem nenhum dado de
  saldo, a seção declara "runway indisponível" com o motivo; saldo zero
  REAL (linha esgotada com valor informado) continua rotulado como
  esgotado. Só gerador (**1.39.1**); basta regerar.

## [1.39.0] — 2026-07-20 (Resiliência de rede do coletor)
### Adicionado
- **Preflight de alcance por região**: antes da varredura, uma chamada leve
  (~5s, sem retry) testa cada região subscrita; região inalcançável é
  **pulada com aviso claro no terminal** (causa provável e soluções) e vira
  erro não-fatal declarado (`preflight.region`) na seção Erros — coleta
  parcial sempre declarada, nunca silenciosa. `--strict-regions` aborta em
  vez de pular. Motivado por caso de campo real: Cloud Shell em "OCI
  service network" não alcança regiões remotas e a coleta pendurava em
  silêncio por minutos.
- **Sub-progresso por compartment** nos dois serviços mais longos (compute e
  block storage): a barra mostra `região · serviço · compartment i/N` —
  varredura grande nunca mais parece travamento (só em TTY; logs sem ruído).
- **Dica de rede no início** quando a autenticação é Cloud Shell: aviso de
  que tenancy multi-região exige Public network.
- **Manual**: seção "Rede do Cloud Shell" com a policy
  `use cloud-shell-public-network` para o admin do cliente e as
  alternativas (`--regions`, config file fora do Cloud Shell).
### Alterado
- **Timeout explícito e retry limitado em TODOS os clients** (ponto único no
  `client_kwargs`): conectar em até 5s, ler em até 60s, retry apenas para
  429/5xx com teto de 3 tentativas/60s — o retry DEFAULT do SDK tem backoff
  longo e mascarava falhas de rede como travamento. Falha de rede agora
  vira erro não-fatal visível em segundos. Coletor **1.39.0**; schema
  1.21.0 e gerador 1.38.1 inalterados.

## [1.38.1] — 2026-07-20 (Overage vs Funded Allocation — rótulo honesto)
### Corrigido
- **O "overage" dos cost reports agora é rotulado pelo estado do contrato**
  — caso de campo na primeira coleta da Antaq (contrato saudável, Funded
  Allocation com 308 mil de saldo até 2028): a fonte marca no MESMO campo
  tanto o excedente de Universal Credits quanto o **débito normal de
  Funded Allocation** (comportamento documentado), e a seção exibiria
  "100% em OVERAGE" num contrato sem estouro algum. Agora: contrato em
  over-age → "excedente do commitment (é estouro mesmo)"; contrato
  vigente com saldo → "débito normal do commitment (Funded Allocation),
  não estouro"; indeterminado → rótulo neutro com a dualidade declarada.
  Helper `_estado_commitment` compartilhado com o Advisor; nota da fonte
  no rodapé da seção. Só gerador (**1.38.1**); basta regerar.

## [1.38.0] — 2026-07-20 (Cost reports oficiais: desconto medido & overage)
### Adicionado
- **Coletor: cost reports oficiais** (`cost.reports`) — novo módulo lê o
  bucket que a Oracle hospeda (namespace `bling`) e grava **apenas
  agregados** (linhas cruas nunca vão ao JSON): (a) **FOCUS** da última
  partição disponível — BilledCost vs ListCost = **desconto medido**, por
  serviço e por categoria de cobrança, com a data e o **atraso declarados**
  (campo: 9 dias nesta tenancy, bem além das ~24h da doc); (b)
  **proprietário** mais recente (~6h de uso) — **overage discriminado por
  SKU** (`myCostOverage`), a lacuna que a Usage API não cobre. Falha (ex.:
  policy ausente) é não-fatal com escopo claro. Parser coberto por teste
  unitário (14 asserts, incluindo policy ausente). Coletor **1.38.0**,
  schema **1.21.0** (aditivo).
- **Cost.html: seção "Desconto medido & overage"** (aba Análises): frase
  executiva do desconto medido (destaque quando ≤2% — consumo a preço de
  lista), tabela por serviço (faturado × lista × desconto) e o overage do
  intervalo com os maiores SKUs. Honestidade explícita: são **amostras**
  (um dia FOCUS + ~6h do proprietário), não o mês.
- **Custo zero fundamentado** (references.json): o bucket pertence à
  Oracle; no modelo OCI, storage/requests/egress faturam o **dono** do
  bucket — não existe "requester pays". A policy exigida está no manual
  (IAM é gratuito; criada pelo admin, nunca pelo coletor). Usage reports
  antigos (deprecados em 31/01/2025) não são usados.

## [1.37.0] — 2026-07-20 (Custo por região + tendência por tag)
### Adicionado
- **Coletor: custo mensal por região** (`cost.monthly_by_region`) — 10ª
  consulta da Usage API (MONTHLY × region, 12 meses). Probe de campo antes
  de codar: total do mês fechado bateu com a soma por SKU (sanidade
  cruzada). Coletor **1.37.0**, schema **1.20.0** (aditivo).
- **Cost.html: seção "Custo por região"** (aba Detalhamento): tabela
  YTD/MTD por região; em tenancy de uma região só, leitura declarada de
  **concentração regional** (insumo para DR/resiliência e residência de
  dados — não tratado como problema por si).
- **Tendência por valor de tag** (seção Por tag de custo): tabela de
  drivers entre os dois últimos meses **fechados** por valor da tag —
  responde "qual área/projeto está puxando a variação" quando o cliente
  usa cost-tracking tags. Mesma mecânica declarada dos drivers de
  serviço/compartment.

## [1.36.0] — 2026-07-20 (4 insights de custo)
### Adicionado
- **Runway de créditos** (Contrato & assinaturas): saldo disponível das
  linhas não expiradas ÷ burn médio dos últimos meses fechados = "dura ~N
  meses (até MM/AAAA)", comparado com o fim da vigência — esgota ANTES
  (risco de over-age) ou dura ALÉM (saldo possivelmente não consumido).
  Contrato esgotado ganha mensagem própria. Método declarado na nota.
- **Preço efetivo por SKU** (Rate card): custo ÷ quantidade do mês fechado
  ao lado do preço contratado, com Δ% quando desvia mais de 5% da
  referência (preço vigente; nos expirados, o último conhecido). Rotulado
  como média observada — não é auditoria de fatura.
- **Padrão fim de semana × dia útil** (gráfico Diário por serviço + card de
  candidatas a agendamento): média diária de fds como % do dia útil, com o
  último dia da série descartado (potencialmente parcial). Fds ~100% =
  pouca elasticidade fora do horário comercial — o card de agendamento
  ganha o argumento quantificado quando a razão passa de 70%.
- **Concentração (Pareto)** no resumo: top 10 recursos como % do custo
  rastreado por recurso no mês fechado (base declarada: itens sem
  resource_id ficam fora). Só aparece com mais de 10 recursos com custo.
- **Desperdício observável agregado** (topo do Advisor): soma dos custos
  observados dos achados de recursos sem função ativa (órfãos, parados,
  LBs sem backend, IPs) — riscos contratuais ficam fora da soma. Cards do
  Advisor ganham a marcação interna `waste`. Só gerador (**1.36.0**);
  coletor 1.35.0 e schema 1.19.0 inalterados — basta regerar.

## [1.35.1] — 2026-07-20 (Legibilidade do rate card)
### Alterado
- **Rate card mais legível** — 3 ajustes motivados por leitura de campo:
  (1) headers renomeados para **"Dentro do commitment"** e **"Se estourar
  (over-age)"** — os nomes antigos ("Preço líquido"/"Excedente") exigiam
  conhecer o jargão; (2) o badge virou **"mais caro se estourar"** e só é
  **vermelho em SKUs consumidos** — em linhas sem consumo vira pill neutro
  cinza ("acima do contratado" numa linha sem consumo lia-se como consumo
  ocorrido); (3) **abertura enxuta**: a mensagem principal (consumo,
  expirados, risco de estouro) em duas frases; contagens do catálogo e o
  "como ler" das colunas desceram para o rodapé. Só gerador (**1.35.1**);
  basta regerar.

## [1.35.0] — 2026-07-20 (Advisor A3c + retry de throttling)
### Adicionado
- **Advisor — regra A3c "maior parte do consumo sem preço vigente no rate
  card"**: cruzamento determinístico do consumo por SKU do último mês
  fechado com a vigência do rate card na data da coleta; quando o % do
  consumo em SKUs **sem entrada vigente** atinge o limiar (50%, declarado
  no card), dispara card de severidade alta com o nº de SKUs, o consumo
  observado (rotulado: não é projeção) e os maiores ofensores. Dispara
  **independente** da regra A3 (linhas de crédito vêm de outra API — se
  aquela coleta falhar, esta evidência sobrevive). Motivada por achado
  real: 92% do consumo de jun/26 da tenancy de validação em SKUs com
  preços expirados desde 30/03/2026. O card não recalcula preço cobrado —
  aponta o risco contratual objetivo. Gerador **1.35.0**.
### Corrigido
- **Coletor: retry com backoff (2s, 5s) no GET de backup policy assignment**
  quando o Block Storage responde **429 TooManyRequests** — throttling
  recorrente confirmado em campo (3 coletas no mesmo dia: 2/28 assets sem
  resposta). Se persistir após os retries, o erro agregado continua
  não-fatal e explícito. Coletor **1.35.0**; schema **1.19.0 inalterado**
  (nenhum campo novo no JSON).

## [1.34.0] — 2026-07-20 (Consumo por SKU × rate card)
### Adicionado
- **Coletor: consumo mensal por SKU** (`cost.monthly_by_sku`) — 9ª consulta
  da Usage API (MONTHLY × skuPartNumber+skuName, mesma janela de 12 meses):
  custo E quantidade física por SKU/mês. Probe de campo antes de codar
  (serviceitoci23): part numbers B* casam 1:1 com `governance.rate_cards`.
  Mantém SKUs de custo ~0 com uso físico (free/incluído). Coletor
  **1.34.0**, schema **1.19.0** (aditivo).
- **Rate card agora responde "o que EU uso disso?"** — motivado por feedback
  de campo (a tabela de catálogo parecia extrato e confundia): com o consumo
  na coleta, a tabela ganha coluna **"Consumo <mês fechado>"** (quantidade
  física no tooltip), ordena os SKUs consumidos por gasto no topo e **abre
  mostrando só eles**; botão alterna para o catálogo completo (o CSV sempre
  exporta tudo). Texto reformulado: catálogo x extrato explícito no
  parágrafo, não só no rodapé. Coletas antigas: catálogo puro, como antes.
- **Advisor A3b refinado**: com consumo por SKU disponível, o card passa a
  apontar **apenas os SKUs efetivamente consumidos** no mês fechado cujo
  excedente é cobrado acima do contratado, ordenados por gasto e com o
  consumo observado total (rotulado; a parcela exata em excedente não é
  discriminada pela Usage API). Sem consumo nos SKUs afetados, o card não
  aparece. Fallback para o comportamento 1.33.0 em coletas antigas.

## [1.33.0] — 2026-07-20 (Rate card do contrato)
### Adicionado
- **Coletor: rate card do contrato** (`governance.rate_cards`) via
  OneSubscription (`RatecardClient.list_rate_cards`, GET /ratecards por
  organization subscription): preço unitário **líquido contratado** por SKU
  (net), preço de **excedente** (overage), desconto discricionário, tiers,
  moeda e vigência por entrada. Grava apenas preço/produto/vigência — nenhum
  dado de contato. Validado em campo antes de codar (CLI, serviceitoci23:
  3.443 entradas em 5 linhas de contrato, BRL). Coletor **1.33.0**, schema
  **1.18.0** (aditivo).
- **Cost.html: seção "Rate card do contrato"** (aba Detalhamento): tabela dos
  SKUs **vigentes na data da coleta** (1 por part number; com mais de uma
  entrada vigente prevalece a de início mais recente — regra declarada no
  relatório), com preço líquido, excedente (badge "acima do contratado"
  quando overage > net), desconto, tiers ("por faixa", com o detalhe no
  tooltip) e vigência; filtros por coluna e CSV. Honestidade explícita:
  são os preços do contrato do cliente, não a lista pública, e o rate card
  é catálogo de preços, não extrato de uso.
- **Advisor FinOps — regra A3b**: quando o contrato está em **over-age**
  (regra A3) e o rate card vigente tem SKUs com excedente MAIS CARO que o
  líquido contratado, card de severidade alta nomeando os SKUs e a razão
  máxima overage/líquido. Achado de campo que motivou a regra: a relação
  overage/net **varia por SKU** no contrato real (2.408 SKUs com overage
  igual ao net, 448 com overage a preço de lista, 396 outras relações) —
  o Advisor aponta SKU a SKU, nunca generaliza.
### Notas
- A **valoração automática** de recursos ociosos por rate card (quantidade ×
  preço do SKU) ficou de fora por decisão de honestidade: o matching por
  nome de produto é ambíguo (ex.: SKUs Cloud@Customer vs região pública) e
  produziria número errado com cara de precisão. Recomendações continuam
  valoradas pelo **custo observado por OCID** (que já reflete o contrato);
  a ponte definitiva recurso→SKU (Usage API por skuPartNumber) fica para
  uma versão futura, validada em campo antes.

## [1.32.0] — 2026-07-20 (Header estilo app)
### Alterado
- **Interface dos DOIS relatórios (baseline e custos) com cara de sistema**:
  o retângulo preto de informações do topo virou uma **topbar compacta de
  56px** (hambúrguer ☰ + título + chip vermelho com o cliente + badges
  "100% read-only"/data + botão ⓘ + logo reduzido), **fixa ao rolar**
  (sticky). As informações detalhadas (tenancy, home region, criação,
  executor, regiões, contagem/período) migraram para um **painel ⓘ
  "Sobre esta coleta/consulta"** que abre sob o botão e fecha ao clicar
  fora; na impressão o painel sai expandido no papel.
- **Menu lateral unificado em preto nos dois relatórios** (o baseline usava
  sidebar branca) com resumo de contexto no topo (tenancy + contagem no
  baseline; moeda + período nos custos). O menu agora é um **drawer
  ancorado na borda esquerda, colado sob o hambúrguer**, descendo até o
  rodapé — antes o layout centralizado deixava o menu "solto" longe do
  botão em telas largas. Abrir/fechar **desliza em leque** (transição de
  0,28s) em vez de sumir; conteúdo mantém largura de leitura centralizada
  no espaço restante. Estado recolhido do cost segue persistido
  (localStorage); impressão oculta o menu.
- **QA ampliado**: `validate_html.js` e `validate_cost.js` ganham checks do
  header (estrutura da topbar, painel ⓘ com as infos migradas, drawer
  preto/leque nas regras CSS, layout full-bleed e **cliques reais** no
  hambúrguer e no ⓘ via jsdom). Só gerador (**1.32.0**); coletor 1.31.2 e
  schema 1.17.0 inalterados — basta regerar os relatórios.

## [1.31.2] — 2026-07-20
### Alterado
- **Coletor: erro agregado de backup assignments com amostra da causa raiz**
  — motivado pela primeira falha real de campo (serviceitoci23, 20-07: "1
  asset(s) sem resposta" sem motivo). A mensagem passa a incluir tipo,
  status/código e texto do **primeiro erro** ocorrido, permitindo distinguir
  throttling de 404/permissão sem recoletar. Coletor **1.31.2**
  (ressincronizado com a tag); gerador 1.31.1 e schema 1.17.0 inalterados.

## [1.31.1] — 2026-07-18
### Alterado
- **Autonomous: cabeçalho da coluna vira "ECPU"** e a célula mostra **só o
  número** (a unidade subiu para o header, pedido de leitura). ADB legado no
  modelo OCPU continua explícito na célula ("2 OCPU"), divergência à vista
  sem mascarar. Só gerador; basta regerar.

## [1.31.0] — 2026-07-18 (Advisor FinOps)
### Adicionado
- **Advisor FinOps** no relatório de custos (topo da aba Análises): novo
  módulo `generator/finops_advisor.py` com **recomendações por regras
  determinísticas — sem LLM** — sobre os dados já coletados. Cada card
  declara **a regra que disparou, a ação sugerida e a severidade**
  (alta/média/info), com link para a seção de detalhe. Quando calculável, o
  card traz o **custo real observado** dos recursos apontados (cruzamento
  por OCID com `cost.monthly_by_resource`, mês de referência = último
  fechado), rotulado como custo observado — **nunca "economia estimada"**.
  Regras da 1ª leva: storage órfão com custo, reservas de capacidade
  ociosas (85%), over-age do contrato, budgets estourados/com previsão de
  estouro/ausentes com gasto ativo, LBs sem backend com custo, instâncias
  paradas que ainda custam, candidatas a agendamento de desligamento
  (RUNNING com nome não-produtivo e sem agenda no Resource Scheduler),
  cobertura de tags < 80% e IPs reservados ociosos. Ordenação por
  severidade e valor. Só gerador — basta **regerar**.
- QA: checks condicionais (cards, regra+ação declaradas, nota "sem IA",
  honestidade dos valores, aba correta).

### Nota
- Só gerador (**1.31.0**); coletor 1.29.0 e schema 1.17.0 inalterados.
  Regras que dependem de blocos novos (agendas, reservas) só disparam em
  coletas que os tenham — sem falso achado em JSONs antigos.

## [1.30.1] — 2026-07-18
### Alterado
- Removido o subtexto "janela do grão diário" do KPI de média diária (a
  janela já está no rótulo "Média diária (Nd)"). Só gerador; basta regerar.

## [1.30.0] — 2026-07-18
### Adicionado
- **KPI "Projeção do ano"** no relatório de custos, ao lado da projeção do
  mês: **YTD realizado + restante do mês corrente (pelo forecast) + média dos
  últimos 3 meses fechados × meses restantes**. Método declarado no próprio
  card ("cálculo do relatório" — a Usage API não fornece forecast anual);
  determinístico e auditável. Aparece quando há mês corrente no ano e
  projeção do mês disponível. Só gerador — basta **regerar**.

### Nota
- Só gerador (**1.30.0**); coletor 1.29.0 e schema 1.17.0 inalterados.

## [1.29.0] — 2026-07-18 (S.O. real de imagens custom)
### Adicionado
- **S.O. com proveniência honesta em 3 níveis** na tabela de Instâncias:
  1. **Agente OS Management Hub** (quando a tenancy usa OSMH): o coletor
     cruza os managed instances (OCID = o da própria instância) e o GET traz
     `os_name`/`os_version`/kernel **verificados dentro da VM** — a célula
     mostra o S.O. real com badge "agente" (kernel e último check-in no
     tooltip). Tenancy sem OSMH: primeiro erro encerra a varredura da região,
     sem ruído.
  2. Metadado da imagem (fiel para platform images) — comportamento atual.
  3. **Imagem custom/exportada**: o metadado é declarativo e NÃO garante o
     S.O. real — a célula mostra "Custom · <nome da imagem>" como pista
     rastreável, sem fingir certeza. O coletor passa a gravar `image_name`.
- Novo bloco `compute.osmh_managed_instances` por região; **schema 1.17.0**
  (aditivo). `references.json`: 2 operações OSMH documentadas.

### Nota
- Coletor **1.29.0**, gerador **1.29.0**, **schema 1.17.0**. O S.O. via
  agente e o nome da imagem exigem **recoletar** com o coletor 1.29.0.

## [1.28.0] — 2026-07-18 (agendas de start/stop)
### Adicionado
- **Coletor: Resource Scheduler** — novo coletor por região/compartment
  (`list_schedules`, GET): agendas nativas de ligar/desligar recursos com
  ação, recorrência (cron/ICS), próxima/última execução com status e alvos
  (OCIDs diretos ou filtros). Novo bloco `scheduler.schedules` por região;
  **schema 1.16.0** (aditivo). **Limite honesto documentado**: automações
  fora do serviço nativo (Functions com cron, scripts, terceiros) não são
  detectáveis por API.
- **Baseline**: novo tipo de inventário **"Agendas de Recursos (Scheduler)"**
  (agenda, ação humanizada Desligar/Ligar, recorrência, próxima/última
  execução, alvos, estado) e **coluna "Agenda start/stop" nas Instâncias** —
  cruzamento por OCID no gerador mostra "Desligar · <recorrência>" com o nome
  da agenda e a próxima execução no tooltip ("n/d" em coletas < 1.28.0; "—"
  sem agenda; alvos por filtro não são cruzados por instância).
- `references.json`: operação nova documentada (custo zero: o serviço de
  agendamento não tem SKU próprio). QA: checks condicionais.

### Nota
- Coletor **1.28.0**, gerador **1.28.0**, **schema 1.16.0** (aditivo). As
  agendas exigem **recoletar** com o coletor 1.28.0.

## [1.27.0] — 2026-07-18
### Adicionado
- **Gráfico de criações por mês (12 meses)** na Evolução do inventário:
  colunas SVG offline (sem bibliotecas) com o total de recursos criados em
  cada mês-calendário, valor no topo de cada barra, tooltip por mês e o mês
  da coleta marcado como parcial. A **tabela por tipo passa à mesma janela de
  12 meses** (antes 6) — gráfico e tabela contam a mesma história. Mesma
  base honesta: `time_created` dos recursos existentes na coleta. Só gerador,
  basta **regerar**. QA: barras, tooltips e 12 meses no eixo.

### Nota
- Só gerador (**1.27.0**); coletor 1.24.0 e schema 1.15.0 inalterados.

## [1.26.0] — 2026-07-18 (evolução do inventário)
### Adicionado
- **Seção "Evolução do inventário"** no baseline, com dois blocos:
  - **Criações por mês (principal, funciona com uma coleta única)**: tabela
    dos últimos 6 meses-calendário a partir do `time_created` dos recursos
    existentes — Total geral e uma linha por tipo (instâncias, DBCS, LBs,
    buckets, todos os tipos do inventário), com o mês da coleta marcado como
    **(parcial)**. Limite do método declarado na nota: recursos criados **e
    removidos antes da coleta não aparecem** — reduções reais exigem comparar
    coletas.
  - **Comparação entre coletas guardadas (complementar, condicional)**: o
    `generate.py` descobre coletas anteriores da MESMA tenancy na pasta do
    JSON (validação pelo OCID no conteúdo; a mais recente de cada mês, até
    6) e compara por OCID: coluna por coleta, **Δ novos/removidos** vs a
    anterior (pega churn de saldo zero) — a única visão que mostra
    **reduções** de fato. Identidade determinística (OCID; fallback
    nome+compartment+criação; sem nada confiável conta no total e fica fora
    do delta); históricos recebem o mesmo pré-processamento do doc atual.
    Flag `--sem-historico` desliga a descoberta.
- QA: checks do bloco principal (sempre) e da comparação (condicional).

### Nota
- Só gerador (**1.26.0**); coletor 1.24.0 e schema 1.15.0 inalterados. Basta
  **regerar**; guardar uma coleta por mês por cliente amplia o histórico da
  comparação automaticamente.

## [1.25.0] — 2026-07-18
### Adicionado
- **Destaques do inventário: "Recursos criados nos últimos 30 dias"** (grupo
  Governança) — contagem de recursos do inventário com `time_created` nos 30
  dias anteriores à data da coleta, com os 3 tipos mais criados na descrição.
  Ligações e estruturas auxiliares (attachments, shapes, db_homes, node
  pools, assignments) ficam fora da contagem. O destaque aparece **mesmo com
  zero** — estabilidade também é informação. Indicador do ritmo de mudança:
  útil para validar janelas de change, crescimento e aderência ao processo de
  provisionamento. Só gerador — basta **regerar**.

### Nota
- Só gerador (**1.25.0**); coletor 1.24.0 e schema 1.15.0 inalterados.

## [1.24.2] — 2026-07-18
### Alterado
- **Volumes e boot volumes não anexados em destaque**: a célula "Anexado a"
  passa a exibir **"não anexado"** como badge vermelho (mesmo estilo do BYOL)
  — volume órfão é custo recorrente sem consumidor e agora salta aos olhos na
  tabela, alinhado ao achado de Otimização que já existia. Só apresentação;
  basta **regerar**. QA condicional (destaque exigido quando houver órfão).

### Nota
- Só gerador (**1.24.2**); coletor 1.24.0 e schema 1.15.0 inalterados.

## [1.24.1] — 2026-07-18
### Corrigido
- **Autonomous DB: CPU com a unidade correta (ECPU)**. A coluna rotulava
  "OCPU" e lia `cpu_core_count`, que no modelo de compute atual do ADB (ECPU)
  vem **0** — caso real na Antaq: 6 ADBs com 2 ECPUs cada exibidos como "0".
  A célula passa a usar `compute_model` + `compute_count` do próprio summary
  ("2 ECPU"), com fallback para `ocpu_count`/`cpu_core_count` + "OCPU" em
  ADBs legados; o cabeçalho vira "CPU" (a unidade acompanha o valor). Dados
  já estavam no JSON — basta **regerar**. QA verifica valor com unidade.

### Nota
- Só gerador (**1.24.1**); coletor 1.24.0 e schema 1.15.0 inalterados.

## [1.24.0] — 2026-07-18 (Object Storage: classe e replicação)
### Adicionado
- **Buckets: colunas "Classe" e "Replicação cross-region"** no inventário. A
  classe (`storage_tier` Standard/Archive) e os flags de auto-tiering,
  réplica read-only e `replication_enabled` **já vinham no JSON** (o coletor
  sempre fez `get_bucket`) — aparecem ao **regerar**. A coluna de replicação
  mostra "—", "Sim" ou "Sim · região → bucket [status]" quando o destino foi
  coletado; bucket de destino aparece como "réplica (destino)".
- **Coletor: destino da replicação** — `list_replication_policies` por bucket,
  chamado **apenas quando** `replication_enabled=true` (GET raro): região e
  bucket de destino, status e último sync, gravados em
  `replication_policies` dentro do próprio bucket.
- `references.json`: operação nova documentada. QA: checks das colunas e da
  classe renderizada.

### Nota
- Coletor **1.24.0**, gerador **1.24.0**, **schema 1.15.0** (aditivo:
  `replication_policies` no bucket). Classe/flags aparecem ao regerar; o
  **destino** da replicação exige recoletar com o coletor 1.24.0.

## [1.23.0] — 2026-07-18 (retenção de backup)
### Adicionado
- **Coletor: políticas de backup de Block Storage** — `volume_backup_policies`
  (Oracle-defined bronze/silver/gold via list sem compartment + user-defined
  por compartment, com dedup) e `volume_backup_policy_assignments`
  (associação recurso→política via `get_volume_backup_policy_asset_assignment`,
  um GET por volume/boot volume; resposta vazia = sem política, que é
  informação válida; falhas por asset agregadas num único erro não-fatal).
- **Baseline: retenção de backup visível onde existe política**:
  - Volumes e Boot Volumes ganham a coluna **"Política de backup"** (nome +
    "retém até X", cruzado no gerador; **"sem política"** em badge vermelho;
    "n/d" em coletas anteriores à 1.23.0);
  - **Autonomous DB**: coluna **"Retenção backup"** (`backup_retention_period_
    in_days`, dado que já vinha no summary — na coleta real da Antaq: 60 dias);
  - **Databases (CDB)**: coluna **"Backup automático"** (Sim · janela Xd / Não,
    de `db_backup_config`, dado que já vinha no summary);
  - Novo tipo de inventário **"Políticas de Backup (Block)"**: origem
    (Oracle/Cliente), destino cross-region e agendamentos humanizados
    ("diário incremental · retém 7 dia(s); …").
- **Novo achado de Resiliência**: "Volumes sem política de backup" — sem
  recorrência garantida nem retenção gerida; recomenda bronze/silver/gold ou
  política própria conforme o RPO (avaliado só em coletas 1.23.0+, sem falso
  achado em JSONs antigos).
- `references.json`: 2 operações novas documentadas. QA: checks condicionais
  das colunas e da tabela de políticas.

### Decisão de escopo
- **FSS snapshot policies ficam para depois**: o summary do file system não
  expõe a política associada (exigiria um GET por file system) e a associação
  de schedules requer o get da política; candidato futuro documentado.

### Nota
- Coletor **1.23.0**, gerador **1.23.0**, **schema 1.14.0** (aditivo). As
  colunas de política de volume exigem **recoletar**; retenção do ADB e
  backup do CDB aparecem já ao **regerar** coletas existentes.

## [1.22.0] — 2026-07-18
### Adicionado
- **Coluna OCID em todas as tabelas do inventário** (visão por tipo, visão
  por compartment e policies): o identificador aparece **truncado** (…12
  últimos caracteres) para não alargar as tabelas, com o OCID **completo no
  tooltip** e **clique para copiar** (Clipboard API com fallback e feedback
  "copiado!"; acessível via Enter/espaço). O **CSV exporta o OCID completo**:
  o script de exportação passou a preferir `data-csv` quando a célula o
  define — o dado já estava no JSON desde a 1.0 (`id` de cada recurso), basta
  **regerar**. Recursos cujo summary não expõe OCID mostram "—".
- QA: checks da coluna, do truncamento, do `data-ocid`/`data-csv` completos e
  do handler de cópia no `validate_html.js`.

### Nota
- Só gerador (**1.22.0**); coletor 1.21.0 e schema 1.13.0 inalterados.

## [1.21.1] — 2026-07-16
### Alterado
- **Budgets vs realizado legível de relance** (feedback de uso real: a barra
  única saturava em vermelho no estouro e o marcador de previsão flutuava sem
  contexto). Consumo e previsão viram **duas linhas rotuladas na mesma
  escala**, cada uma com o valor e o percentual ao lado; a previsão usa barra
  **listrada** (estimativa ≠ realizado); uma única linha vertical com o rótulo
  "budget (100%)" atravessa as trilhas; a escala se estende ao próximo
  múltiplo de 25% quando consumo/previsão passam de 100% e o **excesso vira
  segmento destacado** — nada satura. Cabeçalho concentra escopo, valor do
  budget, limiares de alerta e badge "estourado"; a linha de texto redundante
  saiu. QA acompanha (linhas rotuladas, previsão condicional).

### Nota
- Só gerador (**1.21.1**); coletor 1.21.0 e schema 1.13.0 inalterados. Basta
  **regerar** os JSONs existentes.

## [1.21.0] — 2026-07-16 (queima do contrato — UCC fase 2)
### Adicionado
- **Coletor: valor e queima do contrato (OneSubscription API)** — dois novos
  blocos em `governance`: `organization_subscriptions` (cabeçalho do
  contrato: serviço ex. Universal Credits, tipo, **valor total**, moeda,
  vigência completa, status) e `subscribed_services` (**linhas de crédito**
  com total, consumido, disponível, alocação financiada, período e status).
  Validado em campo ANTES de codar (CLI na serviceitoci23): contrato de
  **152.176,00 BRL** (28/02/2023 → 30/03/2026) com 5 linhas de crédito,
  incluindo a linha corrente **esgotada** (usado 41.334,00, saldo 0). A rota
  `onesubscription list_subscriptions` retornou 404 na mesma tenancy e não é
  usada. **Privacidade**: o summary da fonte traz endereços/contatos de
  billing; o coletor grava apenas campos financeiros/técnicos.
- **Relatório de custos: bloco "Queima do contrato"** na seção Contrato &
  assinaturas: cabeçalho com o valor total e vigência, tabela por linha de
  crédito (período, status, total, **consumido com %** quando a linha tem
  valor próprio, disponível) e badge "ajuste" para linhas de amendment (a
  fonte as registra com total 0 e uso positivo — explicado na nota, nunca
  agregado por conta própria). **Novo achado**: linha de crédito mais recente
  esgotada e expirada com a tenancy ainda consumindo → callout "consumo
  possivelmente fora de commitment (over-age/PAYG) — validar condições com a
  Oracle/parceiro".
- `references.json`: 2 operações OneSubscription documentadas (com o 404 da
  rota irmã e a política de privacidade); `manual/permissions.md` atualizado.
- QA: checks condicionais do bloco de queima no `validate_cost.js`.

### Nota
- Coletor **1.21.0**, gerador **1.21.0**, **schema 1.13.0** (aditivo:
  `governance.organization_subscriptions` e `governance.subscribed_services`).
  O bloco de queima exige **recoletar** com o coletor 1.21.0.

## [1.20.0] — 2026-07-16 (contrato & assinaturas + reservas de capacidade)
### Adicionado
- **Coletor: subscriptions da tenancy (Organizations API)** — novo bloco
  `governance.subscriptions` via `list_subscriptions` (GET no root; policy
  `read organizations-subscriptions in tenancy`): vigência (início/fim),
  modelo de pagamento (ex.: ANNUAL no Universal Credits), estado e metadados.
  Validado em campo antes de codar (CLI na serviceitoci23: contrato CLOUDCM
  ANNUAL retornado com vigência). Falha é não-fatal
  (`governance.subscriptions`).
- **Relatório de custos: seção "Contrato & assinaturas"** (aba Visão geral,
  condicional ao bloco): tabela com serviço, modelo, início, fim de vigência,
  estado e **situação calculada na data da coleta** — dias restantes, alerta
  de **renovação em até 90 dias** e alerta forte de **vigência expirada com
  estado ACTIVE** ("validar renovação"; caso real observado: end-date no
  passado em registro V1 após renovação não refletida). Nota de proveniência
  com o aviso de honestidade: o relatório reporta o registro, não infere
  datas novas.
- **Coletor: capacity reservations** — `capacity_reservations` no bloco de
  compute por região/compartment (`list_compute_capacity_reservations` +
  `get_compute_capacity_reservation` para as configs por shape). O summary já
  traz **reservado × em uso**.
- **Baseline: "Reservas de Capacidade"** como tipo de inventário (contagem no
  resumo, filtro, tabela com reservado/em uso/ocioso/default) e novo achado
  FinOps de **Otimização**: reservas ativas com capacidade ociosa —
  capacidade reservada e não usada é cobrada a **85% do preço da instância**
  (doc oficial), custo recorrente sem nenhuma VM rodando.
- `references.json`: 3 operações novas documentadas (organizations
  subscriptions, list/get capacity reservations) com fontes e políticas;
  `manual/permissions.md` atualizado.
- QA: checks condicionais da seção de contrato no `validate_cost.js`.

### Nota
- Coletor **1.20.0**, gerador **1.20.0** (ressincronizados), **schema 1.12.0**
  (aditivo: `governance.subscriptions` e `compute.capacity_reservations`;
  major 1 — JSONs antigos seguem renderizando). As seções novas exigem
  **recoletar** com o coletor 1.20.0.

## [1.19.0] — 2026-07-16 (forecast nativo destravado)
### Corrigido
- **Forecast nativo da Usage API funcionando**: o probe A-G rodado em campo
  (serviceitoci23, 16-07) revelou o contrato real do endpoint —
  `time_forecast_started` deve ser **igual** a `time_usage_ended` (regra
  entregue pela própria API no 400 da variante F: "usage end time should be
  equal to the forecast start time") e o ponto de junção precisa ser **ontem**
  (coerente com o atraso ~24h do usage store): com `usage_ended=hoje` o
  serviço devolve o 400 enganoso "date does not have the right precision"
  (mesmo texto da issue `oracle/oci-typescript-sdk#218`); com
  `usage_ended=ontem` aceita (variante D: 47 linhas, 17 projetadas). O coletor
  passa a usar **exatamente a variante validada** (uma tentativa, sem
  especulação); se falhar em outra tenancy, o relatório segue com a projeção
  linear rotulada (degradação já existente). O KPI "Projeção do mês" passa a
  exibir o **forecast oficial da Oracle (exponential smoothing)** quando a
  coleta é feita com o coletor 1.19.0.
- **Probe `scripts/diagnose_forecast.py` reescrito** com as lições de campo:
  espera de 60s entre tentativas e retry em `TooManyRequests` (o rate limit
  da Usage API derrubava as tentativas seguintes, mascarando o diagnóstico),
  status HTTP real no print (antes fixava "[400]") e a variante D em primeiro.
- `references.json -> usage_api.constraints.forecast` atualizado com o
  contrato descoberto e a evidência de campo.

### Nota
- Coletor **1.19.0** (ressincronizado com a tag do produto); gerador 1.18.0 e
  **schema 1.11.0 inalterados** (mesma forma de `cost.forecast`). O forecast
  nativo exige **recoletar** com o coletor 1.19.0 e `--cost`.

## [1.18.0] — 2026-07-16 (navegação por abas e janela dos gráficos)
### Adicionado
- **Abas no relatório de custos**: as seções agrupam em **Visão geral**
  (resumo, budgets, gráficos), **Análises** (anomalias, alocação por tag,
  custos sem tag, variação mensal), **Detalhamento** (tabelas YTD, por
  recurso, por tag, créditos) e **Método & erros**. A sidebar continua
  completa: clicar em qualquer âncora (sidebar ou links internos) ativa a aba
  do alvo antes de rolar; abrir o arquivo com `#hash` cai na aba certa.
  Partição por id com salvaguarda: seção sem classificação cai no
  Detalhamento — nada some. A impressão (`@media print`) expande todas as
  abas; o sticky dos filtros é recalculado ao abrir cada aba.
- **Seletor de janela nos gráficos diários** (30d / 60d / janela cheia):
  variantes do SVG pré-renderizadas offline pelo gerador (função pura; sem
  redesenho em JS), séries e cores estáveis entre janelas, escolha **global**
  aos dois gráficos e lembrada via localStorage (try/catch). Só aparece
  quando a janela coletada comporta mais de uma opção.
- QA: novos checks estruturais (panes cobrem todas as seções, um pane ativo,
  print expande) e dinâmicos (troca de aba por clique e por âncora, troca de
  variante no seletor). Só gerador — basta regerar.

## [1.17.0] — 2026-07-16 (redesign do relatório de custos)
### Modificado
- **Novo design system do cost.html** (linguagem visual de dashboard SaaS,
  identidade Service IT preservada via `BRAND`): fundo claro, cards com cantos
  arredondados e sombras suaves, sidebar escura preto/vermelho, títulos com
  traço vermelho, tabelas com cabeçalho discreto em caixa alta e botões em
  pill. Sem mudança de coleta nem de schema — só gerador, basta regerar.
- **Escala Y honesta nos gráficos**: ticks em valores redondos (passos
  1/2/2,5/5 × 10^k, 4–6 divisões) em vez de frações do pico, com a moeda
  declarada no tick do topo.
- **Rótulo do alvo 80%** na alocação por tag desenhado por cima das barras com
  halo branco (antes a última barra podia encobri-lo).
### Adicionado
- **Frase-resumo executiva** no topo: custo do mês (parcial), variação vs os
  mesmos N dias do mês anterior (MTD justo), maior driver do mês e projeção —
  cada parte só aparece quando o dado existe.
- **Donut "custo do mês por serviço"** ao lado dos KPIs (top 6 + Outros, total
  no centro) com o mesmo destaque por segmento no hover dos demais gráficos;
  **badge de variação ▲/▼** no KPI do mês corrente; **sparkline** dos últimos
  30 dias no KPI de média diária.
- **Leitura financeira nas tabelas**: colunas numéricas detectadas pelo
  conteúdo e alinhadas à direita com dígitos tabulares; cabeçalho e linha de
  filtros fixos ao rolar (tabelas longas ganham altura máxima com rolagem).
- **Menu lateral recolhível** (botão no topo da sidebar; recolhido vira botão
  flutuante no canto, estado lembrado via localStorage com try/catch),
  **scroll-spy** destacando a seção visível e **modo estreito** (<900px) com a
  navegação em faixa compacta.
- **Modo de impressão** (`@media print`): sem sidebar/filtros/botões, cards sem
  sombra e sem quebra interna — PDF limpo direto do navegador.
- QA ampliado no `validate_cost.js`: checks estruturais das novidades e bloco
  dinâmico com `runScripts` (colunas numéricas aplicadas, toggle do menu
  recolhe/expande com `aria-expanded`).

## [1.16.0] — 2026-07-16 (fase FinOps Optimize)
### Adicionado
- **Anomalias no custo diário**: detecção determinística e declarada (sem
  caixa-preta) por série — cada serviço e o total diário. Esperado = mediana
  dos até 28 dias anteriores; dispara acima de 3 desvios robustos (MAD, com
  piso de 5% da mediana), no mínimo 20% e R$ 50 de impacto. Quedas no último
  dia são suprimidas (dia possivelmente parcial pelo atraso ~24h da fonte);
  altas no último dia vêm marcadas. Tabela com observado × esperado × impacto
  × direção, estado saudável quando não há achados e o método por extenso na
  nota — a lista aponta onde olhar, não julga a causa. Só gerador.

## [1.15.0] — 2026-07-16 (fase FinOps Operate)
### Adicionado
- **Budgets vs realizado no relatório de custos**: nova seção no topo (logo
  após o resumo) com uma barra de consumo por budget — semáforo por criticidade
  (neutro / atenção a partir do menor limiar de alerta ou 80% / **vermelho
  quando estourado**), ticks com os limiares das alert rules, marcador com a
  previsão do próprio serviço e escopo resolvido para o caminho do compartment.
  KPI "Budget(s) estourado(s)" no resumo quando houver, e dois novos destaques
  de Governança: **budget estourado** e **budget com previsão de estouro**.
  Dados 100% do serviço de Budgets (actual/forecasted_spend, já coletados
  desde a 1.12.0) — só gerador, basta regerar. Motivado por caso real: budget
  de R$ 3.000/mês com consumo de 116% e previsão de 225% passava discreto na
  seção de governança do baseline.

## [1.14.0] — 2026-07-16 (fase FinOps Inform)
### Adicionado
- **F1 — Alocação de custo por tag (showback)**: nova seção no relatório de
  custos com a **cobertura mensal de alocação** por cost-tracking tag
  (barras 0–100% com linha de alvo 80%, tooltip com o valor sem tag de cada
  mês) e o custo **sem tag** do mês corrente em destaque. Cobertura = 1 −
  (sem tag ÷ total), calculada da própria consulta por tag (fecha com o total
  por serviço — validado em coleta real, delta R$ 0,01). Novo destaque de
  Governança quando a cobertura do mês fica abaixo de 80%. Nota explícita:
  tags automáticas (Oracle-Tags.CreatedBy) tendem a 100% e não substituem tag
  de negócio. Só gerador — basta regerar coletas 1.11.0+ (com --cost e tags).

- **F2 — Maiores custos sem tag (recursos ativos)**: cruzamento do custo por
  recurso com as tags do inventário da própria coleta. Tabela top 15 (com
  agregado dos demais e total), KPI do custo YTD sem tag e novo destaque de
  Governança com a contagem — os candidatos prioritários de tagging para
  showback. Recursos fora do inventário ficam de fora (tags desconhecidas),
  com nota explícita. Só gerador — exige coleta 1.13.0+ (com por-recurso).

- **F3 — Variação mensal (drivers)**: comparação entre os dois últimos meses
  **fechados** (o parcial enganaria) com Δ valor e Δ% e tabelas de drivers por
  serviço e por compartment (top 10 por |Δ|, "novo" para custo estreante), mais
  o **MTD justo**: os primeiros N dias do mês corrente vs os mesmos N dias do
  mês anterior no grão diário. Destaque de Governança quando a variação entre
  meses fechados passa de 20%. Só gerador.

- **F4 — FOCUS nativo (orientação, no lugar de export próprio)**: validado na
  doc oficial que a OCI publica cost reports em **FOCUS 1.0** (CSVs a cada
  ~6h, recurso × hora, 1 ano de retenção) — gerar um "FOCUS-aligned" nosso
  seria um subconjunto redundante do dataset oficial. O relatório de custos e
  o manual passam a orientar o acesso (pasta FOCUS/aaaa/mm/dd + policy
  endorse exata) e registram a complementaridade: o FOCUS da Oracle traz
  `ResourceName` **nulo por design**; o nosso relatório preenche esse vazio
  com os nomes cruzados do inventário. O coletor NÃO baixa os arquivos
  (GetObject cross-tenancy fora do escopo; candidato futuro documentado em
  `references.json -> focus_reports`).

## [1.13.3] — 2026-07-15
### Corrigido
- **Hover por segmento: o retângulo apontado também esmaecia** (só o contorno
  aparecia). Causa: especificidade CSS — a regra do dim
  (`svg:has(rect:hover) rect`) vencia a do realce (`rect:hover`). O dim passa a
  mirar apenas `rect:not(:hover)`: o segmento sob o mouse mantém a cor plena e
  os demais esmaecem. QA passa a verificar o `:not(:hover)`.

### Nota
- Só gerador (**1.13.3**); coletor 1.13.0 e schema 1.11.0 inalterados. Basta
  **regerar** os JSONs existentes.

## [1.13.2] — 2026-07-15
### Alterado
- **Hover dos gráficos de custo passa a ser por segmento**: o retângulo
  (serviço × período) sob o mouse ganha contorno e os demais esmaecem,
  facilitando ler barras empilhadas densas; o efeito anterior de elevar o
  card inteiro foi removido (pedido de uso real). CSS puro (`:has`, com
  degradação graciosa para só o contorno em navegadores antigos); tooltip
  nativo mantido.

### Nota
- Só gerador (**1.13.2**); coletor 1.13.0 e schema 1.11.0 inalterados. Basta
  **regerar** os JSONs existentes.

## [1.13.1] — 2026-07-15
### Alterado
- **Por recurso: recursos sem nome ficaram legíveis.** O fallback deixa de ser
  só "…final-do-OCID": o rótulo passa a trazer o **tipo extraído do próprio
  OCID** (ex.: `loadbalancer …zg77irylqzoq`) com o badge **não inventariado** e
  nota explicando os dois casos típicos (recurso excluído ao longo do período,
  com mês corrente tendendo a 0,00; ou tipo fora do escopo do baseline, como
  Logging). Motivado pela 1ª leitura do relatório em coleta real: a Usage API
  devolveu `resource_name` vazio em 100% das linhas e 433 dos 500 maiores itens
  eram recursos já excluídos.

### Nota
- Só gerador (**1.13.1**); coletor 1.13.0 e schema 1.11.0 inalterados. Basta
  **regerar** os JSONs existentes.

## [1.13.0] — 2026-07-15
### Adicionado
- **Custo por recurso ("hostname")** no relatório de custos: nova consulta
  mensal (12 meses) agrupada por `resourceId` (+`service`, com fallback quando
  a combinação não é aceita). A seção "Por recurso" traz top 12 em barras e a
  tabela YTD × mês corrente com **nomes cruzados do inventário da própria
  coleta** (OCID no tooltip); recursos fora do inventário usam o
  `resource_name` da fonte ou o final do OCID; guard-rail de 500 linhas com
  agregado dos demais. `cost.monthly_by_resource` no JSON; **schema 1.11.0**
  (aditivo). Custo zero: mesma Usage API, groupBy diferente. Exige recoletar
  com o coletor 1.13.0.

## [1.12.0] — 2026-07-15
### Adicionado
- **Coletor: Governança (tags & budgets)** — coleta padrão (100% List/GET,
  custo zero, sem exceção): `list_tag_namespaces` (uma chamada no root com
  `include_subcompartments` cobre a tenancy), `list_tags` por namespace (com
  `is_cost_tracking`/`is_retired`), `list_budgets` (`target_type=ALL`, o
  summary já traz **gasto atual e previsto**) e `list_alert_rules` por budget.
  Novo bloco `governance` no JSON; **schema 1.10.0** (aditivo). Barra de
  progresso ganha a etapa; erros não-fatais em `governance.*`.
- **Relatório: seção "Governança (tags & budgets)"** (condicional ao bloco):
  cards de resumo (namespaces, tags ativas, cost-tracking, aposentadas,
  budgets, alert rules), tabelas de namespaces, tags, budgets (escopo
  resolvido para o caminho do compartment, valor/período, gasto atual,
  previsto) e alert rules. Link na sidebar.
- **Dois novos Destaques de Governança**: tenancy **sem budgets** e **sem
  tags de cost-tracking ativas** (só avaliados quando o JSON tem o bloco —
  coletas antigas não geram falso achado).
- **Carimbo de build no pacote**: o `build_pacote_cliente.sh` injeta data/hora
  de **Brasília** em `BUILD_STAMP` (no repo fica `dev`); o coletor imprime
  `v1.12.0 (build ...)` na primeira linha, grava `collection.collector_build`
  no JSON e os dois relatórios exibem o build no rodapé. Elimina a
  ambiguidade de builds pre-release com o mesmo número (aprendizado de campo).
- **Manual/permissões**: `read tag-namespaces` e `read usage-budgets`
  (nomes a confirmar na doc oficial; Opção A cobre).

### Alterado
- **Horários humanos em Brasília**: o nome do arquivo de coleta
  (`..._dd-mm-aaaa-HHhMM.json`) e o carimbo de build passam a usar o horário
  de Brasília (antes saíam no fuso do ambiente — UTC no Cloud Shell). Os
  timestamps técnicos do JSON permanecem UTC (`generated_at_utc` etc.).

### Corrigido / limitação conhecida
- **Forecast de custos (aprendizado de 2 coletas reais com `--cost`)**: o
  endpoint rejeita requests com `forecast` retornando HTTP 400 "date does not
  have the right precision" **mesmo com todas as datas em T00:00:00Z**
  (confirmado por serialização; mesmo sintoma na issue aberta
  `oracle/oci-typescript-sdk#218`, sem solução oficial). O coletor tenta duas
  variantes (started omitido → started=amanhã, registradas em
  `cost.queries[].forecast_variant`) e degrada graciosamente. O **relatório de
  custos passa a calcular projeção LINEAR do mês** (MTD ÷ dias com dado × dias
  do mês) **sempre rotulada como calculada** quando o forecast nativo falta —
  o KPI de projeção nunca some. Novo `scripts/diagnose_forecast.py` (probe
  read-only, 7 combinações) para mapear o comportamento da tenancy. As demais
  6 consultas de custo foram validadas em coleta real (serviceitoci23, BRL,
  compartmentPath com `/`, cost-tracking tag `Oracle-Tags.CreatedBy`);
  governança validada na mesma coleta (4 namespaces, 13 tags, 1 budget,
  2 alert rules).

### Nota
- Coletor **1.12.0**, gerador **1.12.0**, **schema 1.10.0** (aditivo; major 1).
  A seção de Governança exige **recoletar** com o coletor 1.12.0; os demais
  relatórios seguem funcionando com JSONs antigos.
- A tag v1.11.0 carrega o bug conhecido do forecast (corrigido aqui): quem
  coletar custos deve usar o pacote >= 1.12.0.

## [1.11.0] — 2026-07-14
### Adicionado
- **Coletor: custos (opt-in `--cost`)** via **Usage API** (`RequestSummarizedUsages`),
  a mesma fonte do Cost Analysis do console. Consultas tenancy-wide: mensal
  (12 meses, cobre o YTD) e diário (`--cost-days`, padrão 90) por **serviço** e
  por **compartmentPath** (profundidade automática com degradação em 400);
  **forecast** do mês corrente (BASIC/exponential smoothing); **créditos**
  (queryType ALLCREDIT); **tags de cost-tracking** (descobertas via
  `list_cost_tracking_tags`, até 3, ou `--cost-tag Namespace.Chave`). Cada
  consulta grava proveniência em `cost.queries`; falha em um bloco vira erro
  não-fatal (`cost.*`) sem derrubar o baseline.
- **Relatório de custos PRÓPRIO** (`<base>.cost.html`, gerado só quando o JSON
  tem `cost`): baseline (inventário) e análise financeira não se misturam.
  Página standalone com identidade Service IT: KPIs (YTD, mês corrente,
  projeção do mês, média diária, créditos), gráficos SVG empilhados offline
  (diário/mensal por serviço, diário por compartment) em **cards que se
  destacam no hover**, tabelas YTD × mês corrente por serviço, por compartment
  (com barras) e por tag, créditos mensais, avisos de atraso ~24h, seção de
  **proveniência & método** (consultas executadas, janela, linhas, status) e
  erros `cost.*`. **Link cruzado**: sidebar do baseline aponta para o
  relatório de custos e vice-versa (via `doc["_outputs"]`, injetado pelo
  `generate.py`; os renders seguem funções puras do JSON). QA jsdom dedicado
  (`generator/qa/validate_cost.js`); o QA do baseline passa a garantir que a
  seção de custos NÃO está embutida (novo módulo `generator/cost_html.py`).
- **Manual**: nova permissão opcional (`Allow group ... to read usage-report in
  tenancy`, statement oficial do Cost Analysis) e uso das flags de custo.
- **Visibilidade de falhas de custo** (aprendizado do primeiro teste em campo):
  a seção Custos lista as consultas com `ok: false` (nome + erro) apontando
  para a seção Erros; o coletor imprime no terminal os **escopos** dos erros
  não-fatais ao final da execução (antes só imprimia a contagem).
- **Exportação CSV por tabela**: botão "Baixar CSV" em todas as tabelas do
  baseline e do relatório de custos. Exporta as **linhas visíveis** (respeita
  os filtros de coluna aplicados), separador `;` com BOM UTF-8 (Excel pt-BR
  abre sem assistente), nome do arquivo derivado do título da seção
  (`baseline-oci_*.csv` / `custos-oci_*.csv`). Tudo client-side, offline.
- **Ordenação dos cards do Resumo executivo**: botão alterna a ordem dos cards
  entre padrão → **quantidade ▼** → **quantidade ▲** (escolha persistida no
  navegador, como o filtro). Convive com o filtro de baseline (cards ocultos
  permanecem ocultos ao reordenar).
- **Licença BYOL sempre visível**: coluna "Licença" nas tabelas de DB Systems,
  Exadata (ExaCS/ExaCC) e Autonomous — badge vermelho **BYOL** vs "Incluída"
  (`license_model`, já coletado). Novo destaque de Governança quando há BYOL:
  validar cobertura do parque de licenças Oracle (risco de compliance
  LMS/audit). Basta regerar os JSONs existentes.
- **Data de criação dos recursos**: coluna "Criado em" (dd/mm/aaaa, horário de
  Brasília) em todas as tabelas do inventário, na visão por compartment, nas
  policies por compartment e em usuários/grupos IAM. Dado já coletado
  (`time_created`; OKE usa `metadata.time_created`) — basta **regerar** os
  JSONs existentes. Backups deixam de ter coluna própria (a global cobre).
- **Barra de progresso na coleta**: % de etapas concluídas + etapa corrente
  (IAM, região × serviço, blocos de custo, montagem do JSON). Em TTY é uma
  linha viva (`[████░░]  42% · sa-vinhedo-1 · database`); sem TTY (log/pipe),
  uma linha por etapa. Falha fatal não deixa a linha aberta (`abort()`).


### Alterado
- **Exceção documentada à regra "só List/Get"**: `RequestSummarizedUsages` é um
  POST de **consulta** (read-only, sem SKU de cobrança associado). Racional,
  limites (diário ≤ 3 meses, mensal ≤ 12 meses, atraso ~24h, histórico 12
  meses) e `must_revalidate` registrados em `references.json -> usage_api`.
  O módulo é desativado por padrão justamente por isso.
- **Nome do arquivo de coleta ganha a hora**:
  `baseline_<cliente>_<tenancy>_<dd-mm-aaaa-HHhMM>.json`. Motivado pelo teste
  em campo: duas execuções no mesmo dia geravam nomes idênticos em diretórios
  diferentes e o arquivo errado acabou analisado. `.gitignore`
  (`baseline_*.json`) continua cobrindo o novo padrão.

### Nota
- Coletor **1.11.0** (ressincronizado com o gerador), gerador **1.11.0**,
  **schema 1.9.0** (aditivo: bloco opcional `cost`; major 1 — JSONs antigos
  seguem renderizando e o gerador antigo ignora o bloco novo). A seção de
  custos exige **recoletar com `--cost`**.

## [1.10.1] — 2026-06-23
### Corrigido
- **Diagrama, nomes completos:** rótulos de gateways (IGW/NAT/SGW), Load Balancers,
  OKE, títulos de subnet e linhas de recurso (VM/DB/FSS) deixam de ser cortados —
  quebram em múltiplas linhas dentro do box, com a altura ajustada ao nome.
- **Setas internas ortogonais:** o tráfego interno (subnet→gateway, LB→subnets)
  passou de curva para cotovelo ortogonal (vertical-horizontal-vertical), entrando
  no destino na vertical. Acaba com as setas "tortas"/sobrepostas; pontas menores.
- **Pan mais leve:** o arraste para mover usa `requestAnimationFrame` (uma
  atualização por quadro), eliminando o travamento em diagramas grandes.

### Nota
- Só gerador (1.10.1); coletor 1.9.0 e schema 1.8.0 inalterados.

## [1.10.0] — 2026-06-23
### Adicionado
- **Coletor: DRG attachments** (`list_drg_attachments`, List, custo zero) — informa
  qual VCN está ligada a qual DRG, sem inferência; vira a fonte canônica do
  **VCN → DRG** no diagrama.
- **Diagrama: muito mais interligações comprovadas.** Agora desenha,
  além do tráfego LB → subnets/backends:
  - **subnet → gateway** (Internet / NAT / Service Gateway): lido de
    `route_table.route_rules.network_entity_id` da route table de cada subnet;
  - **VCN → DRG**: por **DRG attachment** (nominal) e, como complemento, quando uma
    route table da VCN aponta para o DRG;
  - **On-premises (IPSec) → DRG**, **FastConnect (virtual circuit) → DRG** e
    **Remote Peering → DRG**.
  Os gateways locais (IGW/NAT/SGW) passam a ser **nós** na VCN (antes eram só
  contagem em texto).
- **Layout "Hub DRG + VCNs empilhadas":** o DRG vira hub na coluna esquerda; a
  conectividade híbrida converge nele (roteada pela margem, sem atravessar os nós
  empilhados) e cada VCN liga ao hub por trilhos ortogonais distintos. Tráfego
  interno em curvas finas de baixa curvatura; o hover isola as conexões do recurso.

### Alterado
- **Logo: descoberta endurecida.** A escolha passou a pontuar por **origem** do
  candidato (logo declarado / `og:image` / apple-touch vencem `favicon.ico` e o
  favicon genérico do Google) combinada com a **área**; favicon minúsculo só é
  usado como último recurso. Passa a registrar `logo_meta.quality`
  (`strong`/`ok`/`weak`). Logo `weak` (favicon genérico, o "oval") **não vai** para
  relatório/diagrama: cai no nome do cliente. Override por `--logo` é sempre exibido.
- **Distribuição de recursos:** os rótulos (caminho do compartment) deixam de ser
  cortados — largura maior, quebra de linha e tooltip com o nome completo no hover.

### Nota
- Gerador **1.10.0**, coletor **1.9.0**, **schema 1.8.0** (aditivo; major 1,
  compatível). A única coleta nova é `list_drg_attachments`. As demais relações
  usam campos que o coletor **já capturava** (`route_tables`/`route_rules`,
  `route_table_id` das subnets, `gateway_id`/`drg_id`/`cpe_id`): essas aparecem só
  **regerando** o JSON já coletado. O **VCN↔DRG nominal** (não inferido por rota)
  requer **recoletar** com o coletor 1.9.0.

## [1.9.0] — 2026-06-23
### Adicionado
- **Ícones oficiais da Oracle no diagrama** (HTML e .drawio). Extraídos do OCI
  Architecture Diagram Toolkit (biblioteca draw.io oficial) compondo as camadas
  vetoriais e gerando PNG transparente, via `generator/tools/extract_icons.py`;
  ficam embutidos como asset em `generator/oci_icons.py` (offline, sem dependência
  em runtime). 19 ícones: VM, OKE, Functions, Container, VCN, LB, IGW/NAT/SGW/DRG,
  API Gateway, DB System/Exadata/Autonomous, Object/File/Block Storage, Vault,
  Bastion.
- HTML: ícones via `<symbol>`/`<use>` (definidos uma vez) nos nós, cards e linhas
  de recurso. `.drawio`: ícone no estilo da célula (data URI no formato aceito
  pelo mxGraph).
- QA: `validate_diagram.js` ajustado; diagrama **rasterizado e revisado
  visualmente** antes de entregar (como manda a lógica de ícones).

### Nota
- Só gerador + asset; coletor segue 1.8.0 e o schema 1.7.0.
- Ícones são do toolkit oficial da Oracle (uso previsto para diagramas de
  arquitetura); regeneráveis com `tools/extract_icons.py` apontando para a sua
  cópia da biblioteca.

## [1.8.0] — 2026-06-23
### Alterado
- **Diagrama: conexões em "leque"** (padrão do produto AWS). As setas de tráfego
  (LB → subnets/backends) saem com âncoras distribuídas na borda dos nós e
  curvatura própria por seta, sem sobreposição quando um recurso tem várias
  conexões. Os Load Balancers passam a ligar também aos **backends**, mapeando o
  IP do backend para a subnet por CIDR (relação comprovada pela coleta). A subnet
  que recebe backend é marcada com a linha **"Destino de LB ×N"** (para a seta
  ancorar) e a ligação pode **cruzar VCN** (arquitetura hub-spoke).
- **Limpeza do site do cliente** (`_clean_site`): entradas como
  `[www.x](https://www.x)`, `https://x/caminho` ou `www.x/` são normalizadas para
  só o domínio — no coletor (grava limpo) e no gerador (corrige coletas já feitas
  e o link do logo). Resolve o caso em que o logo não era encontrado por causa de
  markdown/esquema no campo `--site`.

### Nota
- Apenas lógica de coletor/gerador; o schema do JSON segue 1.7.0.

## [1.7.2] — 2026-06-23
### Corrigido
- Coletor (Database): `list_databases` exige `db_home_id` (ou `system_id`);
  chamá-lo só com `compartment_id` retornava HTTP 400 **MissingParameter**, um
  erro não-fatal por compartment (visto em coleta real: 39 erros, todos deste
  tipo). Agora os databases (CDBs) são coletados iterando pelos **DB Homes** já
  listados. O restante da coleta (compute, rede com VNIC, storage, LB, segurança,
  serverless) rodou sem erros.

### Validação
- Produto completo validado em **tenancy real** (`serviceitoci23`): coletor,
  relatório HTML, diagrama HTML interativo e `.drawio` gerados e validados
  (jsdom + XML) com 13 instâncias, 20 VCNs, 35 subnets, 5 LB + 2 NLB, etc.

## [1.7.1] — 2026-06-23
### Alterado
- Coletor: o JSON da coleta passa a ser nomeado
  **`baseline_<cliente>_<tenancy>_<dd-mm-aaaa>.json`** (o segmento do cliente é
  omitido quando `--cliente` não é informado; cliente e tenancy são normalizados
  sem acentos/espaços). Antes era `<tenancy>_<AAAAMMDD-HHMMSS>.baseline.json`.
- `.gitignore` ganhou `baseline_*.json`, mantendo os dados de cliente protegidos
  com o novo padrão de nome (em qualquer pasta).

### Nota
- O nome usa apenas a data (sem hora): coletar duas vezes no mesmo dia para a
  mesma tenancy sobrescreve o arquivo anterior. Use `--out CAMINHO` se quiser
  preservar os dois.

## [1.7.0] — 2026-06-22
### Alterado
- **Novo diagrama de arquitetura** (`generator/diagram.py`), no padrão do produto
  AWS da Service IT: hierarquia **Região > VCN > Availability Domain > Subnet
  (pública/privada) > recursos**, com faixa de serviços da VCN (OKE, gateways) e
  painel lateral de serviços regionais (buckets, FSS, ADB, Functions, API GW,
  Container Instances, vaults, bastions). Saídas:
  - **HTML interativo** (zoom, pan e destaque de conexões ao passar o mouse);
  - **`.drawio`** (mxGraph, uma página por região, editável no diagrams.net),
    gerado das **mesmas posições** do HTML.
  - Só relações **comprovadas** são desenhadas: instância→subnet (via VNIC),
    LB→subnet, IPSec/Remote Peering→DRG. Nada é inferido.
- O `generate.py` passa a emitir `<base>.diagram.html` e `<base>.diagram.drawio`
  (o antigo `.diagram.svg` foi descontinuado, junto do `diagram_svg.py`).

### Adicionado
- **Coletor**: `list_vnic_attachments` (Compute) para ancorar cada instância na
  sua subnet — base do diagrama. Registrado em `references.json` (control-plane,
  custo zero).
- QA: `generator/qa/validate_diagram.js` (jsdom) para o diagrama.

### Nota
- Schema 1.5.0 → 1.7.0 (nova chave `vnic_attachments`; aditivo e compatível).

## [1.6.1] — 2026-06-22
### Adicionado
- Seção "Distribuição de recursos" (gerador) ganha o bloco **Por compartment**
  (barras de contagem), ao lado de "Por região" e "Por tipo de recurso". Usa o
  `compartment_id` já presente em cada recurso. Complementa a visão detalhada
  "Por compartment" (colapsável), que já existia. Só gerador; coletor e schema
  seguem 1.5.0.

## [1.6.0] — 2026-06-22
### Adicionado
- **Distribuição de recursos** (gerador): nova seção com barras (CSS puro, sem
  bibliotecas) mostrando os recursos **por região** e **por tipo de recurso
  (top 12)**.
- **IAM — visão resumida** (gerador): cartões no topo da seção IAM com Usuários,
  Grupos, Policies, **Compartments** e Usuários com MFA.

### Notas
- Só gerador (usa dados já coletados); coletor e schema seguem 1.5.0.
- Adaptação do baseline AWS: OCI não tem "Roles" (usa grupos, dynamic groups e
  policies) nem "MFA de conta root", então esses dois cartões do AWS não se
  aplicam; em troca, exibimos "Compartments", próprio da OCI.

## [1.5.1] — 2026-06-22
### Adicionado
- Mais achados nos "Destaques do inventário" (gerador), ampliando a paridade com
  o produto AWS onde há equivalente confiável em OCI:
  - **Load balancers sem backends** (otimização): LB/NLB com backend sets sem
    nenhum backend (listener para o vazio; LB ativo cobra por hora).
  - **IPs públicos reservados ociosos** (otimização): reserved IPs sem
    associação (estado AVAILABLE), equivalente aos Elastic IPs ociosos.
  - **Backups de volume acumulados** (governança): quantidade e GB dos backups,
    para revisão de política de retenção.

### Notas
- Sem mudança no coletor (usa dados já coletados); coletor e schema seguem 1.5.0.
- Continuam fora, por falta de equivalente confiável em OCI: geração anterior de
  shape, gp2→gp3, target groups órfãos, AWS Config (o Audit do OCI é sempre
  ativo), VPC default e "volumes sem criptografia" (block volume OCI é sempre
  cifrado).

## [1.5.0] — 2026-06-22
### Adicionado
- **Destaques do inventário**: nova seção no relatório com achados
  **determinísticos** derivados do baseline (sem IA, sem rede, sem custo
  estimado), no espírito do produto AWS. Gerados por `generator/insights.py`
  (função pura). Grupos:
  - **Otimização**: instâncias paradas, volumes e boot volumes não anexados,
    clusters OKE sem nós, IPs públicos reservados.
  - **Segurança**: buckets públicos, usuários IAM sem MFA, chaves KMS em
    software (não HSM).
  - **Governança**: cobertura de tags (defined/freeform; critério maior ou igual
    a 80%) e node pools OKE defasados do control plane.
  - **Resiliência**: volumes e boot volumes sem backup.
- **Coletor**: `list_public_ips` (scope REGION) no Networking, base para o
  achado de IPs públicos reservados. Registrado em `references.json`
  (control-plane, custo zero).

### Notas
- Sem estimativa de custo (US$): a OCI tem API pública de preços, mas usá-la
  faria o coletor acessar a web externa; ficou para uma etapa opt-in futura. Os
  destaques trazem contagens e ações, sem cifra.
- "Geração anterior" de shape e a migração gp2→gp3 (do produto AWS) não têm
  equivalente confiável em OCI e não foram portados (sem flag nativa; gp2/gp3
  não existe).

## [1.4.0] — 2026-06-22
### Adicionado
- **Containers das Container Instances**: o coletor lista os containers
  (`list_containers`) e o relatório mostra, por container, o **nome**, a
  **imagem** (`image_url`; a tag indica a versão do código) e a qual Container
  Instance pertence. É a forma de ver a "versão" do que roda em cada instância
  (o objeto da instância não traz isso).
- **Versão do Kubernetes por node pool** (OKE): a tabela de Clusters OKE ganha a
  coluna **Versão nós** (versões distintas dos node pools managed), ao lado da
  versão do control plane, útil para flagrar nós defasados.
- `references.json`: `list_containers` (control-plane, custo zero).

### Notas
- `list_containers` é coberto pela família `compute-container-instance-family`
  já listada no manual de permissões (sem nova permissão).
- Functions não expõe runtime (.NET/Python) como campo; a versão do código é
  inferida pela tag da imagem (coluna Imagem, desde a v1.3.2).

## [1.3.2] — 2026-06-22
### Alterado
- Relatório (gerador): **API Deployments** ganham a coluna **Gateway**,
  indicando a qual API Gateway cada deployment pertence (via `gateway_id`).
  **Functions** ganham a coluna **Imagem** (imagem OCIR do container; a tag
  indica a versão do código). Os dois dados já vinham na coleta; apenas a
  apresentação muda. Coletor e schema seguem 1.3.0.

## [1.3.1] — 2026-06-22
### Alterado
- Relatório (gerador): as tabelas de **DB Systems (DBCS)** e dos **VM Clusters de
  Exadata** (ExaCS e ExaCC) passam a mostrar **OCPU, memória (GB) e
  armazenamento de dados** (GB no DBCS, TB no Exadata), além do nº de nós no
  ExaCS. Os campos já vinham na listagem do coletor (`memory_size_in_gbs`,
  `data_storage_size_in_gbs`/`_tbs`, `cpu_core_count`/`ocpus_enabled`); apenas
  não eram exibidos. Coletor e schema seguem 1.3.0; só a apresentação muda.

## [1.3.0] — 2026-06-22
### Adicionado
- **Versão do banco de dados** no relatório, onde a API expõe:
  - Novo tipo **Databases (CDBs)**: o coletor lista os bancos (`list_databases`)
    e os DB Homes (`list_db_homes`); o relatório cruza os dois e mostra, por
    banco, a **versão** (vinda do DB Home via `db_home_id`), o workload e a que
    DB System/Exadata pertence. Permite **contar quantas bases** existem em cada
    Exadata ou DB System.
  - Coluna **Versão** em DB Systems (DBCS) e Autonomous (ADB), e **Versão GI /
    Sistema** nos VM Clusters de Exadata (ExaCS e ExaCC) — versão do Grid
    Infrastructure e da imagem do Exadata.
- `references.json`: `list_databases` e `list_db_homes` (control-plane, custo
  zero).

### Notas
- A versão do banco não está no objeto do Exadata VM Cluster nem do DB System;
  vem do DB Home (`db_version`), associado a cada banco por `db_home_id`.
- `list_databases` é chamado por compartment; se em alguma tenancy a API exigir
  `system_id`/`db_home_id`, o erro fica registrado (não-fatal) e a coleta segue.
  A validar em coleta real.

## [1.2.1] — 2026-06-22
### Alterado
- Relatório (gerador): os Load Balancers passam a ser exibidos em **uma única
  lista**, em vez de duas tabelas separadas (Load Balancers e Network Load
  Balancers). A tabela ganha a coluna **Tipo**, com "Application (L7)" para o
  Load Balancer (LBaaS, camada 7) e "Network (L4)" para o Network Load Balancer
  (camada 4). O coletor segue coletando os dois normalmente; apenas a
  apresentação muda. Coletor permanece 1.2.0, schema 1.2.0.

## [1.2.0] — 2026-06-22
Ondas 2 e 3 de novos serviços (Segurança e Serverless). Adições compatíveis
(schema 1.1.0 → 1.2.0). Tudo `List`, custo zero validado por introspecção do
SDK e registrado em `references.json`.

### Coletor (custo zero, 100% read-only)
- **KMS** (novo `collectors/security.py`): vaults (`list_vaults`) e, por vault
  ativo, as chaves no endpoint de gerenciamento do vault (`list_keys`) — apenas
  metadados (nome, algoritmo, modo de proteção HSM/software). Nunca toca o
  crypto endpoint.
- **Bastion** (`security.py`): `list_bastions`.
- **Functions** (novo `collectors/serverless.py`): applications e, por
  application, as functions.
- **API Gateway** (`serverless.py`): gateways e deployments.
- **Container Instances** (`serverless.py`): `list_container_instances`.
- `references.json`: 8 novas operações de leitura registradas como não-cobráveis
  (com nota de que invocação de function e crypto endpoint, que cobram, não são
  usados pelo coletor).

### Gerador (offline, função pura do JSON)
- 8 novos tipos no inventário: Vaults e Chaves (KMS, com algoritmo e proteção),
  Bastions, Applications e Functions (com memória e application de origem),
  API Gateways, API Deployments e Container Instances. Campos validados contra
  os modelos reais do SDK.

### Decisão de escopo
- **Cloud Guard, Logging e Certificates** ficaram fora desta entrega (não
  priorizados pelo time). Podem entrar numa onda futura.

## [1.1.0] — 2026-06-22
Primeira leva de novos serviços após a v1.0.0 (Onda 1). Adições compatíveis:
o schema do JSON cresce com chaves novas, sem quebrar consumidores existentes
(schema 1.0.0 → 1.1.0). Tudo segue `List`/`Get`, custo zero confirmado por
introspecção do SDK e registrado em `references.json`.

### Coletor (custo zero, 100% read-only)
- **Balanceadores**: Load Balancers (LBaaS) e Network Load Balancers (NLB), em
  novo `collectors/loadbalancer.py` (chave `load_balancers` por região).
- **Conectividade híbrida** (em `network.py`): VPN IPSec Site-to-Site
  (`list_ip_sec_connections`), FastConnect virtual circuits
  (`list_virtual_circuits`), CPEs (`list_cpes`) e Remote Peering Connections
  (`list_remote_peering_connections`).
- **File Storage (FSS)**: file systems e mount targets, em novo
  `collectors/filestorage.py` (varre por availability domain).
- **Backups de Block Storage**: `list_volume_backups` e
  `list_boot_volume_backups` (em `block_storage.py`).
- `references.json`: 11 novas operações de leitura registradas como não-cobráveis.

### Gerador (offline, função pura do JSON)
- Novos tipos no inventário (cards, filtro de baseline, visão por tipo e por
  compartment): Load Balancers, Network LB, VPN IPSec, FastConnect, CPEs, Remote
  Peering, Backups de Volume, Backups de Boot Volume, File Systems e Mount
  Targets, com colunas próprias (visibilidade público/privado e IPs nos LB,
  banda/provedor/BGP nos circuits, uso em bytes no FSS, tipo/tamanho/data nos
  backups). Campos validados contra os modelos reais do SDK.

## [1.0.0] — 2026-06-22
Primeira versão estável (encerra a fase de PoC). Reúne o coletor read-only de
custo zero e o gerador de relatório offline, validados em tenancy OCI real. A
partir daqui o versionamento segue SemVer: 1.x para melhorias compatíveis; 2.0
apenas para mudanças incompatíveis (ex.: schema do JSON).

### Coletor (custo zero, 100% read-only)
- Autenticação com detecção automática: Cloud Shell (delegation token), config
  file e instance principal. Tenancy via `OCI_TENANCY`; executor pelo nome IAM.
- Descoberta de regiões subscritas e árvore de compartments.
- Serviços: IAM, Compute (com S.O. e attachments de disco), Networking, Block e
  Object Storage, Database (DBCS, Exadata CS/CC, Autonomous) e OKE (clusters,
  node pools managed e virtuais). Branding (cliente/site) e modo `--discover-only`.
- Manifesto de proveniência de custo (`references.json`) gravado no JSON.

### Gerador (offline, função pura do JSON)
- Sidebar de navegação; Resumo executivo com filtro de baseline (salvo no
  navegador); duas visões (por tipo e por compartment colapsável); filtro por
  coluna e ordenação; datas em horário de Brasília; logo do cliente embutido.

### Distribuição
- Pacote do cliente apenas com `collector/`, via `scripts/build_pacote_cliente.sh`.

O histórico de desenvolvimento (0.1.0 a 0.4.x) permanece abaixo para referência.

## [0.4.6] — 2026-06-22
### Adicionado
- Relatório (gerador): **filtro por coluna** (um campo por coluna que reduz as
  linhas, combinando em AND) e **ordenação** ao clicar no cabeçalho (asc/desc,
  numeric-aware) em todas as tabelas. 100% client-side, sem libs. Só o gerador
  muda.

## [0.4.5] — 2026-06-22
### Corrigido
- Relatório (gerador): tabelas largas (ex.: Instâncias, com muitas colunas)
  passam a ter rolagem horizontal (`overflow-x:auto`) e ficam contidas no card,
  em vez de a última coluna ("Região") vazar para fora. Só o gerador muda.

## [0.4.4] — 2026-06-22
### Alterado
- Relatório (gerador): removidas as tabelas separadas de "Node Pools OKE
  (managed)" e "Virtual Node Pools OKE". O resumo por cluster (Node pools /
  Workers / Nós virtuais) na tabela "Clusters OKE" já entrega a visão. O coletor
  segue coletando os node pools (são a base do resumo). Só o gerador muda.

## [0.4.3] — 2026-06-22
### Alterado
- Relatório (gerador): a tabela "Clusters OKE" passa a resumir os nós de cada
  cluster — colunas **Node pools** (managed), **Workers** (total) e **Nós
  virtuais** (total). Permite ver no nível do cluster quando ele está só criado
  (vazio, só control plane), quando tem workers (e quantos) e quando roda
  serverless. Só o gerador muda; coletor segue 0.4.1.

## [0.4.2] — 2026-06-22
### Alterado
- Relatório (gerador): a coluna "Disco" das instâncias foi separada em
  **"Boot (GB)"** e **"Dados/block (GB)"**, distinguindo o disco de sistema do
  disco de dados anexado. Apenas o gerador muda; o coletor (pacote do cliente)
  segue em 0.4.1.

## [0.4.1] — 2026-06-22
### Adicionado
- **OKE — virtual node pools**: coleta de `list_virtual_node_pools` (modo
  serverless), além dos managed. Clusters que rodam em virtual nodes (sem
  instâncias Compute) deixam de parecer falsamente "vazios".
- Coluna de **tamanho** (size desejado) nos node pools managed, ao lado dos
  workers reais, para evidenciar discrepâncias.

## [0.4.0] — 2026-06-22
### Adicionado
- **Database**: coleta de DB Systems (DBCS), Exadata Cloud Service (cloud VM
  clusters), Exadata Cloud@Customer (VM clusters) e Autonomous (ADB); cada tipo
  é identificado no inventário.
- **OKE / Kubernetes**: coleta de clusters e node pools; instâncias worker são
  marcadas ("Worker OKE · cluster") e distinguidas das VMs avulsas (via
  get_node_pool → nodes).
- **Compute**: sistema operacional das instâncias (via `get_image`) e colunas
  separadas de CPU (OCPU), memória (GB) e disco (boot + block anexados).
- **Storage**: coluna "Anexado a" (instância) vs "não anexado", a partir de
  `list_volume_attachments`/`list_boot_volume_attachments`; bucket com tamanho e
  acesso (Público/Privado) em colunas.
### Alterado
- Visão "Por compartment" agora é colapsável (cada compartment é um `<details>`).

## [0.3.0] — 2026-06-22
### Adicionado
- Relatório no estilo do produto AWS: sidebar de navegação (Visão geral +
  Inventário com contagens), filtro de baseline expansível/recolhível
  (checkboxes por tipo, com a escolha salva no navegador via localStorage) e
  cards de KPI clicáveis.
- Duas visões do inventário: por tipo de recurso e por compartment > região.
- Cabeçalho com nome do cliente, "coleta executada por" (nome IAM do usuário
  autenticado, resolvido via `get_user`; no Cloud Shell o OCID é extraído do
  delegation token; flag `--executado-por` para override), nº de regiões
  varridas e nº total de recursos inventariados.
- Logo clicável que leva ao site do cliente quando descoberto pelo site (no
  override `--logo` o logo não recebe link).
### Alterado
- Removido o selo "Custo zero" do cabeçalho (mantidos o selo "100% read-only" e
  a seção "Custo & proveniência").
- Descoberta de logo reescrita com a lógica do produto AWS: captura `<img>` de
  logo com `srcset`/`data-src` (lazy-load), logo declarado (JSON-LD/itemprop/
  image_src), og:image e ícones; baixa e mede os candidatos (SVG vence; depois
  maior área), com fallback para `/favicon.ico` e favicon Google 256px.
- Cabeçalho: rótulo "tenancy criada em" (deixa explícito que a data é a criação
  da tenancy).

## [0.2.0] — 2026-06-22
### Adicionado
- Relatório exibe o sufixo "(root)" no compartment raiz (convenção do Console OCI).
- Data de criação dos compartments e do root no relatório, coletada via
  `get_compartment` (`time_created`); `get_tenancy` não expõe esse campo.
- Branding do cliente: o coletor pergunta nome e site (flags `--cliente`/`--site`
  ou modo interativo) e grava em `branding` no JSON.
- Logo do cliente no relatório: descoberta em cascata a partir do site (logo na
  página com SVG priorizado → og/twitter:image → ícones → favicon), com medição
  dos candidatos e embutido em base64 (single-file). Override por arquivo local
  ou URL via `--logo`. O acesso à web fica isolado em `generator/logo.py`;
  `render_report` permanece função pura do JSON.
### Alterado
- Datas e horários do relatório são exibidos em horário de Brasília, no formato
  DD/MM/AAAA HH:MM:SS (o JSON segue neutro em UTC).

## [0.1.2] — 2026-06-22
### Corrigido
- **Assinaturas de List inconsistentes no SDK OCI.** `list_network_security_groups`
  e `list_volumes` aceitam apenas `**kwargs` (sem `compartment_id` posicional),
  causando `TypeError` durante a coleta. Os coletores passam a enviar
  `compartment_id` sempre como keyword, robusto à inconsistência do SDK.
  Encontrado em tenancy real: a coleta registrou os 2 erros sem abortar.

## [0.1.1] — 2026-06-22
### Corrigido
- **Resolução de tenancy no Cloud Shell.** O coletor usava o `signer.tenancy_id`
  do delegation token, que reflete o tenancy da infraestrutura da Oracle e não o
  do usuário, causando `NotAuthorizedOrNotFound` em `list_region_subscriptions`.
  Agora o tenancy é resolvido por `OCI_TENANCY`/`OCI_CLI_TENANCY`, com fallback
  ao signer. Diagnosticado e validado em tenancy real (a CLI funcionava, os
  OCIDs diferiam).

## [0.1.0] — 2026-06-19
### Adicionado
- Estrutura inicial do repositório espelhando o produto AWS: `collector/`,
  `generator/`, `manual/`, `exemplos/`.
- **Coletor (custo zero, 100% read-only)**:
  - Autenticação com detecção automática: Cloud Shell (delegation token) →
    config file (`~/.oci/config`) → instance principal.
  - Descoberta de regiões subscritas e varredura recursiva da árvore de
    compartments (equivalente nativo ao "org-wide").
  - Coletores do núcleo de infraestrutura: IAM, Compute, Networking,
    Block Storage e Object Storage (apenas operações `List`/`Get`).
  - Modos de execução: coleta completa, filtro por região (`--regions`) e
    `--discover-only` (valida auth/descoberta sem coletar recursos).
  - Manifesto de referências (`references.json`): proveniência de custo por
    operação de API e fontes oficiais, gravado no JSON de saída.
- **Geradores (funções puras do JSON, offline)**:
  - Relatório HTML executivo organizado por compartment (pelo path hierárquico)
    e, dentro de cada um, por região, com índice navegável e identidade
    Service IT. O JSON segue agrupado por região; o gerador pivota pelo
    `compartment_id` presente em cada recurso.
  - Diagrama de arquitetura em SVG.
- **Manual**: passo a passo de execução em Cloud Shell e policy de permissão
  mínima (read-only).
- Validação de HTML com jsdom no QA.
- **Empacotamento para o cliente**: `scripts/build_pacote_cliente.sh` gera
  `dist/ServiceIT-Baseline-OCI-Coletor-v<versao>.zip` contendo apenas a pasta
  `collector/` (sem docs internos, sem `generator/`), com a versão lida de
  `collector/version.py`.

### Decisões de arquitetura (custo zero)
- **OCI Monitoring fica fora do baseline.** A FAQ oficial de billing confirma
  que o *retrieval* de métricas (`summarize_metrics_data`) é cobrado por
  *analyzed metric data point*, ainda que a ingestão em namespaces OCI seja
  gratuita. Coleta de utilização será um módulo opt-in (fase 2) com aviso de
  custo, desativado por padrão.

### A validar (ponto aberto)
- Alinhamento fino do schema do JSON com o produto AWS (não disponível neste
  repositório no momento da criação).
