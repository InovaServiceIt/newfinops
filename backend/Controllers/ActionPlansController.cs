using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Models;
using Predict.Backend.Services.Interfaces;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActionPlansController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INarrativeService _narrativeService;
        private readonly string _prodConnectionString;

        public ActionPlansController(AppDbContext context, INarrativeService narrativeService, IConfiguration config)
        {
            _context = context;
            _narrativeService = narrativeService;
            _prodConnectionString = config.GetConnectionString("ProductionViews")!;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetActionPlans(
            [FromQuery] string? nomeEntidade,
            [FromQuery] string? prioridade,
            [FromQuery] string? rotuloAcao,
            [FromQuery] string? cdCategoria,
            [FromQuery] string? statusChamado)
        {
            try
            {
                var data = new List<ActionPlan>();
                var clientes = new List<string>();
                var categorias = new List<string>();
                DateTime? dtRegistro = null;
                int total = 0;

                using var conn = new NpgsqlConnection(_prodConnectionString);
                await conn.OpenAsync();

                // Build dynamic WHERE clause
                var conditions = new List<string>();
                var parameters = new List<NpgsqlParameter>();

                if (!string.IsNullOrEmpty(nomeEntidade))
                {
                    conditions.Add("nome_entidade = @nomeEntidade");
                    parameters.Add(new NpgsqlParameter("@nomeEntidade", nomeEntidade));
                }
                if (!string.IsNullOrEmpty(prioridade))
                {
                    conditions.Add("prioridade_sugerida = @prioridade");
                    parameters.Add(new NpgsqlParameter("@prioridade", prioridade));
                }
                if (!string.IsNullOrEmpty(rotuloAcao))
                {
                    conditions.Add("rotulo_acao = @rotuloAcao");
                    parameters.Add(new NpgsqlParameter("@rotuloAcao", rotuloAcao));
                }
                if (!string.IsNullOrEmpty(cdCategoria))
                {
                    conditions.Add("cd_categoria = @cdCategoria");
                    parameters.Add(new NpgsqlParameter("@cdCategoria", cdCategoria));
                }
                if (statusChamado == "sem_chamado")
                    conditions.Add("nu_requisicao_itsm IS NULL");
                if (statusChamado == "com_chamado")
                    conditions.Add("nu_requisicao_itsm IS NOT NULL");

                var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

                // Main query - deduplicate by nm_host using DISTINCT ON
                var sql = $@"
                    SELECT * FROM (
                        SELECT DISTINCT ON (nm_host) id_plano, id_contrato, nome_entidade, ranking_hostname, nm_host, nm_completo,
                               descricao_chave, description_exemplo, rotulo_acao, tx_acao_inicial_suger,
                               prioridade_sugerida, justificativa_resumida, titulo_melhoria, score_hostname,
                               qt_eventos, pct_volume_contrato, dias_com_evento, qt_disaster, qt_high,
                               qt_average, qt_warning, pct_graves, pct_nao_graves, ultima_ocorrencia,
                               dt_inicio_analise, dt_fim_analise, dt_registro_sistema, nu_requisicao_itsm,
                               cd_agente, cd_categoria, nu_evm
                        FROM vw_predict_plano_acao_hostname
                        {whereClause}
                        ORDER BY nm_host, ranking_hostname ASC NULLS LAST
                    ) t
                    ORDER BY ranking_hostname ASC NULLS LAST
                ";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    foreach (var p in parameters) cmd.Parameters.Add(p);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        data.Add(new ActionPlan
                        {
                            IdPlano = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            IdContrato = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                            NomeEntidade = reader.IsDBNull(2) ? null : reader.GetString(2),
                            RankingHostname = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            NmHost = reader.IsDBNull(4) ? null : reader.GetString(4),
                            NmCompleto = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DescricaoChave = reader.IsDBNull(6) ? null : reader.GetString(6),
                            DescriptionExemplo = reader.IsDBNull(7) ? null : reader.GetString(7),
                            RotuloAcao = reader.IsDBNull(8) ? null : reader.GetString(8),
                            TxAcaoInicialSuger = reader.IsDBNull(9) ? null : reader.GetString(9),
                            PrioridadeSugerida = reader.IsDBNull(10) ? null : reader.GetString(10),
                            JustificativaResumida = reader.IsDBNull(11) ? null : reader.GetString(11),
                            TituloMelhoria = reader.IsDBNull(12) ? null : reader.GetString(12),
                            ScoreHostname = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                            QtEventos = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            PctVolumeContrato = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                            DiasComEvento = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                            QtDisaster = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                            QtHigh = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                            QtAverage = reader.IsDBNull(19) ? null : reader.GetInt32(19),
                            QtWarning = reader.IsDBNull(20) ? null : reader.GetInt32(20),
                            PctGraves = reader.IsDBNull(21) ? null : reader.GetDecimal(21),
                            PctNaoGraves = reader.IsDBNull(22) ? null : reader.GetDecimal(22),
                            UltimaOcorrencia = reader.IsDBNull(23) ? null : reader.GetDateTime(23),
                            DtInicioAnalise = reader.IsDBNull(24) ? null : reader.GetDateTime(24),
                            DtFimAnalise = reader.IsDBNull(25) ? null : reader.GetDateTime(25),
                            DtRegistroSistema = reader.IsDBNull(26) ? null : reader.GetDateTime(26),
                            NuRequisicaoItsm = reader.IsDBNull(27) ? null : reader.GetInt32(27),
                            CdAgente = reader.IsDBNull(28) ? null : reader.GetString(28),
                            CdCategoria = reader.IsDBNull(29) ? null : reader.GetString(29),
                            NuEvm = reader.IsDBNull(30) ? null : reader.GetInt64(30)
                        });
                    }
                }

                total = data.Count;

                // Get distinct clients 
                using (var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT nome_entidade 
                    FROM vw_predict_plano_acao_hostname 
                    WHERE nome_entidade IS NOT NULL 
                    ORDER BY nome_entidade", conn))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        clientes.Add(reader.GetString(0));
                }

                // Get distinct categories
                using (var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT cd_categoria 
                    FROM vw_predict_plano_acao_hostname 
                    WHERE cd_categoria IS NOT NULL 
                    ORDER BY cd_categoria", conn))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        categorias.Add(reader.GetString(0));
                }

                // Get latest date
                using (var cmd = new NpgsqlCommand(@"
                    SELECT MAX(dt_registro_sistema) 
                    FROM vw_predict_plano_acao_hostname", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        dtRegistro = (DateTime)result;
                }

                // Fetch narratives from Service_Infra_DB_DEV (local context)
                var ids = data.Select(d => d.IdPlano).ToList();
                Dictionary<int, ActionPlanNarrative> narratives;
                try
                {
                    narratives = await _context.ActionPlanNarratives
                        .Where(n => ids.Contains(n.IdPlano))
                        .GroupBy(n => n.IdPlano)
                        .ToDictionaryAsync(g => g.Key, g => g.First());
                }
                catch
                {
                    narratives = new Dictionary<int, ActionPlanNarrative>();
                }

                var resultData = data.Select(d =>
                {
                    var narrative = narratives.ContainsKey(d.IdPlano) ? narratives[d.IdPlano] : null;
                    return new
                    {
                        plan = d,
                        narrative = narrative != null ? new
                        {
                            narrative.Id,
                            narrative.IdPlano,
                            narrative.Problema,
                            narrative.Impacto,
                            Acoes = TryDeserializeAcoes(narrative.Acoes),
                            narrative.Urgencia,
                            narrative.JustificativaUrgencia,
                            narrative.ModeloUtilizado,
                            narrative.DtGeracao,
                            narrative.Status,
                            narrative.Erro
                        } : (object?)null
                    };
                }).ToList();

                return Ok(new
                {
                    total,
                    dtUltimoRegistro = dtRegistro,
                    clientes,
                    categorias,
                    data = resultData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetActionPlan(int id)
        {
            try
            {
                ActionPlan? plan = null;

                using var conn = new NpgsqlConnection(_prodConnectionString);
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(@"
                    SELECT id_plano, id_contrato, nome_entidade, ranking_hostname, nm_host, nm_completo,
                           descricao_chave, description_exemplo, rotulo_acao, tx_acao_inicial_suger,
                           prioridade_sugerida, justificativa_resumida, titulo_melhoria, score_hostname,
                           qt_eventos, pct_volume_contrato, dias_com_evento, qt_disaster, qt_high,
                           qt_average, qt_warning, pct_graves, pct_nao_graves, ultima_ocorrencia,
                           dt_inicio_analise, dt_fim_analise, dt_registro_sistema, nu_requisicao_itsm,
                           cd_agente, cd_categoria, nu_evm
                    FROM vw_predict_plano_acao_hostname
                    WHERE id_plano = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        plan = new ActionPlan
                        {
                            IdPlano = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            IdContrato = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                            NomeEntidade = reader.IsDBNull(2) ? null : reader.GetString(2),
                            RankingHostname = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            NmHost = reader.IsDBNull(4) ? null : reader.GetString(4),
                            NmCompleto = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DescricaoChave = reader.IsDBNull(6) ? null : reader.GetString(6),
                            DescriptionExemplo = reader.IsDBNull(7) ? null : reader.GetString(7),
                            RotuloAcao = reader.IsDBNull(8) ? null : reader.GetString(8),
                            TxAcaoInicialSuger = reader.IsDBNull(9) ? null : reader.GetString(9),
                            PrioridadeSugerida = reader.IsDBNull(10) ? null : reader.GetString(10),
                            JustificativaResumida = reader.IsDBNull(11) ? null : reader.GetString(11),
                            TituloMelhoria = reader.IsDBNull(12) ? null : reader.GetString(12),
                            ScoreHostname = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                            QtEventos = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            PctVolumeContrato = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                            DiasComEvento = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                            QtDisaster = reader.IsDBNull(17) ? null : reader.GetInt32(17),
                            QtHigh = reader.IsDBNull(18) ? null : reader.GetInt32(18),
                            QtAverage = reader.IsDBNull(19) ? null : reader.GetInt32(19),
                            QtWarning = reader.IsDBNull(20) ? null : reader.GetInt32(20),
                            PctGraves = reader.IsDBNull(21) ? null : reader.GetDecimal(21),
                            PctNaoGraves = reader.IsDBNull(22) ? null : reader.GetDecimal(22),
                            UltimaOcorrencia = reader.IsDBNull(23) ? null : reader.GetDateTime(23),
                            DtInicioAnalise = reader.IsDBNull(24) ? null : reader.GetDateTime(24),
                            DtFimAnalise = reader.IsDBNull(25) ? null : reader.GetDateTime(25),
                            DtRegistroSistema = reader.IsDBNull(26) ? null : reader.GetDateTime(26),
                            NuRequisicaoItsm = reader.IsDBNull(27) ? null : reader.GetInt32(27),
                            CdAgente = reader.IsDBNull(28) ? null : reader.GetString(28),
                            CdCategoria = reader.IsDBNull(29) ? null : reader.GetString(29),
                            NuEvm = reader.IsDBNull(30) ? null : reader.GetInt64(30)
                        };
                    }
                }

                if (plan == null) return NotFound();

                var narrative = await _context.ActionPlanNarratives
                    .FirstOrDefaultAsync(n => n.IdPlano == id);

                return Ok(new
                {
                    plan,
                    narrative
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{id}/narrative")]
        public async Task<ActionResult> GenerateNarrative(int id)
        {
            try
            {
                // Get plan from production database
                ActionPlan? plan = null;
                using var conn = new NpgsqlConnection(_prodConnectionString);
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(@"
                    SELECT id_plano, id_contrato, nome_entidade, ranking_hostname, nm_host, nm_completo,
                           descricao_chave, description_exemplo, rotulo_acao, tx_acao_inicial_suger,
                           prioridade_sugerida, justificativa_resumida, titulo_melhoria, score_hostname,
                           qt_eventos, pct_volume_contrato, dias_com_evento, qt_disaster, qt_high,
                           qt_average, qt_warning, pct_graves, pct_nao_graves, ultima_ocorrencia,
                           dt_inicio_analise, dt_fim_analise, dt_registro_sistema, nu_requisicao_itsm,
                           cd_agente, cd_categoria, nu_evm
                    FROM vw_predict_plano_acao_hostname
                    WHERE id_plano = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        plan = new ActionPlan
                        {
                            IdPlano = reader.GetInt32(0),
                            NmHost = reader.IsDBNull(4) ? null : reader.GetString(4),
                            NmCompleto = reader.IsDBNull(5) ? null : reader.GetString(5),
                            DescricaoChave = reader.IsDBNull(6) ? null : reader.GetString(6),
                            DescriptionExemplo = reader.IsDBNull(7) ? null : reader.GetString(7),
                            RotuloAcao = reader.IsDBNull(8) ? null : reader.GetString(8),
                            TxAcaoInicialSuger = reader.IsDBNull(9) ? null : reader.GetString(9),
                            PrioridadeSugerida = reader.IsDBNull(10) ? null : reader.GetString(10),
                            JustificativaResumida = reader.IsDBNull(11) ? null : reader.GetString(11),
                            ScoreHostname = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                            QtEventos = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                            DiasComEvento = reader.IsDBNull(16) ? null : reader.GetInt32(16),
                            PctGraves = reader.IsDBNull(21) ? null : reader.GetDecimal(21),
                        };
                    }
                }

                if (plan == null) return NotFound(new { error = "Action plan not found" });

                var narrative = await _narrativeService.GenerateNarrativeHostAsync(plan);

                // Save or Update narrative in Service_Infra_DB_DEV
                var existing = await _context.ActionPlanNarratives
                    .FirstOrDefaultAsync(n => n.IdPlano == id);

                if (existing != null)
                {
                    existing.Problema = narrative.Problema;
                    existing.Impacto = narrative.Impacto;
                    existing.Acoes = narrative.Acoes;
                    existing.Urgencia = narrative.Urgencia;
                    existing.JustificativaUrgencia = narrative.JustificativaUrgencia;
                    existing.DtGeracao = DateTime.UtcNow;
                    existing.Status = narrative.Status;
                    existing.Erro = narrative.Erro;
                }
                else
                {
                    _context.ActionPlanNarratives.Add(narrative);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, narrative });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static List<string>? TryDeserializeAcoes(string? acoes)
        {
            if (string.IsNullOrEmpty(acoes)) return null;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(acoes);
            }
            catch
            {
                return new List<string> { acoes };
            }
        }
    }
}
