# Predict Discovery — Especificação: Geração de Narrativa Executiva com IA
**Para:** Antigravity (Implementação)
**De:** Service IT — Produto Predict
**Data:** Abril / 2026
**Versão:** 1.0

---

## Visão geral

O Predict Discovery exibe oportunidades de melhoria identificadas pelo modelo preditivo. Atualmente os dados são exibidos em formato técnico — números, códigos e campos brutos da view.

O objetivo desta feature é **transformar esses dados técnicos em narrativa executiva** gerada por Inteligência Artificial, exatamente como um analista sênior escreveria: com problema identificado, contexto de impacto no negócio e plano de ação numerado e acionável.

O modelo utilizado é o **Claude Haiku 4.5** da Anthropic — escolhido pela melhor relação custo-benefício para este caso de uso.

---

## Resultado esperado

Para cada host da view, o sistema deve gerar e exibir:

**1. Problema detectado** — frase executiva descrevendo o que está acontecendo em linguagem clara, não técnica

**2. Impacto no negócio** — consequência real do problema para a operação do cliente

**3. Plano de ação** — até 5 passos numerados, diretos e acionáveis

**4. Nível de urgência** — IMEDIATA / ALTA / MÉDIA com justificativa em uma linha

---

## Segurança — API Key

A API Key da Anthropic será fornecida verbalmente pelo cliente Service IT em videochamada privada.

**Regras obrigatórias:**

- ✅ Armazenar **exclusivamente** como variável de ambiente no servidor
- ✅ Adicionar `.env` ao `.gitignore` antes do primeiro commit
- ❌ Nunca inserir a chave no código-fonte
- ❌ Nunca commitar a chave no repositório Git
- ❌ Nunca compartilhar a chave em canais de comunicação

**Configuração no servidor:**
```bash
ANTHROPIC_API_KEY=sk-ant-xxxxxxxxxxxxxxxxxx
```

---

## Modelo e parâmetros

| Parâmetro | Valor |
|---|---|
| Modelo | `claude-haiku-4-5` |
| Max tokens output | `1024` |
| Temperature | `0` (determinístico — resultados consistentes) |
| Modo de execução | Batch API (assíncrono, 50% de desconto) |
| Frequência | Job agendado — toda segunda-feira às 06h00 |

---

## Instalação da dependência

```bash
npm install @anthropic-ai/sdk
```

---

## System Prompt — Instruções do Agente

```
Você é um analista sênior de operações de TI especializado em ambientes 
corporativos de grande porte.

Sua função é transformar dados técnicos de monitoramento em narrativas 
executivas claras, precisas e acionáveis para gestores e SDMs.

Regras de escrita:
- Escreva de forma direta, sem jargões desnecessários
- Conecte sempre o problema técnico ao impacto no negócio
- Use senso de urgência proporcional à severidade real
- O plano de ação deve ser executável por um técnico de middleware
- Nunca invente dados — use apenas o que foi fornecido
- Responda APENAS com o JSON solicitado, sem texto adicional
```

---

## User Prompt — Template por host

```
Analise os dados do host abaixo e gere uma narrativa executiva estruturada.

DADOS DO HOST:
- Nome: {nm_host}
- Categoria: {cd_categoria}
- Prioridade: {prioridade_sugerida}
- Score de criticidade: {score_hostname}
- Total de eventos: {qt_eventos} em {dias_com_evento} dias consecutivos
- Eventos Disaster: {qt_disaster}
- Eventos High: {qt_high}
- Eventos Average: {qt_average}
- Eventos Warning: {qt_warning}
- Percentual de eventos graves: {pct_graves}%
- Representa {pct_volume_contrato}% do volume total do contrato
- Última ocorrência: {ultima_ocorrencia}
- Período analisado: {dt_inicio_analise} até {dt_fim_analise}
- Problema identificado: {descricao_chave}
- Ação sugerida pelo modelo: {tx_acao_inicial_suger}
- Justificativa do modelo: {justificativa_resumida}

Retorne EXCLUSIVAMENTE o seguinte JSON, sem nenhum texto antes ou depois:

{
  "problema": "Uma frase executiva descrevendo o problema em linguagem clara. Máximo 2 linhas.",
  "impacto": "Uma frase descrevendo o impacto real no negócio ou na operação do cliente. Máximo 2 linhas.",
  "acoes": [
    "Passo 1 — ação específica e executável",
    "Passo 2 — ação específica e executável",
    "Passo 3 — ação específica e executável",
    "Passo 4 — ação específica e executável",
    "Passo 5 — ação específica e executável"
  ],
  "urgencia": "IMEDIATA | ALTA | MÉDIA",
  "justificativa_urgencia": "Uma frase curta justificando o nível de urgência."
}
```

