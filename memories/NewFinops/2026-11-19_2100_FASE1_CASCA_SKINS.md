# Fase 1 — Casca da aplicação (header + sidebar + área central) com 2 skins

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 21:00 (Brasília)
**Responsável humano:** HLGDR
**Pré-requisito:** Fase 0 + ajustes (`a0eb0ca`) — aprovados.

---

## 1. Objetivo

Construir **só a casca** do FinOps — **sem conteúdo de negócio**: header, sidebar e área central vazia — com **dois skins trocáveis em tempo real**, baseados em design tokens. A preocupação central do HLGDR é o **equilíbrio das cores em todos os forms**: a arquitetura de tokens tem que tornar impossível um form "fugir" da paleta.

Skins:
- **`serviceit-light`** (PADRÃO) — fundo claro, cards brancos, destaque vermelho Service IT com azul secundário.
- **`highlander-dark`** — fundo azul-petróleo escuro, superfícies translúcidas (glass), destaque turquesa/teal com roxo secundário.

## 2. Regras inegociáveis

1. **O logo da Service IT aparece SEMPRE, em qualquer skin.** Solução: o **header é escuro nos dois skins** (preto/grafite com linha inferior vermelha Service IT, como no mockup claro). O logo (`public/service-it-logo.png`) fica fixo no header, à direita, sempre sobre fundo escuro. Confira se o PNG é legível sobre fundo escuro; se não for, **avise** (não invente outro logo).
2. **Mesmo layout nos dois skins.** O skin muda **só tokens** (cores, sombras, bordas, raios, efeitos). Nada de estrutura condicional por skin.
3. **Proibido cor literal em componente.** Nenhum `#hex`, `rgb()`, `hsl()` ou nome de cor fora dos arquivos de tema. Componentes só usam `var(--token)`. Crie uma checagem (script `npm run lint:colors` ou teste) que falha se encontrar cor literal fora de `src/theme/`.
4. **Sem banco nesta fase.** Preferência de skin no `localStorage` (chave `finops.theme`) — com `try/catch`. Aplicar o tema **antes** do React montar (script inline no `index.html`) para não piscar.

## 3. Arquitetura de tokens

```
frontend/src/theme/
  tokens.css          # tokens neutros comuns (espaçamento, raios, fontes, z-index, transições)
  serviceit-light.css # [data-theme="serviceit-light"] { --... }
  highlander-dark.css # [data-theme="highlander-dark"] { --... }
  ThemeProvider.tsx   # contexto: theme atual, setTheme, persiste, aplica data-theme no <html>
  themes.ts           # lista de skins disponíveis (id, nome exibido)
```

Tokens **semânticos** (mínimo — mesmo conjunto nos dois skins):

| Grupo | Tokens |
|---|---|
| Fundo/superfície | `--bg`, `--surface`, `--surface-2`, `--surface-hover`, `--overlay` |
| Texto | `--text`, `--text-muted`, `--text-subtle`, `--text-on-accent` |
| Borda | `--border`, `--border-strong`, `--focus-ring` |
| Destaque | `--accent`, `--accent-hover`, `--accent-soft`, `--accent-2` |
| Status | `--success`, `--warning`, `--danger`, `--info` + versões `-soft` (fundo de badge) |
| Header (fixo escuro) | `--header-bg`, `--header-text`, `--header-border` (linha vermelha) |
| Sidebar | `--sidebar-bg`, `--sidebar-item`, `--sidebar-item-active-bg`, `--sidebar-item-active-text`, `--sidebar-section` |
| Gráficos (para fases futuras) | `--chart-aws`, `--chart-azure`, `--chart-gcp`, `--chart-oci`, `--chart-forecast`, `--chart-grid` |
| Efeitos | `--shadow-sm`, `--shadow-md`, `--radius-sm/md/lg`, `--glass-blur` (0 no light) |

**Regra de equilíbrio (60-30-10):** ~60% `--bg`, ~30% `--surface`/sidebar, ~10% `--accent`. `--danger`/`--success` **só** para status — nunca decorativo. As cores de status têm o **mesmo significado** nos dois skins (só o tom muda).

## 4. Contraste (obrigatório)

Script `npm run check:contrast` que lê os dois arquivos de tema e calcula o contraste WCAG dos pares:
`--text`/`--bg`, `--text`/`--surface`, `--text-muted`/`--bg`, `--text-muted`/`--surface`, `--text-on-accent`/`--accent`, `--header-text`/`--header-bg`, `--sidebar-item`/`--sidebar-bg`, `--sidebar-item-active-text`/`--sidebar-item-active-bg`, e cada status sobre seu `-soft`.
Mínimo **4.5:1** (texto normal). Para cores com alpha (glass), calcule sobre a cor de fundo resultante. Saída em tabela; falha se algum par ficar abaixo.

## 5. Casca (sem conteúdo)

**Header (fixo, escuro nos dois skins)**
- Esquerda: ícone + **FINOPS** + tagline atual (manter "Xurupita…" do `GlobalHeader`).
- Direita: seletor de skin (ícone sol/lua ou dropdown com os nomes de `themes.ts`), cartão do usuário (iniciais + nome de `/api/auth/me`), **logo Service IT**.
- Linha inferior vermelha Service IT.

**Sidebar (recolhível)**
- Seções e itens apenas como **placeholders navegáveis**, todos levando à mesma área central vazia com o título do item:
  - ANÁLISE: Visão Geral, Explorador de custos, Alocação & showback, Governança de tags
  - OTIMIZAÇÃO: Recomendações, Anomalias, Orçamentos, Inventário multicloud
  - NEGÓCIO: Faturamento (fator), Copiloto IA, Configurações
- Item ativo destacado com os tokens `--sidebar-item-active-*`. Ícones: use uma lib leve (ex.: `lucide-react`) — sem colorir ícones com literal.
- Botão para recolher (só ícones) — estado no `localStorage`.

**Área central**
- Breadcrumb `FinOps / <item>` + título da página + área vazia (placeholder discreto "Em construção").
- Substitui a página Hello World (pode apagar `pages/HelloWorld`).

**Página de amostras `/_tokens`** (acessível só logado; link discreto no rodapé da sidebar)
- Mostra todos os tokens como swatches com nome e valor, e exemplos: botão primário/secundário, input, select, badge de cada status, card, tabela simples (3 linhas), alerta. Serve para o HLGDR conferir o equilíbrio nos dois skins.

Login: aplicar os tokens também na `LoginPage` (com o logo Service IT visível).

## 6. Validação

1. `dotnet build` e `npm run build` → 0 erros.
2. `npm run lint:colors` → nenhuma cor literal fora de `src/theme/`.
3. `npm run check:contrast` → tabela dos dois skins, todos ≥ 4.5:1.
4. Trocar skin em runtime não recarrega a página e não pisca ao abrir (tema aplicado antes do React).
5. Logo Service IT visível no header nos dois skins e na tela de login.

## 7. Entrega

Commit + push no `main`. Resposta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_FASE1.md` com: árvore de `src/theme`, tabela completa dos tokens dos dois skins (nome → valor light → valor dark), tabela de contraste, resultado das validações e dúvidas. Se possível, anexe **screenshots** (header+sidebar+área central e `/_tokens`) dos dois skins em `memories/NewFinops/img/`.

— Claude (chat)
