using Finops.Backend.Admin;
using Finops.Backend.Coletas;
using Finops.Backend.Custos;
using Finops.Backend.Inventario;

namespace Finops.Backend.Tests;

public class InventarioApiTests
{
    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+55 21", "'+55 21")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tx", "'\tx")]
    [InlineData("vm-01", "vm-01")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("diz \"oi\"", "\"diz \"\"oi\"\"\"")]
    [InlineData(null, "")]
    public void Csv_neutraliza_formula_e_aplica_aspas(string? entrada, string esperado) =>
        Assert.Equal(esperado, InventarioRegras.CelulaCsv(entrada));

    [Fact]
    public void Csv_formula_com_separador_fica_neutralizada_e_entre_aspas() =>
        Assert.Equal("\"'=HYPERLINK(\"\"x\"\";1)\"", InventarioRegras.CelulaCsv("=HYPERLINK(\"x\";1)"));

    [Fact]
    public void Linha_csv_com_ponto_e_virgula() =>
        Assert.Equal("a;'=1;", InventarioRegras.LinhaCsv(["a", "=1", null]));

    [Fact]
    public void Tag_namespace_chave_valor()
    {
        var t = InventarioRegras.Tag("Custo.cc=100")!;
        Assert.Equal("""{"defined":{"Custo":{"cc":"100"}}}""", t.JsonDefined);
        Assert.Equal("""{"freeform":{"Custo.cc":"100"}}""", t.JsonFreeform);
        Assert.Null(t.Valor);
    }

    [Fact]
    public void Tag_chave_valor_e_so_valor()
    {
        var livre = InventarioRegras.Tag("dono=joao")!;
        Assert.Null(livre.JsonDefined);
        Assert.Equal("""{"freeform":{"dono":"joao"}}""", livre.JsonFreeform);
        Assert.Equal("financeiro", InventarioRegras.Tag(" financeiro ")!.Valor);
        Assert.Null(InventarioRegras.Tag("  "));
    }

    [Fact]
    public void Tag_com_aspas_vira_json_valido()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(InventarioRegras.Tag("a\"b=c")!.JsonFreeform!);
        Assert.Equal("c", doc.RootElement.GetProperty("freeform").GetProperty("a\"b").GetString());
    }

    [Fact]
    public void Escapa_curingas_do_like() =>
        Assert.Equal("100\\%\\_a\\\\b", InventarioRegras.EscaparLike("100%_a\\b"));

    [Fact]
    public void Filtro_exige_cliente_e_sinal_conhecido()
    {
        Assert.Throws<ErroNegocioException>(() => InventarioRegras.Validar(null, null, null, null, null, null, null, null, null, false));
        Assert.Throws<ErroNegocioException>(() => InventarioRegras.Validar(1, null, null, null, null, null, null, null, "drop table", false));
        var f = InventarioRegras.Validar(1, null, " sa-saopaulo-1 ", "", null, null, "vm", null, "volumes_soltos", true);
        Assert.Equal(("sa-saopaulo-1", (string?)null, "volumes_soltos", true), (f.Regiao, f.Tipo, f.Sinal, f.Removidos));
    }

    [Fact]
    public void Ordem_so_da_lista_constante()
    {
        Assert.Equal(("lower(r.nome)", false), InventarioRegras.Ordem(null, null));
        Assert.Equal(("r.ultimo_visto_em", true), InventarioRegras.Ordem("visto", null));
        Assert.Equal(("c.caminho", true), InventarioRegras.Ordem("compartimento", "DESC"));
        Assert.Throws<ErroNegocioException>(() => InventarioRegras.Ordem("r.nome; drop", null));
        Assert.Throws<ErroNegocioException>(() => InventarioRegras.Ordem("nome", "sideways"));
    }

    [Fact]
    public void Sql_parametrizado_sem_select_estrela()
    {
        foreach (var sql in new[] { InventarioSql.Listar("ips_sem_uso", "r.tipo", false), InventarioSql.Exportar(null),
                     InventarioSql.Kpis, InventarioSql.Detalhe, InventarioSql.Compartimentos, InventarioSql.Proveniencia })
        {
            Assert.DoesNotContain("SELECT *", sql);
            Assert.DoesNotContain("r.*", sql);
        }
        Assert.Contains("r.cliente_id = @cliente_id", InventarioSql.Contar(null));
        Assert.Contains("r.tags @> CAST(@tag_defined AS jsonb)", InventarioSql.Filtro);
        Assert.Contains("LIMIT @limite", InventarioSql.Exportar(null));
        Assert.Contains("count(*) FILTER (WHERE r.tipo = 'compute.instances' AND r.estado = 'STOPPED') AS instancias_paradas", InventarioSql.Kpis);
    }

    [Theory]
    [InlineData("perfil", 403)]
    [InlineData("inexistente", 404)]
    [InlineData("status", 409)]
    [InlineData("sem_bruta", 409)]
    [InlineData("outra coisa", 409)]
    [InlineData(null, 409)]
    public void Reimportar_mapeia_excecao_do_banco(string? mensagem, int status) =>
        Assert.Equal(status, ReimportarRegras.Mapear(mensagem).Status);

    [Fact]
    public void Top_recursos_usa_nome_do_inventario()
    {
        var mes = new DateOnly(2026, 11, 1);
        var linhas = new[]
        {
            new LinhaAgregada(mes, "ocid1.instance.oc1..vm1", "Compute", "BRL", 10m),
            new LinhaAgregada(mes, "ocid1.instance.oc1..vm2", "Compute", "BRL", 5m),
        };
        var dados = new DadosVisaoGeral([], [], [], linhas, [], [], null, null, null, [],
            new Dictionary<string, string> { ["ocid1.instance.oc1..vm1"] = "vm-01" });
        var top = VisaoGeralCustos.Montar(1, null, new DateOnly(2026, 11, 20), dados, false).Moedas.Single().TopRecursos;
        Assert.Equal(("vm-01", "ocid1.instance.oc1..vm1"), (top[0].Nome, top[0].Chave));
        Assert.Equal("ocid1.instance.oc1..vm2", top[1].Nome);   // sem inventário: OCID
    }
}
