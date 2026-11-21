using Finops.Backend.Admin;
using Finops.Backend.Governanca;
using Finops.Backend.Inventario;

namespace Finops.Backend.Tests;

public class GovernancaTests
{
    [Theory]
    [InlineData("Oracle-Tags", "CreatedBy", true)]
    [InlineData("Oracle-Tags", "CreatedOn", true)]
    [InlineData("UG-Oracle-Tags", "CreatedBy", true)]
    [InlineData("MinhaOracleTagsX", "CreatedBy", false)]
    [InlineData("Oracle-TagsX", "CreatedBy", false)]
    [InlineData("Oracle-Tags", "CentroCusto", false)]   // outras chaves do namespace contam
    [InlineData("CostCenter", "CreatedBy", false)]
    public void Tags_automaticas_da_oracle(string ns, string chave, bool esperado) =>
        Assert.Equal(esperado, GovernancaRegras.EhAutomatica(ns, chave));

    [Fact]
    public void Sql_de_automaticas_espelha_a_regra()
    {
        // Mesma regra no SQL (pares): namespace exato ou sufixo "-Oracle-Tags", só CreatedBy/CreatedOn.
        Assert.Contains("(d.key = 'Oracle-Tags' OR d.key LIKE '%-Oracle-Tags') AND k.key IN ('CreatedBy', 'CreatedOn')", GovernancaSql.Chaves);
        Assert.Contains("WHERE @automaticas", GovernancaSql.Chaves);
    }

    [Fact]
    public void Sql_extrai_defined_em_dois_niveis_e_freeform()
    {
        Assert.Contains("d.key || '.' || k.key AS tag, 'DEFINED'", GovernancaSql.Totais);
        Assert.Contains("f.key AS tag, 'FREEFORM'", GovernancaSql.Totais);
        Assert.Contains("jsonb_typeof(b.tags->'defined') = 'object'", GovernancaSql.Totais);
        Assert.Contains("r.removido_em IS NULL", GovernancaSql.Totais);
    }

    [Fact]
    public void Sql_conformidade_exige_todas_as_chaves()
    {
        Assert.Contains("count(*) FILTER (WHERE cardinality(faltando) = 0)", GovernancaSql.AvaliacaoGeral);
        Assert.Contains("CROSS JOIN unnest(CAST(@chaves AS text[]))", GovernancaSql.AvaliacaoGeral);
        Assert.Contains("cardinality(a.faltando) > 0", GovernancaSql.ListarNaoConformes);
        foreach (var sql in new[] { GovernancaSql.Totais, GovernancaSql.Chaves, GovernancaSql.TopValores, GovernancaSql.AvaliacaoPorTipo,
                     GovernancaSql.ListarNaoConformes, GovernancaSql.ExportarNaoConformes, GovernancaSql.Valores })
        {
            Assert.DoesNotContain("SELECT *", sql);
            Assert.Contains("r.cliente_id = @cliente_id", sql);
        }
    }

    [Theory]
    [InlineData("CostCenter.area")]
    [InlineData("Projeto")]
    [InlineData("ns-1.chave_2:x/y z")]
    public void Chave_valida(string chave) => Assert.Equal(chave, GovernancaRegras.Chave(chave));

    [Theory]
    [InlineData("")]
    [InlineData("a'b")]
    [InlineData("Área")]
    [InlineData("x;drop")]
    public void Chave_invalida(string chave) =>
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Chave(chave));

    [Fact]
    public void Chave_com_mais_de_200_caracteres_e_invalida() =>
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Chave(new string('a', 201)));

    [Fact]
    public void Lista_de_chaves_distinta_e_limitada_a_10()
    {
        Assert.Equal(new[] { "a.b", "c" }, GovernancaRegras.Chaves(" a.b, c ,a.b,"));
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Chaves(string.Join(',', Enumerable.Range(1, 11).Select(i => $"k{i}"))));
        Assert.Equal(10, GovernancaRegras.Chaves(string.Join(',', Enumerable.Range(1, 10).Select(i => $"k{i}"))).Length);
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Chaves(""));
        Assert.Empty(GovernancaRegras.Chaves(null, obrigatorio: false));
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Chaves("ok,não-ok"));
    }

    [Theory]
    [InlineData("Produção", "producao")]
    [InlineData("  PRODUCAO ", "producao")]
    [InlineData("pro   dução", "pro ducao")]
    [InlineData("Ação Ç", "acao c")]
    public void Normaliza_caixa_espacos_e_acentos(string valor, string esperado) =>
        Assert.Equal(esperado, GovernancaRegras.Normalizar(valor));

    [Fact]
    public void Variacoes_agrupam_grafias_diferentes()
    {
        var grupos = GovernancaRegras.Variacoes([
            new Grafia("Producao", 40), new Grafia("PRODUCAO", 3), new Grafia("produção", 2),
            new Grafia("Homologacao", 10),                       // grafia única: não é variação
            new Grafia("ti", 1), new Grafia(" TI", 5),
        ]);
        Assert.Equal(2, grupos.Count);
        Assert.Equal(("producao", 45L), (grupos[0].Normalizado, grupos[0].Total));
        Assert.Equal(new[] { "Producao", "PRODUCAO", "produção" }, grupos[0].Grafias.Select(g => g.Valor));
        Assert.Equal(new[] { " TI", "ti" }, grupos[1].Grafias.Select(g => g.Valor));
    }

    [Fact]
    public void Percentual()
    {
        Assert.Equal(0m, GovernancaRegras.Pct(1, 0));
        Assert.Equal(33.3m, GovernancaRegras.Pct(1, 3));
        Assert.Equal(100m, GovernancaRegras.Pct(5, 5));
    }

    [Fact]
    public void Filtro_reaproveita_validacao_do_inventario()
    {
        Assert.Throws<ErroNegocioException>(() => GovernancaRegras.Validar(null, null, null, null, null, false));
        var f = GovernancaRegras.Validar(1, 2, " sa-saopaulo-1 ", null, "metrorio/Prod", true);
        Assert.Equal(("sa-saopaulo-1", "metrorio/Prod", true), (f.Regiao, f.Compartimento, f.Automaticas));
    }

    [Fact]
    public void Csv_de_nao_conformes_sanitizado()
    {
        var linha = InventarioRegras.LinhaCsv(["conta", "=cmd()", "ocid1.x", "compute.instances", "sa", null, "RUNNING", "CostCenter.area, Projeto", "", "{}"]);
        Assert.StartsWith("conta;'=cmd();", linha);
        Assert.Contains("\"CostCenter.area, Projeto\"", linha);
        Assert.Equal("faltando", GovernancaRegras.CabecalhoCsv[7]);
    }
}
