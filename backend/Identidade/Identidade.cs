using System.Security.Claims;

namespace Finops.Backend.Identidade;

/// <summary>Perfis do CloudVista. A fonte da verdade é a coluna usuario.perfil.</summary>
public static class Perfis
{
    public const string Administrador = "ADMINISTRADOR";
    public const string AnalistaFinOps = "ANALISTA_FINOPS";
    public const string Cliente = "CLIENTE";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string> { Administrador, AnalistaFinOps, Cliente };

    public static bool Valido(string? perfil) => perfil is not null && Todos.Contains(perfil);
}

/// <summary>Usuário CloudVista da requisição (lido do banco).</summary>
public sealed record UsuarioAtual(long Id, string Email, string Nome, string Perfil, bool Ativo);

/// <summary>Linha da tabela usuario usada na resolução de identidade.</summary>
public sealed record UsuarioRegistro(long Id, string Email, string Nome, Guid? EntraOid, string Perfil, bool Ativo, DateTimeOffset? UltimoLoginEm)
{
    public UsuarioAtual ParaUsuarioAtual() => new(Id, Email, Nome, Perfil, Ativo);
}

/// <summary>Identidade vinda das claims do Entra.</summary>
public sealed record IdentidadeEntra(Guid? Oid, string? Email)
{
    public const string ClaimOidCompleta = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <summary>
    /// OID: claim objectidentifier (formato longo) ou "oid".
    /// E-mail: preferred_username → email (inclui ClaimTypes.Email) → upn, normalizado (trim + minúsculas).
    /// </summary>
    public static IdentidadeEntra De(ClaimsPrincipal principal)
    {
        var oidTexto = principal.FindFirst(ClaimOidCompleta)?.Value ?? principal.FindFirst("oid")?.Value;
        Guid? oid = Guid.TryParse(oidTexto, out var g) ? g : null;

        var email = Primeiro(principal, "preferred_username", "email", ClaimTypes.Email, "upn", ClaimTypes.Upn);
        return new IdentidadeEntra(oid, NormalizarEmail(email));
    }

    public static string? NormalizarEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? Primeiro(ClaimsPrincipal p, params string[] tipos) =>
        tipos.Select(t => p.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>Resultado da resolução de identidade.</summary>
public enum DecisaoIdentidade
{
    /// <summary>Usuário encontrado pelo OID (ou por e-mail com o mesmo OID já gravado).</summary>
    Encontrado,
    /// <summary>Encontrado por e-mail e entra_oid ainda nulo: gravar o OID.</summary>
    GravarOid,
    /// <summary>Encontrado por e-mail, mas com OUTRO entra_oid gravado: negar.</summary>
    Conflito,
    NaoEncontrado,
}

public static class ResolucaoIdentidade
{
    /// <summary>Decide o que fazer com as linhas encontradas por OID e por e-mail (função pura).</summary>
    public static (DecisaoIdentidade Decisao, UsuarioRegistro? Usuario) Decidir(
        IdentidadeEntra identidade, UsuarioRegistro? porOid, UsuarioRegistro? porEmail)
    {
        if (porOid is not null)
            return (DecisaoIdentidade.Encontrado, porOid);

        if (porEmail is null)
            return (DecisaoIdentidade.NaoEncontrado, null);

        if (porEmail.EntraOid is null)
            return identidade.Oid is null
                ? (DecisaoIdentidade.Encontrado, porEmail)   // sem OID na claim: aceita por e-mail, nada a gravar
                : (DecisaoIdentidade.GravarOid, porEmail);

        return porEmail.EntraOid == identidade.Oid
            ? (DecisaoIdentidade.Encontrado, porEmail)
            : (DecisaoIdentidade.Conflito, porEmail);
    }

    /// <summary>ultimo_login_em é atualizado no máximo 1x a cada 10 minutos.</summary>
    public static readonly TimeSpan IntervaloUltimoLogin = TimeSpan.FromMinutes(10);

    public static bool DeveAtualizarUltimoLogin(DateTimeOffset? ultimoLogin, DateTimeOffset agora) =>
        ultimoLogin is null || agora - ultimoLogin.Value >= IntervaloUltimoLogin;
}
