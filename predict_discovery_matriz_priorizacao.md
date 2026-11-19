# Predict Discovery — Matriz de Priorização

## Visão geral

Sub-módulo do **Predict Discovery** responsável por permitir que o analista visualize, reordene e priorize as oportunidades de automação cadastradas, com base em scores calculados automaticamente pelo sistema e com suporte a ajustes manuais auditados.

---

## Layout

A tela é dividida em dois painéis lado a lado:

### Painel esquerdo — Quadrante Ganho x Esforço

- Eixo Y = **Ganho** (0 a 100)
- Eixo X = **Esforço** (0 a 100)
- Cada oportunidade é representada por uma **bolha**:
  - Posição = (esforço, ganho) calculados a partir dos campos da `predict_discovery_opportunities`
  - Cor da bolha = nível de risco:
    - Verde (`#639922`) → Risco baixo
    - Âmbar (`#EF9F27`) → Risco médio
    - Vermelho (`#E24B4A`) → Risco alto
  - Tamanho da bolha = proporcional ao `priority_score`
  - Label dentro da bolha = número do rank atual
- Quadrantes nomeados:
  - Superior esquerdo → **Quick win**
  - Superior direito → **Estratégico**
  - Inferior esquerdo → **Baixo valor**
  - Inferior direito → **Reavaliar**
- Hover na bolha exibe tooltip com: título, score, ganho, esforço e risco
- Clique na bolha seleciona/deseleciona a oportunidade (estado visual destacado)

### Painel direito — Ranking de oportunidades

Lista ordenada pelo `priority_rank` atual, com as colunas:

| Coluna | Campo fonte |
|---|---|
| # (rank) | `priority_rank` |
| Título | `title` |
| Score | `priority_score` |
| Ganho | `expected_gain_classification` |
| Risco | `operational_risk_classification` |
| Esforço | `implementation_effort_classification` |
| Handle | ícone de drag |

- **Drag and drop** para reordenar manualmente
- Clique na linha seleciona/deseleciona a oportunidade
- Linhas selecionadas ficam destacadas visualmente
- Oportunidades com score ajustado manualmente exibem indicador `· score ajustado` abaixo do código

---

## Ações disponíveis

### 1. Selecionar oportunidade
- Clique em bolha (quadrante) ou linha (lista)
- Seleção é sincronizada entre os dois painéis
- Múltipla seleção permitida

### 2. Reordenar por drag and drop
- Analista arrasta linha da lista para nova posição
- Ao soltar, os ranks são recalculados localmente
- A reordenação é registrada ao confirmar a priorização

### 3. Ajustar score (override manual)
- Botão **"Ajustar score"** ativado quando há ao menos uma oportunidade selecionada
- Abre modal com:
  - Nome da oportunidade selecionada
  - Campo numérico: **Novo score** (0–100) — obrigatório
  - Campo texto: **Justificativa** — obrigatório
- Ao salvar:
  - Atualiza `priority_score` da oportunidade
  - Re-ordena a lista pelo novo score
  - Recalcula ranks
  - Atualiza posição da bolha no quadrante
  - Marca a oportunidade como `overridden = true` localmente
- **Não persiste ainda** — só persiste ao confirmar priorização

### 4. Confirmar priorização
- Botão **"Confirmar priorização"** ativado quando há ao menos uma oportunidade selecionada
- Ao confirmar, o sistema:
  1. Atualiza `priority_rank` e `priority_score` de todas as oportunidades reordenadas via `PATCH /api/discovery/opportunities/bulk-rank`
  2. Para cada oportunidade com override de score, grava em `predict_audit_history`:
     - `module_name` = `predict_discovery`
     - `entity_type` = `opportunity`
     - `entity_id` = `opportunity_id`
     - `event_type` = `score_override`
     - `field_name` = `priority_score`
     - `old_value` = score anterior
     - `new_value` = novo score
     - `justification` = justificativa informada
     - `changed_by` = usuário logado
  3. Para cada oportunidade priorizada (selecionada), grava em `predict_decision_log`:
     - `module_name` = `predict_discovery`
     - `entity_type` = `opportunity`
     - `entity_id` = `opportunity_id`
     - `decision_type` = `prioritization`
     - `rationale` = justificativa (se override) ou `"Priorizado via matriz de priorização"`
     - `context_payload` = snapshot do ranking completo no momento da decisão (JSON)
     - `decided_by` = usuário logado
  4. Atualiza `status` das oportunidades selecionadas para `prioritized`
  5. Exibe confirmação de sucesso

---

## Regra de elegibilidade — quais oportunidades aparecem na Matriz

> ⚠️ **CRÍTICO — leia antes de implementar o endpoint.**

A Matriz de Priorização exibe **exclusivamente** oportunidades que ainda não foram processadas. Uma oportunidade que já passou por este fluxo ou avançou para o próximo módulo **nunca deve reaparecer na Matriz**, pois isso geraria duplicidade no processo de priorização e comprometeria a rastreabilidade das decisões.

