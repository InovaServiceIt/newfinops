# Predict Discovery — Especificação: Análise de Padrões com IA (Onda 1)
**Para:** Antigravity (Implementação)
**De:** Service IT — Produto Predict
**Data:** Abril / 2026
**Versão:** 1.0

---

## Contexto e Premissa Fundamental

Esta especificação adiciona uma nova funcionalidade ao **Predict Discovery já existente**.

**Regra inviolável:** Nenhuma tela, card, fluxo ou comportamento já implementado no Discovery deve ser alterado. Esta feature é uma **adição**, não uma refatoração.

---

## O que esta feature faz

Permite que o usuário acione manualmente uma rotina de inteligência que:

1. Analisa os alertas recentes da base `alerts_alert`
2. Gera embeddings semânticos de cada descrição via API Anthropic (Voyage 3)
3. Armazena os vetores no pgvector já existente
4. Identifica clusters de alertas similares que ainda não possuem regra cadastrada nas tabelas de apoio do motor preditivo
5. Para cada cluster órfão, o Haiku 4.5 sugere uma regra de classificação
6. As sugestões ficam disponíveis no Predict Approval para revisão e aprovação humana

---

## Onde fica na interface

Um botão **"Analisar novos padrões"** posicionado no canto superior direito da tela do Predict Discovery — discreto, sem interferir nos 6 cards existentes.

```
┌─────────────────────────────────────────────────────────────────┐
│  MÓDULO DE OPERAÇÃO                                             │
│  Predict Discovery                    [Analisar novos padrões]  │
│  ─────────────────────────────────────────────────────────────  │
│                                                                 │
│  [Card 1]  [Card 2]  [Card 3]  [Card 4]  [Card 5]  [Card 6]   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Especificação visual do botão:**
- Estilo: secundário — menos destaque que as ações principais dos cards
- Ícone: lupa com faísca ou símbolo de IA
- Cor: seguir o padrão visual do Discovery
- Texto: "Analisar novos padrões"

---

## Fluxo completo da funcionalidade

```
Usuário clica em "Analisar novos padrões"
      ↓
Sistema consulta predict_controle_execucao
      ↓
Executou nos últimos 7 dias?
  ┌── SIM ──────────────────────────────────────────┐
  │  Exibe Modal Informativo (ver seção abaixo)     │
  │  Usuário pode:                                  │
  │    → Ir para Approval (ver sugestões pendentes) │
  │    → Fechar                                     │
  │    → Forçar nova execução (apenas admin)        │
  └─────────────────────────────────────────────────┘
  ┌── NÃO ──────────────────────────────────────────┐
  │  Inicia execução da rotina                      │
  │  Exibe tela de Processando...                   │
  │  Ao concluir: exibe Modal de Resultado          │
  │  Atualiza predict_controle_execucao             │
  └─────────────────────────────────────────────────┘
```

---

## Modal 1 — Já executado recentemente

Exibido quando a rotina foi executada nos últimos 7 dias.

```
┌─────────────────────────────────────────────────────┐
│  ⚠  Análise já realizada recentemente               │
│  ─────────────────────────────────────────────────  │
│                                                     │
│  Última execução: [dia da semana], [DD/MM/AAAA]     │
│  Clusters encontrados: [qt_clusters]                │
│  Sugestões geradas: [qt_sugestoes]                  │
│  Aguardando sua aprovação: [qt_pendentes]           │
│                                                     │
│  Próxima execução disponível em: [N] dias           │
│                                                     │
│  ┌────────────────────┐  ┌──────────────────────┐   │
│  │  Ir para Approval  │  │       Fechar         │   │
│  └────────────────────┘  └──────────────────────┘   │
│                                                     │
│  [link discreto — apenas admin]                     │
│  Forçar nova execução mesmo assim →                 │
└─────────────────────────────────────────────────────┘
```

**Detalhamento:**
- `qt_pendentes` = COUNT de registros em `predict_sugestao_regra` com status `AGUARDANDO_APROVACAO`
- "Próxima execução disponível em N dias" = 7 - dias desde última execução
- "Forçar nova execução" visível apenas para perfil admin — ao clicar exige confirmação adicional
- Botão "Ir para Approval" navega diretamente para o Predict Approval

---

## Tela de Processando...

Exibida durante a execução da rotina — substitui o conteúdo da tela do Discovery (não abre modal).

```
┌─────────────────────────────────────────────────────┐
│                                                     │
│              [Animação / Imagem de IA]              │
│                                                     │
│           Analisando padrões de alertas...          │
│                                                     │
│  ████████████████░░░░░░░░░░  Etapa 2 de 4           │
│                                                     │
│  ✅ Embeddings gerados                              │
│  ⏳ Identificando clusters...                       │
│  ○  Gerando sugestões de regras                     │
│  ○  Finalizando                                     │
│                                                     │
│  Isso pode levar alguns minutos.                    │
│  Não feche esta tela.                               │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Detalhamento:**
- Barra de progresso com 4 etapas: Gerando embeddings → Identificando clusters → Sugerindo regras → Finalizando
- Lista de etapas com ícones: ✅ concluída, ⏳ em andamento, ○ pendente
- Atualização em tempo real via websocket ou polling a cada 3 segundos
- Não permitir navegação para fora durante a execução — exibir aviso se o usuário tentar sair

