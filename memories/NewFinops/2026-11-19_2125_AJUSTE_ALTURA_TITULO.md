# Ajuste — Compactar o cabeçalho da página (breadcrumb + título)

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:25 (Brasília)
**Responsável humano:** HLGDR
**Ordem:** executar **depois** do `2026-11-19_2120_RENOMEAR_SKIN_DARK.md`.

---

## Pedido do HLGDR

Diminuir a altura da faixa "breadcrumb + título" no topo da área central para sobrar mais espaço para o conteúdo. Hoje: padding-top 24px + breadcrumb + gap + título 1.875rem + gap 24px até o conteúdo (~110px).

## Como fazer (`PageHeader.css` / `PageHeader.tsx`)

- **Uma linha só:** breadcrumb e título na mesma linha — `FinOps › **Explorador de custos**`, com o item atual sendo o título (`<h1>`, `--font-size-lg`, peso 700, cor `--text`) e "FinOps ›" em `--font-size-sm`/`--text-muted`, alinhados pela baseline.
- `.page-content`: padding-top `--space-4` (16px), gap entre cabeçalho e conteúdo `--space-4`.
- Meta: faixa do cabeçalho da página com **no máximo ~48px** do topo da área central até o início do conteúdo. Reserve o lado direito da linha para ações futuras da página (ex.: botões Exportar/filtros) — um slot `actions` opcional no `PageHeader`, vazio por enquanto.
- Mantenha o `<h1>` semântico (acessibilidade) e só tokens (nada de cor/tamanho literal fora de `src/theme/`).
- Aplicar também na página `/_tokens`.

## Validação

`npm run build`, `typecheck`, `lint:colors`, `check:contrast` → OK. Screenshots antes/depois (light) em `memories/NewFinops/img/ajuste_titulo_antes.png` e `ajuste_titulo_depois.png`, informando a altura em px do topo da área central até o conteúdo.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_AJUSTE_TITULO.md`.

— Claude (chat)
