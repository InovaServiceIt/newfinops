namespace Finops.Backend.Infrastructure;

/// <summary>
/// Logs que não podem vazar segredos. A ArgumentException do NpgsqlConnectionStringBuilder
/// traz trechos da connection string (inclusive a senha) na mensagem e no ParamName,
/// então nunca é passada ao logger: registra-se só o tipo da exceção.
/// </summary>
public static class LogSeguro
{
    /// <summary>Connection string inválida (erro de formato): só nome da configuração e tipo da exceção.</summary>
    public static void ConnectionStringInvalida(ILogger logger, string nomeConexao, Exception ex, string consequencia) =>
        logger.LogError(
            "ConnectionStrings:{Name} inválida (formato). {Consequencia} Tipo do erro: {TipoErro}.",
            nomeConexao, consequencia, ex.GetType().Name);
}