---

## Modal 2 — Resultado da execução

Exibido ao final da execução bem-sucedida.

```
┌─────────────────────────────────────────────────────┐
│  ✅  Análise concluída                              │
│  ─────────────────────────────────────────────────  │
│                                                     │
│  Alertas analisados:      [N]                       │
│  Clusters identificados:  [N]                       │
│  Sugestões geradas:       [N]                       │
│  Já possuíam regra:       [N]                       │
│                                                     │
│  [N] sugestões aguardam sua aprovação               │
│  no Predict Approval.                               │
│                                                     │
│  ┌────────────────────┐  ┌──────────────────────┐   │
│  │  Ir para Approval  │  │       Fechar         │   │
│  └────────────────────┘  └──────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**Detalhamento:**
- Se `qt_sugestoes = 0`: mensagem alternativa "Nenhum padrão novo identificado. Todas as ocorrências já possuem regra cadastrada."
- Botão "Ir para Approval" navega diretamente para o Predict Approval

---

## Modal 3 — Erro na execução

Exibido se a rotina falhar.

```
┌─────────────────────────────────────────────────────┐
│  ❌  Erro durante a análise                         │
│  ─────────────────────────────────────────────────  │
│                                                     │
│  Ocorreu um erro na etapa:                          │
│  [nome da etapa que falhou]                         │
│                                                     │
│  O progresso parcial foi salvo.                     │
│  Você pode tentar novamente.                        │
│                                                     │
│  ┌────────────────────┐  ┌──────────────────────┐   │
│  │   Tentar novamente │  │       Fechar         │   │
│  └────────────────────┘  └──────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**Detalhamento:**
- Erro não penaliza o controle de execução — `predict_controle_execucao` registra status `ERRO`, não bloqueia nova tentativa
- "Tentar novamente" reinicia a rotina do zero

---

## Banco de Dados — Novas Tabelas

### predict_controle_execucao

```sql
CREATE TABLE predict_controle_execucao (
  id               SERIAL PRIMARY KEY,
  tp_rotina        VARCHAR(50) NOT NULL DEFAULT 'ANALISE_PADROES',
  dt_execucao      TIMESTAMP NOT NULL DEFAULT NOW(),
  status           VARCHAR(20) NOT NULL,
  -- SUCESSO | ERRO | EM_ANDAMENTO
  qt_alertas       INTEGER,
  qt_clusters      INTEGER,
  qt_sugestoes     INTEGER,
  executado_por    VARCHAR(100),
  erro             TEXT,
  forcado          BOOLEAN DEFAULT FALSE
);

CREATE INDEX idx_controle_tp_rotina_dt 
  ON predict_controle_execucao(tp_rotina, dt_execucao DESC);
```

### predict_alert_embeddings

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE predict_alert_embeddings (
  id               SERIAL PRIMARY KEY,
  alert_id         INTEGER NOT NULL,
  id_contrato      VARCHAR NOT NULL,
  description_flat TEXT NOT NULL,
  embedding        vector(1024),
  dt_geracao       TIMESTAMP DEFAULT NOW(),
  UNIQUE(alert_id)
);

CREATE INDEX idx_embeddings_alert_id
  ON predict_alert_embeddings(alert_id);

CREATE INDEX idx_embeddings_contrato
  ON predict_alert_embeddings(id_contrato);

-- Índice vetorial para busca por similaridade
CREATE INDEX idx_embeddings_vector
  ON predict_alert_embeddings
  USING ivfflat (embedding vector_cosine_ops)
  WITH (lists = 100);
