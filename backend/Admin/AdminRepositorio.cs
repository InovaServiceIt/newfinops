using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Admin;

/// <summary>SQL dos cadastros (todas parametrizadas, sem SELECT *).</summary>
public static class AdminSql
{
    private const string ColunasCliente = "id, nome, apelido, documento, ativo, criado_em, atualizado_em";

    /// <summary>Lista com a contagem de contas de cloud ativas por cliente (coluna "Contas").</summary>
    public const string ListarClientes = $@"
SELECT {ColunasCliente},
       (SELECT count(*) FROM cloud_conta cc WHERE cc.cliente_id = cliente.id AND cc.ativo) AS contas_ativas
FROM cliente
WHERE (@incluir_inativos OR ativo)
ORDER BY nome";
    public const string ClientesDoUsuarioAtual = "SELECT id, nome, apelido FROM cliente WHERE ativo ORDER BY nome";
    public const string CriarCliente = $"INSERT INTO cliente (nome, apelido, documento, ativo) VALUES (@nome, @apelido, @documento, true) RETURNING {ColunasCliente}";
    public const string AtualizarCliente = $"UPDATE cliente SET nome = @nome, apelido = @apelido, documento = @documento WHERE id = @id RETURNING {ColunasCliente}";
    public const string AtivarCliente = $"UPDATE cliente SET ativo = @ativo WHERE id = @id RETURNING {ColunasCliente}";
    public const string ClientesAtivosEntre = "SELECT id FROM cliente WHERE ativo AND id = ANY(@ids)";
    /// <summary>
    /// Usuários CLIENTE ativos para os quais @cliente_id é o único cliente ativo vinculado
    /// (ficariam sem acesso a dados se o cliente fosse desativado).
    /// </summary>
    public const string ImpactoDesativacaoCliente = @"
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
ORDER BY u.nome, u.email";

    public const string ListarUsuarios = "SELECT id, email, nome, perfil, ativo, ultimo_login_em FROM usuario ORDER BY nome, email";
    public const string ListarVinculos = @"
SELECT uc.usuario_id, c.id, c.nome, c.apelido
FROM usuario_cliente uc
JOIN cliente c ON c.id = uc.cliente_id
ORDER BY c.nome";
    public const string VinculosDoUsuario = @"
SELECT uc.usuario_id, c.id, c.nome, c.apelido
FROM usuario_cliente uc
JOIN cliente c ON c.id = uc.cliente_id
WHERE uc.usuario_id = @usuario_id
ORDER BY c.nome";
    public const string UsuarioParaAlterar = "SELECT id, email, nome, perfil, ativo, ultimo_login_em FROM usuario WHERE id = @id FOR UPDATE";
    /// <summary>Trava os administradores ativos: serializa operações que poderiam zerar os administradores.</summary>
    public const string TravarAdministradoresAtivos = "SELECT id FROM usuario WHERE perfil = @perfil_admin AND ativo FOR UPDATE";
    public const string CriarUsuario = "INSERT INTO usuario (email, nome, perfil, ativo) VALUES (@email, @nome, @perfil, true) RETURNING id, email, nome, perfil, ativo, ultimo_login_em";
    public const string AtualizarUsuario = "UPDATE usuario SET nome = @nome, perfil = @perfil WHERE id = @id RETURNING id, email, nome, perfil, ativo, ultimo_login_em";
    public const string AtivarUsuario = "UPDATE usuario SET ativo = @ativo WHERE id = @id RETURNING id, email, nome, perfil, ativo, ultimo_login_em";
    public const string ApagarVinculos = "DELETE FROM usuario_cliente WHERE usuario_id = @usuario_id";
    public const string InserirVinculos = "INSERT INTO usuario_cliente (usuario_id, cliente_id) SELECT @usuario_id, unnest(@cliente_ids)";
}

/// <summary>Cadastros de clientes e usuários. Tudo roda no contexto RLS do administrador.</summary>
public sealed class AdminRepositorio
{
    private readonly CloudVistaDb _db;

    public AdminRepositorio(CloudVistaDb db) => _db = db;

    // ── Clientes ─────────────────────────────────────────────────────────────

