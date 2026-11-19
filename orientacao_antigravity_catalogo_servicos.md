# Correção - Catálogo de Serviços nos Cards do Predict Discovery

## Objetivo

Corrigir a exibição do Catálogo de Serviços nos cards/tela do Predict Discovery.

Hoje o frontend chama APIs que não existem mais no backend, por isso o catálogo não é carregado corretamente.

---

## Problema identificado

O frontend utiliza as rotas:

```ts
GET /api/servicecatalog
GET /api/servicecatalog/stats
```

Arquivo relacionado:

```txt
frontend/src/pages/Discovery/CatalogPage.tsx
```

Porém o backend não possui mais o controller:

```txt
backend/Controllers/ServiceCatalogController.cs
```

Esse controller foi removido para evitar problemas de mapeamento EF com `pgvector`, mas o frontend continuou dependendo dele.

---

## Diretriz técnica

Não recriar o catálogo usando Entity Framework.

A correção deve ser feita criando um controller simples usando SQL raw, evitando mapear a coluna `embedding` como entidade EF.

---

## Criar arquivo

```txt
backend/Controllers/ServiceCatalogController.cs
```

---

## Implementação esperada

Criar um controller com duas rotas:

### 1. Listar catálogo

```http
GET /api/servicecatalog
```

Consulta sugerida:

```sql
SELECT 
    id,
    activity,
    classification,
    agent_action,
    is_active,
    embedding IS NOT NULL AS has_embedding
FROM public.predict_service_catalog
WHERE is_active = true
ORDER BY activity;
```

### 2. Estatísticas do catálogo

```http
GET /api/servicecatalog/stats
```

Consulta sugerida:

```sql
SELECT 
    COUNT(*) AS total,
    COUNT(*) FILTER (WHERE embedding IS NOT NULL) AS indexed,
    COUNT(*) FILTER (WHERE embedding IS NULL) AS pending
FROM public.predict_service_catalog
WHERE is_active = true;
```

---

## Ponto importante

Não adicionar:

```csharp
DbSet<PredictServiceCatalog>
```

Não mapear a tabela `predict_service_catalog` no `DbContext`.

Não mapear a coluna `embedding`.

Usar conexão direta via:

```csharp
AppDbContext.Database.GetDbConnection()
```

ou abordagem equivalente já usada no projeto.

---

## Corrigir também a ordem do processamento automático

No `Program.cs`, garantir que a função:

```sql
find_catalog_match
```

seja criada antes da execução de:

```csharp
RunFullCatalogMatch.Run(...)
```

A ordem correta deve ser:

1. Garantir tabela e colunas do catálogo;
2. Criar ou recriar a função `find_catalog_match`;
3. Gerar/indexar embeddings do catálogo;
4. Executar o match retroativo das oportunidades.

---

## Verificar script de match retroativo

Arquivo:

```txt
backend/Scratch/RunFullCatalogMatch.cs
```

Verificar se ele usa:

```sql
SELECT embedding FROM predict_discovery_opportunities
```

Caso a tabela `predict_discovery_opportunities` não possua a coluna `embedding`, ajustar a lógica para:

- gerar embedding da oportunidade em tempo de execução; ou
- criar a coluna `embedding`; ou
- remover essa dependência do script.

Não deixar o script quebrar silenciosamente.

---

## Critério de aceite

A correção estará válida quando:

1. A tela do Catálogo de Serviços carregar sem erro 404;
2. Os cards do Predict Discovery exibirem informações do catálogo;
3. A API `/api/servicecatalog` retornar registros ativos;
4. A API `/api/servicecatalog/stats` retornar total, indexados e pendentes;
5. O backend subir sem erro relacionado a `pgvector` ou mapeamento EF;
6. O match automático executar depois da criação da função `find_catalog_match`.

---

## Resumo da causa raiz

O frontend continuou chamando APIs do Catálogo de Serviços, mas o controller correspondente foi removido do backend.

A solução correta é restaurar as APIs usando SQL raw, sem voltar a mapear o catálogo no Entity Framework.
