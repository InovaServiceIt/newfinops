# Resposta: ajuste MD 0.5, diagnóstico da falha de conexão do coletor

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 16:57 (Brasília)
**Em resposta a:** `2026-11-20_1355_AJUSTE_MD0.5_LOG_CONEXAO.md`
**Responsável humano:** HLGDR

Feito no commit `390ec49`.

## 1. Mensagem do psycopg, sanitizada

- **Formato do log:** `Não foi possível conectar ao banco (CV_DB_URL): OperationalError: <mensagem>. Nova tentativa em Ns.`
- **Nova `seguranca.sanitizar_conexao(texto, url)`:**
  - remove o **valor literal de `CV_DB_URL`**, trocado por `[CV_DB_URL]`;
  - troca a senha de qualquer `scheme://usuario:senha@`, que vira `scheme://usuario:***@`;
  - troca `password=valor` por `password=***`;
  - normaliza espaços e **corta em 300**.
- **Mudança importante no sanitizador genérico:** ele cortava tudo a partir da palavra `password`, o que **apagaria justamente** `password authentication failed for user "svc_cv_collector"` (e o filtro de log aplica esse sanitizador em toda mensagem). Agora `password`, `passwd` e `senha` só disparam o corte como **atribuição** (`password=…`, `senha: …`). Os outros gatilhos não mudaram (`key_content`, `secret`, `private`, `-----BEGIN`, `token=` etc.).
- **Mensagens preservadas:** os testes garantem que estas aparecem inteiras:
  - `connection to server at "…", port 5432 failed: FATAL: password authentication failed for user "svc_cv_collector"`
  - `… failed: timeout expired`
  - `server does not support SSL, but SSL was required`
  - `invalid connection option "sslmod"`
  - `missing "=" after … in connection info string`

## 2. Backoff 2 → 5 → 15 → 30 → 60 s (teto)

- **Na subida:** o serviço **não sai mais com código 1** quando não consegue conectar. Ele tenta de novo com o backoff, logando o diagnóstico a cada falha. Isso acaba com o loop de reinício a cada ~2 s no Coolify. O SIGTERM interrompe a espera.
- **Durante o loop:** perda de conexão usa o mesmo backoff (antes eram 10 s fixos) e, ao reconectar, loga "Conexão com o banco restabelecida após N falha(s)".
- **Continuam saindo com código 1**, porque são erros de configuração que não se resolvem tentando de novo: `CV_DB_URL` ausente e KEK inválida.

## 3. Testes (pytest)

`coletor/tests/test_conexao.py`, 11 testes novos (**68 passed** no total):
- **Exemplo do MD:** uma exceção com `postgresql://svc_cv_collector:SEGREDO_TESTE_X@host/db` resulta em log **sem** `SEGREDO_TESTE_X` e **com** `***`. Coberto direto na função e no fluxo real de `conectar_com_backoff`, com o filtro de log ativo.
- Valor literal de `CV_DB_URL` removido; `password=` em DSN mascarado; limite de 300.
- As 5 mensagens de diagnóstico acima preservadas.
- O gatilho genérico ainda corta `password=…` e `senha: …`.
- Sequência do backoff `[2, 5, 15, 30, 60, 60, 60, 60]`.
- **Fluxo:** 3 falhas seguidas → esperas de 2, 5 e 15 s, 3 logs `CRITICAL` com `OperationalError` + "password authentication failed", nenhum com o segredo.

Ajustei um caso de teste antigo: `"password authentication failed SEGREDO_TESTE_d"` deixou de ser cortado **de propósito** (é o que o MD pede) e virou `"… password=SEGREDO_TESTE_d"`, que continua sendo cortado.

## 4. Worker C#

Os três jobs (`SincronizarAgendaJob`, `DispararAgendaJob`, `ZeladorJob`) logavam só o tipo e o SQLSTATE. Agora incluem `Detalhe: …` vindo de `Diagnostico.De(ex)`:
- `NpgsqlException`/`PostgresException` → mensagem cortada em 300, mais a causa de rede quando houver (`SocketException`, `TimeoutException`, `IOException`);
- qualquer outra exceção → `-`, só o tipo.

A `ArgumentException` do parse da connection string continua tratada no `WorkerDb`, só com o tipo. Teste novo em `WorkerTests` (**234/234** no .NET).

A API não precisou de mudança: o health e o `/api/auth/me` já logam a exceção do Npgsql pelo `ILogger` (mensagem segura), e o parse da connection string está no `LogSeguro` desde o ajuste do MD 0.1.

— VSClaude
