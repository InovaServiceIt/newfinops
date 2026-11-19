# Briefing Técnico — Associação Discovery ↔ predict_service_catalog
> Predict | Service IT · Versão 1.0 · 02/05/2026
> Destinatário: AntiGravity
> Autor: BU de IA Service IT

---

## 1. Contexto

O Predict Discovery identifica padrões de alertas/incidentes nos últimos 90 dias e os transforma em **oportunidades de automação**, gravadas em `predict_discovery_opportunities`.

O problema atual: a oportunidade existe, mas está "muda". Ela sabe **que** algo precisa ser feito, mas não sabe **o quê** — não tem instrução executável para o agente.

A entrega desta sprint resolve isso. O catálogo `predict_service_catalog` tem **1.344 atividades** com a coluna `agent_action` — a instrução exata que o agente deve executar. A associação semântica entre oportunidade e catálogo transforma uma observação em uma **ordem de execução**.

### Fluxo antes × depois

```
ANTES
predict_discovery_opportunities
  → oportunidade identificada
  → agente não sabe o que fazer

DEPOIS
predict_discovery_opportunities
  → oportunidade identificada
  → lookup semântico em predict_service_catalog
  → catalog_id + agent_action preenchidos
  → agente tem instrução executável
```

---

## 2. A tabela predict_service_catalog

Já criada no banco. DDL:

```sql
CREATE TABLE public.predict_service_catalog (
    id              SERIAL PRIMARY KEY,
    portfolio       VARCHAR(100) NOT NULL,
    service         VARCHAR(150) NOT NULL,
    activity        TEXT NOT NULL,
    weight          INTEGER NOT NULL,
    agent_action    TEXT NOT NULL,
    classification  VARCHAR(10) NOT NULL CHECK (classification IN ('Objetiva', 'Genérica')),
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);

CREATE UNIQUE INDEX uq_psc_activity
    ON public.predict_service_catalog (portfolio, service, activity);
```

### Colunas relevantes para esta implementação

| Coluna | Papel |
|--------|-------|
| `id` | FK que vai ser gravada na oportunidade |
| `activity` | O que o analista faz hoje — texto para gerar embedding |
| `agent_action` | Instrução que o agente executa — texto complementar para embedding |
| `classification` | `Objetiva` = executável por agente autônomo / `Genérica` = requer detalhamento humano |
| `is_active` | Filtro obrigatório nas buscas — ignorar inativos |

### Volume
- 1.344 atividades ativas no momento da carga
- 11 portfólios (Cloud, Backup, Banco de Dados, Unix, Microsoft e outros)

---

## 3. O que precisa ser implementado

### 3.1 Embeddings para o catálogo

O catálogo precisa de uma coluna de embedding. A busca semântica usa pgvector, o mesmo já disponível na stack.

**Migration a criar:**

```sql
-- Adicionar coluna de embedding na tabela do catálogo
ALTER TABLE public.predict_service_catalog
    ADD COLUMN IF NOT EXISTS embedding vector(768);

-- Índice HNSW para performance na busca semântica
CREATE INDEX IF NOT EXISTS idx_psc_embedding
    ON public.predict_service_catalog
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

> **Dimensão do vetor:** usar a mesma dimensão que o Ollama já gera para `predict_alert_embeddings`. Verificar no banco antes de aplicar a migration.

**Texto para gerar o embedding de cada atividade do catálogo:**
Concatenar `activity` + `" | "` + `agent_action`. Isso enriquece o vetor com o contexto da instrução, melhorando a precisão do match.

```
Exemplo:
"Verificar espaço em disco no servidor Linux | Executar df -h e alertar se uso > 85%"
```

### 3.2 Rotina de indexação do catálogo (ServiceCatalogEmbeddingService)

Criar serviço responsável por:

1. Buscar todas as atividades em `predict_service_catalog` onde `embedding IS NULL AND is_active = TRUE`
2. Montar o texto: `activity + " | " + agent_action`
3. Chamar Ollama em lotes (sugestão: lotes de 100) para gerar embeddings
4. Persistir o vetor na coluna `embedding`

**Quando rodar:**
- Uma vez na carga inicial (1.344 registros)
- Delta automático: pode rodar junto com o job diário do Discovery, buscando apenas registros com `embedding IS NULL`

**Não precisa de scheduler próprio** — encaixar no início do pipeline do Discovery (antes da Etapa 0) ou como job separado acionado pelo mesmo trigger.

### 3.3 Coluna catalog_id em predict_discovery_opportunities

```sql
-- Adicionar FK para o catálogo na tabela de oportunidades
ALTER TABLE public.predict_discovery_opportunities
    ADD COLUMN IF NOT EXISTS catalog_id INTEGER REFERENCES public.predict_service_catalog(id),
    ADD COLUMN IF NOT EXISTS catalog_score FLOAT,
    ADD COLUMN IF NOT EXISTS catalog_matched_at TIMESTAMPTZ;
