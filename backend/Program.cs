using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// ── Autenticação: SSO Entra ID (OIDC + cookie) ───────────────────────────────
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Em /api, o desafio (challenge/forbid) do OIDC é encaminhado ao esquema de cookie,
// que responde 401/403 em vez de redirecionar para o Entra. O encaminhamento acontece
// antes de o OIDC buscar a configuração do Entra, então não há 302 nem chamada externa.
// As demais rotas seguem o fluxo OIDC normal.
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Path.StartsWithSegments("/api")
            ? CookieAuthenticationDefaults.AuthenticationScheme
            : null;
});

// Chamadas de API sem sessão recebem 401 e sem permissão recebem 403 (o React redireciona
// para /login); as demais rotas seguem o redirecionamento normal.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// Filtro global: todo controller exige usuário autenticado.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
}).AddMicrosoftIdentityUI();

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Banco finopsdatabase: somente leitura (ConnectionStrings:FinopsDb, opcional) ──
builder.Services.AddSingleton<FinopsDb>();

// ── Visão Geral ──
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Finops.Backend.VisaoGeral.VisaoGeralRepositorio>();
builder.Services.AddSingleton<Finops.Backend.VisaoGeral.VisaoGeralServico>();

// ── CORS: origens via configuração (ex.: Cors__AllowedOrigins="https://a,https://b") ──
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// Instancia já no startup para registrar no log se a conexão com o banco está configurada.
app.Services.GetRequiredService<FinopsDb>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Proxy reverso (Docker/Coolify) ───────────────────────────────────────────
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Força o .NET a aceitar o X-Forwarded-Proto vindo de qualquer rede.
app.Use((context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && !string.IsNullOrEmpty(proto))
    {
        context.Request.Scheme = proto.ToString();
    }
    return next();
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// SPA React servida pelo próprio .NET (build do Vite em wwwroot).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapRazorPages();

// Rotas /api inexistentes retornam 404 (e não o index.html da SPA).
app.MapFallback("/api/{**slug}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
