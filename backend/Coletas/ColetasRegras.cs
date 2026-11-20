using System.Globalization;
using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Quartz;

namespace Finops.Backend.Coletas;

// ── Contratos ────────────────────────────────────────────────────────────────

public sealed record ColetarEntrada(string? TipoJob, int? CostDays);

public sealed record ColetaItem(
    long Id, string TipoJob, string? ClienteNome, string? ContaNome, string? Provedor, string Origem,
    string? SolicitadoPorNome, string Status, int Tentativas, DateTimeOffset CriadoEm, DateTimeOffset? IniciadoEm,
    DateTimeOffset? FinalizadoEm, double? DuracaoSeg, string? Erro, JsonElement? Resumo, string? ColetorVersao);

public sealed record PaginaColetas(IReadOnlyList<ColetaItem> Itens, long Total, int Pagina, int Tamanho);

public sealed record FiltroColetas(long? ClienteId, long? ContaId, string? Status, string? TipoJob,
    DateTimeOffset De, DateTimeOffset Ate, int Pagina, int Tamanho);

public sealed record AgendaEntrada(string? Nome, string? TipoJob, long? ClienteId, long? CloudContaId, string? Cron, string? Fuso, int? CostDays);

public sealed record AgendaSaida(long Id, string Nome, string TipoJob, long? ClienteId, string? ClienteNome, long? CloudContaId,
    string? ContaNome, string Cron, string Fuso, int? CostDays, bool Ativo, DateTimeOffset? AtualizadoEm, string? AtualizadoPor);

public sealed record PreviaCronEntrada(string? Cron, string? Fuso);
public sealed record PreviaCron(bool Valida, string? Motivo, IReadOnlyList<DateTimeOffset> Proximas);

public sealed record ParametroSaida(string Chave, string? Valor, string Tipo, string? Descricao, DateTimeOffset? AtualizadoEm, string? AtualizadoPor);
public sealed record ParametroEntrada(string? Valor);

/// <summary>Tipos de job e status da fila (mesmos CHECKs de job_execucao).</summary>
public static class TiposJob
{
    public const string ColetaCompleta = "COLETA_COMPLETA";
    public const string ColetaCusto = "COLETA_CUSTO";
    public const string ColetaInventario = "COLETA_INVENTARIO";
    public const string TesteCredencial = "TESTE_CREDENCIAL";

    public static readonly string[] Coletas = [ColetaCompleta, ColetaCusto, ColetaInventario];
    public static readonly string[] Todos = [ColetaCompleta, ColetaCusto, ColetaInventario, TesteCredencial];
    public static readonly string[] Status = ["PENDENTE", "EXECUTANDO", "COLETADO", "IMPORTANDO", "IMPORTADO", "CONCLUIDO", "ERRO", "CANCELADO"];
}

/// <summary>Regras de "Coletar agora" (funções puras).</summary>
public static class ColetarRegras
{
    public const string ChavePerfis = "coleta.manual.perfis_permitidos";
    public const string ChaveIntervalo = "coleta.manual.intervalo_minimo_min";
    public const int IntervaloPadraoMin = 15;
    public const int CostDaysMin = 30;
    public const int CostDaysMax = 92;

    /// <summary>
    /// Perfis permitidos a partir do JSON do parâmetro (array de strings). JSON inválido/ausente → null
    /// (o chamador NEGA e loga aviso: falhar fechado).
    /// </summary>
    public static IReadOnlySet<string>? PerfisPermitidos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;
            var perfis = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String) return null;
                perfis.Add(e.GetString()!.Trim().ToUpperInvariant());
            }
            return perfis;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool PodeColetar(string perfil, IReadOnlySet<string>? permitidos) =>
        permitidos is not null && permitidos.Contains(perfil);

    /// <summary>Minutos (arredondados para cima) até poder coletar de novo; 0 = liberado.</summary>
    public static int MinutosRestantes(DateTimeOffset? ultimaManual, DateTimeOffset agora, int intervaloMin)
    {
        if (ultimaManual is null || intervaloMin <= 0) return 0;
        var liberaEm = ultimaManual.Value.AddMinutes(intervaloMin);
        if (agora >= liberaEm) return 0;
        return (int)Math.Ceiling((liberaEm - agora).TotalMinutes);
    }

    public static int IntervaloMinimo(string? valor) =>
        int.TryParse(valor?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 1440 ? n : IntervaloPadraoMin;

    public static string ValidarTipoColeta(string? tipo)
    {
        var t = tipo?.Trim().ToUpperInvariant();
        if (t is null || !TiposJob.Coletas.Contains(t))
            throw ErroNegocioException.Validacao("Tipo: use COLETA_COMPLETA, COLETA_CUSTO ou COLETA_INVENTARIO.");
        return t;
    }

    public static int? ValidarCostDays(int? dias)
    {
        if (dias is null) return null;
        if (dias is < CostDaysMin or > CostDaysMax)
            throw ErroNegocioException.Validacao($"Dias de custo: entre {CostDaysMin} e {CostDaysMax}.");
        return dias;
    }
}

