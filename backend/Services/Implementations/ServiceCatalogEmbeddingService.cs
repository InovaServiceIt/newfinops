using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Predict.Backend.Services.Implementations
{
    /// <summary>
    /// Indexa as atividades do predict_service_catalog gerando embeddings via Ollama.
    /// Texto base: activity + " | " + agent_action  (conforme briefing BU de IA Service IT v1.0)
    /// Dimensão: vector(1024) — modelo qwen3-embedding:0.6b
    /// </summary>
    public class ServiceCatalogEmbeddingService : IServiceCatalogEmbeddingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<ServiceCatalogEmbeddingService> _logger;

        private const int BATCH_SIZE = 100;

        public ServiceCatalogEmbeddingService(
            IServiceScopeFactory scopeFactory,
            IEmbeddingService embeddingService,
            ILogger<ServiceCatalogEmbeddingService> logger)
        {
            _scopeFactory = scopeFactory;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<int> IndexarCatalogoAsync(string jobId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);

            // Contar pendentes
            long totalPendentes = 0;
            using (var cmdCount = conn.CreateCommand())
            {
                cmdCount.CommandText = @"
                    SELECT COUNT(*) 
                    FROM public.predict_service_catalog 
                    WHERE embedding IS NULL AND is_active = TRUE";
                var obj = await cmdCount.ExecuteScalarAsync(ct);
                totalPendentes = obj == DBNull.Value || obj == null ? 0 : Convert.ToInt64(obj);
            }

            if (totalPendentes == 0)
            {
                _logger.LogInformation("[CATALOG] Todos os registros do catálogo já possuem embedding. Nada a indexar.");
                return 0;
            }

            _logger.LogInformation("[CATALOG] Iniciando indexação de {Total} atividades do catálogo.", totalPendentes);

            int indexadas = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Buscar lote de atividades sem embedding
                var lote = new List<(int Id, string Activity, string AgentAction)>();
                using (var cmdRead = conn.CreateCommand())
                {
                    cmdRead.CommandText = @"
                        SELECT id, activity, agent_action 
                        FROM public.predict_service_catalog 
                        WHERE embedding IS NULL AND is_active = TRUE
                        ORDER BY id
                        LIMIT @batch";
                    var p = cmdRead.CreateParameter();
                    p.ParameterName = "batch";
                    p.Value = BATCH_SIZE;
                    cmdRead.Parameters.Add(p);

                    using var reader = await cmdRead.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        lote.Add((
                            reader.GetInt32(0),
                            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                        ));
                    }
                }

                if (lote.Count == 0) break;

                // Montar textos: "activity | agent_action"
                var textos = lote
                    .Select(r => $"{r.Activity} | {r.AgentAction}")
                    .ToList();

                // Gerar embeddings via Ollama (um por request, com semáforo no OllamaEmbeddingService)
                List<float[]> embeddings;
                try
                {
                    embeddings = await _embeddingService.GerarEmbeddingsEmLoteAsync(textos, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CATALOG] Falha ao chamar Ollama para lote iniciando no id={Id}. Interrompendo indexação.", lote[0].Id);
                    // Não propagar — pipeline continua; catalog_id ficará NULL nas oportunidades
                    break;
                }

                // Persistir cada vetor
                for (int i = 0; i < lote.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var item = lote[i];
                    var emb = embeddings[i];

                    if (emb.Length == 0)
                    {
                        _logger.LogWarning("[CATALOG] Embedding vazio para catalog id={Id}. Pulando.", item.Id);
                        continue;
                    }

                    string arrayStr = "[" + string.Join(",",
                        emb.Select(x => x.ToString(CultureInfo.InvariantCulture))) + "]";

                    try
                    {
                        using var cmdUpd = conn.CreateCommand();
                        cmdUpd.CommandText = @"
                            UPDATE public.predict_service_catalog
                            SET embedding = @emb::vector, updated_at = NOW()
                            WHERE id = @id";
                        var p1 = cmdUpd.CreateParameter();
                        p1.ParameterName = "emb";
                        p1.Value = arrayStr;
                        cmdUpd.Parameters.Add(p1);

                        var p2 = cmdUpd.CreateParameter();
                        p2.ParameterName = "id";
                        p2.Value = item.Id;
                        cmdUpd.Parameters.Add(p2);

                        await cmdUpd.ExecuteNonQueryAsync(ct);
                        indexadas++;

                        _logger.LogDebug("[CATALOG] id={Id} indexado ({Atual}/{Total}).",
                            item.Id, indexadas, totalPendentes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[CATALOG] Falha ao persistir embedding para catalog id={Id}.", item.Id);
                    }
                }

                _logger.LogInformation("[CATALOG] Lote concluído. {Indexadas}/{Total} indexadas até agora.", indexadas, totalPendentes);
            }

            _logger.LogInformation("[CATALOG] Indexação concluída. Total indexado nesta execução: {Indexadas}.", indexadas);
            return indexadas;
        }
    }
}
