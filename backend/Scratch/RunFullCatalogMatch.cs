using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Predict.Backend.Scratch
{
    public class RunFullCatalogMatch
    {
        public static async Task Run(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var connStr = context.Database.GetDbConnection().ConnectionString;

            Console.WriteLine("[MATCH] Iniciando processamento semântico retroativo...");

            using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            // 1. Pegar oportunidades sem catálogo
            var opps = await context.PredictDiscoveryOpportunities
                .Where(o => o.CatalogId == null && o.IsActive)
                .Select(o => new { o.Id, o.Title, o.Summary })
                .ToListAsync();

            Console.WriteLine($"[MATCH] Encontradas {opps.Count} oportunidades para processar.");

            int matches = 0;
            foreach (var opp in opps)
            {
                try 
                {
                    // 2. Gerar embedding em tempo de execução (mesma lógica do PatternAnalysisService)
                    string textToEmbed = $"{opp.Title} | {opp.Summary}";
                    var embedding = await embeddingService.GerarAsync(textToEmbed);
                    
                    if (embedding == null || embedding.Length == 0) continue;

                    string embStr = "[" + string.Join(",", embedding.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

                    // 3. Tentar encontrar match no catálogo via function SQL
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE predict_discovery_opportunities o
                        SET 
                            catalog_id = m.catalog_id,
                            catalog_score = m.score,
                            catalog_matched_at = NOW()
                        FROM (
                            SELECT catalog_id, score
                            FROM find_catalog_match(@emb::vector(1024), 0.75, 1)
                        ) m
                        WHERE o.opportunity_id = @id";
                    
                    cmd.Parameters.AddWithValue("emb", embStr);
                    cmd.Parameters.AddWithValue("id", opp.Id);
                    
                    int affected = await cmd.ExecuteNonQueryAsync();
                    if (affected > 0) matches++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MATCH] Erro ao processar oportunidade {opp.Id}: {ex.Message}");
                }
            }

            Console.WriteLine($"[MATCH] Processamento concluído. {matches} oportunidades vinculadas ao catálogo.");
        }
    }
}
