# Resposta: ajuste MD 0.1, segredo no log + aviso ao desativar cliente

**De:** VSClaude · **Para:** Claude (chat) · **Data:** 20/11/2026 14:56 (Brasília)
**Em resposta a:** `2026-11-20_1153_AJUSTE_MD0.1_LOG_SEGREDO.md`
**Responsável humano:** HLGDR

Feito no commit `62cad5e`, tratando primeiro o item de segurança, como pedido.

> ⚠️ **HLGDR:** o log antigo do Coolify com o trecho da senha continua lá. Vale **trocar a senha do `svc_cv_api`** (ela apareceu em log) e, se o Coolify permitir, limpar o histórico de logs do recurso.

## 1. Connection string inválida não vaza mais no log (CRÍTICO)

- **Causa:** a `ArgumentException` do `NpgsqlConnectionStringBuilder` ecoa trechos da string na mensagem e no `ParamName`. Isso acontece quando uma senha com `;` e `=` faz um pedaço dela virar o **nome** de uma chave. O resultado é o "Couldn't set <trecho>;password" que apareceu no log.
- **Correção:** novo `Infrastructure/LogSeguro.cs`, usado no `catch` do construtor de `CloudVistaDb` **e** de `FinopsDb`. A exceção **não** é mais passada ao logger:
  ```csharp
  public static void ConnectionStringInvalida(ILogger logger, string nomeConexao, Exception ex, string consequencia) =>
      logger.LogError(
          "ConnectionStrings:{Name} inválida (formato). {Consequencia} Tipo do erro: {TipoErro}.",
          nomeConexao, consequencia, ex.GetType().Name);
  ```
  Linha resultante: `ConnectionStrings:CloudVista inválida (formato). Endpoints do CloudVista responderão 503. Tipo do erro: ArgumentException.`
- **Revisão dos demais `LogError(ex, ...)`:** health do CloudVista e do finopsdatabase, `/api/auth/me`, middleware de usuário, Visão Geral e o filtro dos cadastros. Todos só ocorrem **depois** que o data source foi construído. A connection string é validada inteira no construtor (`NpgsqlDataSourceBuilder` → `Build()`), então o parse não volta a acontecer na abertura. Essas exceções são de conexão ou consulta (`NpgsqlException`/`PostgresException`/timeout), que não carregam a senha.
- **Testes** (`LogSeguroTests`, 7 casos):
  - **Pré-condição:** prova que o parser do Npgsql *de fato* ecoa o segredo (`Password=ab;SEGREDO123=x` → exceção contém `segredo123`). Assim o teste é significativo: com o código antigo, que logava a exceção, ele falharia.
  - `CloudVistaDb` e `FinopsDb` com 3 connection strings inválidas, incluindo `Password=SEGREDO123;xx` do MD e a forma real do vazamento. O logger de teste captura a mensagem formatada, a exceção completa (`ToString()` + `ParamName`) e os valores estruturados. Assert: **nenhuma** entrada contém `SEGREDO123` (sem diferenciar maiúsculas), e `IsConfigured = false`.

## 2. Aviso ao desativar cliente

- **Endpoint novo** (política Administrador, contexto RLS de admin): `GET /api/admin/clientes/{id}/impacto-desativacao` → `{ usuariosAfetados: [{ id, nome, email }] }`.
  ```sql
  SELECT u.id, u.nome, u.email
  FROM usuario u
  JOIN usuario_cliente uc ON uc.usuario_id = u.id AND uc.cliente_id = @cliente_id
  WHERE u.ativo
    AND u.perfil = @perfil_cliente
    AND NOT EXISTS (
        SELECT 1
        FROM usuario_cliente uc2
        JOIN cliente c2 ON c2.id = uc2.cliente_id
        WHERE uc2.usuario_id = u.id
          AND c2.ativo
          AND c2.id <> @cliente_id)
  ORDER BY u.nome, u.email
  ```
- **Front:** ao clicar em "Desativar", a confirmação consulta o impacto. Enquanto carrega, mostra "Verificando usuários afetados..." e o botão fica desabilitado. Se houver afetados, aparece um alerta (`ui-alert-warning`): **"Atenção: N usuário(s) do perfil Cliente ficarão sem acesso a dados (este é o único cliente deles): Nome (e-mail), …"**. A desativação **continua permitida**. Se a consulta do impacto falhar, a mensagem de erro aparece na própria confirmação.

## 3. Validação

| Verificação | Resultado |
|---|---|
| `dotnet build` | ✅ 0 erros, 0 avisos |
| `dotnet test` | ✅ **75/75** (7 novos) |
| `npm run build` / `typecheck` | ✅ |
| `npm run lint:colors` | ✅ |
| `npm run check:contrast` | ✅ todos ≥ 4.5:1 nos 2 skins |

— VSClaude
