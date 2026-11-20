# Resposta: sidebar cabendo na altura, sem rolagem

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:41 (Brasília)
**Em resposta a:** `2026-11-19_2140_AJUSTE_SIDEBAR_ALTURA.md`
**Responsável humano:** HLGDR

Feito no commit `ccfd9e6` (`tokens.css` + `Sidebar.css`). Segui os valores sugeridos, todos via tokens novos.

## Tokens novos (`tokens.css`)

| Token | Valor | Antes (no CSS do componente) |
|---|---|---|
| `--sidebar-item-height` | `32px` | `38px` |
| `--sidebar-item-gap` | `2px` | `2px` |
| `--sidebar-section-gap` | `var(--space-3)` = 12px | `--space-5` = 20px |
| `--sidebar-padding-y` | `var(--space-3)` = 12px | `--space-4` = 16px |
| `--sidebar-section-title-gap` | `6px` | `--space-2` = 8px |
| `--sidebar-footer-padding` | `var(--space-2)` = 8px | `--space-3` = 12px |

Mantidos: fonte dos itens (`--font-size-sm`), ícones de 18 px, título de seção em `--font-size-xs`. O `overflow-y: auto` ficou só como fallback. A sidebar recolhida usa os mesmos tokens: altura do item igual e divisória entre seções com `--sidebar-section-gap`. Sem títulos, ela fica mais baixa que a expandida.

## Medições

Feitas no Chrome headless com o build de produção e viewport exato (sidebar expandida, skin light). Os valores vêm do DOM (`getBoundingClientRect`, `scrollHeight` e `clientHeight` do `.sidebar-nav`).

**Altura do conteúdo da sidebar** (abaixo do header): lista **494 px** + rodapé **49 px** = **543 px**, dentro da meta de 520–560 px.

| Viewport | Sidebar (px) | `.sidebar-nav` client / scroll | `scrollHeight > clientHeight`? |
|---|---|---|---|
| **Antes** · 1366×650 | 592 | 535 / 590 | **sim** (rolagem) |
| **Antes** · 1920×960 | 902 | 845 / 845 | não |
| **Depois** · 1366×650 | 592 | 543 / 543 | **não** ✅ |
| **Depois** · 1920×960 | 902 | 853 / 853 | **não** ✅ |
| Depois · 1366×640 | 582 | 533 / 533 | não ✅ |
| Depois · 1366×605 | 547 | 498 / 498 | não |
| Depois · 1366×600 | 542 | 493 / 494 | sim (fallback) |

"Sidebar (px)" é a altura da caixa fixa (viewport menos os 58 px do header), não a do conteúdo. **A rolagem só aparece com viewport ≤ 600 px**, abaixo da meta de 640 px.

## Validação

`npm run build` ✅ · `npm run typecheck` ✅ · `npm run lint:colors` ✅ · `npm run check:contrast` ✅ (todos ≥ 4.5:1 nos 2 skins). Bundle com 9 contextos (sem duplicação).

Screenshots (light, sidebar expandida): `img/sidebar_1366x650.png` e `img/sidebar_1920x960.png`. Foram capturados sem esconder as barras de rolagem, e nenhuma aparece.

— VSClaude
