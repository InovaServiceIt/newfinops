# Fase 0 — Reduzir o NewFinops a um "Hello World" com SSO Entra

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 20:40 (Brasília)
**Em resposta a:** `2026-11-19_2333_RESPOSTA_INVENTARIO.md` (ótimo inventário, obrigado!)
**Responsável humano:** HLGDR

---

## 1. Decisão do HLGDR

Aproveitamos apenas o **esqueleto** da aplicação. Apague **tudo o que não for necessário** e deixe só:

1. **Login com SSO Entra ID** (como está hoje: OIDC + cookie, 401 em `/api`, filtro global `RequireAuthenticatedUser`, ForwardedHeaders + middleware X-Forwarded-Proto para o Coolify).
2. Depois do login, **uma única tela com um form exibindo "Hello World"**.
3. O **`Dockerfile` da raiz** (multi-stage, porta **5000**), ajustado só no que for preciso (nomes do projeto/dll).

## 2. O que MANTER

**Backend**
- `Program.cs` reduzido a: auth Entra, ForwardedHeaders/X-Forwarded-Proto, CORS (sem domínios do Predict — deixar lista vazia/configurável por variável de ambiente), `UseStaticFiles` + `MapFallbackToFile` (SPA servida pelo .NET), controllers.
- `Controllers/AuthController.cs` (`/api/auth/me`) e `Controllers/HealthController.cs` (`/api/health`).
- `appsettings.json` só com `Logging`, `AllowedHosts` e `AzureAd` (placeholders `YOUR_..._HERE`).

**Frontend**
- `main.tsx`, `App.tsx`, `index.css`/`App.css`.
- `pages/Login/*` (marca trocada para **FinOps**).
- `components/auth/AuthGuard.tsx`.
- `components/layout/GlobalHeader.*` + o layout que envolve as páginas (marca **FINOPS**; **manter o tagline atual "Xurupita…"**; manter o logo `public/service-it-logo.png`).
- Nova página `pages/HelloWorld/` — rota `/` protegida pelo `AuthGuard` — com um form/card simples mostrando **"Hello World"** e o nome do usuário logado (de `/api/auth/me`).

**Raiz**
- `Dockerfile`, `.gitignore`, `memories/` (inteira, não mexer), `README.md` (reescrever curto: o que é o NewFinops, como rodar local, variáveis necessárias).

## 3. O que APAGAR

**Backend**
- 🔴 **Todo o bloco AUTO-FIX do `Program.cs`** (linhas ~179–290) e **qualquer SQL executado pelo código** (`ExecuteSqlRaw`, `CommandText`, `CREATE/ALTER/INSERT/UPDATE`). Também as flags `--seed-dev`, `--inspect`, `--fix-tables` etc.
- `Controllers` do Predict (ActionPlans, Approval, AttentionCapacity, Discovery, Opportunities, PatternAnalysis, ServiceCatalog).
- `Models/`, `Repositories/`, `Services/`, `Migrations/`, `Scratch/`, `DTOs/`, `Background/`, `Infrastructure/` (inclusive `AppDbContext`).
- Pacotes NuGet que deixarem de ser usados (EF Core/Npgsql etc.). **A aplicação deve subir sem banco.** O acesso ao Postgres volta na próxima fase, com as tabelas FOCUS.
- `ConnectionStrings`, `Anthropic`, `Ollama`, `ProductionViews` do `appsettings.json`.

**Frontend**
- Páginas Dashboard, Discovery, Radar, Approval, AttentionCapacity, DecisionRules.
- `services/*.ts`, `types/*.ts`, componentes que ficarem sem uso (`common/`, `forms/`, `SideFormDrawer` etc.) e pastas vazias.
- `console.log('Predict v1.0.1...')`.

**Raiz**
- Todos os MDs do Predict (`PREDICT_*`, `predict_*`, `briefing_*`, `orientacao_*`, `registro_oportunidades.md`, `UX.md`).
- `docker/` (os dois Dockerfiles), `docker-compose.yml`, `package-lock.json` da raiz.
- Pastas esqueleto vazias (`database/`, `docs/`, `scripts/`).

## 4. Renomear Predict → Finops

- `Predict.Backend.csproj` → `Finops.Backend.csproj` (RootNamespace, AssemblyName, namespaces), novo `UserSecretsId`.
- `Dockerfile` da raiz: referências a `Predict.Backend.csproj`/`.dll` → `Finops.Backend`. **Manter `EXPOSE 5000` e `ASPNETCORE_URLS=http://+:5000`.**
- `frontend/package.json`: `name: finops-frontend`. `index.html`: `<title>FinOps - Service IT</title>`.
- `vite.config.ts`: proxy de dev `/api` → backend **sem remover o prefixo `/api`** (o backend espera `/api/...`).
- No fim, `grep -ri predict` no repositório (fora de `memories/`) deve voltar **vazio**.

## 5. Regras

- **Nada contra banco.** Nenhuma DDL, nenhuma conexão.
- **Nenhum segredo no Git.** No Coolify, o Entra entra por variáveis: `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret` (o HLGDR configura).
- Porta **5000**.

## 6. Validação (antes do commit)

1. `dotnet build` → 0 erros (diga quantos avisos restaram).
2. `npm install && npm run build` → OK.
3. Se tiver Docker local: `docker build -t newfinops .` → OK.
4. `grep -rniE "ALTER TABLE|CREATE TABLE|ExecuteSql|CommandText" backend` → vazio.
5. `grep -rni predict . --exclude-dir=memories --exclude-dir=node_modules --exclude-dir=.git` → vazio.
6. Árvore final de arquivos (`git ls-files`, sem `memories/`).

## 7. Entrega

- Commit(s) no `main` com a limpeza e push.
- Resposta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_FASE0.md` com: resultado dos 6 itens da validação, árvore final, o que ficou de fora/dúvidas, e a lista de variáveis de ambiente que o HLGDR deve criar no Coolify.

— Claude (chat)
