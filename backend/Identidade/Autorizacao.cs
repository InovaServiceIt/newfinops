using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace Finops.Backend.Identidade;

/// <summary>Nomes das políticas de autorização por perfil (perfil lido do banco).</summary>
public static class Politicas
{
    public const string Administrador = "Administrador";
    public const string Analista = "Analista";
    public const string Autorizado = "Autorizado";

    public static void Registrar(AuthorizationOptions options)
    {
        options.AddPolicy(Administrador, p => p.RequireAuthenticatedUser().AddRequirements(new PerfilRequirement(Perfis.Administrador)));
        options.AddPolicy(Analista, p => p.RequireAuthenticatedUser().AddRequirements(new PerfilRequirement(Perfis.Administrador, Perfis.AnalistaFinOps)));
        options.AddPolicy(Autorizado, p => p.RequireAuthenticatedUser().AddRequirements(new PerfilRequirement(Perfis.Todos.ToArray())));
    }
}

public sealed class PerfilRequirement(params string[] perfis) : IAuthorizationRequirement
{
    public IReadOnlySet<string> Perfis { get; } = perfis.ToHashSet();
}

/// <summary>Valida o perfil do usuário ativo no banco (não usa claims/app roles do Entra).</summary>
public sealed class PerfilHandler : AuthorizationHandler<PerfilRequirement>
{
    private readonly UsuarioAtualServico _usuarios;

    public PerfilHandler(UsuarioAtualServico usuarios) => _usuarios = usuarios;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PerfilRequirement requirement)
    {
        try
        {
            var usuario = await _usuarios.ObterAsync(CancellationToken.None);
            if (usuario is not null && requirement.Perfis.Contains(usuario.Perfil))
                context.Succeed(requirement);
        }
        catch (CloudVistaNaoConfiguradoException)
        {
            // Sem banco não há perfil: a requisição falha (o middleware já responde 503 antes).
        }
    }
}

/// <summary>
/// Barreira para /api/*: usuário autenticado que não existe ou está inativo no CloudVista
/// recebe 403 { codigo: "USUARIO_NAO_AUTORIZADO" }. Exceções: /api/health* e /api/auth/me.
/// Sem banco CloudVista configurado: 503 { erro }.
/// Requisições não autenticadas seguem para o filtro global (401).
/// </summary>
public sealed class UsuarioAutorizadoMiddleware
{
    public const string CodigoNaoAutorizado = "USUARIO_NAO_AUTORIZADO";

    private readonly RequestDelegate _next;

    public UsuarioAutorizadoMiddleware(RequestDelegate next) => _next = next;

    public static bool RotaLivre(PathString path) =>
        !path.StartsWithSegments("/api")
        || path.StartsWithSegments("/api/health")
        || path.StartsWithSegments("/api/auth/me");

    public async Task InvokeAsync(HttpContext context, UsuarioAtualServico usuarios, ILogger<UsuarioAutorizadoMiddleware> logger)
    {
        if (RotaLivre(context.Request.Path) || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        UsuarioAtual? usuario;
        try
        {
            usuario = await usuarios.ObterAsync(context.RequestAborted);
        }
        catch (CloudVistaNaoConfiguradoException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { erro = "Banco CloudVista não configurado." });
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao resolver o usuário CloudVista.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { erro = "Não foi possível validar o acesso. Tente novamente em instantes." });
            return;
        }

        if (usuario is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { codigo = CodigoNaoAutorizado });
            return;
        }

        await _next(context);
    }
}