---

## Implementação — Backend

### Estrutura de arquivos sugerida

```
/predict
  /services
    narrativa.service.js     ← lógica de chamada à API
    narrativa.job.js         ← job agendado semanal
  /models
    narrativa.model.js       ← schema da tabela no banco
```

---

### narrativa.service.js

```javascript
const Anthropic = require("@anthropic-ai/sdk");

const client = new Anthropic({
  apiKey: process.env.ANTHROPIC_API_KEY,
});

const SYSTEM_PROMPT = `Você é um analista sênior de operações de TI especializado em ambientes 
corporativos de grande porte.

Sua função é transformar dados técnicos de monitoramento em narrativas 
executivas claras, precisas e acionáveis para gestores e SDMs.

Regras de escrita:
- Escreva de forma direta, sem jargões desnecessários
- Conecte sempre o problema técnico ao impacto no negócio
- Use senso de urgência proporcional à severidade real
- O plano de ação deve ser executável por um técnico de middleware
- Nunca invente dados — use apenas o que foi fornecido
- Responda APENAS com o JSON solicitado, sem texto adicional`;

function buildUserPrompt(host) {
  return `Analise os dados do host abaixo e gere uma narrativa executiva estruturada.

DADOS DO HOST:
- Nome: ${host.nm_host}
- Categoria: ${host.cd_categoria}
- Prioridade: ${host.prioridade_sugerida}
- Score de criticidade: ${host.score_hostname}
- Total de eventos: ${host.qt_eventos} em ${host.dias_com_evento} dias consecutivos
- Eventos Disaster: ${host.qt_disaster}
- Eventos High: ${host.qt_high}
- Eventos Average: ${host.qt_average}
- Eventos Warning: ${host.qt_warning}
- Percentual de eventos graves: ${host.pct_graves}%
- Representa ${host.pct_volume_contrato}% do volume total do contrato
- Última ocorrência: ${host.ultima_ocorrencia}
- Período analisado: ${host.dt_inicio_analise} até ${host.dt_fim_analise}
- Problema identificado: ${host.descricao_chave}
- Ação sugerida pelo modelo: ${host.tx_acao_inicial_suger}
- Justificativa do modelo: ${host.justificativa_resumida}

Retorne EXCLUSIVAMENTE o seguinte JSON, sem nenhum texto antes ou depois:

{
  "problema": "Uma frase executiva descrevendo o problema em linguagem clara. Máximo 2 linhas.",
  "impacto": "Uma frase descrevendo o impacto real no negócio ou na operação do cliente. Máximo 2 linhas.",
  "acoes": [
    "Passo 1 — ação específica e executável",
    "Passo 2 — ação específica e executável",
    "Passo 3 — ação específica e executável",
    "Passo 4 — ação específica e executável",
    "Passo 5 — ação específica e executável"
  ],
  "urgencia": "IMEDIATA | ALTA | MÉDIA",
  "justificativa_urgencia": "Uma frase curta justificando o nível de urgência."
}`;
}

async function gerarNarrativaHost(host) {
  try {
    const response = await client.messages.create({
      model: "claude-haiku-4-5-20251001",
      max_tokens: 1024,
      temperature: 0,
      system: SYSTEM_PROMPT,
      messages: [
        {
          role: "user",
          content: buildUserPrompt(host),
        },
      ],
    });

    const texto = response.content[0].text.trim();
    const narrativa = JSON.parse(texto);

    return {
      id_plano: host.id_plano,
      problema: narrativa.problema,
      impacto: narrativa.impacto,
      acoes: narrativa.acoes,
      urgencia: narrativa.urgencia,
      justificativa_urgencia: narrativa.justificativa_urgencia,
      modelo_utilizado: "claude-haiku-4-5",
      dt_geracao: new Date().toISOString(),
      status: "gerado",
    };
  } catch (error) {
    console.error(`Erro ao gerar narrativa para host ${host.nm_host}:`, error);
    return {
      id_plano: host.id_plano,
      status: "erro",
      erro: error.message,
      dt_geracao: new Date().toISOString(),
    };
  }
}

async function gerarNarrativasEmLote(hosts) {
  const resultados = [];

  // Processar em lotes de 10 para não sobrecarregar a API
  const tamanhoLote = 10;
  for (let i = 0; i < hosts.length; i += tamanhoLote) {
    const lote = hosts.slice(i, i + tamanhoLote);
    const promises = lote.map((host) => gerarNarrativaHost(host));
    const resultadosLote = await Promise.all(promises);
    resultados.push(...resultadosLote);

    // Pausa entre lotes para respeitar rate limits
    if (i + tamanhoLote < hosts.length) {
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
  }

  return resultados;
}

module.exports = { gerarNarrativaHost, gerarNarrativasEmLote };
```

