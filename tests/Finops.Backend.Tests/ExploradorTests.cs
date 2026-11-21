using Finops.Backend.Admin;
using Finops.Backend.Custos;

namespace Finops.Backend.Tests;

public class ExploradorTests
{
    private static DateOnly D(int a, int m, int d) => new(a, m, d);
    private static LinhaAgregada L(DateOnly p, string chave, decimal v, string moeda = "BRL") => new(p, chave, "", moeda, v);
    private static readonly Dictionary<string, RotuloChave> SemRotulos = new();

    private static ConsultaExplorador C(string gran = "MES", DateOnly? de = null, DateOnly? ate = null, string dim = "SERVICO",
        int top = 5, string? moeda = null, string? tag = null, string? filtro = null) =>
        ExploradorRegras.Validar(1, null, gran, de ?? D(2026, 9, 1), ate ?? D(2026, 11, 1), dim, filtro, tag, moeda, false, top);

    [Fact]
    public void Top_n_mais_outros_soma_exatamente_o_total()
    {
        var linhas = Enumerable.Range(1, 8).SelectMany(i => new[]
        {
            L(D(2026, 10, 1), $"S{i}", i * 10.01m), L(D(2026, 11, 1), $"S{i}", i * 3.33m),
        }).ToList();
        var r = ExploradorRegras.Montar(C(top: 5), linhas, null, SemRotulos, false, false, []);
        Assert.Equal(new[] { "S8", "S7", "S6", "S5", "S4" }, r.Grupos);
        foreach (var p in r.Serie)
        {
            var doPeriodo = linhas.Where(l => ExploradorRegras.Rotulo("MES", l.Periodo) == p.Periodo).Sum(l => l.Valor);
            Assert.Equal(doPeriodo, p.Grupos.Sum(g => g.Valor) + p.Outros);
        }
        Assert.Equal(linhas.Sum(l => l.Valor), r.Total);
        Assert.Equal(r.Total, r.Serie.Sum(p => p.Grupos.Sum(g => g.Valor) + p.Outros));
        Assert.Equal(3, r.Serie.Count);                       // set/out/nov, setembro zerado
        Assert.Equal(0m, r.Serie[0].Outros + r.Serie[0].Grupos.Sum(g => g.Valor));
    }

    [Fact]
    public void Periodo_anterior_mesmo_tamanho_dia_e_mes_com_virada_de_ano()
    {
        Assert.Equal((D(2025, 11, 1), D(2025, 12, 1)), ExploradorRegras.PeriodoAnterior("MES", D(2026, 1, 1), D(2026, 2, 1)));
        Assert.Equal((D(2025, 10, 1), D(2025, 12, 1)), ExploradorRegras.PeriodoAnterior("MES", D(2026, 1, 1), D(2026, 3, 1)));
        Assert.Equal((D(2025, 12, 2), D(2025, 12, 31)), ExploradorRegras.PeriodoAnterior("DIA", D(2026, 1, 1), D(2026, 1, 30)));
        Assert.Equal((D(2026, 2, 27), D(2026, 2, 28)), ExploradorRegras.PeriodoAnterior("DIA", D(2026, 3, 1), D(2026, 3, 2)));
    }

    [Fact]
    public void Variacao_contra_o_periodo_anterior()
    {
        var c = C(de: D(2026, 11, 1), ate: D(2026, 11, 1));
        var linhas = new[] { L(D(2026, 10, 1), "Compute", 100m), L(D(2026, 11, 1), "Compute", 125m), L(D(2026, 11, 1), "Novo", 10m) };
        var r = ExploradorRegras.Montar(c, linhas, null, SemRotulos, false, false, []);
        Assert.Equal(100m, r.TotalPeriodoAnterior);
        Assert.Equal(25.0m, r.Tabela.Single(t => t.Chave == "Compute").VariacaoPct);
        Assert.Null(r.Tabela.Single(t => t.Chave == "Novo").VariacaoPct);   // sem dado anterior
        Assert.Null(ExploradorRegras.Montar(c, [L(D(2026, 11, 1), "x", 1m)], null, SemRotulos, false, false, []).TotalPeriodoAnterior);
    }

    [Theory]
    [InlineData("SEMANA", "SERVICO")]
    [InlineData("MES", "SKU")]
    [InlineData("MES; drop", "SERVICO")]
    public void Listas_fixas(string gran, string dim) =>
        Assert.Throws<ErroNegocioException>(() => ExploradorRegras.Validar(1, null, gran, D(2026, 1, 1), D(2026, 2, 1), dim, null, null, null, false, 10));

