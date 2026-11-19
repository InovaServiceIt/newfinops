# Briefing Técnico — ServiceCatalogController
> Predict | Service IT · Versão 1.0 · 02/05/2026
> Destinatário: AntiGravity
> Autor: BU de IA Service IT

---

## 1. O problema

A página `CatalogPage.tsx` existe e está completa. Ela chama dois endpoints:

```
GET /api/servicecatalog?search={query}
GET /api/servicecatalog/stats
```

Nenhum dos dois existe no backend. O servidor retorna **404** nas duas chamadas. A tabela `predict_service_catalog` tem 1.344 registros mas a tela não mostra nada.

A causa: **`ServiceCatalogController.cs` nunca foi criado.**

---

## 2. O que já existe — não mexer

| Arquivo | Status |
|---------|--------|
| `frontend/src/pages/Discovery/CatalogPage.tsx` | ✅ pronto, não tocar |
| `backend/Services/Implementations/ServiceCatalogEmbeddingService.cs` | ✅ pronto, não tocar |
| `backend/Services/Interfaces/IServiceCatalogEmbeddingService.cs` | ✅ pronto, não tocar |
| Tabela `predict_service_catalog` no banco | ✅ populada com 1.344 registros |

---

## 3. O que precisa ser criado

Um único arquivo:

```
backend/Controllers/ServiceCatalogController.cs
```

---

## 4. Interface TypeScript esperada pelo frontend

O `CatalogPage.tsx` declara:

```typescript
interface CatalogItem {
  id: number;
  activity: string;
  classification: string;
  agentAction: string;       // ← camelCase
  hasEmbedding: boolean;     // ← camelCase
}
```

E para stats:

```typescript
{ total: number; indexed: number; pending: number }
```

O controller precisa retornar exatamente esses nomes de campo. O JSON serializer do projeto já está configurado para camelCase (padrão ASP.NET Core com `System.Text.Json`), então basta nomear as propriedades do DTO em PascalCase que a serialização cuida do resto.

---

## 5. Padrão do projeto — seguir à risca

Usar o mesmo padrão dos controllers existentes. Referência: `OpportunitiesController.cs`.

- Namespace: `Predict.Backend.Controllers`
- Herda: `ControllerBase`
- Atributos: `[ApiController]` + `[Route("api/servicecatalog")]`
- Injeção: `AppDbContext` via construtor (igual ao `OpportunitiesController`)
- Acesso ao banco: **ADO.NET raw** via `db.Database.GetDbConnection()` — o padrão já estabelecido no projeto para queries diretas

---

## 6. Implementação completa

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/servicecatalog")]
    public class ServiceCatalogController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ServiceCatalogController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/servicecatalog?search=disco
        [HttpGet]
        public async Task<IActionResult> GetCatalog([FromQuery] string? search)
        {
            var items = new List<CatalogItemDto>();

            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                var whereClause = string.IsNullOrWhiteSpace(search)
                    ? "WHERE is_active = TRUE"
                    : @"WHERE is_active = TRUE
                          AND (
                            activity    ILIKE @search
                            OR agent_action ILIKE @search
                            OR classification ILIKE @search
                          )";

                cmd.CommandText = $@"
                    SELECT
                        id,
                        activity,
                        classification,
                        agent_action,
                        (embedding IS NOT NULL) AS has_embedding
                    FROM public.predict_service_catalog
                    {whereClause}
                    ORDER BY portfolio, service, weight
                    LIMIT 500";

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@search";
                    p.Value = $"%{search}%";
                    cmd.Parameters.Add(p);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new CatalogItemDto
                    {
                        Id            = reader.GetInt32(0),
                        Activity      = reader.GetString(1),
                        Classification = reader.GetString(2),
                        AgentAction   = reader.GetString(3),
                        HasEmbedding  = reader.GetBoolean(4)
                    });
                }

                return Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CATALOG] Erro ao buscar catálogo: {ex.Message}");
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        // GET /api/servicecatalog/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        COUNT(*)                                          AS total,
                        COUNT(*) FILTER (WHERE embedding IS NOT NULL)    AS indexed,
                        COUNT(*) FILTER (WHERE embedding IS NULL)        AS pending
                    FROM public.predict_service_catalog
                    WHERE is_active = TRUE";

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        total   = reader.GetInt64(0),
                        indexed = reader.GetInt64(1),
                        pending = reader.GetInt64(2)
                    });
                }

                return Ok(new { total = 0, indexed = 0, pending = 0 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CATALOG] Erro ao buscar stats: {ex.Message}");
                return StatusCode(500, new { erro = ex.Message });
            }
        }
    }

    // DTO interno — pode ficar no mesmo arquivo ou em Models/
    public class CatalogItemDto
    {
        public int    Id             { get; set; }
        public string Activity       { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string AgentAction    { get; set; } = string.Empty;
        public bool   HasEmbedding   { get; set; }
    }
}
```

---

## 7. Rota do menu — verificar

A `CatalogPage.tsx` já existe. Confirmar se a rota está registrada no `App.tsx`. Procurar por:

```tsx
<Route path="/discovery/catalog" element={<CatalogPage />} />
```

Se não existir, adicionar. O link no menu lateral (se houver) também precisa apontar para `/discovery/catalog`.

---

## 8. Critérios de aceite

- [ ] `GET /api/servicecatalog` retorna array de `CatalogItemDto` com status 200
- [ ] `GET /api/servicecatalog?search=disco` filtra por `activity`, `agent_action` e `classification`
- [ ] `GET /api/servicecatalog/stats` retorna `{ total, indexed, pending }` com status 200
- [ ] A tela `/discovery/catalog` exibe a tabela com os 1.344 itens
- [ ] Os cards de stats (`Total`, `Indexados`, `Pendentes`) exibem os valores corretos
- [ ] A pesquisa funciona sem recarregar a página

---

## 9. O que não precisa fazer

- Não criar repository nem service — query direta no controller é o padrão do projeto para casos simples como este
- Não criar migration — tabela já existe no banco
- Não mexer no frontend — `CatalogPage.tsx` está pronto
- Não alterar `Program.cs` — `AppDbContext` já está registrado no DI

---

## 10. Checklist de entrega

```
1. Criar backend/Controllers/ServiceCatalogController.cs
2. Verificar rota /discovery/catalog no App.tsx
3. Testar GET /api/servicecatalog → 200 com dados
4. Testar GET /api/servicecatalog/stats → 200 com contagens
5. Abrir a tela no browser e confirmar que a tabela carrega
```

---

*Dúvidas sobre este briefing: BU de IA Service IT.*
