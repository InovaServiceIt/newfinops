# Predict Discovery — Especificação: Painel de Oportunidades com Dados Preditivos
**Para:** Antigravity (Implementação de Interface)
**De:** Service IT — Produto Predict
**Data:** Abril / 2026
**Versão:** 1.0

---

## Contexto

O módulo **Predict Discovery** já existe na interface. Esta especificação detalha a implementação da funcionalidade **"Painel de oportunidades"** — o último card do módulo — consumindo dados reais da view `vw_predict_plano_acao_hostname`.

O objetivo desta primeira versão é **demonstração de valor**: mostrar ao cliente as oportunidades de melhoria já identificadas pelo modelo preditivo, rankeadas por criticidade, com narrativa pronta e ação recomendada. Não há automação nem recursividade nesta versão — apenas leitura e exibição dos dados.

---

## Query de origem

```sql
SELECT
  id_plano,
  id_contrato,
  nome_entidade,
  ranking_hostname,
  nm_host,
  nm_completo,
  descricao_chave,
  description_exemplo,
  rotulo_acao,
  tx_acao_inicial_suger,
  prioridade_sugerida,
  justificativa_resumida,
  titulo_melhoria,
  score_hostname,
  qt_eventos,
  pct_volume_contrato,
  dias_com_evento,
  qt_disaster,
  qt_high,
  qt_average,
  qt_warning,
  pct_graves,
  pct_nao_graves,
  ultima_ocorrencia,
  dt_inicio_analise,
  dt_fim_analise,
  dt_registro_sistema,
  nu_requisicao_itsm,
  cd_agente,
  cd_categoria,
  nu_evm
FROM public.vw_predict_plano_acao_hostname
ORDER BY id_plano
```

---

## Mapeamento de campos → interface

### Campos do card (listagem)

| Campo da view | Uso na interface | Observação |
|---|---|---|
| `nm_host` | Título do card | Nome curto exibido em destaque |
| `prioridade_sugerida` | Badge de prioridade | P2 = vermelho, P3 = amarelo, P4 = cinza |
| `rotulo_acao` | Badge de tipo de ação | Ver tabela de badges abaixo |
| `justificativa_resumida` | Descrição do card | Texto narrativo gerado pelo modelo |
| `score_hostname` | Barra de score | Valor numérico + barra proporcional |
| `dias_com_evento` | Destaque numérico | Ex: "63 dias em alerta" |
| `pct_graves` | Indicador de gravidade | Ex: "100% eventos graves" |
| `cd_categoria` | Rótulo de categoria | Exibido em fonte menor abaixo do título |
| `nu_requisicao_itsm` | Status do chamado | Nulo = botão "Abrir chamado" / Preenchido = badge "Em andamento" |
| `nome_entidade` | Filtro / agrupamento | Usado para filtrar por cliente |

### Tabela de badges por `rotulo_acao`

| Valor do campo | Label exibido | Cor |
|---|---|---|
| `INVESTIGAR_CAUSA_RAIZ_DE_EVENTO_CRITICO_RECORRENTE` | Causa Raiz | Vermelho `#E74C3C` |
| `REVISAR_CAPACIDADE_DO_HOST` | Capacidade | Laranja `#E67E22` |
| `REVISAR_COLETA_E_CONECTIVIDADE_DO_MONITORAMENTO` | Conectividade | Amarelo `#F39C12` |
| `REVISAR_LIMIARES_E_SUPRESSAO_DE_RUIDO` | Limiares | Azul `#2980B9` |
| `ANALISE_ESPECIALIZADA_DO_EVENTO` | Análise Especializada | Roxo `#8E44AD` |
| `REVISAR_REGRA_DE_NEGOCIO_OU_INTEGRACAO` | Regra / Integração | Cinza escuro `#2C3E50` |
| `REVISAR_DISPONIBILIDADE_DE_SERVICO_OU_PORTA` | Disponibilidade | Vermelho escuro `#922B21` |

### Campos do painel de detalhe (ao clicar no card)

| Campo da view | Uso na interface | Observação |
|---|---|---|
| `nm_completo` | Título completo no detalhe | Nome expandido do host |
| `descricao_chave` | Problema detectado | Resumo técnico do evento |
| `description_exemplo` | Log de evidência | Exibir em bloco de código ou caixa destacada |
| `tx_acao_inicial_suger` | Ação recomendada | Exibir em destaque como call-to-action |
| `qt_eventos` | Total de eventos | Exibir como métrica |
| `qt_disaster` | Eventos Disaster | Métrica com cor vermelha |
| `qt_high` | Eventos High | Métrica com cor laranja |
| `qt_average` | Eventos Average | Métrica com cor amarela |
| `qt_warning` | Eventos Warning | Métrica com cor azul |
| `pct_volume_contrato` | % do volume do contrato | Ex: "20,79% do volume total" |
| `ultima_ocorrencia` | Última ocorrência | Data/hora formatada |
| `dt_inicio_analise` | Início da análise | Janela do modelo |
| `dt_fim_analise` | Fim da análise | Janela do modelo |
| `dt_registro_sistema` | Identificado pelo Predict em | Data em que o modelo gerou o registro |
| `titulo_melhoria` | Título do chamado | Pré-preenchido ao abrir chamado |
| `cd_categoria` | Categoria GLPI | Pré-preenchido ao abrir chamado |
| `cd_agente` | Agente responsável | Pré-preenchido ao abrir chamado |
| `nu_evm` | EVM vinculado | Pré-preenchido ao abrir chamado |
| `nu_requisicao_itsm` | Número do chamado | Exibir link se já existir |