    [Fact]
    public void Limites_de_intervalo()
    {
        Assert.NotNull(ExploradorRegras.Validar(1, null, "DIA", D(2025, 1, 1), D(2026, 1, 1), "SERVICO", null, null, null, false, 10)); // 366
        Assert.Throws<ErroNegocioException>(() => ExploradorRegras.Validar(1, null, "DIA", D(2025, 1, 1), D(2026, 1, 2), "SERVICO", null, null, null, false, 10));
        Assert.NotNull(ExploradorRegras.Validar(1, null, "MES", D(2024, 1, 15), D(2026, 12, 31), "SERVICO", null, null, null, false, 10)); // 36
        Assert.Throws<ErroNegocioException>(() => ExploradorRegras.Validar(1, null, "MES", D(2024, 1, 1), D(2027, 1, 1), "SERVICO", null, null, null, false, 10));
        Assert.Throws<ErroNegocioException>(() => ExploradorRegras.Validar(1, null, "MES", D(2026, 3, 1), D(2026, 1, 1), "SERVICO", null, null, null, false, 10));
        Assert.Throws<ErroNegocioException>(() => C(top: 4));
        Assert.Throws<ErroNegocioException>(() => C(top: 51));
        Assert.Throws<ErroNegocioException>(() => C(filtro: string.Join('|', Enumerable.Range(1, 21).Select(i => $"v{i}"))));
        Assert.Throws<ErroNegocioException>(() => C(moeda: "R$"));
    }

    [Fact]
    public void Mes_normaliza_para_o_primeiro_dia_e_tag_exige_chave_e_mes()
    {
        var c = C(de: D(2026, 1, 15), ate: D(2026, 3, 20));
        Assert.Equal((D(2026, 1, 1), D(2026, 3, 1)), (c.De, c.Ate));
        Assert.Throws<ErroNegocioException>(() => C(dim: "TAG"));
        Assert.Equal("CostCenter.area", C(dim: "TAG", tag: "CostCenter.area").TagChave);
        Assert.Throws<ErroNegocioException>(() => C(gran: "DIA", de: D(2026, 11, 1), ate: D(2026, 11, 5), dim: "TAG", tag: "x"));
        Assert.Null(C(tag: "ignorada").TagChave);   // fora de TAG não filtra chave2
        Assert.Equal(new[] { "a/b", "c" }, C(filtro: " a/b | c |a/b").FiltroChave);
    }

    [Fact]
    public void Fator_igual_ao_da_visao_geral()
    {
        // Mesmo caso de CustosTests.Fator_aplica_o_vigente_em_cada_periodo: 1,5 a partir de 10/11.
        var fatores = new List<FatorCliente> { new(1, 1, "C", null, null, 1.5m, D(2026, 11, 10), null, DateTimeOffset.UnixEpoch, "x") };
        var dia = new List<LinhaAgregada> { L(D(2026, 11, 2), "Compute", 100m), L(D(2026, 11, 12), "Compute", 100m) };

        var vg = VisaoGeralCustos.Montar(1, null, D(2026, 11, 20),
            new DadosVisaoGeral([], dia, [], [], [], [], null, null, null, fatores), comFatorPedido: true);
        var c = ExploradorRegras.Validar(1, null, "DIA", D(2026, 11, 1), D(2026, 11, 20), "SERVICO", null, null, null, true, 10);
        var ex = ExploradorRegras.Montar(c, FatorRegras.Aplicar(dia, fatores, null), null, SemRotulos, true, true, []);

        Assert.Equal(250m, vg.Moedas.Single().Kpis.MesCorrente);
        Assert.Equal(vg.Moedas.Single().Kpis.MesCorrente, ex.Total);
        Assert.True(FatorRegras.Disponivel(fatores, null, D(2026, 11, 20), []));
        Assert.False(FatorRegras.Disponivel([], null, D(2026, 11, 20), []));
    }

    [Theory]
    [InlineData("ADMINISTRADOR", true)]
    [InlineData("ANALISTA_FINOPS", true)]
    [InlineData("CLIENTE", false)]
    public void Perfil_cliente_sem_fator(string perfil, bool pode) => Assert.Equal(pode, ExploradorController.PodeFator(perfil));

