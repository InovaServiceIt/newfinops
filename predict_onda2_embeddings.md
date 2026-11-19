# 🚀 Predict — Onda 2: Migração para Embeddings Reais + HNSW

> **Destinatário:** Time Antigravity  
> **Elaborado por:** Service IT / Infraestrutura  
> **Data:** Abril 2026  
> **Contexto:** Migração do gerador de Hash Espacial (POC) para embeddings reais via Voyage AI, com implementação de índices HNSW no pgvector.

---

## 📋 Índice

1. [Visão Geral da Onda 2](#1-visão-geral-da-onda-2)
2. [Pré-requisitos](#2-pré-requisitos)
3. [Provedor de Embeddings — Voyage AI](#3-provedor-de-embeddings--voyage-ai)
4. [Migração do Banco de Dados](#4-migração-do-banco-de-dados)
5. [Implementação do Índice HNSW](#5-implementação-do-índice-hnsw)
6. [Migração do Código C#](#6-migração-do-código-c)
7. [Re-embedding do Backlog](#7-re-embedding-do-backlog)
8. [CronJob de Produção](#8-cronjob-de-produção)
9. [Validação e Testes](#9-validação-e-testes)
10. [Custos Estimados](#10-custos-estimados)
11. [Checklist de Go-Live](#11-checklist-de-go-live)

---

## 1. Visão Geral da Onda 2

### O que muda

| Item | Onda 1 (POC) | Onda 2 (Produção) |
|---|---|---|
| Gerador de embeddings | Hash Espacial C# local | Voyage AI API |
| Modelo | Determinístico/randômico | `voyage-3` (1024 dims) |
| Custo de embedding | $0,00 | ~$0,06/1M tokens |
| Índice vetorial | Full Scan | HNSW (cosine) |
| Trigger | Manual (botão UI) | CronJob agendado |
| Qualidade semântica | Pseudoaleatória | Semântica real |

### O que **não** muda

- DDL da tabela `predict_alert_embeddings` — permanece `vector(1024)` ✅
- Operador de busca `<=>` (cosine) — permanece ✅
- Lógica de `NOT EXISTS` para backlog — permanece ✅
- Processamento em lotes de 1.000 em 1.000 — permanece ✅
- Módulo Discovery como consumidor — permanece ✅

---

## 2. Pré-requisitos

### Infraestrutura (já concluído ✅)
- [x] PostgreSQL 16 com pgvector instalado (`pgvector/pgvector:pg16`)
- [x] `CREATE EXTENSION vector;` executado no `Service_Infra_DB_DEV`
- [x] Tabela `predict_alert_embeddings` com coluna `embedding vector(1024)`

### Ainda necessário
- [ ] Chave de API do Voyage AI
- [ ] Configurar chave no `appsettings.json` / variável de ambiente
- [ ] Criar índice HNSW (instruções na seção 5)
- [ ] Re-embedar os 1.234 registros existentes com vetores reais

---

## 3. Provedor de Embeddings — Voyage AI

### Por que Voyage AI?

- Modelo `voyage-3` produz **exatamente 1024 dimensões** — zero mudança no schema
- Melhor performance em recuperação semântica vs OpenAI e Cohere
- Suporte nativo a **português** no modelo multilingual
- Primeiros **200 milhões de tokens gratuitos** por conta
- Integração simples via HTTP REST ou SDK

### Criação da conta e chave

1. Acesse [voyageai.com](https://www.voyageai.com)
2. Crie uma conta e vá em **API Keys**
3. Gere uma nova chave e adicione ao `appsettings.json`:

```json
{
  "VoyageAI": {
    "ApiKey": "pa-SUA_CHAVE_AQUI",
    "Model": "voyage-3",
    "Dimensions": 1024,
    "InputType": "document"
  }
}
```

### Modelos disponíveis (escolha um)

| Modelo | Dimensões | Contexto | Preço/1M tokens |
|---|---|---|---|
| `voyage-3-lite` | 512 | 32K | $0,02 |
| `voyage-3` | 1024 | 32K | $0,06 |
| `voyage-3-large` | 1024 | 32K | $0,18 |

> ⚠️ **Use `voyage-3`** — mantém 1024 dims, melhor custo-benefício para alertas de TI em português.

---

## 4. Migração do Banco de Dados

### Passo 1 — Limpar embeddings pseudoaleatórios existentes

> ⚠️ Os vetores atuais são baseados em hash e **não têm semântica real**. Devem ser removidos antes do re-embedding.

```sql
-- Backup de segurança antes de limpar
CREATE TABLE predict_alert_embeddings_backup_onda1 AS 
SELECT * FROM predict_alert_embeddings;

-- Limpar vetores pseudoaleatórios
TRUNCATE TABLE predict_alert_embeddings;
```

### Passo 2 — Verificar estrutura da tabela

```sql
SELECT column_name, data_type, udt_name
FROM information_schema.columns
WHERE table_name = 'predict_alert_embeddings'
ORDER BY ordinal_position;
```

A coluna `embedding` deve ser `vector(1024)` — se for qualquer outro tamanho, ajustar antes do re-embedding.

---

## 5. Implementação do Índice HNSW

> ⚠️ **Crie o índice ANTES de iniciar o re-embedding** — mais eficiente do que criar após a inserção em massa.

### O que é HNSW?

HNSW (Hierarchical Navigable Small World) é um algoritmo de busca aproximada (ANN) que substitui o Full Scan por uma estrutura de grafo em camadas. Para 10K+ registros, a diferença de performance é de segundos para milissegundos.

### Criação do índice

```sql
-- Criar índice HNSW com distância cosseno
-- m: número de conexões por nó (padrão 16, aumente para maior recall)
-- ef_construction: tamanho da fila de busca na construção (padrão 64)
CREATE INDEX idx_hnsw_alert_embeddings
ON predict_alert_embeddings
USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);
```

### Parâmetros recomendados para o Predict

| Parâmetro | Valor | Impacto |
|---|---|---|
| `m` | 16 | Conexões por nó — maior = mais preciso, mais memória |
| `ef_construction` | 64 | Qualidade de construção — maior = mais lento de construir |
| `ef_search` | 40 | Qualidade de busca em runtime — ajuste conforme necessário |

```sql
-- Configurar ef_search para queries (rode antes das consultas)
SET hnsw.ef_search = 40;
```

### Verificar o índice criado

```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'predict_alert_embeddings';
```

---

## 6. Migração do Código C#

### Instalar o pacote HTTP client (ou usar HttpClient nativo)

```bash
dotnet add package VoyageAI
# ou usar HttpClient nativo — sem dependência externa
```

### Substituir `GerarEmbeddingsAsync`

```csharp
public class VoyageEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.voyageai.com/v1/embeddings";

    public VoyageEmbeddingService(IConfiguration config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = config["VoyageAI:ApiKey"];
        _model = config["VoyageAI:Model"]; // "voyage-3"
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<float[]> GerarEmbeddingAsync(string texto)
    {
        var payload = new
        {
            input = new[] { texto },
            model = _model,
            input_type = "document"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(BaseUrl, content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<VoyageResponse>();
        return result.Data[0].Embedding;
    }

    public async Task<List<float[]>> GerarEmbeddingsEmLoteAsync(List<string> textos)
    {
        // Voyage aceita até 128 textos por request
        var lotes = textos.Chunk(128);
        var embeddings = new List<float[]>();

        foreach (var lote in lotes)
        {
            var payload = new
            {
                input = lote,
                model = _model,
                input_type = "document"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(BaseUrl, content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VoyageResponse>();
            embeddings.AddRange(result.Data.Select(d => d.Embedding));

            // Rate limiting — aguardar entre lotes
            await Task.Delay(100);
        }

        return embeddings;
    }
}

// DTOs
public record VoyageResponse(List<VoyageEmbedding> Data, VoyageUsage Usage);
public record VoyageEmbedding(int Index, float[] Embedding);
public record VoyageUsage(int TotalTokens);
```

### Manter a lógica de paginação de 1.000 em 1.000

```csharp
public async Task ProcessarBacklogAsync()
{
    int pagina = 0;
    const int tamanhoPagina = 1000;
    const int tamanheLote = 128; // Máximo do Voyage por request

    while (true)
    {
        // Buscar alertas sem embedding
        var alertas = await _db.Alertas
            .Where(a => !_db.Embeddings.Any(e => e.AlertId == a.Id))
            .Skip(pagina * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        if (!alertas.Any()) break;

        // Processar em sublotes de 128 (limite do Voyage)
        var sublotes = alertas.Chunk(tamanheLote);

        foreach (var sublote in sublotes)
        {
            var textos = sublote.Select(a => a.DescriptionFlat).ToList();
            var embeddings = await _voyageService.GerarEmbeddingsEmLoteAsync(textos);

            for (int i = 0; i < sublote.Length; i++)
            {
                _db.Embeddings.Add(new PredictAlertEmbedding
                {
                    AlertId = sublote[i].Id,
                    IdContrato = sublote[i].IdContrato,
                    Embedding = embeddings[i]
                });
            }

            await _db.SaveChangesAsync();
        }

        pagina++;
        Console.WriteLine($"✅ Página {pagina} processada — {alertas.Count} alertas vetorizados");
    }

    Console.WriteLine("🎉 Re-embedding concluído!");
}
```

### Query de busca vetorial — permanece igual

```csharp
// Gerar embedding da query do usuário
var queryEmbedding = await _voyageService.GerarEmbeddingAsync(textoBusca);

// Busca por similaridade cosseno — sem mudança no SQL
var sql = @"
    SELECT alert_id, description_flat, id_contrato,
           1 - (embedding <=> @vetor::vector) AS similaridade
    FROM predict_alert_embeddings
    ORDER BY embedding <=> @vetor::vector
    LIMIT 20";
```

---

## 7. Re-embedding do Backlog

Após configurar a chave e o código, re-embedar os 1.234 registros existentes:

```sql
-- Verificar quantos precisam de re-embedding
SELECT COUNT(*) FROM predict_alert_embeddings;

-- Após rodar o ProcessarBacklogAsync, verificar resultado
SELECT 
    COUNT(*) as total,
    COUNT(embedding) as com_embedding,
    COUNT(*) - COUNT(embedding) as sem_embedding
FROM predict_alert_embeddings;
```

> ⏱️ **Estimativa de tempo:** 1.234 registros ÷ 128 por request = ~10 requests. Menos de 1 minuto.  
> 💰 **Custo estimado:** 1.234 alertas × ~50 tokens médios = ~62K tokens = **$0,004** (menos de 1 centavo)

---

## 8. CronJob de Produção

### Opção A — Hangfire (recomendado para .NET)

```csharp
// Program.cs ou Startup.cs
services.AddHangfire(config => config.UsePostgreSqlStorage(connectionString));
services.AddHangfireServer();

// Agendar job diário às 2h da manhã
RecurringJob.AddOrUpdate<EmbeddingService>(
    "predict-embedding-job",
    service => service.ProcessarBacklogAsync(),
    "0 2 * * *" // Cron: todo dia às 02:00
);
```

### Opção B — CronJob no Coolify

No painel do Coolify, adicionar um novo serviço do tipo **Scheduled Task**:

```
Schedule: 0 2 * * *
Command: dotnet predict-embedding-worker.dll
```

---

## 9. Validação e Testes

### Teste 1 — Verificar dimensões dos novos vetores

```sql
SELECT 
    id,
    alert_id,
    array_length(embedding::real[], 1) AS dimensoes
FROM predict_alert_embeddings
LIMIT 5;
```

> ✅ Esperado: **1024** para todos

### Teste 2 — Busca semântica real

```sql
-- Substitua pelo vetor gerado via Voyage para o texto de teste
-- Este é apenas um exemplo estrutural
SELECT 
    alert_id,
    description_flat,
    1 - (embedding <=> '[0.1, 0.2, ...]'::vector) AS similaridade
FROM predict_alert_embeddings
ORDER BY embedding <=> '[0.1, 0.2, ...]'::vector
LIMIT 10;
```

### Teste 3 — Performance com índice HNSW

```sql
-- Verificar uso do índice
EXPLAIN ANALYZE
SELECT alert_id, 1 - (embedding <=> '[0.1, 0.2, ...]'::vector) AS sim
FROM predict_alert_embeddings
ORDER BY embedding <=> '[0.1, 0.2, ...]'::vector
LIMIT 20;
```

> ✅ Esperado: `Index Scan using idx_hnsw_alert_embeddings` — **não** Sequential Scan

### Teste 4 — Qualidade semântica

Buscar por `"servidor fora do ar"` e validar que os resultados retornam alertas semanticamente similares de **Disaster** e **High** — não alertas de **Warning** ou **Average** não relacionados.

---

## 10. Custos Estimados

### Voyage AI

| Cenário | Alertas/mês | Tokens estimados | Custo/mês |
|---|---|---|---|
| Atual (POC) | 1.234 | ~62K | **$0,004** |
| Produção baixa | 10.000 | ~500K | **$0,03** |
| Produção média | 100.000 | ~5M | **$0,30** |
| Produção alta | 1.000.000 | ~50M | **$3,00** |

> 💡 Os primeiros **200M tokens são gratuitos** — cobrirá meses de produção antes de qualquer cobrança.

### Claude Haiku (agrupamento semântico — já em uso)

| Cenário | Custo/mês estimado |
|---|---|
| Atual | ~$0,50–$2,00 |
| Produção | ~$5,00–$20,00 |

---

## 11. Checklist de Go-Live

### Banco de Dados
- [ ] Backup da tabela `predict_alert_embeddings` realizado
- [ ] `TRUNCATE predict_alert_embeddings` executado
- [ ] Índice HNSW criado com `vector_cosine_ops`
- [ ] Índice verificado com `pg_indexes`

### Código
- [ ] Chave Voyage AI adicionada ao `appsettings.json`
- [ ] `GerarEmbeddingsAsync` substituído pelo `VoyageEmbeddingService`
- [ ] Teste unitário do novo service com 1 alerta
- [ ] Processamento em lotes validado (128 por request)

### Re-embedding
- [ ] `ProcessarBacklogAsync` executado para os 1.234 registros
- [ ] Verificação de dimensões (1024) em todos os registros
- [ ] Busca semântica testada com casos reais

### Produção
- [ ] CronJob configurado (Hangfire ou Coolify Scheduled Task)
- [ ] Monitoramento de erros na API Voyage configurado
- [ ] `ef_search = 40` configurado nas queries de busca
- [ ] Dashboard de uso da API Voyage acessível

---

## 📞 Contato e Suporte

- **Infraestrutura DB:** Service IT
- **Voyage AI Docs:** [docs.voyageai.com](https://docs.voyageai.com)
- **pgvector HNSW:** [github.com/pgvector/pgvector](https://github.com/pgvector/pgvector)
- **Dúvidas:** Abrir chamado interno ou contato direto

---

> *"Está tudo justo e perfeito"* 🏛️  
> — Service IT, Abril 2026
