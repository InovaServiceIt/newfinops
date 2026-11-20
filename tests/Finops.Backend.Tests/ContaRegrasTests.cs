using Finops.Backend.Admin;
using Finops.Backend.Contas;

namespace Finops.Backend.Tests;

public class ContaRegrasTests
{
    private const string OciTenancy = "ocid1.tenancy.oc1..aaaaaaaabcdefghijklmnop0123456789";
    private const string GuidMinusculo = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    private static ContaCriacao Criacao(string provedor, string escopo, string identificador, string nome = "Conta", long? cliente = 1, string? regiao = null) =>
        new(cliente, provedor, nome, identificador, escopo, regiao);

    private static ErroNegocioException Erro(Action acao) => Assert.Throws<ErroNegocioException>(acao);

    // ── Válidos (todas as linhas da tabela) ─────────────────────────────────

    [Theory]
    [InlineData("OCI", "TENANCY", OciTenancy)]
    [InlineData("AWS", "CONTA", "123456789012")]
    [InlineData("AWS", "ORGANIZACAO", "000000000001")]
    [InlineData("AZURE", "TENANT", GuidMinusculo)]
    [InlineData("AZURE", "SUBSCRICAO", GuidMinusculo)]
    [InlineData("GCP", "ORGANIZACAO", "123456789")]
    [InlineData("GCP", "PROJETO", "ultragaz-prod-01")]
    [InlineData("GCP", "PROJETO", "abcdef")]                          // mínimo: 6 caracteres
    [InlineData("GCP", "PROJETO", "a23456789012345678901234567890")] // máximo: 30 caracteres
    public void Identificadores_validos(string provedor, string escopo, string identificador)
    {
        var c = ContaRegras.ValidarCriacao(Criacao(provedor, escopo, identificador));
        Assert.Equal(provedor, c.Provedor);
        Assert.Equal(escopo, c.Escopo);
        Assert.Equal(identificador, c.Identificador);
    }

    // ── Inválidos ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("OCI", "TENANCY", "ocid1.compartment.oc1..aaaaaaaabcdef")] // OCID de outro tipo
    [InlineData("OCI", "TENANCY", "ocid1.tenancy.oc1..AAAA")]              // maiúsculas
    [InlineData("OCI", "TENANCY", "ocid1.tenancy.oc2..aaaa")]              // realm diferente
    [InlineData("AWS", "CONTA", "12345678901")]                            // 11 dígitos
    [InlineData("AWS", "CONTA", "1234567890123")]                          // 13 dígitos
    [InlineData("AWS", "ORGANIZACAO", "o-abc123def4")]                     // id da org, não o account id da payer
    [InlineData("AWS", "CONTA", "1234-5678-9012")]
    [InlineData("AZURE", "TENANT", "3f2504e04f8941d39a0c0305e82c3301")]    // GUID sem hífens
    [InlineData("AZURE", "SUBSCRICAO", "não-é-guid")]
    [InlineData("GCP", "ORGANIZACAO", "organizations/123")]
    [InlineData("GCP", "PROJETO", "1ultragaz-prod")]                       // começa com dígito
    [InlineData("GCP", "PROJETO", "Ultragaz-prod")]                        // maiúscula
    [InlineData("GCP", "PROJETO", "ultragaz-")]                            // termina com hífen
    [InlineData("GCP", "PROJETO", "abcde")]                                // 5 caracteres
    [InlineData("GCP", "PROJETO", "a234567890123456789012345678901")]      // 31 caracteres
    public void Identificadores_invalidos(string provedor, string escopo, string identificador)
    {
        var e = Erro(() => ContaRegras.ValidarCriacao(Criacao(provedor, escopo, identificador)));
        Assert.Equal(400, e.Status);
        Assert.StartsWith("Identificador:", e.Message);
    }

    // ── Normalização ────────────────────────────────────────────────────────

    [Fact]
    public void Guid_do_azure_em_maiusculas_e_normalizado_para_minusculas()
    {
        var c = ContaRegras.ValidarCriacao(Criacao("AZURE", "TENANT", "  3F2504E0-4F89-41D3-9A0C-0305E82C3301 "));
        Assert.Equal(GuidMinusculo, c.Identificador);
    }

