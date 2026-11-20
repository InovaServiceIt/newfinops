# Resposta: CloudVista na tela de login

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:49 (Brasília)
**Em resposta a:** `2026-11-19_2150_LOGIN_CLOUDVISTA.md`
**Responsável humano:** HLGDR

Feito no commit `86c628f` (`LoginPage.tsx` + `LoginPage.css`). O push no `main` deve disparar o redeploy em https://newfinops.service.com.br, se o auto-deploy estiver ligado.

| Onde | Antes | Depois |
|---|---|---|
| Marca | `FINOPS` | **CloudVista** |
| Título | "Bem-vindo ao FinOps!" | **"Bem-vindo ao CloudVista!"** |
| Texto | "...acessar a plataforma FinOps da Service IT." | **"...acessar o CloudVista, a plataforma FinOps da Service IT."** |
| Rodapé | "© 2026 Service IT • FinOps" | **"© 2026 Service IT • CloudVista"** |
| Ícone da marca | `Zap` (raio) | **`Settings` (engrenagem)**, o mesmo do header (o do header não mudou) |

**Estilo da marca:**
- `letter-spacing` de `2px` para `0.3px`, igual ao header. Sem `text-transform`: a grafia exibida é "CloudVista".
- Removi o `fill: currentColor` do ícone. Ele preenchia o raio, mas transformaria a engrenagem numa forma sólida. O ícone fica só no contorno, em `--accent`.
- No header o ícone fica dentro de um quadrado colorido; no login fica solto, sobre o card. É o mesmo ícone com apresentação diferente. Se o HLGDR quiser o quadrado também no login, é um ajuste pequeno.

**Mantidos:** logo Service IT na faixa escura, botão Microsoft e os selos (Acesso Seguro / Custos de Nuvem / Multicloud).
**Fora do escopo, não alterado:** `<title>` do `index.html`, raiz do breadcrumb ("FinOps ›"), nomes técnicos.

## Validação

`npm run build` ✅ · `npm run typecheck` ✅ · `npm run lint:colors` ✅ · `npm run check:contrast` ✅ (todos ≥ 4.5:1 nos 2 skins). Bundle com 9 contextos.

Screenshots: `img/login_cloudvista_light.png` e `img/login_cloudvista_dark.png`.

— VSClaude
