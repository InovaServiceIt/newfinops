using Finops.Backend.Admin;
using Finops.Backend.Custos;

namespace Finops.Backend.Tests;

public class CustosTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 20);
    private static DateOnly D(int a, int m, int d) => new(a, m, d);
    private static LinhaAgregada L(DateOnly p, string chave, decimal v, string moeda = "BRL", string chave2 = "") => new(p, chave, chave2, moeda, v);

    private static DadosVisaoGeral Dados(
        IEnumerable<LinhaAgregada>? servicoMes = null, IEnumerable<LinhaAgregada>? servicoDia = null,
        IEnumerable<LinhaAgregada>? comp = null, IEnumerable<LinhaAgregada>? recurso = null,
        IEnumerable<LinhaAgregada>? previsao = null, IEnumerable<LinhaAgregada>? credito = null,
        DateOnly? dadosAte = null, IEnumerable<FatorCliente>? fatores = null) =>
        new((servicoMes ?? []).ToList(), (servicoDia ?? []).ToList(), (comp ?? []).ToList(), (recurso ?? []).ToList(),
            (previsao ?? []).ToList(), (credito ?? []).ToList(), dadosAte, 3, DateTimeOffset.Parse("2026-11-20T10:00:00-03:00"),
            (fatores ?? []).ToList());

    private static FatorCliente Fator(long id, decimal f, DateOnly desde, DateOnly? ate = null, long? conta = null) =>
        new(id, 1, "Cliente", conta, null, f, desde, ate, DateTimeOffset.UnixEpoch, "admin@x");

    private static VisaoGeralResposta Montar(DadosVisaoGeral d, bool comFator = false, long? conta = null) =>
        VisaoGeralCustos.Montar(1, conta, Hoje, d, comFator);

    [Fact]
    public void Sem_dados_e_vazio()
    {
        var r = Montar(Dados());
        Assert.True(r.Vazio);
        Assert.Empty(r.Moedas);
    }

    [Fact]
    public void Moedas_diferentes_nao_sao_somadas()
    {
        var r = Montar(Dados(servicoDia: [L(D(2026, 11, 2), "Compute", 100m), L(D(2026, 11, 2), "Compute", 7m, "USD")]));
        Assert.Equal(new[] { "BRL", "USD" }, r.Moedas.Select(m => m.Moeda));
        Assert.Equal(100m, r.Moedas[0].Kpis.MesCorrente);
        Assert.Equal(7m, r.Moedas[1].Kpis.MesCorrente);
    }

    [Fact]
    public void Previsao_do_mes_e_realizado_mais_dias_restantes()
    {
        var dias = Enumerable.Range(1, 19).Select(d => L(D(2026, 11, d), "Compute", 10m));
        var previsao = new[] { L(D(2026, 11, 19), "", 99m), L(D(2026, 11, 20), "", 12m), L(D(2026, 11, 30), "", 13m) };
        var k = Montar(Dados(servicoDia: dias, previsao: previsao, dadosAte: D(2026, 11, 19))).Moedas.Single().Kpis;
        Assert.Equal(190m, k.MesCorrente);
        Assert.Equal(215m, k.PrevisaoMes);   // o dia 19 já é real: a previsão dele é ignorada
    }

    [Fact]
    public void Mes_anterior_ytd_e_creditos()
    {
        var mensal = new[]
        {
            L(D(2025, 12, 1), "Compute", 500m),
            L(D(2026, 1, 1), "Compute", 100m),
            L(D(2026, 10, 1), "Compute", 300m),
            L(D(2026, 11, 1), "Compute", 50m),
        };
        var k = Montar(Dados(servicoMes: mensal, credito: [L(D(2026, 11, 1), "", -12.5m)])).Moedas.Single().Kpis;
        Assert.Equal(300m, k.MesAnterior);
        Assert.Equal(450m, k.Ytd);           // dez/2025 fora do ano
        Assert.Equal(-12.5m, k.CreditosMes);
    }

    [Fact]
    public void Mes_anterior_cai_para_o_diario_quando_nao_ha_mensal()
    {
        var k = Montar(Dados(servicoDia: [L(D(2026, 10, 30), "Compute", 40m), L(D(2026, 10, 31), "Compute", 2m)])).Moedas.Single().Kpis;
        Assert.Equal(42m, k.MesAnterior);
        Assert.Equal(0m, k.MesCorrente);
    }

    [Fact]
    public void Mensal_tem_12_meses_com_o_corrente_parcial()
    {
        var m = Montar(Dados(servicoMes: [L(D(2026, 11, 1), "Compute", 1m)])).Moedas.Single();
        Assert.Equal(12, m.Mensal.Count);
        Assert.Equal(("2025-12", "Dez/25"), (m.Mensal[0].Mes, m.Mensal[0].Rotulo));
        Assert.Equal(("2026-11", true), (m.Mensal[^1].Mes, m.Mensal[^1].Parcial));
        Assert.All(m.Mensal.SkipLast(1), p => Assert.False(p.Parcial));
    }

    [Fact]
    public void Empilhado_agrupa_alem_dos_8_maiores_em_outros()
    {
        var linhas = Enumerable.Range(1, 10).Select(i => L(D(2026, 11, 1), $"S{i:D2}", i));
        var m = Montar(Dados(servicoMes: linhas)).Moedas.Single();
        Assert.Equal(9, m.ServicosMensal.Count);
        Assert.Equal(VisaoGeralCustos.Outros, m.ServicosMensal[^1]);
        Assert.Equal(3m, m.Mensal[^1].Valores[VisaoGeralCustos.Outros]);   // S01 + S02
        Assert.Equal(55m, m.Mensal[^1].Total);
    }

    [Fact]
    public void Diario_top5_mais_outros_ate_o_ultimo_dia_real()
    {
        var linhas = Enumerable.Range(1, 7).Select(i => L(D(2026, 11, 18), $"S{i}", i * 10m))
            .Append(L(D(2026, 11, 19), "S7", 5m));
        var m = Montar(Dados(servicoDia: linhas, dadosAte: D(2026, 11, 19))).Moedas.Single();
        Assert.Equal(new[] { "S7", "S6", "S5", "S4", "S3", VisaoGeralCustos.Outros }, m.ServicosDiario);
        Assert.Equal("2026-11-19", m.Diario[^1].Dia);
        Assert.Equal(new DateOnly(2026, 10, 22).ToString("yyyy-MM-dd"), m.Diario[0].Dia);
        var dia18 = m.Diario.Single(p => p.Dia == "2026-11-18");
        Assert.Equal(30m, dia18.Valores[VisaoGeralCustos.Outros]);          // S1 + S2
        Assert.Equal(0m, m.Diario.Single(p => p.Dia == "2026-11-01").Valores["S7"]);
    }

    [Fact]
    public void Donut_do_mes_e_tops_de_compartimento_e_recurso()
    {
        var comp = Enumerable.Range(1, 12).Select(i => L(D(2026, 11, 1), $"raiz/c{i}", i));
        var rec = new[] { L(D(2026, 11, 1), "ocid1.a", 9m, chave2: "Compute"), L(D(2026, 11, 1), "ocid1.b", 1m) };
        var m = Montar(Dados(servicoDia: [L(D(2026, 11, 3), "Compute", 8m), L(D(2026, 11, 4), "Storage", 2m)], comp: comp, recurso: rec)).Moedas.Single();
        Assert.Equal(new[] { ("Compute", 8m), ("Storage", 2m) }, m.Donut.Select(i => (i.Nome, i.Valor)));
        Assert.Equal(10, m.TopCompartimentos.Count);
        Assert.Equal("raiz/c12", m.TopCompartimentos[0].Nome);
        Assert.Equal(("ocid1.a", "Compute"), (m.TopRecursos[0].Nome, m.TopRecursos[0].Detalhe));
        Assert.Null(m.TopRecursos[1].Detalhe);
    }

    // ── fator ───────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_fator_cadastrado_toggle_indisponivel_e_valores_brutos()
    {
        var r = Montar(Dados(servicoDia: [L(D(2026, 11, 2), "Compute", 100m)]), comFator: true);
        Assert.False(r.FatorDisponivel);
        Assert.False(r.ComFator);
        Assert.Equal(100m, r.Moedas.Single().Kpis.MesCorrente);
    }

    [Fact]
    public void Fator_aplica_o_vigente_em_cada_periodo()
    {
        var fatores = new[] { Fator(1, 1.5m, D(2026, 11, 10)) };
        var d = Dados(servicoDia: [L(D(2026, 11, 2), "Compute", 100m), L(D(2026, 11, 12), "Compute", 100m)], fatores: fatores);
        Assert.Equal(200m, Montar(d).Moedas.Single().Kpis.MesCorrente);
        var com = Montar(d, comFator: true);
        Assert.True(com.ComFator);
        Assert.Equal(1.5m, com.FatorAtual);
        Assert.Equal(250m, com.Moedas.Single().Kpis.MesCorrente);    // antes do dia 10: sem fator
    }

    [Fact]
    public void Fator_da_conta_tem_precedencia_sobre_o_do_cliente()
    {
        var fatores = new[] { Fator(1, 1.2m, D(2026, 1, 1)), Fator(2, 2m, D(2026, 1, 1), conta: 5) };
        Assert.Equal(2m, FatorRegras.Vigente(fatores, 5, Hoje));
        Assert.Equal(1.2m, FatorRegras.Vigente(fatores, 6, Hoje));
        Assert.Equal(1.2m, FatorRegras.Vigente(fatores, null, Hoje));
        Assert.Null(FatorRegras.Vigente(fatores, null, D(2025, 12, 31)));
    }

    [Fact]
    public void Fator_respeita_vigencia_final()
    {
        var fatores = new[] { Fator(1, 1.1m, D(2026, 1, 1), D(2026, 6, 30)) };
        Assert.Equal(1.1m, FatorRegras.Vigente(fatores, null, D(2026, 6, 30)));
        Assert.Null(FatorRegras.Vigente(fatores, null, D(2026, 7, 1)));
    }

    [Theory]
    [InlineData(null, "1.5", "2026-01-01", null)]          // sem cliente
    [InlineData(1L, "0", "2026-01-01", null)]               // fator zero
    [InlineData(1L, "-1", "2026-01-01", null)]
    [InlineData(1L, "10000", "2026-01-01", null)]           // acima de numeric(10,6)
    [InlineData(1L, "1.1234567", "2026-01-01", null)]       // 7 casas
    [InlineData(1L, "1.5", null, null)]                     // sem início
    [InlineData(1L, "1.5", "2026-02-01", "2026-01-31")]     // fim antes do início
    public void Fator_invalido(long? cliente, string fator, string? desde, string? ate)
    {
        var e = new FatorEntrada(cliente, null, decimal.Parse(fator, System.Globalization.CultureInfo.InvariantCulture),
            desde is null ? null : DateOnly.Parse(desde), ate is null ? null : DateOnly.Parse(ate));
        Assert.Equal(400, Assert.Throws<ErroNegocioException>(() => FatorRegras.Validar(e)).Status);
    }

    [Fact]
    public void Fator_valido()
    {
        var v = FatorRegras.Validar(new FatorEntrada(1, 5, 9999.999999m, D(2026, 1, 1), D(2026, 1, 1)));
        Assert.Equal((1L, (long?)5, 9999.999999m), (v.ClienteId, v.ContaId, v.Fator));
    }

    [Fact]
    public void Sobreposicao_de_vigencias()
    {
        var existentes = new[] { Fator(1, 1.2m, D(2026, 1, 1), D(2026, 6, 30)), Fator(2, 1.3m, D(2026, 7, 1)) };
        Assert.True(FatorRegras.Sobrepoe(existentes, null, null, D(2026, 6, 1), D(2026, 6, 15)));
        Assert.True(FatorRegras.Sobrepoe(existentes, null, null, D(2027, 1, 1), null));          // aberto
        Assert.False(FatorRegras.Sobrepoe(existentes, null, null, D(2025, 1, 1), D(2025, 12, 31)));
        Assert.False(FatorRegras.Sobrepoe(existentes, null, 5, D(2026, 3, 1), null));            // outra conta
        Assert.False(FatorRegras.Sobrepoe(existentes, 2, null, D(2026, 8, 1), null));            // editando o próprio
    }

    [Fact]
    public void Hoje_em_sao_paulo()
    {
        var relogio = new RelogioFixo(DateTimeOffset.Parse("2026-11-21T02:30:00Z"));   // 23:30 em Brasília
        Assert.Equal(D(2026, 11, 20), VisaoGeralCustos.HojeEmSaoPaulo(relogio));
    }

    [Fact]
    public void Sql_filtra_por_cliente_e_conta_sem_select_estrela()
    {
        foreach (var sql in new[] { CustosSql.Fato, CustosSql.Previsao, CustosSql.Credito, CustosSql.DadosAte, CustosSql.UltimaImportacao })
        {
            Assert.Contains("cliente_id = @cliente_id AND (@conta_id IS NULL OR cloud_conta_id = @conta_id)", sql);
            Assert.DoesNotContain("*", sql);
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
