using Microsoft.AspNetCore.Mvc;
using Predict.Backend.Models;
using Predict.Backend.Services.Interfaces;
using System.Threading.Tasks;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/analise-padroes")]
    public class PatternAnalysisController : ControllerBase
    {
        private readonly IPatternAnalysisService _service;

        public PatternAnalysisController(IPatternAnalysisService service)
        {
            _service = service;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _service.GetStatusAsync();
                return Ok(status);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        [HttpPost("executar")]
        public async Task<IActionResult> Executar([FromBody] ExecutarRequest req)
        {
            var forcar = req?.Forcar ?? false;
            var usuario = "sistema";

            // Durante a estabilização, permitimos executar mesmo que o status sugira bloqueio
            /*
            if (!forcar)
            {
                var status = await _service.GetStatusAsync();
                if (!status.PodeExecutar)
                {
                    status.Bloqueado = true;
                    return Conflict(status);
                }
            }
            */

            var jobId = _service.StartAnalysis(usuario, forcar);
            return Ok(new { jobId });
        }

        [HttpGet("progresso/{jobId}")]
        public IActionResult GetProgress(string jobId)
        {
            var job = _service.GetProgress(jobId);
            if (job == null) return NotFound(new { erro = "Job não encontrado" });
            return Ok(job);
        }

        [HttpGet("logs/{jobId}")]
        public async Task<IActionResult> GetLogs(string jobId)
        {
            var logs = await _service.GetLogsAsync(jobId);
            return Ok(logs);
        }
    }

    public class ExecutarRequest
    {
        public bool Forcar { get; set; }
    }
}
