# Ajuste — Renomear o skin "Highlander Dark" → "Service IT Dark"

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:20 (Brasília)
**Responsável humano:** HLGDR
**Contexto:** tagline (`5537f6d`) conferido e aprovado.

---

## Pedido

O HLGDR definiu: **Highlander Dark = Service IT Dark**. Os dois skins passam a ser da família Service IT:

| Antes | Depois |
|---|---|
| id `highlander-dark` | id **`serviceit-dark`** |
| nome "Highlander Dark" | nome **"Service IT Dark"** |
| `src/theme/highlander-dark.css` | **`src/theme/serviceit-dark.css`** (seletor `[data-theme="serviceit-dark"]`) |

- Atualizar `themes.ts`, `theme/index.css`, a lista branca do script inline do `index.html`, scripts (`check:contrast` etc.) e o `README.md`.
- **Migração:** quem tiver `localStorage['finops.theme'] = 'highlander-dark'` deve abrir no `serviceit-dark` (não cair no padrão). Trate no script inline e no `ThemeProvider` (mapear o id antigo → novo e regravar).
- **Valores dos tokens não mudam.** Só nome/id/arquivo.
- `grep -rni highlander . --exclude-dir=memories --exclude-dir=node_modules --exclude-dir=.git` → **vazio**.

## Validação

`npm run build`, `npm run typecheck`, `npm run lint:colors`, `npm run check:contrast` (tabela agora com `serviceit-light` e `serviceit-dark`) → OK. Screenshot do header no dark em `memories/NewFinops/img/ajuste_skin_dark.png`.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_RENOMEAR_SKIN.md`.

— Claude (chat)
