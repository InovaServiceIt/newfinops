using Finops.Backend.Admin;
using Finops.Backend.Coletas;

namespace Finops.Backend.Tests;

public class ColetasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero); // 15:00 em São Paulo
    private static ErroNegocioException Erro(Action a) => Assert.Throws<ErroNegocioException>(a);

    // ── Perfis permitidos (parâmetro JSON) ──────────────────────────────────

    [Fact]
    public void Perfis_permitidos_do_json()
    {
        var p = ColetarRegras.PerfisPermitidos("[\"ADMINISTRADOR\",\"analista_finops\"]");
        Assert.True(ColetarRegras.PodeColetar("ADMINISTRADOR", p));
        Assert.True(ColetarRegras.PodeColetar("ANALISTA_FINOPS", p));
        Assert.False(ColetarRegras.PodeColetar("CLIENTE", p));
        Assert.True(ColetarRegras.PodeColetar("CLIENTE", ColetarRegras.PerfisPermitidos("[\"CLIENTE\"]")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("{\"perfis\":[\"ADMINISTRADOR\"]}")]
    [InlineData("[\"ADMINISTRADOR\", 1]")]
    public void Json_invalido_nega_a_todos(string? json)
    {
        var p = ColetarRegras.PerfisPermitidos(json);
        Assert.Null(p);
        Assert.False(ColetarRegras.PodeColetar("ADMINISTRADOR", p));
    }

    // ── Intervalo mínimo ────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, 15, 0)]
    [InlineData(-20.0, 15, 0)]     // há 20 min: liberado
    [InlineData(-15.0, 15, 0)]     // exatamente no limite: liberado
    [InlineData(-14.5, 15, 1)]     // faltam 30 s → 1 min
    [InlineData(-1.0, 15, 14)]
    [InlineData(-0.1, 15, 15)]     // acabou de coletar → 15 min
    [InlineData(-1.0, 0, 0)]       // intervalo 0 = sem limite
    public void Minutos_restantes(double? minutosAtras, int intervalo, int esperado)
    {
        DateTimeOffset? ultima = minutosAtras is { } m ? Agora.AddMinutes(m) : null;
        Assert.Equal(esperado, ColetarRegras.MinutosRestantes(ultima, Agora, intervalo));
    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("0", 0)]
    [InlineData(null, 15)]
    [InlineData("abc", 15)]
    [InlineData("-5", 15)]
    public void Intervalo_minimo_com_default(string? valor, int esperado) =>
        Assert.Equal(esperado, ColetarRegras.IntervaloMinimo(valor));

    [Fact]
    public void Tipo_e_dias_de_custo()
    {
        Assert.Equal("COLETA_CUSTO", ColetarRegras.ValidarTipoColeta("coleta_custo"));
        Assert.Equal(400, Erro(() => ColetarRegras.ValidarTipoColeta("TESTE_CREDENCIAL")).Status);
        Assert.Null(ColetarRegras.ValidarCostDays(null));
        Assert.Equal(30, ColetarRegras.ValidarCostDays(30));
        Assert.Equal(92, ColetarRegras.ValidarCostDays(92));
        Assert.Equal(400, Erro(() => ColetarRegras.ValidarCostDays(29)).Status);
        Assert.Equal(400, Erro(() => ColetarRegras.ValidarCostDays(93)).Status);
    }

    // ── Cron do Quartz + próximas 3 execuções ───────────────────────────────

    [Fact]
    public void Diario_03h_em_sao_paulo_proximos_3_dias()
    {
        var p = CronRegras.Previa("0 0 3 * * ?", "America/Sao_Paulo", Agora);
        Assert.True(p.Valida, p.Motivo);
        Assert.Equal(3, p.Proximas.Count);
        Assert.All(p.Proximas, d => Assert.Equal((3, 0, TimeSpan.FromHours(-3)), (d.Hour, d.Minute, d.Offset)));
        Assert.Equal(new DateTime(2026, 11, 21), p.Proximas[0].Date);   // 15:00 de hoje já passou das 03:00
        Assert.Equal(new DateTime(2026, 11, 23), p.Proximas[2].Date);
    }

    [Fact]
    public void A_cada_6_horas_e_dias_uteis()
    {
        var seis = CronRegras.Previa("0 0 0/6 * * ?", "America/Sao_Paulo", Agora);
        Assert.Equal(new[] { 18, 0, 6 }, seis.Proximas.Select(d => d.Hour));

        var uteis = CronRegras.Previa("0 0 7 ? * MON-FRI", "America/Sao_Paulo", Agora); // sexta 20/11
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday }, uteis.Proximas.Select(d => d.DayOfWeek));
    }

    [Fact]
    public void Fuso_padrao_e_utc()
    {
        Assert.Equal(TimeSpan.FromHours(-3), CronRegras.Previa("0 0 3 * * ?", null, Agora).Proximas[0].Offset);
        var utc = CronRegras.Previa("0 0 3 * * ?", "UTC", Agora);
        Assert.Equal((3, TimeSpan.Zero), (utc.Proximas[0].Hour, utc.Proximas[0].Offset));
    }

    [Theory]
    [InlineData("0 3 * * *", "6 ou 7 campos")]
    [InlineData("0 0 3 * * *", "inválida")]
    [InlineData("0 0 25 * * ?", "inválida")]
    [InlineData("", "Informe")]
    public void Cron_invalido(string cron, string trecho)
    {
        var p = CronRegras.Previa(cron, "America/Sao_Paulo", Agora);
        Assert.False(p.Valida);
        Assert.Contains(trecho, p.Motivo);
        Assert.Empty(p.Proximas);
    }

    [Fact]
    public void Fuso_inexistente()
    {
        var p = CronRegras.Previa("0 0 3 * * ?", "America/Atlantida", Agora);
        Assert.False(p.Valida);
        Assert.Equal("Fuso horário inexistente.", p.Motivo);
    }

    [Fact]
    public void Atalhos_sao_validos()
    {
        Assert.Equal(3, CronRegras.Atalhos.Count);
        Assert.All(CronRegras.Atalhos, a => Assert.True(CronRegras.Previa(a.Cron, null, Agora).Valida, a.Rotulo));
    }

    // ── Agenda ──────────────────────────────────────────────────────────────

    [Fact]
    public void Agenda_valida_e_alvo_exclusivo()
    {
        var v = AgendaRegras.Validar(new AgendaEntrada(" Coleta diária OCI Ultragaz ", "coleta_completa", null, 4, "0 0 3 * * ?", null, 60), Agora);
        Assert.Equal(("Coleta diária OCI Ultragaz", "COLETA_COMPLETA", (long?)null, (long?)4, "0 0 3 * * ?", "America/Sao_Paulo", (int?)60), v);

        Assert.StartsWith("Alvo:", Erro(() => AgendaRegras.Validar(new AgendaEntrada("x", "COLETA_CUSTO", 1, 4, "0 0 3 * * ?", null, null), Agora)).Message);
        Assert.StartsWith("Cron:", Erro(() => AgendaRegras.Validar(new AgendaEntrada("x", "COLETA_CUSTO", null, null, "0 3 * * *", null, null), Agora)).Message);
        Assert.StartsWith("Nome:", Erro(() => AgendaRegras.Validar(new AgendaEntrada(" ", "COLETA_CUSTO", null, null, "0 0 3 * * ?", null, null), Agora)).Message);
    }

    // ── Parâmetros por tipo ─────────────────────────────────────────────────

    [Theory]
    [InlineData("INTEIRO", " 15 ", "15")]
    [InlineData("INTEIRO", "-3", "-3")]
    [InlineData("DECIMAL", "1.25", "1.25")]
    [InlineData("BOOLEANO", "TRUE", "true")]
    [InlineData("BOOLEANO", "false", "false")]
    [InlineData("JSON", "[\"ADMINISTRADOR\",\"ANALISTA_FINOPS\"]", "[\"ADMINISTRADOR\",\"ANALISTA_FINOPS\"]")]
    [InlineData("TEXTO", " livre ", " livre ")]
    public void Parametro_valido(string tipo, string valor, string esperado) =>
        Assert.Equal(esperado, ParametroRegras.Validar(tipo, valor));

    [Theory]
    [InlineData("INTEIRO", "1.5")]
    [InlineData("INTEIRO", "abc")]
    [InlineData("DECIMAL", "1,25")]
    [InlineData("BOOLEANO", "sim")]
    [InlineData("JSON", "[\"sem fechar\"")]
    [InlineData("OUTRO", "x")]
    public void Parametro_invalido(string tipo, string valor) =>
        Assert.Equal(400, Erro(() => ParametroRegras.Validar(tipo, valor)).Status);
}