```

### predict_alert_clusters

```sql
CREATE TABLE predict_alert_clusters (
  id                   SERIAL PRIMARY KEY,
  id_contrato          VARCHAR NOT NULL,
  nm_host              TEXT,
  descricao_cluster    TEXT NOT NULL,
  qt_alertas           INTEGER NOT NULL,
  similaridade_media   NUMERIC(5,4),
  dt_primeiro_alerta   TIMESTAMP,
  dt_ultimo_alerta     TIMESTAMP,
  tem_regra            BOOLEAN DEFAULT FALSE,
  id_execucao          INTEGER REFERENCES predict_controle_execucao(id),
  dt_geracao           TIMESTAMP DEFAULT NOW()
);
```

### predict_sugestao_regra

```sql
CREATE TABLE predict_sugestao_regra (
  id                       SERIAL PRIMARY KEY,
  id_contrato              VARCHAR NOT NULL,
  nm_host                  TEXT,
  qt_alertas_cluster       INTEGER,
  descricao_representativa TEXT,
  id_cluster               INTEGER REFERENCES predict_alert_clusters(id),

  -- Regra sugerida pelo agente
  tp_alvo                  VARCHAR(30),
  tp_operador              VARCHAR(10),
  tx_padrao                TEXT,
  rotulo_acao              VARCHAR(60),
  cd_prioridade            VARCHAR(5),
  tx_acao_inicial_suger    TEXT,
  justificativa_ia         TEXT,

  -- Controle de aprovação
  status        VARCHAR(30) DEFAULT 'AGUARDANDO_APROVACAO',
  -- AGUARDANDO_APROVACAO | APROVADA | REJEITADA | AJUSTADA

  aprovado_por  VARCHAR(100),
  dt_aprovacao  TIMESTAMP,
  observacao    TEXT,
  id_execucao   INTEGER REFERENCES predict_controle_execucao(id),
  dt_sugestao   TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_sugestao_status
  ON predict_sugestao_regra(status);

CREATE INDEX idx_sugestao_contrato
  ON predict_sugestao_regra(id_contrato);
```

---

## Backend — Serviços

### analise-padroes.service.js

```javascript
const Anthropic = require("@anthropic-ai/sdk");
const { Pool } = require("pg");

const client = new Anthropic({ apiKey: process.env.ANTHROPIC_API_KEY });
const pool = new Pool({ connectionString: process.env.DATABASE_URL });

const LIMIAR_SIMILARIDADE = 0.92;
const MINIMO_ALERTAS_CLUSTER = 3;

// ── Verifica se pode executar ─────────────────────────────────────
async function verificarControle() {
  const result = await pool.query(`
    SELECT id, dt_execucao, status, qt_clusters, qt_sugestoes
    FROM predict_controle_execucao
    WHERE tp_rotina = 'ANALISE_PADROES'
      AND status = 'SUCESSO'
    ORDER BY dt_execucao DESC
    LIMIT 1
  `);

  if (result.rows.length === 0) return { podeExecutar: true };

  const ultima = result.rows[0];
  const diasDesde = Math.floor(
    (Date.now() - new Date(ultima.dt_execucao)) / (1000 * 60 * 60 * 24)
  );

  // Conta sugestões pendentes
  const pendentes = await pool.query(`
    SELECT COUNT(*) AS qt
    FROM predict_sugestao_regra
    WHERE status = 'AGUARDANDO_APROVACAO'
  `);

  return {
    podeExecutar: diasDesde >= 7,
    diasDesde,
    diasRestantes: Math.max(0, 7 - diasDesde),
    ultimaExecucao: ultima.dt_execucao,
    qtClusters: ultima.qt_clusters,
    qtSugestoes: ultima.qt_sugestoes,
    qtPendentes: parseInt(pendentes.rows[0].qt),
  };
}

// ── Limpa o texto para embedding ─────────────────────────────────
function limparTexto(texto) {
  return texto
    .replace(/<[^>]+>/g, " ")
    .replace(/[\r\n\t]+/g, " ")
    .replace(/eventid:\s*\d+/gi, "")
    .replace(/eventstatus:\s*\d+/gi, "")
    .replace(/\d{4}[./-]\d{2}[./-]\d{2}\s+\d{2}:\d{2}:\d{2}/g, "")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase()
    .substring(0, 500);
}

// ── Etapa 1: Gerar embeddings ─────────────────────────────────────
async function gerarEmbeddings(onProgress) {
  const alertas = await pool.query(`
    SELECT a.id, a.contract_id AS id_contrato, a.description
    FROM alerts_alert a
    LEFT JOIN predict_alert_embeddings e ON e.alert_id = a.id
    WHERE e.id IS NULL
      AND a.description IS NOT NULL
      AND a.open_datetime >= NOW() - INTERVAL '90 days'
    ORDER BY a.open_datetime DESC
    LIMIT 500
  `);

  onProgress({ etapa: 1, total: alertas.rows.length, processados: 0 });

  let processados = 0;
  for (const alerta of alertas.rows) {
    const textoLimpo = limparTexto(alerta.description);
    if (!textoLimpo) continue;

    try {
      const response = await client.embeddings.create({
        model: "voyage-3",
        input: textoLimpo,
      });

      await pool.query(`
        INSERT INTO predict_alert_embeddings
          (alert_id, id_contrato, description_flat, embedding)
        VALUES ($1, $2, $3, $4)
        ON CONFLICT (alert_id) DO UPDATE
          SET embedding = $4, dt_geracao = NOW()
      `, [
        alerta.id,
        alerta.id_contrato,
        textoLimpo,
        JSON.stringify(response.embeddings[0].embedding),
      ]);

      processados++;
      onProgress({ etapa: 1, total: alertas.rows.length, processados });
      await new Promise(r => setTimeout(r, 100));

    } catch (err) {
      console.error(`Erro embedding alerta ${alerta.id}:`, err.message);
    }
  }

  return processados;
}

// ── Etapa 2: Identificar clusters ────────────────────────────────
async function identificarClusters(idExecucao, onProgress) {
  onProgress({ etapa: 2 });

  // Busca alertas sem regra correspondente
  const alertas = await pool.query(`
    SELECT 
      e.alert_id,
      e.id_contrato,
      a.hostname AS nm_host,
      e.description_flat,
      e.embedding
    FROM predict_alert_embeddings e
    JOIN alerts_alert a ON a.id = e.alert_id
    LEFT JOIN predict_hostname_regra_acao ra ON
      ra.in_ativo = true AND
      (ra.id_contrato = e.id_contrato OR ra.id_contrato = 'GLOBAL') AND
      (
        (ra.tp_operador = 'ILIKE' AND e.description_flat ILIKE ra.tx_padrao) OR
        (ra.tp_operador = 'REGEX' AND e.description_flat ~ ra.tx_padrao)
      )
    WHERE ra.id_regra_acao IS NULL
      AND a.open_datetime >= NOW() - INTERVAL '90 days'
    ORDER BY e.id_contrato, a.hostname
  `);

  const processados = new Set();
  const clusters = [];

  for (const alerta of alertas.rows) {
    if (processados.has(alerta.alert_id)) continue;

    // Busca similares no pgvector
    const similares = await pool.query(`
      SELECT 
        e.alert_id,
        e.description_flat,
        1 - (e.embedding <=> $1::vector) AS similaridade
      FROM predict_alert_embeddings e
      JOIN alerts_alert a ON a.id = e.alert_id
      WHERE e.id_contrato = $2
        AND a.hostname = $3
        AND a.open_datetime >= NOW() - INTERVAL '90 days'
        AND 1 - (e.embedding <=> $1::vector) >= $4
      ORDER BY similaridade DESC
    `, [
      alerta.embedding,
      alerta.id_contrato,
      alerta.nm_host,
      LIMIAR_SIMILARIDADE,
    ]);

    if (similares.rows.length >= MINIMO_ALERTAS_CLUSTER) {
      // Salva cluster
      const cluster = await pool.query(`
        INSERT INTO predict_alert_clusters
          (id_contrato, nm_host, descricao_cluster, qt_alertas,
           similaridade_media, id_execucao)
        VALUES ($1, $2, $3, $4, $5, $6)
        RETURNING id
      `, [
        alerta.id_contrato,
        alerta.nm_host,
        alerta.description_flat,
        similares.rows.length,
        similares.rows.reduce((acc, r) => acc + r.similaridade, 0) / similares.rows.length,
        idExecucao,
      ]);

      clusters.push({
        id: cluster.rows[0].id,
        id_contrato: alerta.id_contrato,
        nm_host: alerta.nm_host,
        descricao: alerta.description_flat,
        qt_alertas: similares.rows.length,
      });

      similares.rows.forEach(r => processados.add(r.alert_id));
    }
  }

  return clusters;
}

// ── Etapa 3: Sugerir regras ───────────────────────────────────────
async function sugerirRegras(clusters, idExecucao, onProgress) {
  onProgress({ etapa: 3, total: clusters.length, processados: 0 });

  let processados = 0;
  for (const cluster of clusters) {
    try {
      const response = await client.messages.create({
        model: "claude-haiku-4-5-20251001",
        max_tokens: 512,
        temperature: 0,
        messages: [{
          role: "user",
          content: `Você é um especialista em operações de TI.

Analise o cluster de alertas abaixo e sugira uma regra de classificação.

CLUSTER:
- Host: ${cluster.nm_host}
- Alertas similares agrupados: ${cluster.qt_alertas}
- Exemplo de descrição: ${cluster.descricao}

Retorne APENAS o JSON abaixo sem texto adicional:
{
  "tp_alvo": "DESCRICAO_CHAVE",
  "tp_operador": "ILIKE",
  "tx_padrao": "padrão que identifica este tipo de alerta",
  "rotulo_acao": "INVESTIGAR_CAUSA_RAIZ_DE_EVENTO_CRITICO_RECORRENTE | REVISAR_CAPACIDADE_DO_HOST | REVISAR_COLETA_E_CONECTIVIDADE_DO_MONITORAMENTO | REVISAR_LIMIARES_E_SUPRESSAO_DE_RUIDO | ANALISE_ESPECIALIZADA_DO_EVENTO | REVISAR_REGRA_DE_NEGOCIO_OU_INTEGRACAO | REVISAR_DISPONIBILIDADE_DE_SERVICO_OU_PORTA",
  "cd_prioridade": "P1 | P2 | P3 | P4",
  "tx_acao_inicial_suger": "descrição da ação recomendada em português",
  "justificativa": "por que você sugeriu essa classificação"
}`
        }],
      });

      const sugestao = JSON.parse(response.content[0].text.trim());

      await pool.query(`
        INSERT INTO predict_sugestao_regra (
          id_contrato, nm_host, qt_alertas_cluster,
          descricao_representativa, id_cluster,
          tp_alvo, tp_operador, tx_padrao,
          rotulo_acao, cd_prioridade,
          tx_acao_inicial_suger, justificativa_ia,
          id_execucao
        ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
      `, [
        cluster.id_contrato,
        cluster.nm_host,
        cluster.qt_alertas,
        cluster.descricao,
        cluster.id,
        sugestao.tp_alvo,
        sugestao.tp_operador,
        sugestao.tx_padrao,
        sugestao.rotulo_acao,
        sugestao.cd_prioridade,
        sugestao.tx_acao_inicial_suger,
        sugestao.justificativa,
        idExecucao,
      ]);

      processados++;
      onProgress({ etapa: 3, total: clusters.length, processados });
      await new Promise(r => setTimeout(r, 200));

    } catch (err) {
      console.error(`Erro ao sugerir regra para cluster ${cluster.id}:`, err.message);
    }
  }

  return processados;
}

// ── Orquestrador principal ────────────────────────────────────────
async function executarAnalise(usuario, forcar = false, onProgress = () => {}) {
  // Registra início
  const execucao = await pool.query(`
    INSERT INTO predict_controle_execucao
      (tp_rotina, status, executado_por, forcado)
    VALUES ('ANALISE_PADROES', 'EM_ANDAMENTO', $1, $2)
    RETURNING id
  `, [usuario, forcar]);

  const idExecucao = execucao.rows[0].id;

  try {
    // Etapa 1
    const qtAlertas = await gerarEmbeddings(onProgress);

    // Etapa 2
    const clusters = await identificarClusters(idExecucao, onProgress);

    // Etapa 3
    onProgress({ etapa: 3 });
    const qtSugestoes = await sugerirRegras(clusters, idExecucao, onProgress);

    // Etapa 4 — Finaliza
    onProgress({ etapa: 4 });
    await pool.query(`
      UPDATE predict_controle_execucao
      SET status = 'SUCESSO',
          qt_alertas = $1,
          qt_clusters = $2,
          qt_sugestoes = $3
      WHERE id = $4
    `, [qtAlertas, clusters.length, qtSugestoes, idExecucao]);

    return {
      sucesso: true,
      qtAlertas,
      qtClusters: clusters.length,
      qtSugestoes,
    };

  } catch (err) {
    await pool.query(`
      UPDATE predict_controle_execucao
      SET status = 'ERRO', erro = $1
      WHERE id = $2
    `, [err.message, idExecucao]);

    throw err;
  }
}

module.exports = { verificarControle, executarAnalise };
```

---

## Backend — Endpoints

```javascript
const { verificarControle, executarAnalise } = require("./analise-padroes.service");

// GET /api/analise-padroes/status
// Verifica se pode executar e retorna dados da última execução
router.get("/analise-padroes/status", async (req, res) => {
  try {
    const status = await verificarControle();
    res.json(status);
  } catch (err) {
    res.status(500).json({ erro: err.message });
  }
});

// POST /api/analise-padroes/executar
// Inicia a execução da rotina
router.post("/analise-padroes/executar", async (req, res) => {
  const { forcar = false } = req.body;
  const usuario = req.user?.nome || "sistema";

  // Verifica controle (exceto se forçado por admin)
  if (!forcar) {
    const controle = await verificarControle();
    if (!controle.podeExecutar) {
      return res.status(409).json({
        bloqueado: true,
        ...controle,
      });
    }
  }

  // Executa de forma assíncrona
  // Frontend acompanha via polling no endpoint de progresso
  const jobId = `job_${Date.now()}`;
  global.progressJobs = global.progressJobs || {};
  global.progressJobs[jobId] = { etapa: 0, status: "iniciando" };

  executarAnalise(usuario, forcar, (progress) => {
    global.progressJobs[jobId] = { ...progress, status: "em_andamento" };
  })
  .then(resultado => {
    global.progressJobs[jobId] = { ...resultado, status: "concluido" };
  })
  .catch(err => {
    global.progressJobs[jobId] = { status: "erro", erro: err.message };
  });

  res.json({ jobId });
});

// GET /api/analise-padroes/progresso/:jobId
// Polling de progresso (a cada 3 segundos pelo frontend)
router.get("/analise-padroes/progresso/:jobId", (req, res) => {
  const job = global.progressJobs?.[req.params.jobId];
  if (!job) return res.status(404).json({ erro: "Job não encontrado" });
  res.json(job);
});
```

---

## Critério de aceite

- [ ] Botão "Analisar novos padrões" aparece no canto superior direito do Discovery sem alterar os 6 cards existentes
- [ ] Ao clicar, o sistema consulta `predict_controle_execucao` antes de qualquer ação
- [ ] Se executado nos últimos 7 dias: exibe Modal 1 com dados da última execução e quantidade de sugestões pendentes
- [ ] Botão "Ir para Approval" no Modal 1 navega para o Predict Approval
- [ ] Link "Forçar execução" visível apenas para perfil admin com confirmação adicional
- [ ] Se não executado nos últimos 7 dias: inicia rotina e exibe tela de Processando com 4 etapas
- [ ] Tela de Processando atualiza em tempo real via polling a cada 3 segundos
- [ ] Ao concluir: exibe Modal 2 com resultado e botão "Ir para Approval"
- [ ] Se erro: exibe Modal 3 com etapa que falhou e opção de tentar novamente
- [ ] Erro não bloqueia nova tentativa no controle de execução
- [ ] Sugestões geradas aparecem no Predict Approval com status AGUARDANDO_APROVACAO
- [ ] Nenhuma tela, card ou comportamento existente do Discovery foi alterado

---

## O que esta feature NÃO faz

- Não aprova regras automaticamente — toda sugestão passa pelo humano no Predict Approval
- Não altera a query `vw_predict_proposta_plano_acao_hostname` — as CTEs continuam iguais
- Não substitui as regras existentes — apenas sugere novas para padrões não cobertos
- Não executa automaticamente por agendamento — sempre acionada manualmente pelo botão

---

## Observação final para o Antigravity

A query do motor preditivo não muda. As tabelas de apoio existentes não mudam. Esta feature apenas **alimenta** as tabelas `predict_sugestao_regra` com sugestões que o humano aprovará no Predict Approval. O ciclo completo só se fecha quando o Predict Action inserir a regra aprovada na `predict_hostname_regra_acao` — isso será especificado em documento separado.

---

*Documento gerado por Service IT — Produto Predict | Abril/2026*
