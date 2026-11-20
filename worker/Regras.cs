using System.Globalization;
using System.Text.Json;
using Quartz;

namespace CloudVista.Worker;

/// <summary>Chaves e defaults seguros dos parâmetros (tabela parametro).</summary>
public static class Parametros
{
    public const string TimeoutExecucaoMin = "fila.timeout_execucao_min";
    public const string MaxTentativas = "fila.max_tentativas";
    public const string SincronizarAgendaSeg = "agenda.sincronizar_seg";

    public const int TimeoutExecucaoMinPadrao = 60;
    public const int MaxTentativasPadrao = 3;
    public const int SincronizarAgendaSegPadrao = 60;

    /// <summary>Inteiro dentro de [min, max]; ausente, inválido ou fora da faixa → padrão.</summary>
    public static int Inteiro(string? valor, int padrao, int min, int max) =>
        int.TryParse(valor?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
            ? n
            : padrao;
}

/// <summary>Resultado da conversão de uma linha da agenda em agendamento Quartz.</summary>
public sealed record AgendaConvertida(bool Valida, string? Motivo, TimeZoneInfo? Fuso);

public static class ConversaoAgenda
{
    public const string FusoPadrao = "America/Sao_Paulo";

    /// <summary>Valida a expressão cron do Quartz (6–7 campos) e o fuso (IANA ou Windows).</summary>
    public static AgendaConvertida Converter(string? cron, string? fuso)
    {
        if (string.IsNullOrWhiteSpace(cron))
            return new(false, "cron vazio", null);

        var campos = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (campos is < 6 or > 7)
            return new(false, "cron do Quartz precisa de 6 ou 7 campos", null);
        if (!CronExpression.IsValidExpression(cron.Trim()))
            return new(false, "cron inválido", null);

        var idFuso = string.IsNullOrWhiteSpace(fuso) ? FusoPadrao : fuso.Trim();
        try
        {
            return new(true, null, TimeZoneInfo.FindSystemTimeZoneById(idFuso));
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(false, "fuso inexistente", null);
        }
    }

    /// <summary>Assinatura do agendamento: muda quando algo que afeta o trigger muda.</summary>
    public static string Assinatura(AgendaItem a) => string.Join("|", a.Cron.Trim(), a.Fuso?.Trim() ?? FusoPadrao);
}

/// <summary>Linha ativa de job_agenda.</summary>
public sealed record AgendaItem(long Id, string Nome, string TipoJob, long? ClienteId, long? CloudContaId, string Cron, string? Fuso, string ParametrosJson);

/// <summary>Job EXECUTANDO candidato ao zelador.</summary>
public sealed record JobEmExecucao(long Id, int Tentativas, DateTimeOffset? HeartbeatEm, DateTimeOffset? IniciadoEm, DateTimeOffset CriadoEm);

public enum DecisaoZelador { Manter, VoltarParaPendente, MarcarErro }

public static class RegraZelador
{
    public const string MensagemTimeout = "Tempo de execução excedido";

    /// <summary>
    /// Sem sinal (heartbeat, senão início, senão criação) há mais de <paramref name="timeout"/>:
    /// tentativas &lt; máximo → volta para PENDENTE (mantém tentativas); senão → ERRO.
    /// </summary>
    public static DecisaoZelador Decidir(JobEmExecucao job, DateTimeOffset agora, TimeSpan timeout, int maxTentativas)
    {
        var ultimoSinal = job.HeartbeatEm ?? job.IniciadoEm ?? job.CriadoEm;
        if (agora - ultimoSinal <= timeout)
            return DecisaoZelador.Manter;
        return job.Tentativas < maxTentativas ? DecisaoZelador.VoltarParaPendente : DecisaoZelador.MarcarErro;
    }
}

public static class Json
{
    /// <summary>JSON de parâmetros da agenda; vazio/inválido → "{}".</summary>
    public static string ObjetoOuVazio(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? json : "{}";
        }
        catch (JsonException)
        {
            return "{}";
        }
    }
}

/// <summary>
/// Diagnóstico seguro de exceções nos jobs. NpgsqlException/PostgresException de conexão ou consulta
/// não trazem a senha (só a ArgumentException do parse da connection string traz, e essa é tratada no
/// WorkerDb): para elas loga a mensagem, cortada em 300. Para as demais, só o tipo.
/// </summary>
public static class Diagnostico
{
    public const int Limite = 300;

    public static string De(Exception ex)
    {
        if (ex is not Npgsql.NpgsqlException)
            return "-";
        var msg = string.Join(" ", (ex.Message ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (ex.InnerException is { } inner && inner is System.Net.Sockets.SocketException or TimeoutException or System.IO.IOException)
            msg += $" ({inner.GetType().Name}: {inner.Message})";
        return msg.Length <= Limite ? msg : msg[..(Limite - 1)] + "…";
    }
}
