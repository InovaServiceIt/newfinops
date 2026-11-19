using Microsoft.AspNetCore.Mvc;
using Predict.Backend.Models;
using Predict.Backend.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/discovery")]
    public class DiscoveryController : ControllerBase
    {
        private readonly IPatternAnalysisService _patternService;

        public DiscoveryController(IPatternAnalysisService patternService)
        {
            _patternService = patternService;
        }

        [HttpPost("analisar-padroes")]
        public async Task<IActionResult> AnalisarPadroes()
        {
            try
            {
                var result = await _patternService.AnalisarPadroesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { erro = ex.Message });
            }
        }
    }
}
