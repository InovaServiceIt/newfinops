using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Models;
using Predict.Backend.Repositories.Interfaces;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OpportunitiesController : ControllerBase
    {
        private readonly IOpportunityRepository _repository;

        public OpportunitiesController(IOpportunityRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PredictDiscoveryOpportunity>>> GetOpportunities([FromQuery] string? clientCode, [FromQuery] string? status, [FromQuery] string? search)
        {
            try 
            {
                var opportunities = await _repository.GetAllAsync(clientCode, status, search);
                
                // Se o EF retornar vazio, vamos tentar entender o porquê via log interno
                if (opportunities == null || !System.Linq.Enumerable.Any(opportunities))
                {
                    System.Console.WriteLine($"[DIAG] EF retornou 0 registros para filtros: client={clientCode}, status={status}, search={search}");
                }
                
                return Ok(opportunities);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[DIAG] Erro no controller de oportunidades: {ex.Message}");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("debug-raw-sql")]
        public async Task<IActionResult> GetRawSqlDebug()
        {
            var results = new System.Collections.Generic.List<object>();
            using (var scope = HttpContext.RequestServices.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Predict.Backend.Infrastructure.Data.AppDbContext>();
                var conn = db.Database.GetDbConnection();
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT opportunity_id, client_code, opportunity_code, is_active, status FROM public.predict_discovery_opportunities LIMIT 50";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new { 
                        id = reader[0], 
                        clientCode = reader[1], 
                        opportunityCode = reader[2], 
                        isActive = reader[3], 
                        status = reader[4] 
                    });
                }
            }
            return Ok(results);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PredictDiscoveryOpportunity>> GetOpportunity(int id)
        {
            var opportunity = await _repository.GetByIdAsync(id);
            if (opportunity == null) return NotFound();
            return Ok(opportunity);
        }

        [HttpPost]
        public async Task<ActionResult<PredictDiscoveryOpportunity>> CreateOpportunity(PredictDiscoveryOpportunity opportunity)
        {
            var created = await _repository.AddAsync(opportunity);
            return CreatedAtAction(nameof(GetOpportunity), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOpportunity(int id, PredictDiscoveryOpportunity opportunity)
        {
            if (id != opportunity.Id) return BadRequest();
            await _repository.UpdateAsync(opportunity);
            return NoContent();
        }

        [HttpPatch("{id}/inactivate")]
        public async Task<IActionResult> InactivateOpportunity(int id)
        {
            var result = await _repository.InactivateAsync(id);
            if (!result) return NotFound();
            return NoContent();
        }

        [HttpGet("processes")]
        public async Task<ActionResult<IEnumerable<PredictDiscoveryProcess>>> GetProcesses()
        {
            var processes = await _repository.GetProcessesAsync();
            return Ok(processes);
        }

        [HttpGet("matrix")]
        public async Task<ActionResult<IEnumerable<PredictDiscoveryOpportunity>>> GetMatrix([FromQuery] string clientCode)
        {
            if (string.IsNullOrEmpty(clientCode)) return BadRequest("clientCode is required");
            var opportunities = await _repository.GetMatrixOpportunitiesAsync(clientCode);
            return Ok(opportunities);
        }

        [HttpPatch("bulk-rank")]
        public async Task<IActionResult> BulkUpdateRank([FromBody] BulkRankRequest request)
        {
            if (request == null || request.Updates == null) return BadRequest("Invalid request");
            
            // In a real app, we'd get the user from the token
            string changedBy = "system_user"; 
            
            var result = await _repository.BulkUpdateRankAsync(request, changedBy);
            if (!result) return StatusCode(500, "Failed to update ranking");
            
            return Ok(new { message = "Prioritization confirmed successfully" });
        }
    }
}
