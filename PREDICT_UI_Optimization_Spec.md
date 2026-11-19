# Predict — Especificação de Otimização de Funcionalidades

**Para:** Antigravity (Implementação de Interface)
**De:** Service IT — Produto Predict
**Data:** Abril / 2026
**Versão:** 1.0

\---

## Contexto

A interface atual dos 6 módulos do Predict apresenta **60 funcionalidades distribuídas em cards**, sendo 10 por módulo. Após revisão de produto, identificamos sobreposições, funcionalidades que pertencem a outros módulos e itens de infraestrutura técnica que não deveriam aparecer como cards para o usuário final.

Este documento orienta a **refatoração dos cards de cada módulo**, reduzindo o total de 60 para 35 funcionalidades (-42%), sem perda de capacidade real do sistema.

\---

## Regras Gerais de Refatoração

Antes de implementar, observar os seguintes princípios:

1. **Fusão:** Se dois cards respondem à mesma pergunta do usuário, viram um só card com nome unificado.
2. **Eliminação:** Se um card representa infraestrutura técnica invisível ao operador (ex: fila de execução), ele é removido da grade de cards — pode existir no sistema, mas não como card navegável.
3. **Movimentação:** Se um card pertence semanticamente a outro módulo, ele é realocado.
4. **Descrição:** Toda descrição de card deve ser uma frase curta, ativa e orientada ao que o usuário **faz**, não ao que o sistema faz.

\---

## Módulo 1 — Predict Radar

> \*\*Propósito do módulo:\*\* Alertar o especialista no momento certo.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Dashboard operacional|⚠️ Fundir com Visão executiva|
|Eventos em tempo real|✅ Manter|
|Correlação de eventos|🔀 Mover para Predict Gate|
|Top ofensores|✅ Manter|
|Tendências e recorrências|✅ Manter|
|Perturbações por hostname|✅ Manter|
|Visão por ambiente e serviço|❌ Eliminar (virar aba interna)|
|Linha do tempo dos eventos|❌ Eliminar (virar aba interna)|
|Filtros avançados de análise|🔀 Mover para Predict Discovery|
|Visão executiva da saúde operacional|⚠️ Fundir com Dashboard operacional|

### Resultado Esperado (5 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Painel executivo**|Visão consolidada de saúde operacional em tempo real|
|2|**Eventos em tempo real**|Monitoramento de fluxo contínuo de eventos|
|3|**Top ofensores**|Ranking de hosts por criticidade e volume|
|4|**Tendências e recorrências**|Identificação de padrões sazonais e recorrentes|
|5|**Perturbações por hostname**|Status detalhado de alertas por máquina|

### Orientações de implementação

* **Visão por ambiente e serviço** e **Linha do tempo dos eventos** devem continuar existindo no sistema, mas como **abas ou seções internas** dentro do card "Perturbações por hostname" — não como cards independentes na grade.
* **Correlação de eventos** deve ser removida deste módulo e adicionada ao **Predict Gate** (ver seção correspondente).
* **Filtros avançados de análise** deve ser removida deste módulo e adicionada ao **Predict Discovery** (ver seção correspondente).
* A fusão de "Dashboard operacional" + "Visão executiva" deve resultar em um único card chamado **"Painel executivo"** com acesso a ambas as visões internamente.

\---

## Módulo 2 — Predict Discovery

> \*\*Propósito do módulo:\*\* Especialista mapeia oportunidades de automação.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Cadastro de oportunidades|✅ Manter|
|Mapeamento de processos|✅ Manter|
|Identificação de tarefas|❌ Eliminar (coberto por Mapeamento de processos)|
|Registro de dores|❌ Eliminar (subjetivo, não acionável)|
|Classificação por ganho|⚠️ Fundir com Esforço e Risco|
|Classificação por esforço|⚠️ Fundir com Ganho e Risco|
|Classificação por risco|⚠️ Fundir com Ganho e Esforço|
|Priorização de oportunidades|✅ Manter|
|Estimativa de volumetria|✅ Manter|
|Painel de oportunidades|✅ Manter|

