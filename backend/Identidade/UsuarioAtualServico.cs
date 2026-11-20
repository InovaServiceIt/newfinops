using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Identidade;

/// <summary>SQL da tabela usuario (sem RLS) usado na resolução de identidade.</summary>
public static class UsuarioSql
{
    private const string Colunas = "id, email, nome, entra_oid, perfil, ativo, ultimo_login_em";

    public const string PorOid = $"SELECT {Colunas} FROM usuario WHERE entra_oid = @oid";
    public const string PorEmail = $"SELECT {Colunas} FROM usuario WHERE lower(email) = @email";
    public const string GravarOid = "UPDATE usuario SET entra_oid = @oid WHERE id = @id AND entra_oid IS NULL";
    public const string AtualizarUltimoLogin = "UPDATE usuario SET ultimo_login_em = now() WHERE id = @id";

    public static UsuarioRegistro Ler(NpgsqlDataReader r) => new(
        r.GetInt64(0),
        r.GetString(1),
        r.IsDBNull(2) ? "" : r.GetString(2),
        r.IsDBNull(3) ? null : r.GetGuid(3),
        r.GetString(4),
        r.GetBoolean(5),
        r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6));
}

/// <summary>
/// Resolve o usuário CloudVista da requisição (scoped: 1x por requisição).
/// Busca por entra_oid; senão por e-mail. Grava o OID no 1º login; nega se o e-mail
/// já estiver associado a outro OID. Atualiza ultimo_login_em no máximo a cada 10 min.
/// </summary>
public sealed class UsuarioAtualServico
{
    private readonly IHttpContextAccessor _http;
    private readonly CloudVistaDb _db;
    private readonly TimeProvider _relogio;
    private readonly ILogger<UsuarioAtualServico> _logger;

    private bool _resolvido;
    private UsuarioAtual? _usuario;

    public UsuarioAtualServico(IHttpContextAccessor http, CloudVistaDb db, TimeProvider relogio, ILogger<UsuarioAtualServico> logger)
    {
        _http = http;
        _db = db;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>Usuário autorizado (existente e ativo) ou null. Lança <see cref="CloudVistaNaoConfiguradoException"/>.</summary>
    public async Task<UsuarioAtual?> ObterAsync(CancellationToken ct)
    {
        var usuario = await ObterRegistroAsync(ct);
        return usuario is { Ativo: true } ? usuario : null;
    }

    /// <summary>Usuário cadastrado (ativo ou não) ou null.</summary>
    public async Task<UsuarioAtual?> ObterRegistroAsync(CancellationToken ct)
    {
        if (_resolvido) return _usuario;

        var principal = _http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            _resolvido = true;
            return null;
        }

        var identidade = IdentidadeEntra.De(principal);
        _usuario = await ResolverAsync(identidade, ct);
        _resolvido = true;
        return _usuario;
    }

    private async Task<UsuarioAtual?> ResolverAsync(IdentidadeEntra identidade, CancellationToken ct)
    {
        if (identidade.Oid is null && identidade.Email is null)
            return null;

        return await _db.ExecutarSemContextoAsync(async (conn, tx) =>
        {
            UsuarioRegistro? porOid = null, porEmail = null;

            if (identidade.Oid is { } oid)
                porOid = await BuscarAsync(conn, tx, UsuarioSql.PorOid, c => c.Parameters.Add(new NpgsqlParameter("oid", NpgsqlDbType.Uuid) { Value = oid }), ct);

            if (porOid is null && identidade.Email is { } email)
                porEmail = await BuscarAsync(conn, tx, UsuarioSql.PorEmail, c => c.Parameters.Add(new NpgsqlParameter("email", NpgsqlDbType.Text) { Value = email }), ct);

            var (decisao, registro) = ResolucaoIdentidade.Decidir(identidade, porOid, porEmail);
            switch (decisao)
            {
                case DecisaoIdentidade.NaoEncontrado:
                    return null;

                case DecisaoIdentidade.Conflito:
                    // Log de segurança sem dados pessoais além do id.
                    _logger.LogWarning("Acesso negado: o e-mail do usuário {UsuarioId} está associado a outro entra_oid.", registro!.Id);
                    return null;

                case DecisaoIdentidade.GravarOid:
                    await using (var cmd = new NpgsqlCommand(UsuarioSql.GravarOid, conn, tx))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("oid", NpgsqlDbType.Uuid) { Value = identidade.Oid!.Value });
                        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = registro!.Id });
                        await cmd.ExecuteNonQueryAsync(ct);
                    }
                    _logger.LogInformation("entra_oid gravado para o usuário {UsuarioId} no primeiro login.", registro.Id);
                    break;
            }

            if (registro!.Ativo && ResolucaoIdentidade.DeveAtualizarUltimoLogin(registro.UltimoLoginEm, _relogio.GetUtcNow()))
            {
                await using var cmd = new NpgsqlCommand(UsuarioSql.AtualizarUltimoLogin, conn, tx);
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = registro.Id });
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return registro.ParaUsuarioAtual();
        }, ct);
    }

    private static async Task<UsuarioRegistro?> BuscarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql,
        Action<NpgsqlCommand> parametros, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        parametros(cmd);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? UsuarioSql.Ler(r) : null;
    }
}
