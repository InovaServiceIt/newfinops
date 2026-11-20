namespace Finops.Backend.VisaoGeral;

/// <summary>Mês de calendário (ano + mês), comparável e com aritmética simples.</summary>
public readonly record struct AnoMes(int Ano, int Mes) : IComparable<AnoMes>
{
    public static AnoMes De(DateOnly data) => new(data.Year, data.Month);

    public AnoMes Somar(int meses)
    {
        var indice = Ano * 12 + (Mes - 1) + meses;
        return new AnoMes(indice / 12, indice % 12 + 1);
    }

    public DateOnly PrimeiroDia => new(Ano, Mes, 1);

    public int CompareTo(AnoMes outro) => (Ano * 12 + Mes).CompareTo(outro.Ano * 12 + outro.Mes);

    public static bool operator <(AnoMes a, AnoMes b) => a.CompareTo(b) < 0;
    public static bool operator >(AnoMes a, AnoMes b) => a.CompareTo(b) > 0;
    public static bool operator <=(AnoMes a, AnoMes b) => a.CompareTo(b) <= 0;
    public static bool operator >=(AnoMes a, AnoMes b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Ano:D4}-{Mes:D2}";
}

/// <summary>
/// Datas de referência da Visão Geral, calculadas a partir de "hoje" no fuso America/Sao_Paulo.
/// Função pura: tudo deriva de <see cref="Hoje"/>, para ser testável.
/// </summary>
public sealed class DatasReferencia
{
    public DateOnly Hoje { get; }
    public AnoMes MesAtual { get; }
    /// <summary>Último mês fechado (o anterior ao atual).</summary>
    public AnoMes UltimoMesFechado { get; }
    /// <summary>Os 3 meses fechados anteriores ao atual, do mais antigo para o mais recente.</summary>
    public IReadOnlyList<AnoMes> TresMesesFechados { get; }
    /// <summary>12 meses fechados + o mês atual (13 itens), do mais antigo para o mais recente.</summary>
    public IReadOnlyList<AnoMes> UltimosDozeMesesEAtual { get; }
    /// <summary>Segunda-feira da semana passada.</summary>
    public DateOnly SemanaAnteriorInicio { get; }
    /// <summary>Domingo da semana passada.</summary>
    public DateOnly SemanaAnteriorFim { get; }
    public DateOnly Ontem { get; }

    public DatasReferencia(DateOnly hoje)
    {
        Hoje = hoje;
        MesAtual = AnoMes.De(hoje);
        UltimoMesFechado = MesAtual.Somar(-1);
        TresMesesFechados = new[] { MesAtual.Somar(-3), MesAtual.Somar(-2), MesAtual.Somar(-1) };
        UltimosDozeMesesEAtual = Enumerable.Range(0, 13).Select(i => MesAtual.Somar(i - 12)).ToArray();

        // Semana ISO: segunda = 0 ... domingo = 6.
        var diasDesdeSegunda = ((int)hoje.DayOfWeek + 6) % 7;
        var segundaDestaSemana = hoje.AddDays(-diasDesdeSegunda);
        SemanaAnteriorInicio = segundaDestaSemana.AddDays(-7);
        SemanaAnteriorFim = segundaDestaSemana.AddDays(-1);

        Ontem = hoje.AddDays(-1);
    }

    /// <summary>"Hoje" no fuso de São Paulo a partir de um <see cref="TimeProvider"/>.</summary>
    public static DateOnly HojeEmSaoPaulo(TimeProvider relogio)
    {
        var agora = TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), FusoSaoPaulo);
        return DateOnly.FromDateTime(agora.DateTime);
    }

    public static readonly TimeZoneInfo FusoSaoPaulo = ResolverFuso();

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Sem base de fusos no sistema: Brasília sem horário de verão (UTC-3).
        return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
    }
}