### Resultado Esperado (6 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Cadastro de oportunidades**|Registro de oportunidades identificadas para automação|
|2|**Mapeamento de processos**|Processos e tarefas candidatos à automação|
|3|**Matriz de priorização**|Avaliação consolidada por ganho, esforço e risco|
|4|**Priorização de oportunidades**|Fila consolidada de prioridades para execução|
|5|**Estimativa de volumetria**|Análise estrutural de frequência e impacto esperado|
|6|**Painel de oportunidades**|Visão consolidada do Discovery com filtros avançados|

### Orientações de implementação

* As três classificações (**por ganho, por esforço, por risco**) devem ser fundidas em um único card chamado **"Matriz de priorização"**. Internamente, o card deve apresentar os três eixos de avaliação — a fusão é apenas na interface de navegação, não na lógica de negócio.
* **"Identificação de tarefas"** e **"Registro de dores"** devem ser removidos como cards. Se necessário, o conceito de "dores" pode aparecer como um campo opcional dentro do Cadastro de oportunidades.
* O card **"Painel de oportunidades"** deve absorver os **Filtros avançados de análise** recebidos do Radar, integrando as capacidades de busca e segmentação.

\---

## Módulo 3 — Predict Gate

> \*\*Propósito do módulo:\*\* Especialista valida riscos antes de agir.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Análise de risco operacional|⚠️ Fundir com Verificação de impacto|
|Validação de pré-condições|⚠️ Fundir com Validação de dependências|
|Regras de bloqueio|⚠️ Fundir com Checagem de janelas e Aplicação de políticas|
|Simulação antes da execução|✅ Manter|
|Verificação de impacto|⚠️ Fundir com Análise de risco|
|Validação de dependências|⚠️ Fundir com Validação de pré-condições|
|Checagem de janelas|⚠️ Fundir com Regras de bloqueio e Políticas|
|Aplicação de políticas|⚠️ Fundir com Regras de bloqueio e Janelas|
|Registro de justificativas|⚠️ Fundir com Parecer final|
|Parecer final|⚠️ Fundir com Registro de justificativas|

### Resultado Esperado (5 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Avaliação de risco e impacto**|Análise consolidada de risco operacional e impacto por ativo|
|2|**Checagem de pré-condições**|Validação de dependências e pré-requisitos antes da execução|
|3|**Políticas e restrições**|Regras de bloqueio, janelas permitidas e baseline de segurança|
|4|**Simulação de execução**|Dry-run e validação antes de agir em produção|
|5|**Parecer e auditoria**|Registro de justificativas e decisão de prosseguir ou reter|

### Orientações de implementação

* O card **"Avaliação de risco e impacto"** deve receber também a funcionalidade de **"Correlação de eventos"** movida do Radar — ela se encaixa semanticamente aqui, pois é uma análise de causa raiz que antecede a decisão de agir.
* O card **"Políticas e restrições"** concentra três conceitos anteriores (regras de bloqueio, janelas, políticas de segurança). Internamente pode ter abas ou seções, mas externamente é um único ponto de entrada.
* O card **"Parecer e auditoria"** deve gerar registro imutável — o design deve deixar claro que é uma ação final irreversível dentro do Gate.

\---

## Módulo 4 — Predict Builder

> \*\*Propósito do módulo:\*\* Especialista codifica seu conhecimento em agentes.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Cadastro de agentes|⚠️ Fundir com Edição de agentes|
|Edição de agentes|⚠️ Fundir com Cadastro de agentes|
|Modelagem de regras|✅ Manter|
|Fluxos de decisão|⚠️ Fundir com Entradas e saídas|
|Entradas e saídas|⚠️ Fundir com Fluxos de decisão|
|Instruções e prompts|✅ Manter|
|Versionamento|✅ Manter (mover para cá vindo do Approval)|
|Biblioteca de componentes|✅ Manter|
|Testes de comportamento|✅ Manter|
|Publicação|✅ Manter|