/// <summary>Validação de cron do Quartz e prévia das próximas execuções (funções puras).</summary>
public static class CronRegras
{
    public const string FusoPadrao = "America/Sao_Paulo";

    public static readonly IReadOnlyList<(string Rotulo, string Cron)> Atalhos =
    [
        ("Diário 03:00", "0 0 3 * * ?"),
        ("A cada 6 horas", "0 0 0/6 * * ?"),
        ("Dias úteis 07:00", "0 0 7 ? * MON-FRI"),
    ];

    public static PreviaCron Previa(string? cron, string? fuso, DateTimeOffset agora, int quantidade = 3)
    {
        if (string.IsNullOrWhiteSpace(cron))
            return new(false, "Informe a expressão cron.", []);
        var expressao = cron.Trim();
        var campos = expressao.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (campos is < 6 or > 7)
            return new(false, "O cron do Quartz tem 6 ou 7 campos (segundos minutos horas dia-do-mês mês dia-da-semana [ano]).", []);
        if (!CronExpression.IsValidExpression(expressao))
            return new(false, "Expressão cron inválida para o Quartz (dia do mês e dia da semana: um deles precisa ser ?).", []);

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(fuso) ? FusoPadrao : fuso.Trim());
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(false, "Fuso horário inexistente.", []);
        }

        var cronExp = new CronExpression(expressao) { TimeZone = tz };
        var proximas = new List<DateTimeOffset>();
        var cursor = agora;
        while (proximas.Count < quantidade && cronExp.GetNextValidTimeAfter(cursor) is { } proxima)
        {
            proximas.Add(TimeZoneInfo.ConvertTime(proxima, tz));
            cursor = proxima;
        }
        return new(true, null, proximas);
    }
}

/// <summary>Validação da agenda (função pura, exceto pela prévia do cron).</summary>
public static class AgendaRegras
{
    public const int NomeMax = 100;

    public static (string Nome, string Tipo, long? ClienteId, long? ContaId, string Cron, string Fuso, int? CostDays) Validar(AgendaEntrada e, DateTimeOffset agora)
    {
        var nome = e.Nome?.Trim();
        if (string.IsNullOrEmpty(nome)) throw ErroNegocioException.Validacao("Nome: informe um nome para a agenda.");
        if (nome.Length > NomeMax) throw ErroNegocioException.Validacao($"Nome: no máximo {NomeMax} caracteres.");

        var tipo = ColetarRegras.ValidarTipoColeta(e.TipoJob);
        if (e.ClienteId is not null && e.CloudContaId is not null)
            throw ErroNegocioException.Validacao("Alvo: escolha todas as contas, um cliente OU uma conta.");

        var fuso = string.IsNullOrWhiteSpace(e.Fuso) ? CronRegras.FusoPadrao : e.Fuso.Trim();
        var previa = CronRegras.Previa(e.Cron, fuso, agora);
        if (!previa.Valida) throw ErroNegocioException.Validacao($"Cron: {previa.Motivo}");

        return (nome, tipo, e.ClienteId, e.CloudContaId, e.Cron!.Trim(), fuso, ColetarRegras.ValidarCostDays(e.CostDays));
    }
}

/// <summary>Validação de valores da tabela parametro pelo tipo (função pura).</summary>
public static class ParametroRegras
{
    public const int ValorMax = 4000;

    public static string Validar(string tipo, string? valor)
    {
        var v = valor?.Trim() ?? "";
        if (v.Length > ValorMax) throw ErroNegocioException.Validacao($"Valor: no máximo {ValorMax} caracteres.");
        switch (tipo)
        {
            case "INTEIRO":
                if (!long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    throw ErroNegocioException.Validacao("Valor: informe um número inteiro.");
                return v;
            case "DECIMAL":
                if (!decimal.TryParse(v, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _))
                    throw ErroNegocioException.Validacao("Valor: informe um número decimal com ponto (ex.: 1.25).");
                return v;
            case "BOOLEANO":
                var b = v.ToLowerInvariant();
                if (b is not ("true" or "false"))
                    throw ErroNegocioException.Validacao("Valor: use true ou false.");
                return b;
            case "JSON":
                try
                {
                    using var _ = JsonDocument.Parse(v);
                }
                catch (JsonException)
                {
                    throw ErroNegocioException.Validacao("Valor: JSON inválido.");
                }
                return v;
            case "TEXTO":
                return valor ?? "";
            default:
                throw ErroNegocioException.Validacao("Tipo do parâmetro desconhecido.");
        }
    }
}

public static class PerfisColeta
{
    /// <summary>Perfis que veem a tela Coletas nesta fase.</summary>
    public static readonly string[] VeemColetas = [Perfis.Administrador, Perfis.AnalistaFinOps];
}
