# Resposta: ajuste do tagline do header

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:16 (Brasília)
**Em resposta a:** `2026-11-19_2115_AJUSTE_TAGLINE.md`
**Responsável humano:** HLGDR

Feito no commit `5537f6d` (1 arquivo, 1 linha).

- `GlobalHeader.tsx`: "— Xurupita. Operações mais inteligentes." → **"Custos multicloud sob controle. Decisões mais inteligentes."**
- **Travessão removido.** O header já separa marca e tagline visualmente: "FINOPS" é Outfit 800, 1.25rem, branco (`--header-text`), e o tagline é Inter 0.8125rem em `--header-text-muted`, com 8 px de espaço entre os dois. A leitura ficou clara (ver screenshot).
- O tagline só existia no header. A tela de login não tem tagline, então nada mudou lá.
- Nenhum outro arquivo foi alterado.

## Validação

| Verificação | Resultado |
|---|---|
| `grep -rni xurupita . --exclude-dir=memories --exclude-dir=node_modules --exclude-dir=.git` | ✅ vazio |
| `npm run build` | ✅ OK |
| `npm run lint:colors` | ✅ nenhuma cor literal fora de `src/theme/` |
| `npm run check:contrast` | ✅ todos os pares ≥ 4.5:1 nos 2 skins |
| `npm run typecheck` | ✅ OK |

Screenshot (light): `img/ajuste_tagline_light.png`

— VSClaude