### Resultado Esperado (7 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Gestão de agentes**|Criação e manutenção de agentes de automação|
|2|**Modelagem de regras**|Definição de regras operacionais do agente|
|3|**Modelagem de fluxos**|Construção de árvores de decisão com entradas e saídas|
|4|**Instruções e prompts**|Parametrização de IA e LLMs para comportamento do agente|
|5|**Biblioteca de componentes**|Componentes reutilizáveis entre agentes|
|6|**Testes de comportamento**|Simulação e validação do comportamento do agente|
|7|**Versionamento e publicação**|Controle de versões, histórico e disponibilização para homologação|

### Orientações de implementação

* **"Cadastro de agentes"** e **"Edição de agentes"** são CRUD da mesma entidade. Devem ser fundidos em **"Gestão de agentes"** — a distinção entre criar e editar é tratada internamente pelo estado do objeto (novo vs. existente).
* **"Fluxos de decisão"** e **"Entradas e saídas"** devem ser fundidos em **"Modelagem de fluxos"** — entradas e saídas são propriedades dos nós do fluxo, não uma funcionalidade separada.
* **"Versionamento"** deve ser movido do Approval para o Builder, pois o versionamento é uma atividade de desenvolvimento de agente, não de aprovação. No Builder, deve ser fundido com **"Publicação"** em um único card **"Versionamento e publicação"**.
* Este é o módulo mais técnico — 7 cards é o máximo aceitável dado o perfil do usuário (desenvolvedor de agentes).

\---

## Módulo 5 — Predict Approval

> \*\*Propósito do módulo:\*\* Especialista autoriza a entrada em produção.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Workflow de aprovação|✅ Manter|
|Aprovação por perfil|✅ Manter|
|Controle de versão|🔀 Mover para Predict Builder|
|Parecer técnico|⚠️ Fundir com Parecer executivo|
|Parecer executivo|⚠️ Fundir com Parecer técnico|
|Histórico de tramitações|⚠️ Fundir com Auditoria|
|Checklists de prontidão|✅ Manter|
|Evidências de teste|✅ Manter|
|Liberação por ambiente|✅ Manter|
|Auditoria|⚠️ Fundir com Histórico de tramitações|

### Resultado Esperado (6 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Workflow de aprovação**|Fila e gestão de aprovações ativas|
|2|**Aprovação por perfil**|Controle de alçadas e permissões por papel|
|3|**Parecer por alçada**|Registro de validação técnica e executiva em uma visão unificada|
|4|**Checklists de prontidão**|Verificações obrigatórias antes do Go-Live|
|5|**Evidências de teste**|Anexação e validação de provas de homologação|
|6|**Liberação e rastreabilidade**|Aprovação segmentada por ambiente com histórico auditável|

### Orientações de implementação

* **"Controle de versão"** deve ser **removido** deste módulo e incorporado ao Builder (ver seção anterior).
* **"Parecer técnico"** e **"Parecer executivo"** devem ser fundidos em **"Parecer por alçada"**. A distinção entre técnico e executivo é tratada pelo perfil do aprovador (Aprovação por perfil), não por cards separados. Internamente o card pode exibir campos diferentes conforme o perfil logado.
* **"Histórico de tramitações"** e **"Auditoria"** devem ser fundidos em **"Liberação e rastreabilidade"** junto com a Liberação por ambiente — os três estão relacionados ao ciclo final de aprovação e registro.

\---

## Módulo 6 — Predict Action

> \*\*Propósito do módulo:\*\* Especialista executa com velocidade e governança.

### Situação Atual (10 cards)