---

### narrativa.job.js

```javascript
const cron = require("node-cron");
const { gerarNarrativasEmLote } = require("./narrativa.service");
const { buscarHostsDaView } = require("../database/queries");
const { salvarNarrativas } = require("../models/narrativa.model");

// Executa toda segunda-feira às 06h00
cron.schedule("0 6 * * 1", async () => {
  console.log(`[${new Date().toISOString()}] Iniciando geração de narrativas...`);

  try {
    // 1. Buscar todos os hosts da view
    const hosts = await buscarHostsDaView();
    console.log(`${hosts.length} hosts encontrados para processamento`);

    // 2. Gerar narrativas via Haiku 4.5
    const narrativas = await gerarNarrativasEmLote(hosts);

    // 3. Salvar no banco
    await salvarNarrativas(narrativas);

    const sucessos = narrativas.filter((n) => n.status === "gerado").length;
    const erros = narrativas.filter((n) => n.status === "erro").length;

    console.log(`Concluído: ${sucessos} gerados, ${erros} erros`);
  } catch (error) {
    console.error("Erro no job de narrativas:", error);
  }
});

console.log("Job de geração de narrativas agendado — toda segunda-feira às 06h00");
```

---

### Endpoint sob demanda (opcional)

Para permitir regeneração manual de uma narrativa específica:

```javascript
// POST /api/narrativa/gerar/:id_plano
router.post("/narrativa/gerar/:id_plano", async (req, res) => {
  try {
    const { id_plano } = req.params;
    const host = await buscarHostPorId(id_plano);

    if (!host) {
      return res.status(404).json({ erro: "Host não encontrado" });
    }

    const narrativa = await gerarNarrativaHost(host);
    await salvarNarrativa(narrativa);

    res.json({ sucesso: true, narrativa });
  } catch (error) {
    res.status(500).json({ erro: error.message });
  }
});
```

---

## Schema da tabela de narrativas

```sql
CREATE TABLE predict_narrativas (
  id               SERIAL PRIMARY KEY,
  id_plano         INTEGER NOT NULL REFERENCES vw_predict_plano_acao_hostname(id_plano),
  problema         TEXT NOT NULL,
  impacto          TEXT NOT NULL,
  acoes            JSONB NOT NULL,       -- array de strings
  urgencia         VARCHAR(10) NOT NULL, -- IMEDIATA | ALTA | MÉDIA
  justificativa_urgencia TEXT NOT NULL,
  modelo_utilizado VARCHAR(50) NOT NULL,
  dt_geracao       TIMESTAMP NOT NULL DEFAULT NOW(),
  status           VARCHAR(20) NOT NULL DEFAULT 'gerado',
  erro             TEXT,                 -- preenchido apenas se status = 'erro'
  UNIQUE (id_plano)                      -- uma narrativa por host, sobrescreve a anterior
);

CREATE INDEX idx_narrativas_id_plano ON predict_narrativas(id_plano);
CREATE INDEX idx_narrativas_urgencia ON predict_narrativas(urgencia);
CREATE INDEX idx_narrativas_dt_geracao ON predict_narrativas(dt_geracao);
```

---

## Implementação — Frontend

### Como consumir os dados

O frontend deve fazer um JOIN entre os dados da view e a tabela de narrativas:

```sql
SELECT
  v.*,
  n.problema,
  n.impacto,
  n.acoes,
  n.urgencia,
  n.justificativa_urgencia,
  n.dt_geracao,
  n.status AS status_narrativa
FROM vw_predict_plano_acao_hostname v
LEFT JOIN predict_narrativas n ON v.id_plano = n.id_plano
ORDER BY v.ranking_hostname;
```

---

### Anatomia do card com narrativa

```
┌──────────────────────────────────────────────────────────────────┐
│  [P2]  [Regra / Integração]                    Score  4.924 ████ │
│                                                                  │
│  ODO - BRDTCJBS22                                                │
│  Administração de Middleware                                     │
│                                                                  │
│  ✦ Gerado pelo Predict Radar · Haiku 4.5 · Seg 14/04/2026       │
│  ─────────────────────────────────────────────────────────────── │
│                                                                  │
│  PROBLEMA                                                        │
│  Erro recorrente no motor de regras do servidor JBoss EAP,      │
│  com 2.462 ocorrências em 60 dias consecutivos sem resolução.   │
│                                                                  │
│  IMPACTO                                                         │
│  Risco de falha na aplicação dcmsweb-dique, com potencial       │
│  indisponibilidade do fluxo de negócio associado.               │
│                                                                  │
│  ⚠ URGÊNCIA: ALTA                                               │
│  60 dias de recorrência sem correção indica problema estrutural. │
│                                                                  │
│  [Ver plano de ação completo]           [Abrir chamado no GLPI] │
└──────────────────────────────────────────────────────────────────┘
```