    [Fact]
    public void Mais_de_uma_moeda_escolhe_a_maior_e_nao_soma()
    {
        var linhas = new[] { L(D(2026, 11, 1), "Compute", 100m), L(D(2026, 11, 1), "Compute", 900m, "USD"), L(D(2026, 11, 1), "Storage", 50m) };
        var r = ExploradorRegras.Montar(C(de: D(2026, 11, 1), ate: D(2026, 11, 1)), linhas, null, SemRotulos, false, false, []);
        Assert.Equal(("USD", 900m), (r.Moeda, r.Total));
        Assert.Equal(new[] { "BRL", "USD" }, r.Moedas);
        var brl = ExploradorRegras.Montar(C(de: D(2026, 11, 1), ate: D(2026, 11, 1), moeda: "BRL"), linhas, null, SemRotulos, false, false, []);
        Assert.Equal(("BRL", 150m), (brl.Moeda, brl.Total));
        var inexistente = ExploradorRegras.Montar(C(de: D(2026, 11, 1), ate: D(2026, 11, 1), moeda: "EUR"), linhas, null, SemRotulos, false, false, []);
        Assert.Equal("USD", inexistente.Moeda);
    }

    [Fact]
    public void Rotulos_de_recurso_e_compartimento_com_e_sem_inventario()
    {
        var c = C(de: D(2026, 11, 1), ate: D(2026, 11, 1), dim: "RECURSO");
        var linhas = new[] { L(D(2026, 11, 1), "ocid1.instance.oc1..a", 10m), L(D(2026, 11, 1), "ocid1.instance.oc1..b", 5m) };
        var rot = new Dictionary<string, RotuloChave> { ["ocid1.instance.oc1..a"] = new("vm-01", 5) };
        var r = ExploradorRegras.Montar(c, linhas, null, rot, false, false, []);
        Assert.Equal(("vm-01", (long?)5), (r.Tabela[0].Rotulo, r.Tabela[0].ContaId));
        Assert.Equal(("ocid1.instance.oc1..b", (long?)null), (r.Tabela[1].Rotulo, r.Tabela[1].ContaId));

        var comp = ExploradorRegras.Montar(C(de: D(2026, 11, 1), ate: D(2026, 11, 1), dim: "COMPARTIMENTO"),
            [L(D(2026, 11, 1), "raiz/prod", 3m)], null, SemRotulos, false, false, []);
        Assert.Equal("raiz/prod", comp.Tabela[0].Rotulo);     // chave já é caminho: mantém
    }

    [Fact]
    public void Creditos_so_no_mensal_e_vazio()
    {
        var mes = ExploradorRegras.Montar(C(), [L(D(2026, 11, 1), "x", 1m)], -12m, SemRotulos, false, false, []);
        Assert.Equal(-12m, mes.Creditos);
        var dia = ExploradorRegras.Montar(C(gran: "DIA", de: D(2026, 11, 1), ate: D(2026, 11, 3)), [], -12m, SemRotulos, false, false, []);
        Assert.Null(dia.Creditos);
        Assert.True(dia.Vazio);
        Assert.Equal(3, dia.Serie.Count);
    }

    [Fact]
    public void Csv_longo_sanitizado_com_moeda_e_fator()
    {
        var c = C(de: D(2026, 10, 1), ate: D(2026, 11, 1));
        var r = ExploradorRegras.Montar(c, [L(D(2026, 10, 1), "=HYPERLINK(1)", 2m), L(D(2026, 11, 1), "=HYPERLINK(1)", 3.5m)], null, SemRotulos, true, true, []);
        var linhas = ExploradorCsv.Linhas(r);
        Assert.Equal("periodo;chave;rotulo;valor;moeda;fator_aplicado", linhas[0]);
        Assert.Equal("2026-10;'=HYPERLINK(1);'=HYPERLINK(1);2;BRL;sim", linhas[1]);
        Assert.Equal("2026-11;'=HYPERLINK(1);'=HYPERLINK(1);3.5;BRL;sim", linhas[2]);
    }

    [Fact]
    public void Sql_agrega_no_banco_sem_select_estrela()
    {
        Assert.Contains("GROUP BY f.periodo, f.chave, f.moeda", ExploradorSql.Consulta);
        Assert.Contains("(@tag_chave IS NULL OR f.chave2 = @tag_chave)", ExploradorSql.Consulta);
        Assert.Contains("f.cliente_id = @cliente_id", ExploradorSql.Consulta);
        foreach (var sql in new[] { ExploradorSql.Consulta, ExploradorSql.Creditos, ExploradorSql.Proveniencia, ExploradorSql.Valores,
                     ExploradorSql.NomesRecursos, ExploradorSql.CaminhosCompartimentos })
            Assert.DoesNotContain("SELECT *", sql);
    }
}
