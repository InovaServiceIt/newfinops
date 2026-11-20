# NewFinops

Novo FinOps da Service IT: **C# (.NET 8) + React (Vite) + Postgres + Coolify**. Vai substituir os dashboards do Grafana (Metrô Rio e Unimed) e os scripts Python do SRV-FINOPS.

**Fase atual (1):** casca da aplicação (header, sidebar e área central) com login SSO Entra ID e dois skins trocáveis. Ainda sem banco de dados; o acesso ao Postgres (tabelas FOCUS) entra numa próxima fase.

## Estrutura

```
backend/    API .NET 8 (auth Entra, /api/auth/me, /api/health, /api/health/db) e servidor da SPA
frontend/   React + Vite + TypeScript (login, casca, skins/tokens, Visão Geral)
tests/      Testes unitários do backend (xUnit)
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

## Testes

```bash
dotnet test tests/Finops.Backend.Tests   # regras de datas, projeções e montagem da Visão Geral (sem banco)
```

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
| `ConnectionStrings__CloudVista` | sim (segredo) | Banco próprio `cloudvista` (usuário `svc_cv_api`, leitura e escrita com RLS). Sem ela a app sobe, mas as rotas `/api/*` (exceto health) respondem 503 |
| `CV_MASTER_KEY_V1` | sim p/ cofre (segredo) | Chave mestra (KEK) do cofre de credenciais: base64 de 32 bytes. Rotação: `CV_MASTER_KEY_V2`, … (várias podem coexistir) |
| `CV_MASTER_KEY_ATUAL` | sim p/ cofre | Versão da KEK usada para cifrar DEKs novas (ex.: `1`). Ausente/inválida → cofre desativado (gravação de credencial responde 503) |
| `ConnectionStrings__FinopsDb` | não (segredo) | Conexão com o `finopsdatabase`. A app força sessão **somente leitura**. Sem ela, a app sobe e `/api/health/db` responde `nao_configurado` |
| `Cors__AllowedOrigins` | não | Origens extras, separadas por vírgula. Vazio = mesma origem (SPA servida pelo .NET) |

No App Registration, cadastrar o redirect `https://<domínio>/signin-oidc`. Porta do container: **5000**.

## Tema (skins)

- Tokens em `frontend/src/theme/`: `tokens.css` (neutros) + um arquivo por skin (`serviceit-light.css`, padrão, e `serviceit-dark.css`).
- Componentes **só** usam `var(--token)`. Forms usam as classes de `src/styles/ui.css` (`ui-btn`, `ui-input`, `ui-badge-*`...).
- Página de amostras: `/_tokens` (link "Tokens" no rodapé da sidebar).
- Checks (rodar antes de commitar):

```bash
cd frontend
npm run lint:colors     # falha se houver cor literal fora de src/theme/
npm run check:contrast  # contraste WCAG ≥ 4.5:1 dos pares texto/fundo nos dois skins
npm run typecheck
```

## Perfis e acesso

- Três perfis, gravados na tabela `usuario` do banco `cloudvista` (fonte da verdade, não app roles do Entra): `ADMINISTRADOR`, `ANALISTA_FINOPS` e `CLIENTE`.
- Usuário autenticado no Entra mas sem cadastro ativo → `/api/*` responde 403 `{ codigo: "USUARIO_NAO_AUTORIZADO" }` e o front mostra "Acesso não autorizado".
- `cliente` e `usuario_cliente` têm RLS: toda consulta passa por `CloudVistaDb.ExecutarComContextoAsync`, que define `app.perfil` e `app.usuario_id` com `set_config(..., true)` (escopo da transação). Sem contexto, o RLS retorna zero linhas.
- Cadastros de clientes e usuários em **Configurações** (só administrador; o backend exige a política `Administrador`).

## Cofre de credenciais

- Cifragem em envelope AES-256-GCM: KEK (variável `CV_MASTER_KEY_V{n}`) → DEK por cliente (`chave_dados`) → credencial (`credencial.conteudo_cifrado`).
- Formato binário v1: `0x01 | nonce(12) | ct | tag(16)`; AAD `cv:dek:v1:{cliente}:{versao_kek}` e `cv:cred:v1:{conta}:{tipo}`.
- A API só cifra e grava (o `svc_cv_api` não tem SELECT em `conteudo_cifrado`); quem decifra é o coletor.
- Vetores de interoperabilidade: `tests/vetores/cofre_v1.json` (gerados por `tests/vetores/gerar_cofre_v1.py` com o pacote Python `cryptography`).

## Regras

- DDL é executada pelo HLGDR; o código nunca altera o banco (a conexão é forçada para somente leitura).
- Nada de conexão com o banco a partir de máquina de desenvolvimento: a validação de banco é feita no Coolify.
- Nenhum segredo no Git: só placeholders em `appsettings.json`.
- Parâmetros de negócio ficam no banco; no Coolify só configuração estática e segredos.