Uma oportunidade é elegível para a Matriz **somente se atender a todos os critérios abaixo simultaneamente:**

| Critério | Condição obrigatória |
|---|---|
| Status | `status = 'new'` |
| Não convertida | `converted_to_builder = false` |
| Ativa | `is_active = true` |

**Oportunidades que devem ser excluídas da Matriz:**

| Status / Condição | Motivo da exclusão |
|---|---|
| `status = 'prioritized'` | Já passou pela Matriz e foi priorizada pelo analista |
| `status = 'discarded'` | Foi descartada — não faz parte do pipeline ativo |
| `converted_to_builder = true` | Já avançou para o Predict Builder — ciclo de vida encerrado no Discovery |
| `is_active = false` | Registro inativo — não deve ser exibido em nenhuma tela |

A query de backend que alimenta este endpoint deve implementar todos esses filtros de forma **obrigatória e não opcional**. Não deve existir parâmetro que permita contornar esta regra.

---

## Fonte de dados

### Endpoint de leitura

```
GET /api/discovery/opportunities/matrix
```

> Endpoint dedicado à Matriz — **não reutilizar** o endpoint genérico de listagem de oportunidades, pois este possui filtros específicos de elegibilidade que não se aplicam a outras telas.

Parâmetros de filtro (querystring):
- `client_code` — obrigatório

Filtros aplicados **internamente e obrigatoriamente** pelo backend (não expostos como parâmetros opcionais):
- `status = 'new'`
- `converted_to_builder = false`
- `is_active = true`

Campos retornados relevantes para a matriz:

| Campo API | Tabela |
|---|---|
| `opportunity_id` | `predict_discovery_opportunities.opportunity_id` |
| `opportunity_code` | `predict_discovery_opportunities.opportunity_code` |
| `title` | `predict_discovery_opportunities.title` |
| `priority_score` | `predict_discovery_opportunities.priority_score` |
| `priority_rank` | `predict_discovery_opportunities.priority_rank` |
| `expected_gain_classification` | `predict_discovery_opportunities.expected_gain_classification` |
| `implementation_effort_classification` | `predict_discovery_opportunities.implementation_effort_classification` |
| `operational_risk_classification` | `predict_discovery_opportunities.operational_risk_classification` |
| `status` | `predict_discovery_opportunities.status` |

### Endpoint de atualização bulk

```
PATCH /api/discovery/opportunities/bulk-rank
```

Body:
```json
{
  "client_code": "string",
  "updates": [
    {
      "opportunity_id": 123,
      "priority_rank": 1,
      "priority_score": 92,
      "status": "prioritized"
    }
  ]
}
```

---

## Mapeamento de classificações para coordenadas do quadrante

Os campos de classificação (`low`, `medium`, `high`) devem ser convertidos em valores numéricos para posicionamento no quadrante:

| Classificação | Ganho (Y) | Esforço (X) | Risco (cor) |
|---|---|---|---|
| `high` | 80 | 75 | Vermelho |
| `medium` | 55 | 50 | Âmbar |
| `low` | 25 | 25 | Verde |

> O `priority_score` define o tamanho da bolha (range: 24px a 42px proporcional a 0–100).

---

## Tabelas de auditoria envolvidas

| Ação | Tabela | event_type / decision_type |
|---|---|---|
| Override de score | `predict_audit_history` | `score_override` |
| Priorização confirmada | `predict_decision_log` | `prioritization` |
| Reordenação manual | `predict_audit_history` | `rank_reorder` |

---

## Comportamento de estado

| Estado | Descrição |
|---|---|
| `new` | Oportunidade cadastrada, ainda não priorizada |
| `prioritized` | Selecionada e confirmada pelo analista |
| `discarded` | Descartada (fora do escopo desta tela) |

---

## Observações técnicas

- A seleção e reordenação são **operações locais** até o clique em "Confirmar priorização"
- O botão "Confirmar priorização" deve ser **desabilitado** se nenhuma oportunidade estiver selecionada
- O botão "Ajustar score" deve ser **desabilitado** se nenhuma oportunidade estiver selecionada
- A justificativa no modal de override é **campo obrigatório** — não permitir salvar sem preenchimento
- O `context_payload` gravado no `predict_decision_log` deve conter o array completo do ranking no momento da confirmação, para permitir auditoria histórica da decisão
- Os dois painéis devem manter **seleção sincronizada** — selecionar no quadrante reflete na lista e vice-versa
- **Regra de elegibilidade é inviolável** — o backend deve rejeitar qualquer tentativa de incluir na Matriz oportunidades com `status != 'new'`, `converted_to_builder = true` ou `is_active = false`. Esta validação deve existir tanto na query de leitura quanto na operação de `bulk-rank`, impedindo que uma oportunidade já priorizada ou convertida seja reprocessada acidentalmente
- Se a Matriz estiver vazia (nenhuma oportunidade elegível), exibir estado vazio com mensagem: *"Não há oportunidades disponíveis para priorização. Todas as oportunidades ativas já foram priorizadas ou encaminhadas para o Builder."*