---

### Painel de detalhe — Plano de ação

Ao clicar em "Ver plano de ação completo", abrir drawer lateral com:

```
┌─────────────────────────────────────────────────────┐
│  ODO - BRDTCJBS22                              [  X] │
│  Administração de Middleware · P2 · Score 4.924     │
│  ✦ Narrativa gerada por Claude Haiku 4.5            │
│  ─────────────────────────────────────────────────  │
│                                                     │
│  PROBLEMA DETECTADO                                 │
│  [texto do campo "problema"]                        │
│                                                     │
│  IMPACTO NO NEGÓCIO                                 │
│  [texto do campo "impacto"]                         │
│                                                     │
│  PLANO DE AÇÃO                                      │
│  1. [acoes[0]]                                      │
│  2. [acoes[1]]                                      │
│  3. [acoes[2]]                                      │
│  4. [acoes[3]]                                      │
│  5. [acoes[4]]                                      │
│                                                     │
│  URGÊNCIA: ALTA                                     │
│  [justificativa_urgencia]                           │
│                                                     │
│  ─────────────────────────────────────────────────  │
│  DADOS TÉCNICOS                                     │
│  2.462 eventos · 60 dias · 0% graves                │
│  Última ocorrência: 13/03/2026 18:48                │
│  Análise: 14/01/2026 → 13/03/2026                   │
│                                                     │
│  [Abrir chamado no GLPI]  [Regenerar narrativa ↺]   │
└─────────────────────────────────────────────────────┘
```

---

### Estados visuais da narrativa no card

| Situação | Visual |
|---|---|
| Narrativa gerada com sucesso | Exibir normalmente com badge "✦ Gerado pelo Predict Radar" |
| Narrativa ainda não gerada | Exibir skeleton/placeholder com texto "Análise em processamento..." |
| Erro na geração | Exibir botão "Regenerar narrativa" com ícone de refresh |
| Narrativa com mais de 30 dias | Exibir badge amarelo "Atualizar análise" |

---

### Botão "Regenerar narrativa"

Disponível no painel de detalhe — chama o endpoint `POST /api/narrativa/gerar/:id_plano` e atualiza o card sem recarregar a página.

Feedback visual:
1. Botão entra em estado loading com spinner
2. Após retorno, card atualiza com nova narrativa
3. Toast: "Narrativa atualizada com sucesso"

---

## Cores de urgência

| Nível | Cor de fundo | Cor do texto | Borda |
|---|---|---|---|
| IMEDIATA | `#FFEBEE` | `#B71C1C` | `#E53935` |
| ALTA | `#FFF3E0` | `#E65100` | `#F57C00` |
| MÉDIA | `#F3F8FF` | `#1565C0` | `#1E88E5` |

---

## Estimativa de custo

| Cenário | Volume | Custo estimado |
|---|---|---|
| 825 hosts · execução mensal | ~660k tokens | ~$0,85/mês |
| 825 hosts · execução semanal | ~660k tokens | ~$3,40/mês |
| Regenerações manuais | ~800 tokens/host | ~$0,001/host |

*Valores estimados com Haiku 4.5 em modo padrão. Usar Batch API reduz em 50%.*

---

## Critério de aceite

- [ ] Job agendado roda toda segunda-feira às 06h00 sem intervenção manual
- [ ] Narrativas geradas são salvas na tabela `predict_narrativas`
- [ ] Cards do Discovery exibem problema, impacto e urgência gerados pelo Haiku
- [ ] Painel de detalhe exibe o plano de ação com os 5 passos numerados
- [ ] Badge "Gerado pelo Predict Radar · Haiku 4.5 · [data]" aparece em todos os cards com narrativa
- [ ] Cards sem narrativa exibem skeleton de loading
- [ ] Cards com erro exibem botão "Regenerar narrativa"
- [ ] Botão "Regenerar narrativa" funciona sem recarregar a página
- [ ] API Key **nunca** aparece no código-fonte ou em logs
- [ ] Erros de API são tratados silenciosamente sem quebrar a tela

---

## Observação final

Todo o processamento de inteligência — score, prioridade, ranking, classificação de ação — já está feito pelo modelo preditivo e persistido na view. O papel do Haiku 4.5 é exclusivamente **transformar esses dados em linguagem executiva**. Ele não toma decisões, não altera prioridades e não inventa informações — apenas escreve o que os dados já dizem, de forma que qualquer gestor entenda e aja.

---

*Documento gerado por Service IT — Produto Predict | Abril/2026*