---

## Experiência 1 — Painel de Oportunidades (listagem)

### Layout geral

- Grade de cards em **2 colunas** no desktop, 1 coluna no mobile
- Cards ordenados por `ranking_hostname` ASC (menor ranking = mais crítico = aparece primeiro)
- Barra de filtros no topo da listagem
- Contador de resultados: ex: **"825 oportunidades identificadas"**
- Badge fixo no topo da tela: **"Gerado pelo Predict Radar"** com ícone de radar e data de `dt_registro_sistema` mais recente

### Barra de filtros

| Filtro | Campo | Tipo |
|---|---|---|
| Cliente | `nome_entidade` | Select (dropdown) |
| Prioridade | `prioridade_sugerida` | Multi-select (P2, P3, P4) |
| Tipo de ação | `rotulo_acao` | Multi-select com badges |
| Categoria | `cd_categoria` | Select |
| Status do chamado | `nu_requisicao_itsm` | Toggle: Todos / Sem chamado / Com chamado |

### Anatomia do card

```
┌─────────────────────────────────────────────────────┐
│  [Badge: P2 vermelho]  [Badge: Regra / Integração]  │
│                                                     │
│  ODO - BRDTCJBS22                        Score      │
│  Administração de Middleware             4.924 ████  │
│                                                     │
│  Padrão recorrente associado a aplicação,           │
│  regra de negócio ou integração, com 2.462          │
│  eventos em 60 dias...                              │
│                                                     │
│  ⚠ 60 dias em alerta    🔴 0% eventos graves        │
│                                                     │
│  [Abrir chamado]                    [Ver detalhe →] │
└─────────────────────────────────────────────────────┘
```

**Detalhamento dos elementos do card:**

- **Badges no topo:** `prioridade_sugerida` (esquerda) + `rotulo_acao` traduzido (direita)
- **Título:** `nm_host` em negrito, tamanho 16px
- **Subtítulo:** `cd_categoria` em cinza, tamanho 12px
- **Score:** valor de `score_hostname` + barra de progresso proporcional ao maior score da lista
- **Descrição:** `justificativa_resumida` truncada em 3 linhas com reticências
- **Linha de métricas:** `dias_com_evento` + `pct_graves` com ícones
- **Botão primário:** "Abrir chamado" — visível apenas se `nu_requisicao_itsm` for nulo. Se preenchido, substituir por badge verde **"Chamado #XXXX"** com link
- **Botão secundário:** "Ver detalhe" — abre o painel lateral de detalhe

---

## Experiência 2 — Painel de Detalhe (ao clicar em "Ver detalhe")

### Comportamento

- Abre como **painel lateral (drawer)** da direita, sobrepondo parcialmente a listagem
- Largura: 480px no desktop
- Fechamento: botão X ou clique fora do painel

### Seções do painel de detalhe

**Seção 1 — Cabeçalho**
- `nm_completo` como título
- `cd_categoria` como subtítulo
- Badges de `prioridade_sugerida` e `rotulo_acao`
- Score em destaque: número grande + label "Score de criticidade"
- Label: "Gerado pelo Predict Radar em [dt_registro_sistema]"

**Seção 2 — Problema detectado**
- Título da seção: "Problema detectado"
- Campo `descricao_chave` em texto normal
- Campo `description_exemplo` exibido em **caixa de código** (fundo escuro, fonte monospace) — representa o log real do evento

**Seção 3 — Métricas do período**
- Janela de análise: `dt_inicio_analise` → `dt_fim_analise`
- Grid 2x3 com os seguintes indicadores:

| Indicador | Campo | Cor |
|---|---|---|
| Total de eventos | `qt_eventos` | Neutro |
| Dias em alerta | `dias_com_evento` | Laranja |
| Disaster | `qt_disaster` | Vermelho |
| High | `qt_high` | Laranja |
| Average | `qt_average` | Amarelo |
| Warning | `qt_warning` | Azul |

