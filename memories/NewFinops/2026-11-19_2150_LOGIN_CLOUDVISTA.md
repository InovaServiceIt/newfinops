# Ajuste — CloudVista na tela de login

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:50 (Brasília)
**Responsável humano:** HLGDR
**Contexto:** sidebar (`ccfd9e6`) aprovada — "ficou excelente a UX". App publicada no Coolify em **https://newfinops.service.com.br** (health OK, tela de login OK).

---

## Pedido do HLGDR

Colocar a marca **CloudVista** na tela de login (`pages/Login/LoginPage.tsx`):

| Onde | Hoje | Depois |
|---|---|---|
| Marca | `FINOPS` | **CloudVista** (grafia exata, sem caixa alta forçada; mesmo `letter-spacing` usado no header) |
| Título | "Bem-vindo ao FinOps!" | **"Bem-vindo ao CloudVista!"** |
| Texto | "...acessar a plataforma FinOps da Service IT." | **"...acessar o CloudVista, a plataforma FinOps da Service IT."** |
| Rodapé | "© 2026 Service IT • FinOps" | **"© 2026 Service IT • CloudVista"** |

- **Ícone da marca:** o login usa `Zap` (raio) e o header usa `Settings` (engrenagem). Use **o mesmo ícone nos dois** — o do header — para a marca ficar consistente. (Não troque o ícone do header.)
- Logo Service IT na faixa escura, botão Microsoft e os selos (Acesso Seguro / Custos de Nuvem / Multicloud): **manter**.

## Fora do escopo (não alterar)

`<title>` do `index.html`, raiz do breadcrumb ("FinOps ›"), nomes técnicos (`Finops.Backend`, pacotes, repo).

## Validação

`npm run build`, `typecheck`, `lint:colors`, `check:contrast` → OK. Screenshots do login nos dois skins: `memories/NewFinops/img/login_cloudvista_light.png` e `login_cloudvista_dark.png`.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_LOGIN_CLOUDVISTA.md`. Lembrete: o push no `main` dispara o redeploy no Coolify (se o auto-deploy estiver ligado).

— Claude (chat)
