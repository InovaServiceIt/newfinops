using Finops.Backend.VisaoGeral;

namespace Finops.Backend.Tests;

public class DatasReferenciaTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso);

    [Fact]
    public void Dia_comum_quarta_feira()
    {
        var d = new DatasReferencia(D("2026-11-18")); // quarta

        Assert.Equal(new AnoMes(2026, 11), d.MesAtual);
        Assert.Equal(new AnoMes(2026, 10), d.UltimoMesFechado);
        Assert.Equal(new[] { new AnoMes(2026, 8), new AnoMes(2026, 9), new AnoMes(2026, 10) }, d.TresMesesFechados);
        Assert.Equal(D("2026-11-09"), d.SemanaAnteriorInicio); // segunda
        Assert.Equal(D("2026-11-15"), d.SemanaAnteriorFim);    // domingo
        Assert.Equal(D("2026-11-17"), d.Ontem);
    }

    [Fact]
    public void Segunda_feira_ontem_e_o_domingo_da_semana_anterior()
    {
        var d = new DatasReferencia(D("2026-11-16")); // segunda

        Assert.Equal(D("2026-11-09"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2026-11-15"), d.SemanaAnteriorFim);
        Assert.Equal(D("2026-11-15"), d.Ontem);
        Assert.Equal(d.SemanaAnteriorFim, d.Ontem);
    }

    [Fact]
    public void Domingo_semana_anterior_nao_inclui_a_semana_corrente()
    {
        var d = new DatasReferencia(D("2026-11-22")); // domingo

        Assert.Equal(D("2026-11-09"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2026-11-15"), d.SemanaAnteriorFim);
        Assert.Equal(D("2026-11-21"), d.Ontem);
    }

    [Fact]
    public void Dia_primeiro_ontem_e_o_ultimo_dia_do_mes_anterior()
    {
        var d = new DatasReferencia(D("2026-12-01")); // terça

        Assert.Equal(new AnoMes(2026, 12), d.MesAtual);
        Assert.Equal(new AnoMes(2026, 11), d.UltimoMesFechado);
        Assert.Equal(D("2026-11-30"), d.Ontem);
        Assert.Equal(D("2026-11-23"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2026-11-29"), d.SemanaAnteriorFim);
    }

    [Fact]
    public void Semana_anterior_atravessa_virada_de_mes()
    {
        var d = new DatasReferencia(D("2026-10-06")); // terça

        Assert.Equal(D("2026-09-28"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2026-10-04"), d.SemanaAnteriorFim);
    }

    [Fact]
    public void Virada_de_ano_primeiro_de_janeiro()
    {
        var d = new DatasReferencia(D("2027-01-01")); // sexta

        Assert.Equal(new AnoMes(2027, 1), d.MesAtual);
        Assert.Equal(new AnoMes(2026, 12), d.UltimoMesFechado);
        Assert.Equal(D("2026-12-31"), d.Ontem);
        Assert.Equal(new[] { new AnoMes(2026, 10), new AnoMes(2026, 11), new AnoMes(2026, 12) }, d.TresMesesFechados);
        Assert.Equal(D("2026-12-21"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2026-12-27"), d.SemanaAnteriorFim);
    }

    [Fact]
    public void Semana_anterior_atravessa_virada_de_ano()
    {
        var d = new DatasReferencia(D("2027-01-04")); // segunda

        Assert.Equal(D("2026-12-28"), d.SemanaAnteriorInicio);
        Assert.Equal(D("2027-01-03"), d.SemanaAnteriorFim);
        Assert.Equal(D("2027-01-03"), d.Ontem);
    }

    [Fact]
    public void Janeiro_tres_meses_fechados_sao_do_ano_anterior()
    {
        var d = new DatasReferencia(D("2026-01-15"));

        Assert.Equal(new[] { new AnoMes(2025, 10), new AnoMes(2025, 11), new AnoMes(2025, 12) }, d.TresMesesFechados);
        Assert.Equal(new AnoMes(2025, 12), d.UltimoMesFechado);
    }

    [Fact]
    public void Ultimos_doze_meses_mais_o_atual_tem_13_itens_em_ordem()
    {
        var d = new DatasReferencia(D("2026-01-15"));

        Assert.Equal(13, d.UltimosDozeMesesEAtual.Count);
        Assert.Equal(new AnoMes(2025, 1), d.UltimosDozeMesesEAtual[0]);
        Assert.Equal(new AnoMes(2026, 1), d.UltimosDozeMesesEAtual[^1]);
        Assert.True(d.UltimosDozeMesesEAtual.Zip(d.UltimosDozeMesesEAtual.Skip(1)).All(p => p.First < p.Second));
    }

    [Fact]
    public void AnoMes_aritmetica_atravessa_anos()
    {
        Assert.Equal(new AnoMes(2025, 12), new AnoMes(2026, 1).Somar(-1));
        Assert.Equal(new AnoMes(2027, 1), new AnoMes(2026, 12).Somar(1));
        Assert.Equal(new AnoMes(2024, 11), new AnoMes(2026, 1).Somar(-14));
    }

    [Fact]
    public void Hoje_em_Sao_Paulo_usa_o_fuso_e_nao_UTC()
    {
        // 02:30 UTC do dia 19 = 23:30 do dia 18 em São Paulo (UTC-3).
        var relogio = new RelogioFixo(new DateTimeOffset(2026, 11, 19, 2, 30, 0, TimeSpan.Zero));
        Assert.Equal(D("2026-11-18"), DatasReferencia.HojeEmSaoPaulo(relogio));
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