- Barra de severidade: `pct_graves` em vermelho + `pct_nao_graves` em verde — exibir como barra horizontal dividida
- Linha: "Representa `pct_volume_contrato`% do volume total do contrato"
- Linha: "Última ocorrência: `ultima_ocorrencia`"

**Seção 4 — Ação recomendada**
- Título da seção: "Ação recomendada"
- Campo `tx_acao_inicial_suger` em destaque — caixa com borda esquerda vermelha
- Botão primário: **"Abrir chamado no GLPI"**
  - Pré-preenche: `titulo_melhoria`, `cd_categoria`, `cd_agente`, `nu_evm`
  - Após abertura: salva `nu_requisicao_itsm` de volta no registro
  - Se chamado já existir: exibir **"Chamado já aberto: #[nu_requisicao_itsm]"** com link externo

**Seção 5 — Rastreabilidade**
- Exibir em fonte pequena e cor cinza:
  - `id_plano` — ID do plano de ação
  - `id_contrato` — ID do contrato
  - `nu_evm` — EVM vinculado
  - `cd_agente` — Agente responsável

---

## Experiência 3 — Abertura de Chamado

### Comportamento ao clicar em "Abrir chamado"

1. Exibir modal de confirmação com os dados pré-preenchidos:

```
Confirmar abertura de chamado?

Título:     [titulo_melhoria]
Categoria:  [cd_categoria]
Agente:     [cd_agente]
EVM:        [nu_evm]

[Cancelar]  [Confirmar e abrir]
```

2. Ao confirmar: chamar API do GLPI via webhook
3. Retorno da API: salvar `nu_requisicao_itsm` no registro
4. Atualizar o card na listagem: substituir botão "Abrir chamado" por badge **"Chamado #XXXX"**
5. Exibir toast de confirmação: "Chamado #XXXX aberto com sucesso"

### Campos enviados ao GLPI

| Campo GLPI | Origem |
|---|---|
| Título | `titulo_melhoria` |
| Categoria | `cd_categoria` |
| Agente atribuído | `cd_agente` |
| EVM | `nu_evm` |
| Descrição | `justificativa_resumida` + `tx_acao_inicial_suger` |
| Prioridade | Mapeamento: P2 → Alta, P3 → Média, P4 → Baixa |

---

## Estados do card

| Situação | Estado visual |
|---|---|
| `nu_requisicao_itsm` nulo | Botão "Abrir chamado" ativo |
| `nu_requisicao_itsm` preenchido | Badge verde "Chamado #XXXX" + link |
| Score acima de 3.000 | Borda do card em vermelho com sombra |
| `prioridade_sugerida` = P2 | Borda superior do card em vermelho escuro |
| `pct_graves` = 100% | Ícone de alerta vermelho na linha de métricas |

---

## Label de origem dos dados

Em **todos os cards e no painel de detalhe**, exibir de forma visível:

> 🔴 **Gerado pelo Predict Radar** — Análise de [dt_inicio_analise] a [dt_fim_analise]

Isso diferencia oportunidades geradas pelo modelo preditivo de oportunidades cadastradas manualmente pelo usuário, reforçando o valor do produto.

---

## O que esta versão NÃO faz (escopo futuro)

- Não atualiza os dados automaticamente — a view é consultada sob demanda
- Não cria registros novos na view — apenas leitura
- Não fecha o loop de score — validação pós-execução é Onda 3
- Não gera narrativa automaticamente — o texto já vem pronto da view
- Não tem recursividade — cada execução é independente

---

## Critério de aceite

- [ ] O card "Painel de oportunidades" no módulo Discovery abre a listagem com dados reais da view
- [ ] Os cards exibem os campos mapeados na seção "Anatomia do card"
- [ ] Os filtros funcionam corretamente para todos os campos especificados
- [ ] O painel de detalhe abre ao clicar em "Ver detalhe" com todas as seções preenchidas
- [ ] O campo `description_exemplo` é exibido em caixa de código (fundo escuro, fonte monospace)
- [ ] O botão "Abrir chamado" exibe o modal de confirmação com os campos pré-preenchidos
- [ ] Após abertura do chamado, o card atualiza o status sem recarregar a página
- [ ] Cards com score acima de 3.000 exibem borda vermelha destacada
- [ ] O label "Gerado pelo Predict Radar" aparece no card e no painel de detalhe
- [ ] Oportunidades com `nu_requisicao_itsm` preenchido exibem badge "Em andamento" em vez do botão

---

## Observação final para o Antigravity

Esta feature é uma **leitura direta da view** — não há lógica de negócio adicional a implementar nesta versão. Todo o processamento (score, prioridade, ranking, narrativa) já foi feito pelo modelo preditivo e está persistido na view. O papel da interface é **apresentar esses dados de forma clara, hierarquizada e acionável**.

---

*Documento gerado por Service IT — Produto Predict | Abril/2026*
