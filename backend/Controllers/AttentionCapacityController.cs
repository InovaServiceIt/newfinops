using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Models;

namespace Predict.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/attention")]
    public class AttentionCapacityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AttentionCapacityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("rules")]
        public async Task<ActionResult<IEnumerable<object>>> GetRules()
        {
            try
            {
                var rules = await _context.AssignmentRules
                    .Where(r => r.Level == "R4" && r.IsActive)
                    .OrderBy(r => r.SortOrder)
                    .ToListAsync();

                // Formatar para incluir maxSlots parsed para facilitar no frontend
                var result = rules.Select(r => new
                {
                    r.Id,
                    r.Level,
                    r.Priority,
                    r.Description,
                    r.Criteria,
                    r.SortOrder,
                    r.GroupKey,
                    MaxSlots = ParseMaxSlots(r.Criteria)
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("rules")]
        public async Task<IActionResult> SaveRules([FromBody] List<RuleUpdateDto> updatedRules)
        {
            if (updatedRules == null || updatedRules.Count == 0)
            {
                return BadRequest(new { error = "Nenhuma regra fornecida." });
            }

            try
            {
                foreach (var dto in updatedRules)
                {
                    // UPSERT Lógica: garante que atualize ou crie, mas sem duplicar
                    var existingRule = await _context.AssignmentRules
                        .FirstOrDefaultAsync(r => r.Level == "R4" && r.Priority == dto.Priority);

                    string criteriaJson = $"{{\"maxSlots\": {dto.MaxSlots}}}";

                    if (existingRule != null)
                    {
                        // UPDATE: apenas atualiza o criteria
                        existingRule.Criteria = criteriaJson;
                        existingRule.IsActive = true;
                    }
                    else
                    {
                        // INSERT (Fallback): caso a prioridade não exista no banco
                        string defaultDesc = $"Regra {dto.Priority} padrão - {dto.MaxSlots} slot(s) máximo(s)";
                        string groupKey = (dto.Priority == "ACN" || dto.Priority == "P1") ? "ACN_P1" : dto.Priority;
                        int sortOrder = dto.Priority == "ACN" ? 1 : (dto.Priority == "P1" ? 2 : 3);

                        _context.AssignmentRules.Add(new PredictAttAssignmentRule
                        {
                            Level = "R4",
                            Priority = dto.Priority,
                            Description = defaultDesc,
                            Criteria = criteriaJson,
                            SortOrder = sortOrder,
                            GroupKey = groupKey,
                            IsActive = true
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Configurações de capacidade salvas com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private static int ParseMaxSlots(string criteria)
        {
            if (string.IsNullOrEmpty(criteria)) return 0;
            try
            {
                // Parser manual ultra-leve para evitar dependência complexa de JsonSerializer
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(criteria);
                if (jsonDoc.RootElement.TryGetProperty("maxSlots", out var prop))
                {
                    return prop.GetInt32();
                }
            }
            catch
            {
                // Ignorar erro de parsing e usar fallback
            }
            return 1; // Fallback
        }
    }

    public class RuleUpdateDto
    {
        public string Priority { get; set; } = string.Empty;
        public int MaxSlots { get; set; }
    }
}
