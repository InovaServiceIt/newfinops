using Finops.Backend.Compartilhado;
using Finops.Backend.Coletas;
using Finops.Backend.Governanca;
using Finops.Backend.Inventario;

namespace Finops.Backend.Tests;

/// <summary>Ajuste pós-aceite do MD 1.2/1.3: duração após reimportar e sinal "sem tag".</summary>
public class AjusteAceiteTests
{
    [Fact]
    public void Duracao_da_coleta_usa_coletado_em_e_nao_muda_ao_reimportar()
    {
        // Job reimportado: finalizado_em é regravado pelo importador, coletado_em não muda.
        Assert.Contains("COALESCE(j.coletado_em, j.finalizado_em, now()) - j.iniciado_em", ColetasSql.ListarColetas);
        Assert.DoesNotContain("COALESCE(j.finalizado_em, now()) - j.iniciado_em", ColetasSql.ListarColetas);
    }

    [Fact]
    public void Sinal_sem_tag_usa_a_mesma_regra_de_automaticas_da_governanca()
    {
        Assert.False(InventarioRegras.Sinais.ContainsKey("sem_tag_defined"));
        var sinal = InventarioRegras.Sinais["sem_tag"];
        Assert.Equal("NOT " + TagsSql.TemTagNaoAutomaticaR, sinal);
        // Mesma condição de automáticas nos dois lados (sem duplicar texto).
        Assert.Contains("NOT (" + TagsSql.Automatica + ")", sinal);
        Assert.Contains("OR NOT (" + TagsSql.Automatica + ")", GovernancaSql.Totais);
        // Conta defined E freeform (antes só olhava defined).
        Assert.Contains("r.tags->'freeform'", sinal);
        Assert.Contains("r.tags->'defined'", sinal);
    }

    [Theory]
    // Volume do aceite: só Oracle-Tags.CreatedBy/CreatedOn → é "sem tag (exceto automáticas)".
    [InlineData("Oracle-Tags", "CreatedBy", true)]
    [InlineData("Oracle-Tags", "CreatedOn", true)]
    [InlineData("UG-Oracle-Tags", "CreatedOn", true)]
    [InlineData("CostCenter", "area", false)]
    public void Automaticas_do_exemplo_do_aceite(string ns, string chave, bool automatica) =>
        Assert.Equal(automatica, GovernancaRegras.EhAutomatica(ns, chave));

    [Fact]
    public void Kpi_do_sinal_usa_o_alias_novo()
    {
        Assert.Contains("AS sem_tag", InventarioSql.Kpis);
        Assert.DoesNotContain("sem_tag_defined", InventarioSql.Kpis);
    }
}
