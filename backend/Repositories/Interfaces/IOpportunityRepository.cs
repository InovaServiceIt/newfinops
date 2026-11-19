using System.Collections.Generic;
using System.Threading.Tasks;
using Predict.Backend.Models;

namespace Predict.Backend.Repositories.Interfaces
{
    public interface IOpportunityRepository
    {
        Task<IEnumerable<PredictDiscoveryOpportunity>> GetAllAsync(string? clientCode = null, string? status = null, string? search = null);
        Task<PredictDiscoveryOpportunity?> GetByIdAsync(int id);
        Task<PredictDiscoveryOpportunity> AddAsync(PredictDiscoveryOpportunity opportunity);
        Task<PredictDiscoveryOpportunity> UpdateAsync(PredictDiscoveryOpportunity opportunity);
        Task<bool> InactivateAsync(int id);
        Task<IEnumerable<PredictDiscoveryProcess>> GetProcessesAsync();
        Task<IEnumerable<PredictDiscoveryOpportunity>> GetMatrixOpportunitiesAsync(string clientCode);
        Task<bool> BulkUpdateRankAsync(BulkRankRequest request, string changedBy);
    }
}
