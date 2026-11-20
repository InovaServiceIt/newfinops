using System.Diagnostics;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly FinopsDb _db;
        private readonly ILogger<HealthController> _logger;

        public HealthController(FinopsDb db, ILogger<HealthController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Health anônimo da aplicação (não toca no banco).</summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Health do banco finopsdatabase (autenticado pelo filtro global).
        /// Não expõe host, usuário nem mensagem de erro; o detalhe fica só no log do servidor.
        /// </summary>
        [HttpGet("db")]
        public async Task<IActionResult> GetDb(CancellationToken cancellationToken)
        {
            if (!_db.IsConfigured)
            {
                return Ok(new { status = "nao_configurado", latencyMs = (long?)null, database = (string?)null, serverVersion = (string?)null, readOnly = (bool?)null });
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await using var conn = await _db.OpenConnectionAsync(cancellationToken);

                // Só leitura: nome do banco e versão do servidor.
                string database, serverVersion;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT current_database(), current_setting('server_version')";
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    await reader.ReadAsync(cancellationToken);
                    database = reader.GetString(0);
                    serverVersion = reader.GetString(1);
                }

                // Prova de read-only por leitura: valor efetivo do parâmetro da sessão (esperado "on").
                bool readOnly;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SHOW default_transaction_read_only";
                    readOnly = (await cmd.ExecuteScalarAsync(cancellationToken)) as string == "on";
                }
                sw.Stop();

                return Ok(new { status = "ok", latencyMs = sw.ElapsedMilliseconds, database, serverVersion, readOnly });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Falha no health check do finopsdatabase.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { status = "erro", latencyMs = sw.ElapsedMilliseconds, database = (string?)null, serverVersion = (string?)null, readOnly = (bool?)null });
            }
        }
    }
}
