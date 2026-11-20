using Finops.Backend.Admin;
using Finops.Backend.Identidade;

namespace Finops.Backend.Tests;

public class AdminRegrasTests
{
    private static readonly IReadOnlySet<long> Ativos = new HashSet<long> { 1, 2, 3 };

    private static int Status(Action acao) => Assert.Throws<ErroNegocioException>(acao).Status;

    // ── Cliente ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cliente_valido_e_normalizado()
    {
        var c = ClienteRegras.Validar(new ClienteEntrada("  Ultragaz  ", " ultragaz ", " 0123 "));
        Assert.Equal(new ClienteValido("Ultragaz", "ultragaz", "0123"), c);
        Assert.Null(ClienteRegras.Validar(new ClienteEntrada("Ultragaz", "ultragaz", "  ")).Documento);
    }

    [Theory]
    [InlineData("Ultragaz")]       // maiúscula
    [InlineData("ultra gaz")]      // espaço
    [InlineData("ultragás")]       // acento
    [InlineData("ultra_gaz")]      // sublinhado
    [InlineData("")]
    public void Apelido_invalido(string apelido) =>
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada("Ultragaz", apelido, null))));

    [Fact]
    public void Apelido_com_hifen_e_numeros_e_valido() =>
        Assert.Equal("metro-rio-2", ClienteRegras.Validar(new ClienteEntrada("Metrô Rio", "metro-rio-2", null)).Apelido);

    [Fact]
    public void Limites_de_tamanho()
    {
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada(new string('a', 151), "x", null))));
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada("A", new string('a', 61), null))));
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada("A", "a", new string('1', 21)))));
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada(" ", "a", null))));
    }

    [Theory]
    [InlineData("12.345.678/0001-90")]
    [InlineData("12a")]
    public void Documento_so_digitos(string documento) =>
        Assert.Equal(400, Status(() => ClienteRegras.Validar(new ClienteEntrada("A", "a", documento))));

    // ── Usuário: dados ──────────────────────────────────────────────────────

    [Fact]
    public void Email_normalizado_e_validado()
    {
        Assert.Equal("fulano@ultragaz.com.br", UsuarioRegras.ValidarEmail("  Fulano@Ultragaz.com.br "));
        Assert.Equal(400, Status(() => UsuarioRegras.ValidarEmail("sem-arroba")));
        Assert.Equal(400, Status(() => UsuarioRegras.ValidarEmail("")));
    }

    [Fact]
    public void Perfil_validado_e_normalizado()
    {
        Assert.Equal(Perfis.AnalistaFinOps, UsuarioRegras.ValidarPerfil("analista_finops"));
        Assert.Equal(400, Status(() => UsuarioRegras.ValidarPerfil("SUPERUSUARIO")));
    }

    // ── Usuário: vínculos ───────────────────────────────────────────────────

    [Fact]
    public void Cliente_sem_cliente_e_erro_de_validacao() =>
        Assert.Equal(400, Status(() => UsuarioRegras.ValidarVinculos(Perfis.Cliente, [], Ativos)));

    [Fact]
    public void Cliente_com_cliente_inativo_ou_inexistente_e_erro() =>
        Assert.Equal(400, Status(() => UsuarioRegras.ValidarVinculos(Perfis.Cliente, [1, 99], Ativos)));

    [Fact]
    public void Analista_pode_ter_zero_ou_varios()
    {
        Assert.Empty(UsuarioRegras.ValidarVinculos(Perfis.AnalistaFinOps, null, Ativos));
        Assert.Equal(new long[] { 1, 2 }, UsuarioRegras.ValidarVinculos(Perfis.AnalistaFinOps, [1, 2, 2], Ativos));
    }

    [Fact]
    public void Administrador_nunca_tem_vinculos() =>
        Assert.Empty(UsuarioRegras.ValidarVinculos(Perfis.Administrador, [1, 2, 99], Ativos));

    // ── Usuário: proteções ──────────────────────────────────────────────────

    [Fact]
    public void Admin_nao_desativa_a_si_mesmo() =>
        Assert.Equal(409, Status(() => UsuarioRegras.ValidarProtecoes(1, 1, Perfis.Administrador, true, Perfis.Administrador, false, 3)));

    [Fact]
    public void Admin_nao_rebaixa_o_proprio_perfil() =>
        Assert.Equal(409, Status(() => UsuarioRegras.ValidarProtecoes(1, 1, Perfis.Administrador, true, Perfis.AnalistaFinOps, true, 3)));

    [Fact]
    public void Nao_pode_ficar_sem_administrador_ativo()
    {
        // outro admin (id 2) é o último administrador ativo
        Assert.Equal(409, Status(() => UsuarioRegras.ValidarProtecoes(1, 2, Perfis.Administrador, true, Perfis.Cliente, true, 1)));
        Assert.Equal(409, Status(() => UsuarioRegras.ValidarProtecoes(1, 2, Perfis.Administrador, true, Perfis.Administrador, false, 1)));
    }

    [Fact]
    public void Operacoes_permitidas()
    {
        // rebaixar outro admin quando há mais de um
        UsuarioRegras.ValidarProtecoes(1, 2, Perfis.Administrador, true, Perfis.AnalistaFinOps, true, 2);
        // desativar um analista
        UsuarioRegras.ValidarProtecoes(1, 5, Perfis.AnalistaFinOps, true, Perfis.AnalistaFinOps, false, 1);
        // editar o próprio nome mantendo o perfil
        UsuarioRegras.ValidarProtecoes(1, 1, Perfis.Administrador, true, Perfis.Administrador, true, 1);
        // reativar um admin inativo
        UsuarioRegras.ValidarProtecoes(1, 3, Perfis.Administrador, false, Perfis.Administrador, true, 1);
    }
}
