// v1.0.4 - Force redeploy after UI header navigation cleanup
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => {
    options.Events.OnRedirectToLogin = context => {
        if (context.Request.Path.StartsWithSegments("/api")) {
            context.Response.StatusCode = 401;
        } else {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(); // Removido FallbackPolicy global para permitir que o React gerencie o login customizado

builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
}).AddMicrosoftIdentityUI();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRazorPages();

// Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<Predict.Backend.Repositories.Interfaces.IOpportunityRepository, Predict.Backend.Repositories.Implementations.OpportunityRepository>();

// IA Services
builder.Services.AddHttpClient<Predict.Backend.Services.Interfaces.INarrativeService, Predict.Backend.Services.Implementations.NarrativeService>();
builder.Services.AddScoped<Predict.Backend.Services.Interfaces.INarrativeService, Predict.Backend.Services.Implementations.NarrativeService>();

builder.Services.AddHttpClient<Predict.Backend.Services.Interfaces.IEmbeddingService, Predict.Backend.Services.Implementations.OllamaEmbeddingService>();
builder.Services.AddScoped<Predict.Backend.Services.Interfaces.IEmbeddingService, Predict.Backend.Services.Implementations.OllamaEmbeddingService>();

// Catálogo Semântico
builder.Services.AddScoped<Predict.Backend.Services.Interfaces.IServiceCatalogEmbeddingService, Predict.Backend.Services.Implementations.ServiceCatalogEmbeddingService>();

builder.Services.AddHttpClient<Predict.Backend.Services.Interfaces.IPatternAnalysisService, Predict.Backend.Services.Implementations.PatternAnalysisService>();
builder.Services.AddScoped<Predict.Backend.Services.Interfaces.IPatternAnalysisService, Predict.Backend.Services.Implementations.PatternAnalysisService>();
builder.Services.AddScoped<Predict.Backend.Services.Interfaces.IAlertsStagingSyncService, Predict.Backend.Services.Implementations.AlertsStagingSyncService>();
builder.Services.AddHostedService<Predict.Backend.Services.NarrativeGenerationJob>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder =>
        {
            builder.WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:3002",
                "http://localhost:3003",
                "http://localhost:5174",
                "http://localhost:5175",
                "http://5.161.109.224",
                "https://predict.service.com.br",
                "http://predict.service.com.br"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
});

var app = builder.Build();

// Seed data if requested
if (args.Contains("--seed-dev"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.SeedMockTable.Run(scope.ServiceProvider);
    }
}

if (args.Contains("--inspect"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.InspectSchema.Run(scope.ServiceProvider);
    }
}

if (args.Contains("--fix-tables"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.FixTables.Run(scope.ServiceProvider);
    }
    return;
}

if (args.Contains("--check-view"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.CheckViewStatus.Run(scope.ServiceProvider);
    }
    return; // Exit after check, don't start web server
}

if (args.Contains("--restore-view"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.RestoreRealView.Run(scope.ServiceProvider);
    }
    return;
}

if (args.Contains("--read-prod"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.ReadProductionView.Run(scope.ServiceProvider);
    }
    return;
}

if (args.Contains("--check-pgvector"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.CheckPgVector.Run(scope.ServiceProvider);
    }
    return;
}

if (args.Contains("--run-catalog-match"))
{
    using (var scope = app.Services.CreateScope())
    {
        await Predict.Backend.Scratch.RunFullCatalogMatch.Run(scope.ServiceProvider);
    }
    return;
}

