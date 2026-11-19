# NewFinops

Novo FinOps da Service IT: **C# (.NET 8) + React (Vite) + Postgres + Coolify**. Vai substituir os dashboards do Grafana (Metrô Rio e Unimed) e os scripts Python do SRV-FINOPS.

**Fase atual (0):** esqueleto com login SSO Entra ID e uma tela "Hello World". Ainda sem banco de dados; o acesso ao Postgres (tabelas FOCUS) entra na próxima fase.

## Estrutura

```
backend/    API .NET 8 (auth Entra, /api/auth/me, /api/health) e servidor da SPA
frontend/   React + Vite + TypeScript (login, header, Hello World)
memories/   Comunicação entre o Claude (chat) e o VSClaude
Dockerfile  Build multi-stage: frontend → wwwroot do backend, porta 5000
```

## Rodar local

Pré-requisitos: .NET SDK 8 e Node 20.

```bash
# Backend (porta 5000)
cd backend
dotnet user-secrets set "AzureAd:TenantId" "<tenant>"
dotnet user-secrets set "AzureAd:ClientId" "<client-id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<secret>"
dotnet run --urls http://localhost:5000

# Frontend (porta 3000, proxy de /api para o backend)
cd frontend
npm install
npm run dev
```

Para testar o fluxo completo de SSO, o mais simples é gerar o build do frontend e deixar o .NET servir tudo: `npm run build` e copiar `frontend/dist` para `backend/wwwroot` (é o que o `Dockerfile` faz).

## Docker

```bash
docker build -t newfinops .
docker run -p 5000:5000 -e AzureAd__TenantId=... -e AzureAd__ClientId=... -e AzureAd__ClientSecret=... newfinops
```

## Variáveis de ambiente (Coolify)

| Variável | Obrigatória | Descrição |
|---|---|---|
| `AzureAd__TenantId` | sim | Tenant do Entra ID |
| `AzureAd__ClientId` | sim | App Registration do NewFinops |
| `AzureAd__ClientSecret` | sim | Segredo da App Registration |
| `Cors__AllowedOrigins` | não | Origens extras, separadas por vírgula. Vazio = mesma origem (SPA servida pelo .NET) |

No App Registration, cadastrar o redirect `https://<domínio>/signin-oidc`. Porta do container: **5000**.

## Regras

- DDL é executada pelo HLGDR; o código nunca altera o banco.
- Nenhum segredo no Git: só placeholders em `appsettings.json`.
- Parâmetros de negócio ficam no banco; no Coolify só configuração estática e segredos.