```

| Coluna nova | Propósito |
|-------------|-----------|
| `catalog_id` | FK para a atividade do catálogo associada |
| `catalog_score` | Score de similaridade cosine do match (0 a 1) |
| `catalog_matched_at` | Timestamp do match — rastreabilidade |

### 3.4 Função de lookup semântico

Criar função no banco para encapsular a busca:

```sql
CREATE OR REPLACE FUNCTION find_catalog_match(
    p_embedding     vector,
    p_threshold     float DEFAULT 0.80,
    p_limit         int   DEFAULT 1
)
RETURNS TABLE (
    catalog_id      int,
    activity        text,
    agent_action    text,
    classification  varchar,
    score           float
)
LANGUAGE sql STABLE AS $$
    SELECT
        id,
        activity,
        agent_action,
        classification,
        1 - (embedding <=> p_embedding) AS score
    FROM public.predict_service_catalog
    WHERE is_active = TRUE
      AND embedding IS NOT NULL
      AND 1 - (embedding <=> p_embedding) >= p_threshold
    ORDER BY embedding <=> p_embedding
    LIMIT p_limit;
$$;
```

> **Threshold 0.80** — deliberadamente mais flexível que os 0.90 do clustering do Discovery. A oportunidade já é um padrão consolidado; o match com o catálogo pode ter alguma variação semântica.

### 3.5 Atualização do pipeline — PatternAnalysisService.cs

O ponto de inserção é **após a Etapa 6** (inserção em `predict_discovery_opportunities`).

**Fluxo a adicionar:**

```
Etapa 7 — Associação ao catálogo
Para cada oportunidade recém-inserida (ou com catalog_id IS NULL):
    1. Recuperar o embedding da oportunidade
       (usar o embedding já gerado para o cluster ou gerar novo a partir da descrição)
    2. Chamar find_catalog_match(embedding, threshold: 0.80, limit: 1)
    3. Se houver resultado:
         UPDATE predict_discovery_opportunities
         SET catalog_id = result.catalog_id,
             catalog_score = result.score,
             catalog_matched_at = NOW()
         WHERE id = oportunidade.id
    4. Se não houver resultado:
         Logar em predict_process_logs como "sem match no catálogo"
         catalog_id permanece NULL (oportunidade válida, mas sem instrução automática)
