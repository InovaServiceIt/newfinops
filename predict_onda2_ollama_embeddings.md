# 🔄 Predict — Onda 2: Troca do Gerador de Embeddings para Ollama Local

> **Destinatário:** Time Antigravity  
> **Data:** Abril 2026  
> **Objetivo:** Substituir o Hash Espacial C# pelo modelo `mxbai-embed-large` rodando via Ollama no servidor local — custo zero, 1024 dimensões, DDL intacta.

---

## ✅ O que já está pronto na infraestrutura

| Item | Status |
|---|---|
| Ollama rodando em Docker | ✅ `204.168.147.122:11434` |
| Modelo `mxbai-embed-large` | ✅ Instalado (1024 dims) |
| pgvector no PostgreSQL | ✅ `Service_Infra_DB_DEV` |
| DDL `embedding vector(1024)` | ✅ Sem alterações necessárias |
| Custo | ✅ **$0,00 para sempre** |

---

## 1. Endpoint da API

O Ollama expõe uma API REST local compatível com qualquer HTTP client:

```
POST http://204.168.147.122:11434/api/embeddings
Content-Type: application/json

{
  "model": "mxbai-embed-large",
  "prompt": "texto do alerta aqui"
}
```

**Resposta:**
```json
{
  "embedding": [0.0234, -0.0412, 0.1873, ...] // array de 1024 floats
}
```

---

## 2. Alterações no `appsettings.json`

Substitua qualquer configuração de embedding existente por:

```json
{
  "Ollama": {
    "BaseUrl": "http://204.168.147.122:11434",
    "Model": "mxbai-embed-large",
    "Dimensions": 1024
  }
}
```

---

## 3. Substituição do `GerarEmbeddingsAsync` em C#

Substitua o gerador de Hash Espacial pelo seguinte service:

```csharp
public class OllamaEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaEmbeddingService(IConfiguration config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = config["Ollama:BaseUrl"]; // http://204.168.147.122:11434
        _model = config["Ollama:Model"];     // mxbai-embed-large
    }

    public async Task<float[]> GerarEmbeddingAsync(string texto)
    {
        var payload = new
        {
            model = _model,
            prompt = texto
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result.Embedding;
    }

    public async Task<List<float[]>> GerarEmbeddingsEmLoteAsync(List<string> textos)
    {
        var embeddings = new List<float[]>();

        // Ollama processa um texto por request — paralelizar com limite de concorrência
        var semaforo = new SemaphoreSlim(5); // máximo 5 requests simultâneos

        var tasks = textos.Select(async texto =>
        {
            await semaforo.WaitAsync();
            try
            {
                return await GerarEmbeddingAsync(texto);
            }
            finally
            {
                semaforo.Release();
            }
        });

        var resultados = await Task.WhenAll(tasks);
        return resultados.ToList();
    }
}

// DTOs
public record OllamaEmbeddingResponse(
    [property: JsonPropertyName("embedding")] float[] Embedding
);
```

### Registrar no DI (Program.cs)

```csharp
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddScoped<OllamaEmbeddingService>();
```

---

## 4. Manter a lógica de paginação 1.000 em 1.000

A lógica existente de backlog com `NOT EXISTS` permanece **100% igual** — apenas substitua a chamada do gerador:

```csharp
// ANTES (Hash Espacial)
var embedding = GerarHashEspacial(alerta.DescriptionFlat);

// DEPOIS (Ollama)
var embedding = await _ollamaService.GerarEmbeddingAsync(alerta.DescriptionFlat);
```

---

## 5. Query de busca vetorial — sem alterações

A query de Cosine Similarity permanece **idêntica**:

```csharp
var sql = @"
    SELECT alert_id, description_flat, id_contrato,
           1 - (embedding <=> @vetor::vector) AS similaridade
    FROM predict_alert_embeddings
    ORDER BY embedding <=> @vetor::vector
    LIMIT 20";
```

O embedding da query do usuário também passa pelo Ollama:

```csharp
// Gerar embedding da query antes de buscar
var queryEmbedding = await _ollamaService.GerarEmbeddingAsync(textoBusca);
```

---

## 6. Limpar embeddings pseudoaleatórios e re-embedar

Os vetores atuais são baseados em hash e não têm semântica real. Antes de rodar o backlog:

```sql
-- Backup de segurança
CREATE TABLE predict_alert_embeddings_backup_onda1 AS 
SELECT * FROM predict_alert_embeddings;

-- Limpar vetores pseudoaleatórios
TRUNCATE TABLE predict_alert_embeddings;
```

Depois acionar o `ProcessarBacklogAsync` existente — ele vai re-embedar os 1.234 registros com vetores reais.

> ⏱️ **Estimativa:** ~5-10 minutos para 1.234 registros (processamento local no servidor)  
> 💰 **Custo:** $0,00

---

## 7. Criar índice HNSW (obrigatório antes do go-live)

```sql
CREATE INDEX idx_hnsw_alert_embeddings
ON predict_alert_embeddings
USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);
```

Configurar nas queries de busca:

```sql
SET hnsw.ef_search = 40;
```

---

## 8. Teste rápido via curl (validação antes de subir o código)

```bash
curl http://204.168.147.122:11434/api/embeddings \
  -H "Content-Type: application/json" \
  -d '{"model": "mxbai-embed-large", "prompt": "Servidor fora do ar"}'
```

> ✅ Esperado: array JSON com 1024 valores float

---

## 9. Checklist de Go-Live

- [ ] `appsettings.json` atualizado com URL e modelo do Ollama
- [ ] `OllamaEmbeddingService` implementado e registrado no DI
- [ ] Chamadas ao Hash Espacial substituídas
- [ ] Teste unitário com 1 alerta via Ollama
- [ ] `TRUNCATE predict_alert_embeddings` executado
- [ ] Índice HNSW criado
- [ ] `ProcessarBacklogAsync` executado para os 1.234 registros
- [ ] Busca semântica testada com casos reais em PT-BR
- [ ] CronJob agendado para novos alertas diários

---

> *"Está tudo justo e perfeito"* 🏛️  
> — Service IT, Abril 2026
