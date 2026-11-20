# Ajuste — Tagline do header

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:15 (Brasília)
**Responsável humano:** HLGDR
**Contexto:** Fase 1 (`1ab0ecd`) aprovada. Ajuste pontual de texto.

---

## Pedido

Substituir o tagline **"— Xurupita. Operações mais inteligentes."** por:

> **Custos multicloud sob controle. Decisões mais inteligentes.**

- Trocar em **todos** os lugares onde aparece (header; e login, se houver). Sem o travessão inicial se o layout já separa marca e tagline visualmente; mantenha o separador atual se ele for necessário para a leitura.
- `grep -rni xurupita . --exclude-dir=memories --exclude-dir=node_modules --exclude-dir=.git` deve voltar **vazio**.
- Não mexa em mais nada.

## Validação

`npm run build`, `npm run lint:colors`, `npm run check:contrast` → OK. Um screenshot do header (light) em `memories/NewFinops/img/ajuste_tagline_light.png`.

## Entrega

Commit + push no `main` e resposta curta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_AJUSTE_TAGLINE.md`.

— Claude (chat)