    public Task<List<ClienteSaida>> ListarClientesAsync(UsuarioAtual ator, bool incluirInativos, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            await using var cmd = Comando(conn, tx, AdminSql.ListarClientes);
            cmd.Parameters.Add(new NpgsqlParameter("incluir_inativos", NpgsqlDbType.Boolean) { Value = incluirInativos });
            return await LerClientesAsync(cmd, ct);
        }, ct);

    /// <summary>Clientes ativos visíveis ao usuário (RLS: admin = todos; demais = vinculados).</summary>
    public Task<List<ClienteResumo>> ClientesVisiveisAsync(UsuarioAtual usuario, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            var lista = new List<ClienteResumo>();
            await using var cmd = Comando(conn, tx, AdminSql.ClientesDoUsuarioAtual);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lista.Add(new ClienteResumo(r.GetInt64(0), r.GetString(1), r.GetString(2)));
            return lista;
        }, ct);

    public Task<ClienteSaida> CriarClienteAsync(UsuarioAtual ator, ClienteValido c, CancellationToken ct) =>
        GravarClienteAsync(ator, AdminSql.CriarCliente, c.Apelido, cmd => ParametrosCliente(cmd, c), ct);

    public Task<ClienteSaida> AtualizarClienteAsync(UsuarioAtual ator, long id, ClienteValido c, CancellationToken ct) =>
        GravarClienteAsync(ator, AdminSql.AtualizarCliente, c.Apelido, cmd =>
        {
            ParametrosCliente(cmd, c);
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        }, ct);

    public Task<ClienteSaida> AlterarAtivoClienteAsync(UsuarioAtual ator, long id, bool ativo, CancellationToken ct) =>
        GravarClienteAsync(ator, AdminSql.AtivarCliente, null, cmd =>
        {
            cmd.Parameters.Add(new NpgsqlParameter("ativo", NpgsqlDbType.Boolean) { Value = ativo });
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        }, ct);

    private async Task<ClienteSaida> GravarClienteAsync(UsuarioAtual ator, string sql, string? apelido,
        Action<NpgsqlCommand> parametros, CancellationToken ct)
    {
        try
        {
            return await _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
            {
                await using var cmd = Comando(conn, tx, sql);
                parametros(cmd);
                var lista = await LerClientesAsync(cmd, ct);
                return lista.FirstOrDefault() ?? throw ErroNegocioException.NaoEncontrado("Cliente não encontrado.");
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ErroNegocioException.Conflito($"Já existe um cliente com o apelido \"{apelido}\".");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw ErroNegocioException.Validacao("Dados do cliente inválidos.");
        }
    }

    /// <summary>Usuários CLIENTE que ficariam sem nenhum cliente ativo se este cliente fosse desativado.</summary>
    public Task<List<UsuarioAfetado>> ImpactoDesativacaoClienteAsync(UsuarioAtual ator, long clienteId, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            var lista = new List<UsuarioAfetado>();
            await using var cmd = Comando(conn, tx, AdminSql.ImpactoDesativacaoCliente);
            cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
            cmd.Parameters.Add(new NpgsqlParameter("perfil_cliente", NpgsqlDbType.Varchar) { Value = Perfis.Cliente });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lista.Add(new UsuarioAfetado(r.GetInt64(0), r.IsDBNull(1) ? "" : r.GetString(1), r.GetString(2)));
            return lista;
        }, ct);

    // ── Usuários ─────────────────────────────────────────────────────────────

    public Task<List<UsuarioSaida>> ListarUsuariosAsync(UsuarioAtual ator, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            var vinculos = await LerVinculosAsync(Comando(conn, tx, AdminSql.ListarVinculos), ct);
            var lista = new List<UsuarioSaida>();
            await using var cmd = Comando(conn, tx, AdminSql.ListarUsuarios);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetInt64(0);
                lista.Add(LerUsuario(r, vinculos.GetValueOrDefault(id) ?? []));
            }
            return lista;
        }, ct);

    public async Task<UsuarioSaida> CriarUsuarioAsync(UsuarioAtual ator, string email, string nome, string perfil,
        long[]? clienteIds, CancellationToken ct)
    {
        try
        {
            return await _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
            {
                var ativos = await ClientesAtivosAsync(conn, tx, clienteIds, ct);
                var vinculos = UsuarioRegras.ValidarVinculos(perfil, clienteIds, ativos);

                long id;
                await using (var cmd = Comando(conn, tx, AdminSql.CriarUsuario))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Varchar) { Value = email });
                    cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = nome });
                    cmd.Parameters.Add(new NpgsqlParameter("perfil", NpgsqlDbType.Varchar) { Value = perfil });
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    await r.ReadAsync(ct);
                    id = r.GetInt64(0);
                }

                await SubstituirVinculosAsync(conn, tx, id, vinculos, ct);
                return await RecarregarUsuarioAsync(conn, tx, id, ct);
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ErroNegocioException.Conflito($"Já existe um usuário com o e-mail {email}.");
        }
    }

    public Task<UsuarioSaida> AtualizarUsuarioAsync(UsuarioAtual ator, long id, string nome, string perfil,
        long[]? clienteIds, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            var admins = await TravarAdministradoresAsync(conn, tx, ct);
            var alvo = await CarregarParaAlterarAsync(conn, tx, id, ct);

            UsuarioRegras.ValidarProtecoes(ator.Id, id, alvo.Perfil, alvo.Ativo, perfil, alvo.Ativo, admins);
            var ativos = await ClientesAtivosAsync(conn, tx, clienteIds, ct);
            var vinculos = UsuarioRegras.ValidarVinculos(perfil, clienteIds, ativos);

            await using (var cmd = Comando(conn, tx, AdminSql.AtualizarUsuario))
            {
                cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = nome });
                cmd.Parameters.Add(new NpgsqlParameter("perfil", NpgsqlDbType.Varchar) { Value = perfil });
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await SubstituirVinculosAsync(conn, tx, id, vinculos, ct);
            return await RecarregarUsuarioAsync(conn, tx, id, ct);
        }, ct);

    public Task<UsuarioSaida> AlterarAtivoUsuarioAsync(UsuarioAtual ator, long id, bool ativo, CancellationToken ct) =>
        _db.ExecutarComContextoAsync(ator, async (conn, tx) =>
        {
            var admins = await TravarAdministradoresAsync(conn, tx, ct);
            var alvo = await CarregarParaAlterarAsync(conn, tx, id, ct);

            UsuarioRegras.ValidarProtecoes(ator.Id, id, alvo.Perfil, alvo.Ativo, alvo.Perfil, ativo, admins);

            await using (var cmd = Comando(conn, tx, AdminSql.AtivarUsuario))
            {
                cmd.Parameters.Add(new NpgsqlParameter("ativo", NpgsqlDbType.Boolean) { Value = ativo });
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return await RecarregarUsuarioAsync(conn, tx, id, ct);
        }, ct);

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static NpgsqlCommand Comando(NpgsqlConnection conn, NpgsqlTransaction tx, string sql) => new(sql, conn, tx);

    private static void ParametrosCliente(NpgsqlCommand cmd, ClienteValido c)
    {
        cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = c.Nome });
        cmd.Parameters.Add(new NpgsqlParameter("apelido", NpgsqlDbType.Varchar) { Value = c.Apelido });
        cmd.Parameters.Add(new NpgsqlParameter("documento", NpgsqlDbType.Varchar) { Value = (object?)c.Documento ?? DBNull.Value });
    }

    private static async Task<List<ClienteSaida>> LerClientesAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<ClienteSaida>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new ClienteSaida(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetBoolean(4),
                r.GetFieldValue<DateTimeOffset>(5),
                r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6),
                r.FieldCount > 7 && !r.IsDBNull(7) ? (int)r.GetInt64(7) : null));
        }
        return lista;
    }

    private static async Task<Dictionary<long, List<ClienteResumo>>> LerVinculosAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var mapa = new Dictionary<long, List<ClienteResumo>>();
        await using (cmd)
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var usuarioId = r.GetInt64(0);
                if (!mapa.TryGetValue(usuarioId, out var lista))
                    mapa[usuarioId] = lista = [];
                lista.Add(new ClienteResumo(r.GetInt64(1), r.GetString(2), r.GetString(3)));
            }
        }
        return mapa;
    }

    private static UsuarioSaida LerUsuario(NpgsqlDataReader r, IReadOnlyList<ClienteResumo> clientes) => new(
        r.GetInt64(0),
        r.GetString(1),
        r.IsDBNull(2) ? "" : r.GetString(2),
        r.GetString(3),
        r.GetBoolean(4),
        r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5),
        clientes);

    private static async Task<HashSet<long>> ClientesAtivosAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long[]? ids, CancellationToken ct)
    {
        var ativos = new HashSet<long>();
        if (ids is null || ids.Length == 0) return ativos;

        await using var cmd = Comando(conn, tx, AdminSql.ClientesAtivosEntre);
        cmd.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = ids.Distinct().ToArray() });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            ativos.Add(r.GetInt64(0));
        return ativos;
    }

    private static async Task<int> TravarAdministradoresAsync(NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        await using var cmd = Comando(conn, tx, AdminSql.TravarAdministradoresAtivos);
        cmd.Parameters.Add(new NpgsqlParameter("perfil_admin", NpgsqlDbType.Varchar) { Value = Perfis.Administrador });
        var total = 0;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) total++;
        return total;
    }

    private static async Task<UsuarioSaida> CarregarParaAlterarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id, CancellationToken ct)
    {
        await using var cmd = Comando(conn, tx, AdminSql.UsuarioParaAlterar);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw ErroNegocioException.NaoEncontrado("Usuário não encontrado.");
        return LerUsuario(r, []);
    }

    /// <summary>Vínculos por substituição do conjunto (DELETE + INSERT) na mesma transação.</summary>
    private static async Task SubstituirVinculosAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long usuarioId, long[] clienteIds, CancellationToken ct)
    {
        await using (var del = Comando(conn, tx, AdminSql.ApagarVinculos))
        {
            del.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = usuarioId });
            await del.ExecuteNonQueryAsync(ct);
        }

        if (clienteIds.Length == 0) return;

        await using var ins = Comando(conn, tx, AdminSql.InserirVinculos);
        ins.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = usuarioId });
        ins.Parameters.Add(new NpgsqlParameter("cliente_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = clienteIds });
        await ins.ExecuteNonQueryAsync(ct);
    }

    private static async Task<UsuarioSaida> RecarregarUsuarioAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id, CancellationToken ct)
    {
        var vinculosCmd = Comando(conn, tx, AdminSql.VinculosDoUsuario);
        vinculosCmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = id });
        var vinculos = await LerVinculosAsync(vinculosCmd, ct);

        await using var cmd = Comando(conn, tx, "SELECT id, email, nome, perfil, ativo, ultimo_login_em FROM usuario WHERE id = @id");
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw ErroNegocioException.NaoEncontrado("Usuário não encontrado.");
        return LerUsuario(r, vinculos.GetValueOrDefault(id) ?? []);
    }
}
