using System.Threading.Tasks;

namespace Predict.Backend.Services.Interfaces
{
    public interface IAlertsStagingSyncService
    {
        /// <summary>
        /// Sincroniza alertas da origem para o staging local.
        /// </summary>
        /// <param name="jobId">ID do Job para reporte de progresso (opcional)</param>
        Task SyncAsync(string? jobId = null);
    }
}
