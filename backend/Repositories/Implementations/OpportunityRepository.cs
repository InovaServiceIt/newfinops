using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Models;
using Predict.Backend.Repositories.Interfaces;

namespace Predict.Backend.Repositories.Implementations
{
    public class OpportunityRepository : IOpportunityRepository
    {
        private readonly AppDbContext _context;

        public OpportunityRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PredictDiscoveryOpportunity>> GetAllAsync(string? clientCode = null, string? status = null, string? search = null)
        {
            var query = _context.PredictDiscoveryOpportunities
                .AsQueryable();

            if (!string.IsNullOrEmpty(clientCode))
                query = query.Where(o => o.ClientCode == clientCode);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(o => o.Title.Contains(search) || o.OpportunityCode.Contains(search));

            var opportunities = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            await PopulateClientNamesAsync(opportunities);
            await PopulateCatalogDataAsync(opportunities);
            return opportunities;
        }

        private async Task PopulateCatalogDataAsync(IEnumerable<PredictDiscoveryOpportunity> opportunities)
        {
            var withCatalog = opportunities.Where(o => o.CatalogId.HasValue).ToList();
            if (!withCatalog.Any()) return;

            var catalogIds = withCatalog.Select(o => o.CatalogId!.Value).Distinct().ToList();

            // Buscar os dados do catálogo via SQL raw para evitar dependência de EF na tabela
            var conn = _context.Database.GetDbConnection();
            bool opened = false;
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
                opened = true;
            }

            try
            {
                var catalogMap = new Dictionary<int, (string Activity, string AgentAction, string Classification)>();

                using (var cmd = conn.CreateCommand())
                {
                    var ids = string.Join(",", catalogIds);
                    cmd.CommandText = $@"
                        SELECT id, activity, agent_action, classification
                        FROM public.predict_service_catalog
                        WHERE id IN ({ids}) AND is_active = TRUE";

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        catalogMap[reader.GetInt32(0)] = (
                            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                        );
                    }
                }

                foreach (var op in withCatalog)
                {
                    if (catalogMap.TryGetValue(op.CatalogId!.Value, out var data))
                    {
                        op.CatalogActivity = data.Activity;
                        op.CatalogAgentAction = data.AgentAction;
                        op.CatalogClassification = data.Classification;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha ao popular dados do catálogo: {ex.Message}");
            }
            finally
            {
                if (opened) await conn.CloseAsync();
            }
        }

