using Finops.Backend.VisaoGeral;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Controllers
{
    /// <summary>Visão Geral (réplica corrigida dos dashboards Grafana). Autenticado pelo filtro global.</summary>
    [ApiController]
    [Route("api/visao-geral")]
    public class VisaoGeralController : ControllerBase
    {
        private readonly VisaoGeralServico _servico;
        private readonly ILogger<VisaoGeralController> _logger;

        public VisaoGeralController(VisaoGeralServico servico, ILogger<VisaoGeralController> logger)
        {
            _servico = servico;
            _logger = logger;
        }

        /// <summary>Provedores, anos, serviços, SKUs, regiões e fator do provedor.</summary>
        [HttpGet("opcoes")]
        public Task<IActionResult> Opcoes([FromQuery] int? provedor, CancellationToken ct) =>
            Executar(async () => Ok(await _servico.OpcoesAsync(provedor, ct)));

        /// <summary>Payload único com os 6 blocos da Visão Geral.</summary>
        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery] int provedor,
            [FromQuery] int? ano,
            [FromQuery] int[]? meses,
            [FromQuery] string[]? servicos,
            [FromQuery] string[]? skus,
            [FromQuery] string[]? regioes,
            [FromQuery] bool comFator,
            CancellationToken ct) =>
            Executar(async () =>
            {
                if (provedor <= 0)
                    return BadRequest(new { erro = "Informe o provedor." });

                var mesesValidos = (meses ?? []).Where(m => m is >= 1 and <= 12).Distinct().ToArray();
                var filtros = new FiltrosVisaoGeral(
                    provedor,
                    ano ?? _servico.DatasDeHoje().MesAtual.Ano,
                    mesesValidos,
                    Limpar(servicos),
                    Limpar(skus),
                    Limpar(regioes),
                    comFator);

                return Ok(await _servico.VisaoGeralAsync(filtros, ct));
            });

        private static string[] Limpar(string[]? valores) =>
            (valores ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToArray();

        /// <summary>Converte falhas em respostas amigáveis; o detalhe técnico fica só no log.</summary>
        private async Task<IActionResult> Executar(Func<Task<IActionResult>> acao)
        {
            try
            {
                return await acao();
            }
            catch (BancoNaoConfiguradoException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Banco de dados não configurado." });
            }
            catch (DadosEmAtualizacaoException ex)
            {
                _logger.LogWarning(ex, "Visão Geral: dados em atualização (42P01 persistente).");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Dados em atualização, tente novamente em instantes." });
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Visão Geral: falha ao carregar dados.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { erro = "Não foi possível carregar os dados." });
            }
        }
    }
}
