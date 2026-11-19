using Microsoft.AspNetCore.Mvc;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Services.Interfaces;
using System.Threading.Tasks;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/analise-padroes")]
    public class ApprovalController : ControllerBase
    {
        private readonly IPatternAnalysisService _analysisService;

        public ApprovalController(IPatternAnalysisService analysisService)
        {
            _analysisService = analysisService;
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions()
        {
            var suggestions = await _analysisService.GetPendingSuggestionsAsync();
            return Ok(suggestions);
        }

        [HttpPost("suggestions/{id}/approve")]
        public async Task<IActionResult> ApproveSuggestion(int id, [FromBody] ApprovalRequest request)
        {
            var success = await _analysisService.ApproveSuggestionAsync(id, request.ApprovedBy, request.Observation);
            if (!success) return NotFound();
            return Ok();
        }

        [HttpPost("suggestions/{id}/reject")]
        public async Task<IActionResult> RejectSuggestion(int id, [FromBody] ApprovalRequest request)
        {
            var success = await _analysisService.RejectSuggestionAsync(id, request.ApprovedBy, request.Observation);
            if (!success) return NotFound();
            return Ok();
        }
    }

    public class ApprovalRequest
    {
        public string ApprovedBy { get; set; } = "sistema";
        public string? Observation { get; set; }
    }
}
