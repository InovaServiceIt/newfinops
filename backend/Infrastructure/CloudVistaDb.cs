using Finops.Backend.Identidade;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Infrastructure;

/// <summary>Banco CloudVista não configurado (ConnectionStrings:CloudVista ausente ou inválida).</summary>
public sealed class CloudVistaNaoConfiguradoException() : Exception("Banco CloudVista não configurado.");

/// <summary>
/// Acesso ao banco próprio <c>cloudvista</c> (leitura e escrita), usuário <c>svc_cv_api</c>.
/// As tabelas <c>cliente</c> e <c>usuario_cliente</c> têm RLS: toda consulta nelas passa por
/// <see cref="ExecutarComContextoAsync{T}"/>, que define app.perfil/app.usuario_id com
/// <c>set_config(..., true)</c> (escopo da transação). Sem contexto, o RLS retorna zero linhas.
/// </summary>
public sealed class CloudVistaDb : IAsyncDisposable
{
    public const string ConnectionStringName = "CloudVista";

    /// <summary>
    /// SQL do contexto de RLS. is_local = true: o valor morre no fim da transação e nunca vaza
    /// para outra requisição que reaproveite a mesma conexão do pool.
    /// </summary>
    public const string SqlContexto =
        "SELECT set_config('app.perfil', @perfil, true), set_config('app.usuario_id', @usuario_id, true)";

    private readonly NpgsqlDataSource? _dataSource;

    public CloudVistaDb(IConfiguration configuration, ILogger<CloudVistaDb> logger)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_CONNECTION_STRING_HERE"))
        {
            logger.LogWarning("ConnectionStrings:{Name} não configurada. Endpoints do CloudVista responderão 503.", ConnectionStringName);
            return;
        }

        try
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.ConnectionStringBuilder.ApplicationName = "CloudVista-API";
            _dataSource = builder.Build();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ConnectionStrings:{Name} inválida. Endpoints do CloudVista responderão 503.", ConnectionStringName);
        }
    }

    public bool IsConfigured => _dataSource is not null;

    /// <summary>
    /// Executa <paramref name="acao"/> numa transação com o contexto de RLS do usuário.
    /// COMMIT ao final; ROLLBACK (via dispose) em exceção.
    /// </summary>
    public async Task<T> ExecutarComContextoAsync<T>(
        UsuarioAtual usuario,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> acao,
        CancellationToken ct)
    {
        await using var conn = await AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = CriarComandoContexto(usuario))
        {
            cmd.Connection = conn;
            cmd.Transaction = tx;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var resultado = await acao(conn, tx);
        await tx.CommitAsync(ct);
        return resultado;
    }

    /// <summary>
    /// Executa <paramref name="acao"/> numa transação SEM contexto de RLS. Só para a tabela
    /// <c>usuario</c> (sem RLS), usada pelo serviço de identidade e pelo health check.
    /// </summary>
    public async Task<T> ExecutarSemContextoAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> acao,
        CancellationToken ct)
    {
        await using var conn = await AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var resultado = await acao(conn, tx);
        await tx.CommitAsync(ct);
        return resultado;
    }

    /// <summary>Comando do contexto de RLS (parametrizado; sem conexão, para ser testável).</summary>
    public static NpgsqlCommand CriarComandoContexto(UsuarioAtual usuario)
    {
        var cmd = new NpgsqlCommand(SqlContexto);
        cmd.Parameters.Add(new NpgsqlParameter("perfil", NpgsqlDbType.Text) { Value = usuario.Perfil });
        cmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Text) { Value = usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return cmd;
    }

    private async Task<NpgsqlConnection> AbrirAsync(CancellationToken ct)
    {
        if (_dataSource is null) throw new CloudVistaNaoConfiguradoException();
        return await _dataSource.OpenConnectionAsync(ct);
    }

    public ValueTask DisposeAsync() => _dataSource?.DisposeAsync() ?? ValueTask.CompletedTask;
}
