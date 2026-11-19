# 🔄 Predict — Onda 2: Troca do Gerador de Embeddings para Ollama Local

> **Destinatário:** Time Antigravity  
> **Data:** Abril 2026  
> **Objetivo:** Substituir o Hash Espacial C# pelo modelo `qwen3-embedding:0.6b` rodando via Ollama no servidor local — custo zero, 1024 dimensões, DDL intacta, suporte nativo a português.

---

## ✅ O que já está pronto na infraestrutura

| Item | Status |
|---|---|
| Ollama rodando em Docker | ✅ `204.168.147.122:11434` |
| Modelo `qwen3-embedding:0.6b` | ✅ Instalado (1024 dims, PT-BR ✅) |
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
  "model": "qwen3-embedding:0.6b",
  "prompt": "texto do alerta aqui"
}
```

**Resposta:**
```json
{
  "embedding": [0.0234, -0.0412, 0.1873, ...] // array de 1024 floats
}
```

> ⚠️ **Atenção:** O Qwen3-Embedding usa o último token `<|endoftext|>` como âncora. O Ollama já gerencia isso internamente — nenhum ajuste manual necessário no prompt.

---

## 2. Alterações no `appsettings.json`

Substitua qualquer configuração de embedding existente por:

```json
{
  "Ollama": {
    "BaseUrl": "http://204.168.147.122:11434",
    "Model": "qwen3-embedding:0.6b",
    "Dimensions": 1024
  }
}
```

---

## 3. Implementação C# — OllamaEmbeddingService

```csharp
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaEmbeddingService(IConfiguration config, HttpClient http)
    {
        _http = http;
        _baseUrl = config["Ollama:BaseUrl"]!;
        _model  = config["Ollama:Model"]!;
    }

    public async Task<float[]> GerarAsync(string texto, CancellationToken ct = default)
    {
        var payload = new { model = _model, prompt = texto };

        var response = await _http.PostAsJsonAsync(
            $"{_baseUrl}/api/embeddings", payload, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: ct);

        return result!.Embedding;
    }
}

public record OllamaEmbeddingResponse(
    [property: JsonPropertyName("embedding")] float[] Embedding
);
```

---

## 4. Registro no DI (`Program.cs`)

```csharp
builder.Services.AddHttpClient<OllamaEmbeddingService>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
```

---

## 5. Interface esperada

```csharp
public interface IEmbeddingService
{
    Task<float[]> GerarAsync(string texto, CancellationToken ct = default);
}
```

---

## 6. Uso no Radar / Discovery

```csharp
// Gerar embedding de um alerta
var embedding = await _embeddingService.GerarAsync(alerta.Descricao);

// Persistir no banco via pgvector
await _repo.SalvarEmbeddingAsync(alertaId, embedding);

// Buscar alertas similares (cosine similarity)
var similares = await _repo.BuscarSimilaresAsync(embedding, threshold: 0.90f, limit: 5);
```

---

## 7. DDL — Sem alterações

A DDL permanece **100% intacta**:

```sql
-- Nenhuma alteração necessária
-- A coluna já está correta:
embedding vector(1024)
```

---

## 8. Checklist de go-live

- [ ] Confirmar acesso ao endpoint `http://204.168.147.122:11434/api/embeddings`
- [ ] Substituir `appsettings.json` com as novas chaves `Ollama.*`
- [ ] Remover/desativar o Hash Espacial C# anterior
- [ ] Registrar `OllamaEmbeddingService` no DI
- [ ] Testar geração de embedding em DEV
- [ ] Validar que o retorno tem exatamente 1024 dimensões
- [ ] Validar busca por similaridade com threshold 0.90

---

## 9. Teste rápido de sanidade (no servidor)

```bash
curl -s http://localhost:11434/api/embeddings -d '{"model": "qwen3-embedding:0.6b", "prompt": "Servidor fora do ar"}' | python3 -c "import json,sys; e=json.load(sys.stdin)['embedding']; print(f'Dimensões: {len(e)}')"
# Esperado: Dimensões: 1024
```

---

## Por que Qwen3-Embedding e não BGE-M3?

| Critério | BGE-M3 | Qwen3-Embedding-0.6B |
|---|---|---|
| Parâmetros | 568M | 600M |
| Dimensões | 1024 | 1024 |
| Contexto | 8.192 tokens | **32.768 tokens** |
| MMTEB multilíngue | 59,56 | **64,33 (+7,9%)** |
| Português | ✅ | ✅ |
| RAM em inferência | ~1–2 GB | ~1–2 GB |
| Custo | $0 | $0 |

Mesma infraestrutura, mesmo custo, ~8% mais preciso em português.
