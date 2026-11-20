# Resposta — ajuste do coletor (diagnóstico de custo + amostra de avisos)

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 23:21 (Brasília)

**Commit:** `0ac5611` no `main`. O ponto 4 (mascaramento de `...@host`) já tinha ido em `e99036c`. Nada em `vendor/`, nenhuma DDL e nenhum acesso ao banco.

## 1. `resumo` do job (`coletor/cloudvista_coletor/diagnostico.py`)
- **`custo`** (só em `COLETA_CUSTO`/`COLETA_COMPLETA`): `{ status, motivo, blocos, queries_falhas }`.
  - **`blocos`:** linhas de `monthly/daily_by_service`, `monthly/daily_by_compartment`, `monthly_by_resource`, `monthly_by_tag`, `forecast` (daily) e `credits` (monthly).
  - **`queries_falhas`:** `{name, ok, truncated, erro?}` de `cost.queries`, considerando a **última ocorrência** de cada nome (o vendor re-tenta com profundidade menor). `erro` sai sanitizado, com ≤200 caracteres.
  - **`status`:**

    | Status | Condição |
    |---|---|
    | `ausente` | nenhuma linha de custo |
    | `parcial` | há linhas, mas alguma consulta falhou ou veio truncada |
    | `ok` | demais casos |

  - **`motivo`** (status `ausente`), na ordem:
    1. o erro da primeira consulta que falhou, como `monthly_by_service: Authorization failed or requested resource not found.`;
    2. senão, o primeiro `error` de escopo `cost*`/`usage_api*`/`cost_reports*`, como `usage_api (HTTP 404): ...`;
    3. senão, "Bloco de custo não retornado pelo coletor." ou "A Usage API respondeu sem linhas no período.".
- **`avisos_amostra`:** até 20 itens `{area, operacao, status_http, mensagem}` vindos dos `errors` do ErrorSink do vendor.
  - **mensagem:** do `str(ServiceError)` sai **só o campo `message`**, sem `request_endpoint`, headers, `opc-request-id` ou dicas. URLs viram apenas o host.
  - **OCIDs:** OCID de usuário (e de apikey, credential etc.) vira `ocid1.[omitido]`. Os demais ficam truncados, como `ocid1.compartment...wxyz`. Depois disso a mensagem passa pelo `sanitizar` (gatilhos, PEM, sequências longas) e é cortada em 200.
  - **operacao:** o `operation_name` do ServiceError ou, na falta dele, o `error_type`; o código textual vai entre parênteses.
  - **status_http:** o `error_code` numérico ou o `status` do ServiceError.
- **`meses_custo`:** antes contava meses distintos em **todos** os `monthly_by_*`. Agora conta **só `monthly_by_service`**, como você pediu.

## 2. Ajuste no sanitizador (atenção)
O gatilho `authorization` cortava a mensagem típica do 404 da OCI, **"Authorization failed or requested resource not found"**, justamente a que queremos ver. Agora ele só dispara como atribuição ou header (`authorization:`, `authorization=`, `'authorization':`).

O valor do header continua protegido: ele traz `Signature ...`, e `signature` segue como gatilho. Há teste para os dois casos.

## 3. Tela Coletas
- O botão "Ver erro" virou **"Ver detalhes"** e aparece quando há erro, diagnóstico de custo, amostra de avisos ou avisos da importação (`resumo.importacao.avisos`, do MD 1.1). O painel mostra:
  - o bloco **Custo**, com badge de status, motivo, contagem por bloco e a tabela de consultas com falha;
  - os **avisos da importação**;
  - a **amostra de avisos** em tabela (Área, Operação, HTTP, Mensagem), indicando "amostra de 20 de N" quando há mais avisos.
- **Badges** ao lado do resumo: **"Sem custo"** (âmbar) para `ausente`, **"Custo parcial"** (âmbar) para `parcial` e nada quando `ok`. O tooltip do badge mostra o motivo.
- `typecheck`, `build`, `lint:colors` e `check:contrast` verdes. Não tirei screenshot do painel expandido, porque o headless não clica.

## 4. Testes
- `pytest`: **83 aprovados** (eram 72). O novo `test_diagnostico.py` usa uma fixture de `errors` e `cost.queries` com falha, com o formato real do `str(ServiceError)` (404 `NotAuthorizedOrNotFound`). Ele cobre:
  - `ausente` com motivo;
  - sem bloco `cost`;
  - `parcial` com última ocorrência e truncamento;
  - `ok`;
  - amostra sem endpoint/headers e com limites de 20 itens e 200 caracteres;
  - OCIDs de usuário e de compartment;
  - PEM/`key_content`;
  - URL e `Authorization: Signature`;
  - `meses_custo` só de `monthly_by_service`.

## 5. Para o HLGDR (aceite)
- O job #3 é anterior a esta versão e não tem o diagnóstico.
- Uma nova coleta do Metrô Rio (`COLETA_COMPLETA`) vai mostrar em Coletas → Ver detalhes o **motivo** de o custo ter vindo vazio. Se a hipótese se confirmar, será `HTTP 404 NotAuthorizedOrNotFound` em `monthly_by_service`, o que indica policy `read usage-report` ausente.
- Depois disso o importador (MD 1.1) leva o job a `IMPORTADO`. Com custo, a Visão Geral passa a mostrar dados.
