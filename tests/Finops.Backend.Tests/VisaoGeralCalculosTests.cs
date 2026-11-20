using Finops.Backend.VisaoGeral;

namespace Finops.Backend.Tests;

public class VisaoGeralCalculosTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso);

    private static Dictionary<AnoMes, decimal> Mensais(params (int ano, int mes, decimal valor)[] itens) =>
        itens.ToDictionary(i => new AnoMes(i.ano, i.mes), i => i.valor);

    private static FiltrosVisaoGeral Filtros(int ano, bool comFator = false, params int[] meses) =>
        new(3, ano, meses, [], [], [], comFator);

    // ── Projeção do mês ─────────────────────────────────────────────────────

    [Fact]
    public void Projecao_do_mes_e_a_media_dos_3_meses_fechados()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var mensais = Mensais((2026, 8, 100), (2026, 9, 200), (2026, 10, 300), (2026, 11, 999));

        Assert.Equal(200m, VisaoGeralCalculos.ProjecaoMes(mensais, datas));
    }

    [Fact]
    public void Projecao_do_mes_ignora_meses_sem_dados()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var mensais = Mensais((2026, 9, 200), (2026, 10, 400));

        Assert.Equal(300m, VisaoGeralCalculos.ProjecaoMes(mensais, datas));
    }

    [Fact]
    public void Projecao_do_mes_sem_dados_e_nula()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        Assert.Null(VisaoGeralCalculos.ProjecaoMes(Mensais((2026, 11, 50)), datas));
    }

    [Fact]
    public void Projecao_do_mes_em_janeiro_usa_meses_do_ano_anterior()
    {
        var datas = new DatasReferencia(D("2026-01-10"));
        var mensais = Mensais((2025, 10, 90), (2025, 11, 120), (2025, 12, 150), (2026, 1, 10));

        Assert.Equal(120m, VisaoGeralCalculos.ProjecaoMes(mensais, datas));
    }

    // ── Projeção anual ──────────────────────────────────────────────────────

    [Fact]
    public void Projecao_anual_do_ano_atual_usa_fechados_mais_projecao_mais_restantes()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        // Jan..Out = 100 cada (fechados = 1000); Nov parcial = 30 (não entra); média 3 meses = 100.
        var mensais = Enumerable.Range(1, 10).ToDictionary(m => new AnoMes(2026, m), _ => 100m);
        mensais[new AnoMes(2026, 11)] = 30m;

        // 1000 (fechados) + 100 (projeção nov) + 100 × 1 (dez) = 1200
        Assert.Equal(1200m, VisaoGeralCalculos.ProjecaoAnual(2026, mensais, datas));
    }

    [Fact]
    public void Projecao_anual_em_janeiro_e_doze_vezes_a_media()
    {
        var datas = new DatasReferencia(D("2026-01-10"));
        var mensais = Mensais((2025, 10, 100), (2025, 11, 100), (2025, 12, 100), (2026, 1, 5));

        // 0 fechados no ano + 100 (jan) + 100 × 11 = 1200
        Assert.Equal(1200m, VisaoGeralCalculos.ProjecaoAnual(2026, mensais, datas));
    }

    [Fact]
    public void Projecao_anual_em_dezembro_nao_tem_meses_restantes()
    {
        var datas = new DatasReferencia(D("2026-12-05"));
        var mensais = Enumerable.Range(1, 11).ToDictionary(m => new AnoMes(2026, m), _ => 10m);

        // 110 fechados + 10 (projeção dez) + 0
        Assert.Equal(120m, VisaoGeralCalculos.ProjecaoAnual(2026, mensais, datas));
    }

    [Fact]
    public void Projecao_anual_de_ano_passado_e_o_total_e_de_ano_futuro_e_nula()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var mensais = Mensais((2025, 1, 10), (2025, 12, 20), (2026, 10, 99));

        Assert.Equal(30m, VisaoGeralCalculos.ProjecaoAnual(2025, mensais, datas));
        Assert.Null(VisaoGeralCalculos.ProjecaoAnual(2027, mensais, datas));
    }

    // ── Faixas de orçamento ─────────────────────────────────────────────────

    [Theory]
    [InlineData(90, 100, Faixa.Ok)]
    [InlineData(90.01, 100, Faixa.Atencao)]
    [InlineData(100, 100, Faixa.Atencao)]
    [InlineData(100.01, 100, Faixa.Acima)]
    public void Faixa_de_orcamento(decimal valor, decimal orcamentoMensal, string esperado) =>
        Assert.Equal(esperado, VisaoGeralCalculos.FaixaOrcamento(valor, orcamentoMensal));

    [Fact]
    public void Faixa_sem_orcamento()
    {
        Assert.Equal(Faixa.SemOrcamento, VisaoGeralCalculos.FaixaOrcamento(50, null));
        Assert.Equal(Faixa.SemOrcamento, VisaoGeralCalculos.FaixaOrcamento(50, 0));
    }

    // ── Montagem do payload ─────────────────────────────────────────────────

    private static DadosBrutosVisaoGeral Dados(
        IEnumerable<TotalDia>? diarios = null,
        IEnumerable<TotalDiaDimensoes>? dims = null,
        Dictionary<int, decimal>? orcamento = null,
        decimal? fator = null) =>
        new((diarios ?? []).ToList(), (dims ?? []).ToList(), orcamento ?? new(), fator, "OCI · Metrô Rio");

    [Fact]
    public void Semana_anterior_e_dia_anterior_somam_so_os_dias_certos_e_fazem_top5()
    {
        var datas = new DatasReferencia(D("2026-10-06")); // semana anterior: 28/09..04/10; ontem 05/10
        var dims = new List<TotalDiaDimensoes>
        {
            new(D("2026-09-27"), "Compute", "A", "sa-saopaulo-1", 1000), // domingo antes: fora
            new(D("2026-09-28"), "Compute", "A", "sa-saopaulo-1", 10),
            new(D("2026-10-04"), "Storage", "B", "(sem região)", 5),
            new(D("2026-10-05"), "Network", "C", "sa-vinhedo-1", 7),     // ontem: fora da semana
        };
        // 6 SKUs diferentes no dia 30/09 para testar o corte em 5
        for (var i = 1; i <= 6; i++)
            dims.Add(new(D("2026-09-30"), "Compute", $"SKU{i}", "sa-saopaulo-1", i));

        var r = VisaoGeralCalculos.Montar(Filtros(2026), datas, Dados(dims: dims));

        Assert.Equal(10 + 5 + 21m, r.SemanaAnterior.Total);
        Assert.Equal(5, r.SemanaAnterior.TopSkus.Count);
        Assert.Equal("A", r.SemanaAnterior.TopSkus[0].Nome); // 10
        Assert.DoesNotContain(r.SemanaAnterior.TopSkus, i => i.Nome == "SKU1");
        Assert.Equal(new ItemRanking("Compute", 31), r.SemanaAnterior.TopServicos[0]);

        Assert.Equal(D("2026-10-05"), r.DiaAnterior.Inicio);
        Assert.Equal(7m, r.DiaAnterior.Total);
        Assert.Equal(new ItemRanking("sa-vinhedo-1", 7), Assert.Single(r.DiaAnterior.TopRegioes));
    }

    [Fact]
    public void Indicadores_custo_mensal_e_projecao_com_orcamento()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var diarios = new List<TotalDia>();
        for (var m = 1; m <= 10; m++) diarios.Add(new(new DateOnly(2026, m, 1), 1000));
        diarios.Add(new(D("2026-11-01"), 200));
        diarios.Add(new(D("2026-11-02"), 300));

        // orçamento 12.000/ano → 1.000/mês: meses de 1.000 = 100% (atenção)
        var r = VisaoGeralCalculos.Montar(Filtros(2026), datas, Dados(diarios, orcamento: new() { [2026] = 12000 }));

        Assert.Equal(12000m, r.Indicadores.OrcamentoAnual.Valor);
        Assert.Equal(10500m, r.Indicadores.AcumuladoAno.Valor);
        Assert.Equal(87.5m, r.Indicadores.AcumuladoAno.PercentualOrcamento);
        Assert.Equal(1000m, r.Indicadores.UltimoMes.Valor);
        Assert.Equal(500m, r.Indicadores.MesAtual.Valor);
        Assert.Equal(1000m, r.Indicadores.ProjecaoMes.Valor);
        Assert.Equal(12000m, r.Indicadores.ProjecaoAnual.Valor); // 10.000 + 1.000 + 1.000 × 1
        Assert.Equal(100m, r.Indicadores.ProjecaoAnual.PercentualOrcamento);

        Assert.Equal(12, r.CustoMensal.Count); // 11 meses reais + barra de projeção
        Assert.Equal(Faixa.Atencao, r.CustoMensal[0].Faixa);
        Assert.Equal(Faixa.Ok, r.CustoMensal[10].Faixa); // nov parcial = 500
        Assert.True(r.CustoMensal[^1].Projecao);
        Assert.Equal("Projeção mês atual", r.CustoMensal[^1].Rotulo);

        Assert.Equal(13, r.UltimosDozeMeses.Count);
        Assert.True(r.UltimosDozeMeses[^1].Parcial);
        Assert.Equal(new[] { new PontoDiario(1, 200), new PontoDiario(2, 300) }, r.CustoDiario);
    }

    [Fact]
    public void Ano_passado_nao_tem_barra_de_projecao_e_filtro_de_meses_vale()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var diarios = Enumerable.Range(1, 12).Select(m => new TotalDia(new DateOnly(2025, m, 1), 100)).ToList();

        var r = VisaoGeralCalculos.Montar(Filtros(2025, false, 1, 2, 3), datas, Dados(diarios));

        Assert.Equal(3, r.CustoMensal.Count);
        Assert.DoesNotContain(r.CustoMensal, b => b.Projecao);
        Assert.Equal(300m, r.Indicadores.AcumuladoAno.Valor);
        Assert.All(r.CustoMensal, b => Assert.Equal(Faixa.SemOrcamento, b.Faixa));
        Assert.Null(r.Indicadores.OrcamentoAnual.Valor);
    }

    [Fact]
    public void Com_fator_multiplica_custos_mas_nao_o_orcamento()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var diarios = new List<TotalDia> { new(D("2026-11-01"), 100) };

        var semFator = VisaoGeralCalculos.Montar(Filtros(2026), datas, Dados(diarios, orcamento: new() { [2026] = 1200 }, fator: 1.5m));
        var comFator = VisaoGeralCalculos.Montar(Filtros(2026, comFator: true), datas, Dados(diarios, orcamento: new() { [2026] = 1200 }, fator: 1.5m));

        Assert.Equal(100m, semFator.Indicadores.MesAtual.Valor);
        Assert.Equal(150m, comFator.Indicadores.MesAtual.Valor);
        Assert.Equal(1200m, comFator.Indicadores.OrcamentoAnual.Valor);
        Assert.True(comFator.ComFator);
    }

    [Fact]
    public void Com_fator_sem_fator_cadastrado_fica_bruto()
    {
        var datas = new DatasReferencia(D("2026-11-18"));
        var r = VisaoGeralCalculos.Montar(Filtros(2026, comFator: true), datas,
            Dados(new List<TotalDia> { new(D("2026-11-01"), 100) }));

        Assert.Equal(100m, r.Indicadores.MesAtual.Valor);
        Assert.False(r.ComFator);
    }

    [Fact]
    public void Intervalo_de_meses_cobre_ano_selecionado_e_ultimos_12()
    {
        var datas = new DatasReferencia(D("2026-03-10"));

        Assert.Equal((new AnoMes(2025, 1), new AnoMes(2026, 3)), VisaoGeralCalculos.IntervaloMeses(2025, datas));
        Assert.Equal((new AnoMes(2025, 3), new AnoMes(2026, 12)), VisaoGeralCalculos.IntervaloMeses(2026, datas));
        Assert.Equal((new AnoMes(2023, 1), new AnoMes(2026, 3)), VisaoGeralCalculos.IntervaloMeses(2023, datas));
    }

    [Fact]
    public void Chave_de_cache_nao_depende_da_ordem_dos_filtros()
    {
        var a = new FiltrosVisaoGeral(3, 2026, [2, 1], ["b", "a"], [], ["x"], false);
        var b = new FiltrosVisaoGeral(3, 2026, [1, 2], ["a", "b"], [], ["x"], false);
        Assert.Equal(a.ChaveCache(D("2026-11-18")), b.ChaveCache(D("2026-11-18")));
    }
}
