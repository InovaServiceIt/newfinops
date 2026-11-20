using Npgsql;

namespace Finops.Backend.Infrastructure;

/// <summary>
/// Acesso ao banco finopsdatabase, SOMENTE LEITURA.
/// Toda conexão aberta por este data source roda com default_transaction_read_only=on,
/// então qualquer INSERT/UPDATE/DELETE/DDL é recusado pelo próprio Postgres.
/// Se a connection string não estiver configurada, a aplicação sobe normalmente
/// e <see cref="IsConfigured"/> fica false.
/// </summary>
public sealed class FinopsDb : IAsyncDisposable
{
    public const string ConnectionStringName = "FinopsDb";

    private readonly NpgsqlDataSource? _dataSource;

    public FinopsDb(IConfiguration configuration, ILogger<FinopsDb> logger)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_CONNECTION_STRING_HERE"))
        {
            logger.LogWarning(
                "ConnectionStrings:{Name} não configurada. A aplicação segue sem acesso ao banco.",
                ConnectionStringName);
            return;
        }

        try
        {
            _dataSource = BuildReadOnlyDataSource(connectionString);
        }
        catch (Exception ex)
        {
            // Connection string inválida: não derruba a aplicação. Nunca logar a exceção:
            // a de parse traz trechos da connection string (senha).
            LogSeguro.ConnectionStringInvalida(logger, ConnectionStringName, ex, "A aplicação segue sem acesso ao banco.");
        }
    }

    public bool IsConfigured => _dataSource is not null;

    /// <summary>Abre uma conexão somente leitura. Lança se o banco não estiver configurado.</summary>
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_dataSource is null)
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada.");
        return await _dataSource.OpenConnectionAsync(cancellationToken);
    }

    /// <summary>Cria um data source com a sessão forçada para somente leitura.</summary>
    public static NpgsqlDataSource BuildReadOnlyDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        var csb = builder.ConnectionStringBuilder;

        // Parâmetro de sessão enviado no startup da conexão: toda transação nasce READ ONLY.
        const string readOnlyOption = "-c default_transaction_read_only=on";
        csb.Options = string.IsNullOrWhiteSpace(csb.Options) ? readOnlyOption : $"{csb.Options} {readOnlyOption}";

        csb.ApplicationName ??= "CloudVista";
        return builder.Build();
    }

    public ValueTask DisposeAsync() => _dataSource?.DisposeAsync() ?? ValueTask.CompletedTask;
}
