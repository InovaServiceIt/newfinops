using System.Security.Claims;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Http;
using NpgsqlTypes;

namespace Finops.Backend.Tests;

public class IdentidadeTests
{
    private static readonly Guid Oid = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OutroOid = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private static ClaimsPrincipal Principal(params (string tipo, string valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.tipo, c.valor)), "teste"));

    private static UsuarioRegistro Usuario(Guid? oid, bool ativo = true, string perfil = Perfis.Administrador) =>
        new(1, "matheus.junior@service.com.br", "Matheus", oid, perfil, ativo, null);

    // ── Claims ──────────────────────────────────────────────────────────────

    [Fact]
    public void Oid_pela_claim_longa_e_email_pelo_preferred_username_normalizado()
    {
        var id = IdentidadeEntra.De(Principal(
            (IdentidadeEntra.ClaimOidCompleta, Oid.ToString()),
            ("preferred_username", "  Matheus.Junior@Service.com.br "),
            ("email", "outro@service.com.br")));

        Assert.Equal(Oid, id.Oid);
        Assert.Equal("matheus.junior@service.com.br", id.Email);
    }

    [Fact]
    public void Oid_pela_claim_curta_e_email_com_fallback_para_email_e_upn()
    {
        Assert.Equal(Oid, IdentidadeEntra.De(Principal(("oid", Oid.ToString()))).Oid);
        Assert.Equal("a@b.com", IdentidadeEntra.De(Principal(("email", "A@B.com"))).Email);
        Assert.Equal("a@b.com", IdentidadeEntra.De(Principal((ClaimTypes.Email, "a@b.com"))).Email);
        Assert.Equal("c@d.com", IdentidadeEntra.De(Principal(("upn", "C@D.com"))).Email);
    }

    [Fact]
    public void Sem_claims_ou_oid_invalido_retorna_nulos()
    {
        var id = IdentidadeEntra.De(Principal(("oid", "nao-e-guid")));
        Assert.Null(id.Oid);
        Assert.Null(id.Email);
    }

    // ── Decisão de identidade ───────────────────────────────────────────────

    [Fact]
    public void Encontrado_pelo_oid()
    {
        var u = Usuario(Oid);
        var (d, r) = ResolucaoIdentidade.Decidir(new IdentidadeEntra(Oid, "x@y.com"), u, null);
        Assert.Equal(DecisaoIdentidade.Encontrado, d);
        Assert.Same(u, r);
    }

    [Fact]
    public void Encontrado_por_email_com_oid_nulo_grava_oid()
    {
        var (d, _) = ResolucaoIdentidade.Decidir(new IdentidadeEntra(Oid, "matheus.junior@service.com.br"), null, Usuario(null));
        Assert.Equal(DecisaoIdentidade.GravarOid, d);
    }

    [Fact]
    public void Encontrado_por_email_com_outro_oid_e_conflito()
    {
        var (d, _) = ResolucaoIdentidade.Decidir(new IdentidadeEntra(Oid, "matheus.junior@service.com.br"), null, Usuario(OutroOid));
        Assert.Equal(DecisaoIdentidade.Conflito, d);
    }

    [Fact]
    public void Nao_encontrado()
    {
        var (d, r) = ResolucaoIdentidade.Decidir(new IdentidadeEntra(Oid, "ninguem@service.com.br"), null, null);
        Assert.Equal(DecisaoIdentidade.NaoEncontrado, d);
        Assert.Null(r);
    }

    [Fact]
    public void Ultimo_login_atualiza_no_maximo_a_cada_10_minutos()
    {
        var agora = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        Assert.True(ResolucaoIdentidade.DeveAtualizarUltimoLogin(null, agora));
        Assert.False(ResolucaoIdentidade.DeveAtualizarUltimoLogin(agora.AddMinutes(-9), agora));
        Assert.True(ResolucaoIdentidade.DeveAtualizarUltimoLogin(agora.AddMinutes(-10), agora));
    }

    // ── Contexto de RLS ─────────────────────────────────────────────────────

    [Fact]
    public void Sql_do_contexto_usa_is_local_true_e_parametros()
    {
        var sql = CloudVistaDb.SqlContexto;

        Assert.Contains("set_config('app.perfil', @perfil, true)", sql);
        Assert.Contains("set_config('app.usuario_id', @usuario_id, true)", sql);
        Assert.DoesNotContain("false", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET ", sql.Replace("set_config", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Comando_do_contexto_passa_perfil_e_id_como_parametros_sem_concatenar()
    {
        var usuario = new UsuarioAtual(42, "a@b.com", "A", Perfis.AnalistaFinOps, true);
        using var cmd = CloudVistaDb.CriarComandoContexto(usuario);

        Assert.Equal(CloudVistaDb.SqlContexto, cmd.CommandText);
        Assert.DoesNotContain("42", cmd.CommandText);
        Assert.DoesNotContain(Perfis.AnalistaFinOps, cmd.CommandText);
        Assert.Equal(2, cmd.Parameters.Count);
        Assert.Equal(Perfis.AnalistaFinOps, cmd.Parameters["perfil"].Value);
        Assert.Equal("42", cmd.Parameters["usuario_id"].Value);
        Assert.Equal(NpgsqlDbType.Text, cmd.Parameters["usuario_id"].NpgsqlDbType);
    }

    // ── Barreira de rotas ───────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/health", true)]
    [InlineData("/api/health/db", true)]
    [InlineData("/api/auth/me", true)]
    [InlineData("/visao-geral", true)]
    [InlineData("/api/visao-geral", false)]
    [InlineData("/api/admin/clientes", false)]
    [InlineData("/api/auth/outra", false)]
    public void Rotas_livres_da_barreira_de_usuario(string caminho, bool livre) =>
        Assert.Equal(livre, UsuarioAutorizadoMiddleware.RotaLivre(new PathString(caminho)));
}
