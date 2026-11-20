# Resposta: compactar o cabeçalho da página

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 00:30 (Brasília)
**Em resposta a:** `2026-11-19_2125_AJUSTE_ALTURA_TITULO.md`
**Responsável humano:** HLGDR

Feito no commit `af8d521` (`PageHeader.tsx` + `PageHeader.css`). Executado depois da renomeação do skin, como pedido.

## O que mudou

- **Uma linha só:** `FinOps ›` (`--font-size-sm`, `--text-muted`) + título como `<h1>` (`--font-size-lg`, peso 700, `--text`), alinhados pela baseline. O subtítulo, quando existe (só no `/_tokens`), vai na mesma linha, em `--text-muted`, com reticências se não couber.
- **Slot `actions`** opcional no `PageHeader`, alinhado à direita da mesma linha e vazio por enquanto. Uso futuro: `<PageHeader title="..." actions={<button className="ui-btn ui-btn-secondary">Exportar</button>} />`.
- `.page-content`: padding-top `--space-4` (16 px) e gap `--space-4` entre o cabeçalho e o conteúdo.
- Vale para todas as páginas, inclusive `/_tokens`, porque todas usam o mesmo `PageHeader`. Só tokens, nenhum valor literal.

## Altura (topo da área central → início do conteúdo)

Medida nos screenshots, pela primeira linha de pixels do card de conteúdo:

| | Altura |
|---|---|
| Antes | **112 px** (`img/ajuste_titulo_antes.png`) |
| Depois | **55 px** (`img/ajuste_titulo_depois.png`) |

**Sobre a meta de ~48 px:** com os valores pedidos, 16 px de padding mais 16 px de gap já somam 32 px. Sobram 16 px para uma linha com título de 18 px (`--font-size-lg`), cuja altura de linha é cerca de 23 px. Então 55 px é o mínimo com esses tokens. Para chegar a 48 px, a troca seria padding e gap em `--space-3` (12 px): 12 + 23 + 12 ≈ 47 px. Faço se o HLGDR preferir.

## Validação

`npm run build` ✅ · `npm run typecheck` ✅ · `npm run lint:colors` ✅ · `npm run check:contrast` ✅ (todos ≥ 4.5:1 nos 2 skins).

## 🔧 Causa da tela em branco (citada na resposta da renomeação do skin)

Reproduzi o build quebrado durante este ajuste e achei a causa. Corrigi no commit `2ff5150` (`vite.config.ts`).

- **Sintoma:** em cerca de 1 de cada 4 `npm run build` no Windows, o bundle saía com **dois `ThemeContext`**: o `ThemeProvider` usava um e o `useTheme` usava outro. A tela ficava vazia com "useTheme precisa estar dentro de `<ThemeProvider>`".
- **Causa:** o Node recebia a pasta atual como `c:\MCA\...` (drive minúsculo), mas o caminho real é `C:\MCA\...`. Às vezes o Rollup resolvia o mesmo arquivo com os dois ids (`c:/...` e `C:/...`) e incluía o módulo duas vezes. É um problema conhecido do Vite no Windows.
- **Correção:** o `vite.config.ts` fixa `root` (e o `process.chdir`) no caminho real retornado por `fs.realpathSync.native`.
- **Verificação:** 20 builds seguidos, todos com 9 contextos (o normal), contra 1 falha em 4 antes.
- **Produção não era afetada:** o build do `Dockerfile` roda em Linux, onde não existe essa diferença de maiúscula no drive.

— VSClaude
