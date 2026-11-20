# Resposta: renomear o skin "Highlander Dark" → "Service IT Dark"

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:21 (Brasília)
**Em resposta a:** `2026-11-19_2120_RENOMEAR_SKIN_DARK.md`
**Responsável humano:** HLGDR

Feito no commit `a6e8dd5` (6 arquivos). **Os valores dos tokens não mudaram.**

| Antes | Depois |
|---|---|
| id `highlander-dark` | id `serviceit-dark` |
| nome "Highlander Dark" | nome "Service IT Dark" |
| `src/theme/highlander-dark.css` | `src/theme/serviceit-dark.css` (`git mv`, seletor `[data-theme="serviceit-dark"]`) |

Também atualizados: `themes.ts`, `theme/index.css`, a lista branca do script inline do `index.html` e o `README.md`. O `check:contrast` não precisou de mudança, porque lê os skins pelo seletor `[data-theme]` de cada arquivo.

## Migração do `localStorage` (e o grep vazio)

O pedido tinha duas exigências em tensão: migrar `'highlander-dark'` e deixar `grep -rni highlander` vazio. Uma tabela "id antigo → novo" obrigaria o nome antigo a ficar no código. Por isso a migração é feita **pelo modo**, sem citar o id antigo:

- Se o valor salvo é um skin válido, usa ele.
- Se não existe mais e termina em `-dark`, usa o skin escuro (`serviceit-dark`). Se termina em `-light`, usa o claro.
- Qualquer outro valor cai no padrão.
- O id novo é **regravado** no `localStorage`.

A mesma regra está em `themes.ts` (`resolveStoredTheme()`, usada pelo `ThemeProvider`, que regrava via `useEffect`) e no script inline do `index.html` (que regrava na hora, antes do React). Isso também cobre renomeações futuras.

**Teste:** abri o app com `localStorage['finops.theme'] = 'highlander-dark'`. Ele abriu direto no **Service IT Dark**, com `data-theme="serviceit-dark"` no DOM (screenshot abaixo).

## Validação

| Verificação | Resultado |
|---|---|
| `grep -rni highlander . --exclude-dir=memories --exclude-dir=node_modules --exclude-dir=.git` | ✅ vazio |
| `npm run build` | ✅ OK |
| `npm run typecheck` | ✅ OK |
| `npm run lint:colors` | ✅ nenhuma cor literal fora de `src/theme/` |
| `npm run check:contrast` | ✅ todos os pares ≥ 4.5:1. Tabela agora com `serviceit-light` e `serviceit-dark`, com valores idênticos aos da resposta da Fase 1 |

Screenshot (dark, aberto a partir do id antigo): `img/ajuste_skin_dark.png`

## ⚠️ Observação

Um dos builds desta rodada gerou um bundle quebrado: a tela ficou vazia com o erro "useTheme precisa estar dentro de `<ThemeProvider>`". Um build sem minificação do mesmo código funcionou, e um novo `npm run build` também (bundle com outro hash, testado com os dois ids). Não consegui reproduzir nem identificar a causa. O código-fonte não mudou entre os builds e o bundle quebrado não tinha módulos duplicados. Fica o registro: se a tela aparecer vazia depois de um deploy, o primeiro passo é refazer o build limpo.

— VSClaude
