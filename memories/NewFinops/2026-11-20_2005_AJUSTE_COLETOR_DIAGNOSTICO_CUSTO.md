# Ajuste do coletor — diagnóstico de custo e amostra de avisos no `resumo` (FAZER ANTES do MD 1.1)

**De:** Claude (chat) · **Para:** VSClaude · **Data:** 20/11/2026 20:05 (Brasília) · **Prioridade:** ALTA (pequeno; destrava o aceite do 1.1)

## Contexto
Primeira coleta real (job #3, OCI Metrô Rio, `COLETA_COMPLETA`): `COLETADO` em 7 min 34 s, `resumo = { recursos: 1116, regioes: 2, meses_custo: 0, erros_nao_fatais: 20 }`.
**Custo veio vazio** e não sabemos por quê: o resumo só tem a contagem. Hipótese principal: o usuário OCI tem acesso aos Cost Reports, mas não à **Usage API** (`read usage-report`), ou a Usage API respondeu 404/401. O JSON completo está em `coleta_bruta` (gzip), mas ninguém consegue lê-lo fácil.

## O que fazer (só no `coletor/`, fora de `vendor/`)
1. Acrescentar ao `resumo` do job:
   - `custo`: `{ "status": "ok" | "ausente" | "parcial", "motivo": "<texto curto sanitizado>", "blocos": { "monthly_by_service": N, "daily_by_service": N, "monthly_by_resource": N, ... }, "queries_falhas": [ { "name", "ok", "truncated" } ] }` — a partir de `cost.queries`, `cost.notes` e dos `errors` cujo escopo seja custo (`usage_api`, `cost_reports`, `cost`).
   - `avisos_amostra`: até **20** itens `{ "area", "operacao", "status_http", "mensagem" }` dos `errors` do coletor do Maurício, **sanitizados** (≤200 chars cada, sem OCIDs de usuário, sem trechos de request/headers; OCIDs de compartment/recurso podem ficar truncados `ocid1.compartment...abcd`).
   - `meses_custo` passa a contar meses distintos em `monthly_by_service` (confirme que é isso que já faz).
2. A tela **Coletas** já mostra o `resumo`; acrescentar no "Ver detalhes" (expansível) o bloco **Custo** (status + motivo + queries com falha) e a **amostra de avisos** em tabela. Status `ausente` aparece como badge âmbar "Sem custo".
3. Testes `pytest` com fixture de `errors` e `cost.queries` falhos (sanitização incluída).
4. Revise também o mascaramento citado no MD 1.1 (host `...@...` em erro de resolução).

Entrega: commit no `main` + `RESPOSTA_AJUSTE_COLETOR_DIAGNOSTICO_CUSTO.md`. Depois siga com o MD 1.1.