        private async Task PopulateClientNamesAsync(IEnumerable<PredictDiscoveryOpportunity> opportunities)
        {
            if (!opportunities.Any()) return;

            var clientCodes = opportunities.Select(o => o.ClientCode)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .ToList();

            Console.WriteLine($"[DEBUG] Tentando buscar nomes para os códigos: {string.Join(", ", clientCodes)}");

            var clientIds = clientCodes
                .Select(c => int.TryParse(c, out var id) ? (int?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (!clientIds.Any()) 
            {
                Console.WriteLine("[DEBUG] Nenhum código numérico encontrado para busca.");
                return;
            }

            try 
            {
                // TENTATIVA: Buscar pelo IdContrato4Biz (confirmado via print do DB)
                var clientMap = await _context.IntegrationEntities
                    .Where(e => e.IdContrato4Biz.HasValue && clientIds.Contains(e.IdContrato4Biz.Value))
                    .ToDictionaryAsync(e => e.IdContrato4Biz!.Value.ToString(), e => e.Nome);

                Console.WriteLine($"[DEBUG] Nomes encontrados pelo Contrato4Biz: {clientMap.Count}");
                
                foreach (var o in opportunities)
                {
                    // Tratamento especial para o código de teste
                    if (o.ClientCode == "SERVICE_IT_01")
                    {
                        o.ClientName = "Service IT (Test)";
                        continue;
                    }

                    if (clientMap.TryGetValue(o.ClientCode, out var name))
                    {
                        o.ClientName = name;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erro ao buscar nomes na foreign table: {ex.Message}");
            }
        }

        public async Task<PredictDiscoveryOpportunity?> GetByIdAsync(int id)
        {
            var opportunity = await _context.PredictDiscoveryOpportunities
                .Include(o => o.Process)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (opportunity != null)
                await PopulateCatalogDataAsync(new[] { opportunity });
            return opportunity;
        }

        public async Task<PredictDiscoveryOpportunity> AddAsync(PredictDiscoveryOpportunity opportunity)
        {
            _context.PredictDiscoveryOpportunities.Add(opportunity);
            await _context.SaveChangesAsync();
            return opportunity;
        }

        public async Task<PredictDiscoveryOpportunity> UpdateAsync(PredictDiscoveryOpportunity opportunity)
        {
            _context.Entry(opportunity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return opportunity;
        }

        public async Task<bool> InactivateAsync(int id)
        {
            var opportunity = await _context.PredictDiscoveryOpportunities.FindAsync(id);
            if (opportunity == null) return false;

            opportunity.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<PredictDiscoveryProcess>> GetProcessesAsync()
        {
            try {
                return await _context.PredictDiscoveryProcesses.Where(p => p.IsActive).ToListAsync();
            } catch (Exception ex) {
                Console.WriteLine($"[ERROR] Falha ao listar processos: {ex.Message}. Ignorando erro para estabilizar página.");
                return new List<PredictDiscoveryProcess>();
            }
        }

        public async Task<IEnumerable<PredictDiscoveryOpportunity>> GetMatrixOpportunitiesAsync(string clientCode)
        {
            Console.WriteLine($"[DEBUG] Buscando oportunidades para matriz. Client: {clientCode}");
            
            // Busca primeiro todos os candidatos ativos ordenando por Score mais alto
            var allCandidates = await _context.PredictDiscoveryOpportunities
                .Where(o => o.ClientCode == clientCode && 
                            (o.Status == "new" || o.Status == "Identificada") && 
                            o.ConvertedToBuilder == false && 
                            o.IsActive == true)
                .OrderByDescending(o => o.PriorityScore)
                .ThenBy(o => o.PriorityRank)
                .ToListAsync();

            // Deduplica em memória (mais seguro para o EF Core) e pega o TOP 10
            var results = allCandidates
                .GroupBy(o => new { o.ClientCode, o.Title, o.SourceReference })
                .Select(g => g.First())
                .Take(10)
                .ToList();
            
            await PopulateClientNamesAsync(results);
            await PopulateCatalogDataAsync(results);
            
            Console.WriteLine($"[DEBUG] Oportunidades encontradas após deduplicação: {results.Count}");
            return results;
        }

        public async Task<bool> BulkUpdateRankAsync(BulkRankRequest request, string changedBy)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var update in request.Updates)
                {
                    var opportunity = await _context.PredictDiscoveryOpportunities.FindAsync(update.OpportunityId);
                    if (opportunity != null)
                    {
                        // 1. Audit score override if applicable
                        if (update.ScoreOverridden)
                        {
                            _context.AuditHistories.Add(new PredictAuditHistory
                            {
                                ModuleName = "predict_discovery",
                                EntityType = "opportunity",
                                EntityId = opportunity.Id.ToString(),
                                EventType = "score_override",
                                FieldName = "priority_score",
                                OldValue = update.OldScore?.ToString(),
                                NewValue = update.PriorityScore.ToString(),
                                Justification = update.OverrideJustification,
                                ChangedBy = changedBy
                            });
                        }

                        // 2. Log decision
                        _context.DecisionLogs.Add(new PredictDecisionLog
                        {
                            ModuleName = "predict_discovery",
                            EntityType = "opportunity",
                            EntityId = opportunity.Id.ToString(),
                            DecisionType = "prioritization",
                            Rationale = update.ScoreOverridden ? update.OverrideJustification ?? "Priorizado via matriz" : "Priorizado via matriz de priorização",
                            ContextPayload = request.ContextPayload,
                            DecidedBy = changedBy
                        });

                        // 3. Update opportunity
                        opportunity.PriorityRank = update.PriorityRank;
                        opportunity.PriorityScore = update.PriorityScore;
                        opportunity.Status = update.Status;
                        opportunity.UpdatedAt = DateTime.UtcNow;
                        opportunity.UpdatedBy = changedBy;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
