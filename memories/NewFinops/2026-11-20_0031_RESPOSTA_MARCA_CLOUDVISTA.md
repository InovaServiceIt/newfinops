# Resposta: marca do header FINOPS → CloudVista

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:31 (Brasília)
**Em resposta a:** `2026-11-19_2130_MARCA_CLOUDVISTA.md`
**Responsável humano:** HLGDR

Feito no commit `7af80dc`. Executado depois da renomeação do skin e do ajuste do cabeçalho da página, como pedido.

- `GlobalHeader.tsx`: `FINOPS` → **`CloudVista`**.
- O `.header-brand` **não tinha** `text-transform`, então a grafia exibida é exatamente "CloudVista" (conferido nos screenshots).
- Único ajuste de estilo: `letter-spacing` da marca de `1px` para `0.3px`. O espaçamento anterior foi pensado para "FINOPS" em caixa alta e deixava a palavra em grafia mista visualmente "aberta". Fonte, peso, tamanho e cor não mudaram.
- Tagline ("Custos multicloud sob controle. Decisões mais inteligentes.") e logo Service IT inalterados.

**Fora do escopo, não alterado:** login (`FINOPS`, "Bem-vindo ao FinOps!", rodapé), `<title>` do `index.html`, raiz do breadcrumb ("FinOps ›") e nomes técnicos (`Finops.Backend`, pacotes, repo).

## Validação

`npm run build` ✅ · `npm run typecheck` ✅ · `npm run lint:colors` ✅ · `npm run check:contrast` ✅ (todos ≥ 4.5:1 nos 2 skins). Bundle conferido com 9 contextos, sem a duplicação descrita na resposta do ajuste do título.

Screenshots: `img/marca_cloudvista_light.png` e `img/marca_cloudvista_dark.png`.

— VSClaude
