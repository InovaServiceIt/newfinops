using Finops.Backend.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Finops.Backend.Tests;

public class LogSeguroTests
{
    private const string Segredo = "SEGREDO123";

    /// <summary>Connection strings inválidas com trechos da senha onde o parser os ecoa na exceção.</summary>
    public static TheoryData<string> ConnectionStringsInvalidas => new()
    {
        $"Host=localhost;Password={Segredo};xx",
        // Senha com ';' e '=' (ex.: "ab;SEGREDO123=x"): o trecho vira nome de chave e o parser o ecoa.
        $"Host=localhost;Database=cloudvista;Username=svc_cv_api;Password=ab;{Segredo}=x;SSL Mode=Prefer",
        $"Host=localhost;Password=ab;{Segredo};password=x",
    };

    [Fact]
    public void Pre_condicao_o_parser_do_npgsql_ecoa_o_segredo_na_excecao()
    {
        // Garante que o teste abaixo é significativo: a exceção original de fato carrega o segredo.
        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            new NpgsqlConnectionStringBuilder($"Host=localhost;Password=ab;{Segredo}=x"));
        Assert.Contains(Segredo, ex.Message + ex.ParamName, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(ConnectionStringsInvalidas))]
    public void CloudVistaDb_com_connection_string_invalida_nao_loga_a_senha(string connectionString)
    {
        var logger = new LoggerDeTeste<CloudVistaDb>();
        var db = new CloudVistaDb(Configuracao("CloudVista", connectionString), logger);

        Assert.False(db.IsConfigured);
        Assert.NotEmpty(logger.Entradas);
        Assert.DoesNotContain(logger.Entradas, e => e.Contains(Segredo, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Entradas, e => e.Contains("inválida (formato)"));
    }

    [Theory]
    [MemberData(nameof(ConnectionStringsInvalidas))]
    public void FinopsDb_com_connection_string_invalida_nao_loga_a_senha(string connectionString)
    {
        var logger = new LoggerDeTeste<FinopsDb>();
        var db = new FinopsDb(Configuracao("FinopsDb", connectionString), logger);

        Assert.False(db.IsConfigured);
        Assert.NotEmpty(logger.Entradas);
        Assert.DoesNotContain(logger.Entradas, e => e.Contains(Segredo, StringComparison.OrdinalIgnoreCase));
    }

    private static IConfiguration Configuracao(string nome, string valor) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"ConnectionStrings:{nome}"] = valor })
            .Build();

    /// <summary>Logger que guarda a mensagem formatada E a exceção completa (mensagem, ParamName, ToString).</summary>
    private sealed class LoggerDeTeste<T> : ILogger<T>
    {
        public List<string> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var texto = formatter(state, exception);
            if (exception is not null)
                texto += " | " + exception + " | " + (exception as ArgumentException)?.ParamName;
            // Também os valores estruturados (o provider do Coolify pode serializá-los).
            if (state is IEnumerable<KeyValuePair<string, object?>> pares)
                texto += " | " + string.Join(";", pares.Select(p => $"{p.Key}={p.Value}"));
            Entradas.Add(texto);
        }
    }
}
