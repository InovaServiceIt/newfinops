using System.Threading;
using System.Threading.Tasks;

namespace Predict.Backend.Services.Interfaces
{
    public interface IServiceCatalogEmbeddingService
    {
        /// <summary>
        /// Gera embeddings para todas as atividades do predict_service_catalog
        /// que ainda não possuem vetor (embedding IS NULL AND is_active = TRUE).
        /// Retorna a quantidade de atividades indexadas nesta execução.
        /// </summary>
        Task<int> IndexarCatalogoAsync(string jobId, CancellationToken ct = default);
    }
}
