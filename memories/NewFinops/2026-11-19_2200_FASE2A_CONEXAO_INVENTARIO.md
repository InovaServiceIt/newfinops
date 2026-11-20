# Fase 2A — Conexão somente leitura com o `finopsdatabase` + inventário do schema

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 22:00 (Brasília)
**Responsável humano:** HLGDR
**Contexto:** casca e login CloudVista validados em produção (https://newfinops.service.com.br). Checkpoint: `2026-11-19_2155_CHECKPOINT.md`.

---

## 0. Atenção

- O banco é o **`finopsdatabase`** (Postgres, usuário `svc_finops`). Provavelmente é o que alimenta **hoje** o FinOps legado (Python/Celery) e os dashboards do Grafana (Metrô Rio e Unimed). **Trate como produção.**
- **Somente leitura. Nenhuma DDL, nenhum INSERT/UPDATE/DELETE, nenhuma migration do EF.** A DDL é sempre do HLGDR.
- **Senha nunca no Git, no MD ou no log.** No Coolify a conexão já está em `ConnectionStrings__FinopsDb` (segredo). Localmente, o HLGDR vai gravar em **user-secrets** com a chave `ConnectionStrings:FinopsDb` no projeto `backend/` (o `UserSecretsId` já existe no `.csproj`).

## Parte 1 — Conexão no backend (somente leitura)

1. Adicionar `Npgsql` (sem EF Core por enquanto — acesso com `NpgsqlDataSource` + Dapper **ou** ADO puro; escolha o mais simples e diga qual).
2. Registrar um `NpgsqlDataSource` a partir de `ConnectionStrings:FinopsDb`. **Forçar sessão somente leitura** em toda conexão aberta (ex.: `Options=-c default_transaction_read_only=on` aplicado no builder, ou `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY` no `ConnectionInitializer`). Teste que um `CREATE TEMP TABLE`/`UPDATE` falha com erro de read-only (num teste local — **não** em tabela real: use `SELECT` para validar e um comando inofensivo que deve ser recusado, como `CREATE TABLE _cv_teste_ro(x int)`, que precisa **falhar**).
3. Se a variável não existir, a app **continua subindo** (log de aviso, sem crash).
4. Novo endpoint **autenticado** `GET /api/health/db` → `{ status: "ok"|"erro", latencyMs, database, serverVersion, readOnly: true }`. Sem expor host, usuário ou mensagem de erro detalhada para o cliente (detalhe só no log do servidor). O `/api/health` anônimo **não muda**.
5. `appsettings.json`: `ConnectionStrings.FinopsDb` com placeholder `YOUR_CONNECTION_STRING_HERE`.

## Parte 2 — Inventário do schema (local, somente leitura)

Com a conexão do user-secrets, rode consultas **só em `information_schema` / `pg_catalog`** (nenhum `SELECT` em dados de negócio, exceto contagem estimada via `pg_class.reltuples`). Script em `tools/inventory/` (pode ser um console .NET ou `psql` — não versionar credencial).

Gere `memories/NewFinops/2026-11-19_HHMM_INVENTARIO_FINOPSDATABASE.md` com:

1. Versão do Postgres, extensões instaladas, encoding/timezone do banco.
2. Schemas (exceto `pg_*`/`information_schema`).
3. **Tabelas:** schema, nome, linhas estimadas, tamanho total (`pg_total_relation_size`), comentário.
4. **Colunas** de cada tabela: nome, tipo, nullable, default, comentário.
5. **PKs, FKs, índices e constraints únicas.**
6. **Views e materialized views:** nome + definição (`pg_get_viewdef`) — prováveis consultas do Grafana.
7. Funções/procedures (só nome, argumentos e retorno — sem corpo, salvo se curto).
8. **Privilégios do `svc_finops`:** em cada tabela/view (`has_table_privilege` SELECT/INSERT/UPDATE/DELETE) e se é dono de objetos/superuser.
9. Atividade (se o usuário tiver acesso): `pg_stat_user_tables` (`last_autoanalyze`, `n_live_tup`) para ver quais tabelas estão sendo alimentadas hoje.
10. Sua leitura: quais tabelas parecem o modelo **FOCUS** (custos de nuvem), quais são **cadastro** (clientes, contas, `fator_cliente`), quais são **legado/temporárias**.

## Validação

- `dotnet build` 0 erros / 0 avisos; `npm run build` OK (frontend não muda).
- Prova de read-only: o comando de escrita de teste **falhou** com erro de transação somente leitura (cole a mensagem).
- `GET /api/health/db` local → `ok` com latência.
- `git grep -n -i "password=" -- . ':!memories'` → vazio.

## Entrega

Commit + push no `main` (código + `tools/inventory/` + MD do inventário) e resposta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_FASE2A.md`. Avise no MD que, após o redeploy no Coolify, o HLGDR deve testar `https://newfinops.service.com.br/api/health/db` logado.

— Claude (chat)