```

**Sobre o embedding da oportunidade:**
Se `predict_discovery_opportunities` já tem um campo de embedding (vetor do cluster), reutilizá-lo diretamente. Se não, gerar embedding a partir do campo de descrição/texto da oportunidade usando Ollama, com o mesmo modelo usado no Discovery.

> Verificar o schema atual de `predict_discovery_opportunities` antes de implementar.

---

## 4. Critérios de aceite

### Funcionais
- [ ] Todas as atividades do catálogo com `is_active = TRUE` têm `embedding` preenchido após a indexação inicial
- [ ] Oportunidades criadas pelo Discovery têm `catalog_id` preenchido quando há match com score ≥ 0.80
- [ ] Oportunidades sem match mantêm `catalog_id = NULL` e são logadas em `predict_process_logs`
- [ ] O campo `agent_action` do catálogo associado está acessível via JOIN a partir da oportunidade
- [ ] Oportunidades com `classification = 'Genérica'` são associadas mas **sinalizadas** — o agente não executa automaticamente

### Não-funcionais
- [ ] Indexação das 1.344 atividades concluída em menos de 5 minutos
- [ ] Lookup semântico por oportunidade em menos de 200ms
- [ ] Pipeline completo do Discovery (Etapas 0–7) sem regressão de tempo > 20%

### Rastreabilidade
- [ ] `predict_process_logs` registra: total de oportunidades processadas, total com match, total sem match, tempo da Etapa 7
- [ ] `catalog_score` e `catalog_matched_at` preenchidos em todos os registros que tiveram match

---

## 5. O que o AntiGravity não precisa decidir

Estas decisões estão tomadas — só implementar:

| Decisão | Valor definido |
|---------|----------------|
| Threshold de similaridade | 0.80 |
| Modelo de embedding | Ollama — mesmo modelo do Discovery |
| Texto base para embedding do catálogo | `activity + " | " + agent_action` |
| Comportamento sem match | `catalog_id = NULL`, log, sem erro |
| Atividades Genéricas | Associar mas não executar automaticamente |

---

## 6. Sequência de entrega sugerida

```
1. Migration: coluna embedding em predict_service_catalog
2. Migration: colunas catalog_id, catalog_score, catalog_matched_at em predict_discovery_opportunities
3. ServiceCatalogEmbeddingService — indexação inicial (roda uma vez)
4. Função find_catalog_match no banco
5. Etapa 7 no PatternAnalysisService.cs
6. Testes: verificar matches em amostra de oportunidades existentes
7. Ajuste de threshold se necessário (base: analisar distribuição dos scores)
```

---

## 7. Exemplo end-to-end

### Oportunidade identificada pelo Discovery
```
hostname: srv-db-prod-01
cliente: ClienteX
padrão: "Disco /var/lib/postgresql crescendo > 5GB/semana nos últimos 30 dias"
embedding: [0.12, -0.45, 0.78, ...] (vetor gerado pelo Ollama)
```

### Resultado do find_catalog_match
```
catalog_id:    291
activity:      "Monitorar crescimento de tablespace PostgreSQL"
agent_action:  "Verificar crescimento de tablespace via pg_database_size(),
                alertar DBA se crescimento semanal > 10% e acionar rotina
                de VACUUM/ANALYZE se bloat > 20%"
classification: Objetiva
score:          0.87
```

### Estado final em predict_discovery_opportunities
```
id:                  1042
hostname:            srv-db-prod-01
catalog_id:          291          ← preenchido
catalog_score:       0.87         ← preenchido
catalog_matched_at:  2026-05-02   ← preenchido
```

### O que o Predict Gate recebe (JOIN)
```sql
SELECT
    o.hostname,
    o.catalog_score,
    c.activity,
    c.agent_action,
    c.classification
FROM predict_discovery_opportunities o
JOIN predict_service_catalog c ON c.id = o.catalog_id
WHERE o.id = 1042;
```

**Resultado:** instrução completa, pronta para o agente executar.

---

## 8. Dúvidas esperadas e respostas

**"O que fazer se uma oportunidade mapeia para múltiplas atividades do catálogo?"**
Por ora, gravar apenas o melhor match (`LIMIT 1`). Se o score do segundo resultado for > 0.85 também, logar para análise futura — não bloquear a entrega.

**"O pipeline do Discovery roda em oportunidades antigas?"**
A Etapa 7 deve processar todas as oportunidades com `catalog_id IS NULL`, não apenas as da execução corrente. Isso garante que o backlog existente seja associado na primeira execução.

**"E se o Ollama estiver fora?"**
Mesmo comportamento das etapas anteriores do Discovery — logar falha em `predict_process_logs`, `catalog_id` permanece NULL. Sem exception não tratada.

---

*Dúvidas sobre este briefing: BU de IA Service IT.*
