# Fase 2B — Visão Geral (réplica corrigida dos dashboards Grafana) — OCI primeiro

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 19/11/2026 22:50 (Brasília)
**Responsável humano:** HLGDR
**Ordem:** executar **depois** da Fase 2A (`2026-11-19_2200_FASE2A_CONEXAO_INVENTARIO.md` + `2026-11-19_2205_FASE2A_CORRECAO.md` — a correção prevalece). Pode ser no mesmo ciclo, em commits separados.
**Referência:** JSONs dos 4 dashboards Grafana (Unimed: AWS, Azure, GCP, OCI) em `memories/NewFinops/grafana/`. Os 4 têm **o mesmo layout** (24 painéis); só mudam o provedor e limites.

---

## 0. Regras (relembrando)

- **Somente leitura** no `finopsdatabase` (sessão read-only da 2A). Nenhuma DDL/DML.
- **Você NÃO testa contra o banco** (nem local, nem por script). Código + build + testes unitários. A validação com dados é do HLGDR no Coolify.
- **SQL sempre parametrizado** (nada de concatenar filtro). Só tokens no frontend (`lint:colors`, `check:contrast`).
- Hoje o banco só tem **um provedor: id 3 (OCI)**, contrato 1 (Metrô Rio), ~9 milhões de linhas em `utilizacao_recurso`. AWS/Azure/GCP entram depois **sem mudar a tela**.

## 1. Modelo (schema `svc_finops`, colunas confirmadas pelo HLGDR)

| Tabela | Colunas |
|---|---|
| `resumo_soma_dia_mes_ano_recurso` | `cloudproviderid` int, `id_recurso_resumo` int, `ano` numeric, `mes` numeric, `dia` numeric, `total_custo` numeric |
| `recurso_nuvem` | `id_recurso`, `nome` (= **SKU**), `id_provedor`, `tipo_recurso` (= **Serviço**), `custo_unitario`, `data_bruta` jsonb, `regiao`, `recurso` (OCID), `hash` |
| `provedor_nuvem` | `id_provedor`, `tipo`, `nome`, `id_contrato`, … (**nunca** selecionar `config_de_acesso`) |
| `contrato` | `id_contrato`, `id_cliente`, `data_inicio`, `data_fim`, `valor_total` |
| `cliente` | `id_cliente`, `nome`, `contato`, `email`, `telefone` |
| `fator_cliente` | `id`, `id_provedor`, `fator` |
| `orcamento` | `id_orcamento`, `id_cliente`, `periodo` varchar (ex.: `'2025'`), `valor_previsto`, `valor_realizado` |

Ligações: `recurso_nuvem.id_provedor → provedor_nuvem → contrato → cliente → orcamento (periodo = ano::text)`; `fator_cliente.id_provedor`.

⚠️ **`resumo_soma_dia_mes_ano_recurso` é recriada (`DROP`/`CREATE`) a cada 2 h pelo legado.** Durante a recriação a consulta falha com `42P01` (relation does not exist). Trate: **1 retry após ~3 s**; se falhar de novo, retorne erro amigável ("dados em atualização, tente em instantes"). Ela não tem índice: minimize varreduras (ver §4).

## 2. O que a tela mostra (layout do Grafana, com correções)

Página **Visão Geral** (`/visao-geral`). Cabeçalho da página com **slot de ações**: seletor de **Provedor** (lista de `provedor_nuvem`: `tipo` + nome do cliente; hoje só "OCI · Metrô Rio") e chave **"Com fator"** (desligada = bruto, como o Grafana).

**Filtros** (linha abaixo do cabeçalho): **Ano** (anos existentes no resumo para o provedor), **Mês** (multi, padrão todos), **Serviço** (`tipo_recurso`), **SKU** (`nome`), **Região** (`regiao`) — listas `DISTINCT` de `recurso_nuvem` do provedor, multi-seleção, padrão "todos". Região nula/vazia conta como "(sem região)".

**Bloco 1 — Indicadores (6 cards)**
1. **Orçamento anual** — `orcamento.valor_previsto` do cliente do provedor para `periodo = ano`. Se não houver → card "Sem orçamento cadastrado para <ano>" (estado neutro). *(No Grafana era fixo no painel.)*
2. **Acumulado do ano** — soma do ano selecionado.
3. **Custo do último mês** — mês fechado anterior ao atual.
4. **Custo do mês atual** — mês corrente até hoje.
5. **Projeção do mês** — média dos **3 meses fechados** anteriores (mesma lógica do Grafana).
6. **Projeção anual** — **corrigida**: soma dos meses fechados do ano + projeção do mês atual + (média dos 3 meses fechados × meses restantes após o atual). *(O Grafana usava a média incluindo o mês parcial.)*
Cada card mostra % do orçamento quando houver orçamento.

