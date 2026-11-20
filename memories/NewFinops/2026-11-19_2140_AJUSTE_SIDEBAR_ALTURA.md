# Ajuste — Sidebar cabendo na altura, sem barra de rolagem

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:40 (Brasília)
**Responsável humano:** HLGDR
**Contexto:** título compacto (`af8d521`), CloudVista (`7af80dc`) e fix do Vite (`2ff5150`) conferidos e aprovados.

---

## Pedido do HLGDR

A sidebar deve **caber inteira na altura, sem barra de rolagem vertical**, e ainda "respirar" bem (não pode virar uma lista espremida).

## Diagnóstico (Sidebar.css hoje)

Item 38px + gap 2px (×11) ≈ 438px · títulos de seção ~24px (×3) · `gap` entre seções `--space-5` (20px ×2) · padding `--space-4` (16px ×2) · rodapé (Tokens + recolher) ~56px · header 57px → **~700px**. Num notebook 1366×768 o navegador deixa ~650px de viewport → rolagem.

## Meta

- **Sem rolagem com viewport ≥ 640px de altura** (notebook 1366×768 com navegador maximizado). Sidebar inteira (3 seções + 11 itens + rodapé) com **~520–560px** abaixo do header.
- Respiro preservado: item com área clicável confortável, títulos de seção bem separados, ícones alinhados.

## Valores sugeridos (ajuste fino é seu, mantendo a meta)

| Elemento | Hoje | Proposta |
|---|---|---|
| Altura do item | 38px | **32px** (fonte `--font-size-sm` mantida, ícone 16–18px) |
| Gap entre itens | 2px | 2px |
| Título de seção | padding-bottom 8px | **6px**, `--font-size-xs` |
| Gap entre seções | 20px | **12px** (`--space-3`) |
| Padding vertical da nav | 16px | **12px** |
| Rodapé (Tokens + recolher) | ~56px | **~44px** (padding `--space-2`) |

- Crie tokens para isso em `tokens.css` (ex.: `--sidebar-item-height`, `--sidebar-section-gap`) em vez de números soltos no CSS do componente.
- Manter `overflow-y: auto` **só como fallback** para telas muito baixas (< 640px); acima disso não pode aparecer barra.
- Sidebar recolhida segue a mesma altura dos itens.

## Validação

`npm run build`, `typecheck`, `lint:colors`, `check:contrast` → OK.
Screenshots (light, sidebar expandida) com viewport **1366×650** e **1920×960**: `memories/NewFinops/img/sidebar_1366x650.png` e `sidebar_1920x960.png`. Informe a altura total da sidebar (px) e se há `scrollHeight > clientHeight` em cada um.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_AJUSTE_SIDEBAR.md`.

— Claude (chat)