if (args.Contains("--onda2-migration"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Predict.Backend.Scratch.Onda2Migration.Run(db);
    }
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// AUTO-FIX: Garantir que registros órfãos do Discovery sejam ativados na subida da app
using (var scope = app.Services.CreateScope())
{
    try 
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.predict_discovery_opportunities SET is_active = true WHERE is_active IS NULL OR is_active = false";
        int rows = await cmd.ExecuteNonQueryAsync();
        if (rows > 0) Console.WriteLine($"[AUTO-FIX] {rows} oportunidades Discovery ativadas com sucesso.");

        // FIX SCHEMA FOR MATRIX
        Console.WriteLine("[AUTO-FIX] Verificando schema da Matriz...");
        
        // Renomear colunas se necessário
        string[] tables = { "predict_discovery_opportunities", "predict_discovery_processes", "predict_discovery_tasks", "predict_discovery_pain_points" };
        string[] idCols = { "opportunity_id", "process_id", "task_id", "pain_point_id" };
        for (int i = 0; i < tables.Length; i++) {
            try {
                cmd.CommandText = $"ALTER TABLE {tables[i]} RENAME COLUMN id TO {idCols[i]}";
                await cmd.ExecuteNonQueryAsync();
            } catch { /* Ignorar se já renomeado */ }
        }

        // Add origin_module
        try {
            cmd.CommandText = "ALTER TABLE predict_discovery_opportunities ADD COLUMN IF NOT EXISTS origin_module TEXT DEFAULT 'predict_radar'";
            await cmd.ExecuteNonQueryAsync();
        } catch { }

        // ===== SPRINT: Discovery ↔ Service Catalog (Etapa 7) =====
        Console.WriteLine("[AUTO-FIX] Aplicando migrations do Service Catalog (Etapa 7)...");

        // 1. Coluna embedding no catálogo (vector 1024)
        try {
            cmd.CommandText = @"ALTER TABLE public.predict_service_catalog
                ADD COLUMN IF NOT EXISTS embedding vector(1024),
                ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT TRUE";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[AUTO-FIX] Colunas embedding/is_active adicionadas em predict_service_catalog.");
        } catch (Exception ex) { Console.WriteLine($"[AUTO-FIX] embedding catalog: {ex.Message}"); }

        // 2. Índice HNSW para busca semântica no catálogo
        try {
            cmd.CommandText = @"CREATE INDEX IF NOT EXISTS idx_psc_embedding
                ON public.predict_service_catalog
                USING hnsw (embedding vector_cosine_ops)
                WITH (m = 16, ef_construction = 64)";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[AUTO-FIX] Índice HNSW criado em predict_service_catalog.");
        } catch (Exception ex) { Console.WriteLine($"[AUTO-FIX] idx HNSW: {ex.Message}"); }

        // 3. Colunas de match nas oportunidades
        try {
            cmd.CommandText = @"ALTER TABLE public.predict_discovery_opportunities
                ADD COLUMN IF NOT EXISTS catalog_id INTEGER REFERENCES public.predict_service_catalog(id),
                ADD COLUMN IF NOT EXISTS catalog_score FLOAT,
                ADD COLUMN IF NOT EXISTS catalog_matched_at TIMESTAMPTZ";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[AUTO-FIX] Colunas catalog_id/score/matched_at adicionadas em predict_discovery_opportunities.");
        } catch (Exception ex) { Console.WriteLine($"[AUTO-FIX] catalog cols: {ex.Message}"); }

        // 4. Função find_catalog_match com vector(1024)
        try {
            cmd.CommandText = @"
                CREATE OR REPLACE FUNCTION public.find_catalog_match(
                    p_embedding     vector(1024),
                    p_threshold     float DEFAULT 0.80,
                    p_limit         int   DEFAULT 1
                )
                RETURNS TABLE (
                    catalog_id      int,
                    activity        text,
                    agent_action    text,
                    classification  varchar,
                    score           float
                )
                LANGUAGE sql STABLE AS $$
                    SELECT
                        id,
                        activity,
                        agent_action,
                        classification,
                        1 - (embedding <=> p_embedding) AS score
                    FROM public.predict_service_catalog
                    WHERE is_active = TRUE
                      AND embedding IS NOT NULL
                      AND 1 - (embedding <=> p_embedding) >= p_threshold
                    ORDER BY embedding <=> p_embedding
                    LIMIT p_limit;
                $$";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[AUTO-FIX] Função find_catalog_match criada/atualizada com sucesso.");
        } catch (Exception ex) { Console.WriteLine($"[AUTO-FIX] find_catalog_match: {ex.Message}"); }

        // 5. Rodar Match Automático no Startup (Retroativo) - Em BACKGROUND para não travar o boot
        _ = Task.Run(async () => {
            try {
                using (var catalogScope = app.Services.CreateScope()) {
                    await Predict.Backend.Scratch.RunFullCatalogMatch.Run(catalogScope.ServiceProvider);
                }
            } catch (Exception ex) { Console.WriteLine($"[AUTO-FIX] run-match background error: {ex.Message}"); }
        });

        // Create new audit tables
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS predict_audit_history (
                id SERIAL PRIMARY KEY,
                module_name VARCHAR(50) NOT NULL,
                entity_type VARCHAR(50) NOT NULL,
                entity_id TEXT NOT NULL,
                event_type VARCHAR(50) NOT NULL,
                field_name VARCHAR(50),
                old_value TEXT,
                new_value TEXT,
                justification TEXT,
                changed_by VARCHAR(100) NOT NULL,
                timestamp TIMESTAMPTZ DEFAULT NOW()
            );
            CREATE TABLE IF NOT EXISTS predict_decision_log (
                id SERIAL PRIMARY KEY,
                module_name VARCHAR(50) NOT NULL,
                entity_type VARCHAR(50) NOT NULL,
                entity_id TEXT NOT NULL,
                decision_type VARCHAR(50) NOT NULL,
                rationale TEXT NOT NULL,
                context_payload JSONB,
                decided_by VARCHAR(100) NOT NULL,
                timestamp TIMESTAMPTZ DEFAULT NOW()
            );";
        await cmd.ExecuteNonQueryAsync();

        // Seed data if empty or no SERVICE_IT_01 records
        cmd.CommandText = "SELECT COUNT(*) FROM predict_discovery_opportunities WHERE client_code = 'SERVICE_IT_01'";
        var countObj = await cmd.ExecuteScalarAsync();
        long count = countObj != null ? Convert.ToInt64(countObj) : 0;
        
        if (count == 0) {
            Console.WriteLine("[AUTO-FIX] Semeando dados de teste para SERVICE_IT_01...");
            cmd.CommandText = @"
                INSERT INTO predict_discovery_opportunities 
                (client_code, opportunity_code, title, summary, expected_gain_classification, implementation_effort_classification, operational_risk_classification, priority_score, priority_rank, status, is_active, created_at, origin_module, converted_to_builder)
                VALUES 
                ('SERVICE_IT_01', 'OPT-001', 'Automação de Reset de Senha SAP', 'Reduzir chamados de N1', 'high', 'low', 'low', 95, 1, 'new', true, NOW(), 'predict_radar', false),
                ('SERVICE_IT_01', 'OPT-002', 'Provisionamento de VM Azure', 'Agilizar entrega de infra', 'medium', 'medium', 'medium', 65, 2, 'new', true, NOW(), 'predict_radar', false),
                ('SERVICE_IT_01', 'OPT-003', 'Backup de Bases SQL Críticas', 'Garantir compliance', 'high', 'high', 'high', 45, 3, 'new', true, NOW(), 'predict_radar', false),
                ('SERVICE_IT_01', 'OPT-004', 'Limpeza de Logs de Aplicação', 'Manutenção preventiva', 'low', 'low', 'low', 30, 4, 'new', true, NOW(), 'predict_radar', false);
            ";
            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine("[AUTO-FIX] Dados de teste para matriz semeados com sucesso.");
        } else {
            Console.WriteLine($"[AUTO-FIX] {count} registros encontrados para SERVICE_IT_01. Pulando seed.");
        }
    }
    catch (Exception ex) 
    {
        Console.WriteLine($"[AUTO-FIX] Erro durante inicialização: {ex.Message}");
    }
}

// Forçar o .NET a aceitar os cabeçalhos de proxy em qualquer rede (necessário para Docker/Coolify)
app.Use((context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto))
    {
        context.Request.Scheme = proto;
    }
    return next();
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.MapControllers();
app.MapRazorPages();

app.Run();