**Bloco 2 — Custo mensal** (barras) do ano selecionado + barra extra "Projeção mês atual". Cor de cada barra vs **orçamento mensal = anual/12**: ≤ 90% `--success`, ≤ 100% `--warning`, > 100% `--danger`; sem orçamento → `--accent`. Legenda explícita das cores.

**Bloco 3 — Últimos 12 meses** (linha/área) — **dados reais** (o painel do Grafana era `randomWalk`, falso). 12 meses fechados + mês atual.

**Bloco 4 — Semana anterior** (corrigida): **segunda a domingo da semana passada** (datas calculadas no C# e passadas como parâmetro; comparar com `make_date(ano::int, mes::int, dia::int)` — atravessa virada de mês). Total + **Top 5 por Serviço**, **Top 5 por SKU**, **Top 5 por Região** (barras horizontais). *(O Grafana só olhava o mês atual e incluía a semana corrente.)*

**Bloco 5 — Dia anterior** (corrigido): **exatamente ontem** (fuso `America/Sao_Paulo`). Total + os mesmos 3 Top 5. *(O Grafana usava `dia >= ontem`, somando hoje.)*

**Bloco 6 — Custo diário** do mês corrente (barras por dia).

Todos os blocos respeitam os filtros (Serviço/SKU/Região) — inclusive os Top 5 de "dia anterior", que no Grafana ignoravam filtros. Valores em BRL (`pt-BR`), 2 casas; **"Com fator"** multiplica todos os valores por `fator_cliente.fator` do provedor (sem fator cadastrado → chave desabilitada com tooltip).

## 3. API (autenticada)

- `GET /api/visao-geral/opcoes?provedor=3` → provedores, anos, serviços, SKUs, regiões.
- `GET /api/visao-geral?provedor=3&ano=2026&meses=…&servicos=…&skus=…&regioes=…&comFator=false` → **um único payload** com os 6 blocos.
- Datas de referência (hoje, mês atual, semana passada, ontem) calculadas **no C#** em `America/Sao_Paulo` e passadas como parâmetro → **testes unitários** cobrindo: virada de mês, virada de ano, janeiro (3 meses fechados atravessam o ano), segunda-feira, dia 1º.
- Mesmo endpoint, mesmos resultados: **cache em memória de 5 min** por combinação de parâmetros (os dados mudam a cada 2 h).

## 4. Desempenho

- Uma conexão, **uma transação read-only** por requisição; consultas do payload em sequência (ou um CTE base filtrado por provedor/recursos reaproveitado). Sem `SELECT *`.
- Filtrar o resumo por `cloudproviderid` **e** pelos `id_recurso` do provedor (join com `recurso_nuvem` filtrado), como o Grafana.
- Logar o tempo total de cada endpoint (ms) no servidor.

## 5. Frontend

- Biblioteca de gráficos leve (ex.: `recharts`) com **cores lidas dos tokens** (`--chart-*`, `--success/--warning/--danger`, `--chart-grid`) via `getComputedStyle` — e atualizando ao trocar o skin. Proibido cor literal.
- Cards/tabelas com as classes `ui-*`. Estados: carregando (skeleton), vazio ("sem dados para o filtro"), erro (com o texto amigável da API).
- Layout responsivo; nada de rolagem horizontal em 1366 px.
- Rota `/` continua indo para `/visao-geral`.

## 6. Validação (sua, sem banco)

1. `dotnet build` 0/0; `dotnet test` (testes de datas/projeções) verde; `npm run build`, `typecheck`, `lint:colors`, `check:contrast` OK.
2. Screenshot da tela com **dados simulados** (mock no frontend, só para o print — não comitar mock ativo) nos dois skins: `memories/NewFinops/img/fase2b_light.png`, `fase2b_dark.png`.
3. No MD de resposta: **todas as consultas SQL** usadas (texto completo), para eu conferir antes do HLGDR validar no Coolify.

## 7. Entrega

Commits + push no `main`. Resposta em `memories/NewFinops/2026-11-19_HHMM_RESPOSTA_FASE2B.md`. Lembre o HLGDR: após o redeploy, testar `/api/health/db` e abrir `/visao-geral` logado.

— Claude (chat)
