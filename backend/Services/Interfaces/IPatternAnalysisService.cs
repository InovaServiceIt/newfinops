using System.Threading.Tasks;
using Predict.Backend.Models;

namespace Predict.Backend.Services.Interfaces
{
    public interface IPatternAnalysisService
    {
        Task<PatternAnalysisStatusResponse> GetStatusAsync();
        string StartAnalysis(string executor, bool force);
        ProgressJob GetProgress(string jobId);
        Task<System.Collections.Generic.List<ProcessLogDto>> GetLogsAsync(string jobId);
        Task<System.Collections.Generic.List<SuggestionDto>> GetPendingSuggestionsAsync();
        Task<bool> ApproveSuggestionAsync(int id, string approvedBy, string? observation);
        Task<bool> RejectSuggestionAsync(int id, string rejectedBy, string? observation);
        Task<AnalisarPadroesResult> AnalisarPadroesAsync(string? jobId = null, string? executor = "System", bool force = false, System.Diagnostics.Stopwatch? sw = null);
    }
}
