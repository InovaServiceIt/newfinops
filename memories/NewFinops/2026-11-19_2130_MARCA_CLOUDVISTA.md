# Ajuste — Marca do header: FINOPS → CloudVista

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:30 (Brasília)
**Responsável humano:** HLGDR
**Ordem:** executar **depois** de `2026-11-19_2120_RENOMEAR_SKIN_DARK.md` e `2026-11-19_2125_AJUSTE_ALTURA_TITULO.md`.

---

## Pedido do HLGDR

A marca do produto no **header** passa a ser **CloudVista**.

- `GlobalHeader.tsx`: `FINOPS` → **`CloudVista`** (grafia exata: C e V maiúsculos, junto). Ajustar o estilo da marca se o uppercase forçado via CSS (`text-transform`) existir — a grafia exibida tem que ser **CloudVista**, não "CLOUDVISTA".
- Tagline continua: "Custos multicloud sob controle. Decisões mais inteligentes."
- Logo Service IT continua fixo no header.

## Fora do escopo (NÃO alterar ainda — aguardando decisão do HLGDR)

Login (`FINOPS`, "Bem-vindo ao FinOps!", rodapé), `<title>` do `index.html`, raiz do breadcrumb ("FinOps ›"), nomes técnicos (repo, `Finops.Backend`, pacotes). Não mexa nesses.

## Validação

`npm run build`, `typecheck`, `lint:colors`, `check:contrast` → OK. Screenshot do header nos dois skins: `memories/NewFinops/img/marca_cloudvista_light.png` e `marca_cloudvista_dark.png`.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_MARCA_CLOUDVISTA.md`.

— Claude (chat)