|Card|Status|
|-|-|
|Execução assistida|⚠️ Fundir com Execução automática e Disparo de ações|
|Execução automática|⚠️ Fundir com Execução assistida e Disparo de ações|
|Disparo de ações|⚠️ Fundir com Execuções assistida e automática|
|Integração com sistemas|⚠️ Fundir com Scripts e APIs|
|Chamados|✅ Manter|
|Scripts e APIs|⚠️ Fundir com Integração com sistemas|
|Fila de execução|❌ Eliminar (infraestrutura técnica, não feature de usuário)|
|Monitoramento da execução|✅ Manter|
|Registro de resultados|✅ Manter|
|Painel de produtividade|✅ Manter|

### Resultado Esperado (6 cards)

|#|Nome do Card|Descrição|
|-|-|-|
|1|**Modos de execução**|Execução assistida, automática ou agendada em um único ponto|
|2|**Conectores e integrações**|Scripts, APIs e webhooks para comunicação com sistemas externos|
|3|**Chamados**|Abertura e atualização de tickets integrada ao ITSM|
|4|**Monitoramento da execução**|Console e feedback em tempo real da execução|
|5|**Registro de resultados**|Coleta de logs, evidências e rastreabilidade das ações|
|6|**Painel de produtividade**|Relatório global de efetividade e performance dos agentes|

### Orientações de implementação

* **"Execução assistida"**, **"Execução automática"** e **"Disparo de ações"** devem ser fundidos em **"Modos de execução"**. O card deve apresentar internamente um seletor de modo (assistido / automático / agendado). A lógica de negócio não muda — apenas a entrada do usuário é unificada.
* **"Scripts e APIs"** e **"Integração com sistemas"** devem ser fundidos em **"Conectores e integrações"** — ambos tratam da camada de comunicação com sistemas externos.
* **"Fila de execução"** deve ser **removida como card**. Ela continua existindo como mecanismo interno do sistema, mas não deve ser exposta como funcionalidade navegável para o usuário final.

\---

## Resumo Consolidado das Mudanças

|Módulo|Cards Antes|Cards Depois|Redução|Ações|
|-|-|-|-|-|
|Predict Radar|10|5|-50%|2 fusões, 2 eliminações, 2 movimentações|
|Predict Discovery|10|6|-40%|1 fusão tripla, 2 eliminações, 1 recepção|
|Predict Gate|10|5|-50%|5 fusões, 1 recepção|
|Predict Builder|10|7|-30%|2 fusões, 1 recepção do Approval|
|Predict Approval|10|6|-40%|2 fusões, 1 movimentação para Builder|
|Predict Action|10|6|-40%|2 fusões, 1 eliminação|
|**Total**|**60**|**35**|**-42%**||

\---

## Movimentações entre módulos (resumo)

|Funcionalidade|De|Para|
|-|-|-|
|Correlação de eventos|Radar|Gate (Avaliação de risco e impacto)|
|Filtros avançados de análise|Radar|Discovery (Painel de oportunidades)|
|Controle de versão|Approval|Builder (Versionamento e publicação)|

\---

## O que NÃO muda

* A **ordem dos módulos** na barra de navegação superior permanece: Radar → Discovery → Gate → Builder → Approval → Action.
* A **identidade visual** de cada módulo (ícone, cor de destaque, descrição curta) permanece inalterada.
* A **lógica de negócio** e as **funcionalidades técnicas** do sistema não são removidas — apenas a **exposição como cards** é otimizada.
* Os cards que foram eliminados da grade podem continuar existindo como **seções internas**, **abas** ou **campos** dentro de outros cards.

\---

## Critério de aceite

A entrega será considerada concluída quando:

* \[ ] Cada módulo apresentar exatamente o número de cards especificado neste documento
* \[ ] Os nomes e descrições dos cards seguirem exatamente a tabela "Resultado Esperado" de cada módulo
* \[ ] As funcionalidades movidas entre módulos estiverem presentes no módulo de destino
* \[ ] As funcionalidades eliminadas não aparecerem como cards em nenhum módulo
* \[ ] O fluxo de navegação entre os 6 módulos continuar funcionando normalmente

\---

*Documento gerado por Service IT — Produto Predict | Revisão de produto Abril/2026*