    [Fact]
    public void Trim_em_identificador_nome_e_regiao_e_provedor_escopo_case_insensitive()
    {
        var c = ContaRegras.ValidarCriacao(new ContaCriacao(7, " oci ", "  OCI Ultragaz principal ", $"  {OciTenancy}  ", "tenancy", "  sa-saopaulo-1 "));
        Assert.Equal(new ContaValida(7, "OCI", "OCI Ultragaz principal", OciTenancy, "TENANCY", "sa-saopaulo-1"), c);
    }

    [Fact]
    public void Regiao_vazia_vira_nula_e_tem_limite()
    {
        Assert.Null(ContaRegras.ValidarCriacao(Criacao("AWS", "CONTA", "123456789012", regiao: "   ")).RegiaoPadrao);
        Assert.Equal(400, Erro(() => ContaRegras.ValidarCriacao(Criacao("AWS", "CONTA", "123456789012", regiao: new string('r', 51)))).Status);
    }

    // ── Escopo, provedor, cliente, nome ─────────────────────────────────────

    [Theory]
    [InlineData("OCI", "CONTA")]
    [InlineData("AWS", "TENANCY")]
    [InlineData("AZURE", "PROJETO")]
    [InlineData("GCP", "SUBSCRICAO")]
    [InlineData("AWS", "")]
    public void Escopo_incompativel_com_o_provedor(string provedor, string escopo)
    {
        var e = Erro(() => ContaRegras.ValidarCriacao(Criacao(provedor, escopo, "123456789012")));
        Assert.StartsWith("Escopo:", e.Message);
    }

    [Fact]
    public void Provedor_cliente_e_nome_obrigatorios()
    {
        Assert.StartsWith("Provedor:", Erro(() => ContaRegras.ValidarCriacao(Criacao("IBM", "CONTA", "1"))).Message);
        Assert.StartsWith("Cliente:", Erro(() => ContaRegras.ValidarCriacao(Criacao("AWS", "CONTA", "123456789012", cliente: null))).Message);
        Assert.StartsWith("Nome:", Erro(() => ContaRegras.ValidarCriacao(Criacao("AWS", "CONTA", "123456789012", nome: "  "))).Message);
        Assert.StartsWith("Nome:", Erro(() => ContaRegras.ValidarCriacao(Criacao("AWS", "CONTA", "123456789012", nome: new string('n', 151)))).Message);
    }

    // ── Alteração: provedor e identificador imutáveis ───────────────────────

    [Fact]
    public void Alteracao_nao_tem_campos_de_provedor_nem_identificador()
    {
        var props = typeof(ContaAlteracao).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Provedor", props);
        Assert.DoesNotContain("Identificador", props);
        Assert.DoesNotContain("ClienteId", props);
    }

    [Fact]
    public void Alteracao_aceita_escopo_compativel_com_o_identificador_atual()
    {
        // AWS: CONTA ↔ ORGANIZACAO usam o mesmo formato (12 dígitos)
        var a = ContaRegras.ValidarAlteracao("AWS", "123456789012", new ContaAlteracao(" Payer ", "organizacao", null));
        Assert.Equal(new ContaAlteracaoValida("Payer", "ORGANIZACAO", null), a);

        // Azure: TENANT ↔ SUBSCRICAO (GUID)
        Assert.Equal("SUBSCRICAO", ContaRegras.ValidarAlteracao("AZURE", GuidMinusculo, new ContaAlteracao("x", "SUBSCRICAO", null)).Escopo);
    }

    [Fact]
    public void Alteracao_rejeita_escopo_incompativel_com_o_identificador_atual()
    {
        // GCP: um id numérico de organização não vira PROJETO
        var e = Erro(() => ContaRegras.ValidarAlteracao("GCP", "123456789", new ContaAlteracao("x", "PROJETO", null)));
        Assert.StartsWith("Escopo:", e.Message);

        // e escopo de outro provedor
        Assert.StartsWith("Escopo:", Erro(() => ContaRegras.ValidarAlteracao("OCI", OciTenancy, new ContaAlteracao("x", "CONTA", null))).Message);
    }
}
