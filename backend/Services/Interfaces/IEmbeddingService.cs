using System.Collections.Generic;
using System.Threading.Tasks;

namespace Predict.Backend.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> GerarAsync(string texto, CancellationToken ct = default);
        Task<List<float[]>> GerarEmbeddingsEmLoteAsync(List<string> textos, CancellationToken ct = default);
    }
}
