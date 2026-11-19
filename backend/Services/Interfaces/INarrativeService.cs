using System.Threading.Tasks;
using System.Collections.Generic;
using Predict.Backend.Models;

namespace Predict.Backend.Services.Interfaces
{
    public interface INarrativeService
    {
        Task<ActionPlanNarrative> GenerateNarrativeHostAsync(ActionPlan host);
        Task<List<ActionPlanNarrative>> GenerateNarrativesBatchAsync(List<ActionPlan> hosts);
    }
}
