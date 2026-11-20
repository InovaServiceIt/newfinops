using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

namespace Finops.Backend.Cofre;

/// <summary>
/// Erros dos endpoints de credencial. NUNCA loga mensagem de exceção (poderia conter dados do
/// corpo): só o tipo e, para erros do Postgres, o SQLSTATE. Respostas sempre genéricas.
/// </summary>
public sealed class CofreErrosFilter : IExceptionFilter
{
    private readonly ILogger<CofreErrosFilter> _logger;

    public CofreErrosFilter(ILogger<CofreErrosFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        IActionResult resultado;
        switch (context.Exception)
        {
            case ErroNegocioException e:
                resultado = new ObjectResult(new { erro = e.Message }) { StatusCode = e.Status };
                break;
            case CofreNaoConfiguradoException:
                resultado = new ObjectResult(new { erro = "Cofre de credenciais não configurado." }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case CloudVistaNaoConfiguradoException:
                resultado = new ObjectResult(new { erro = "Banco CloudVista não configurado." }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case PostgresException pg when pg.SqlState == PostgresErrorCodes.InsufficientPrivilege:
                // RLS WITH CHECK/permissão: ex.: perfil sem direito de gravar a credencial desta conta.
                _logger.LogWarning("Credencial: gravação negada pelo banco (SQLSTATE {SqlState}).", pg.SqlState);
                resultado = new ObjectResult(new { erro = "Você não tem permissão para esta operação nesta conta." }) { StatusCode = StatusCodes.Status403Forbidden };
                break;
            case OperationCanceledException:
                return;
            default:
                var sqlState = (context.Exception as PostgresException)?.SqlState;
                _logger.LogError("Credencial: falha em {Rota}. Tipo: {Tipo}. SQLSTATE: {SqlState}.",
                    context.HttpContext.Request.Path, context.Exception.GetType().Name, sqlState ?? "-");
                resultado = new ObjectResult(new { erro = "Não foi possível concluir a operação com a credencial." }) { StatusCode = StatusCodes.Status500InternalServerError };
                break;
        }
        context.Result = resultado;
        context.ExceptionHandled = true;
    }
}

/// <summary>Credencial de uma conta de cloud (cofre). O segredo nunca volta em nenhuma resposta.</summary>
[ApiController]
[Route("api/contas/{contaId:long}/credencial")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(CofreErrosFilter))]
public sealed class CredencialController(CofreServico cofre, UsuarioAtualServico usuarios) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    [HttpGet]
    public async Task<IActionResult> Obter(long contaId, CancellationToken ct) =>
        Ok(await cofre.MetadadosAsync(await UsuarioAsync(ct), contaId, ct));

    /// <summary>Cadastra ou substitui (ADMINISTRADOR ou CLIENTE; o RLS restringe ao próprio cliente).</summary>
    [HttpPut]
    [Authorize(Policy = Politicas.AdministradorOuCliente)]
    public async Task<IActionResult> Salvar(long contaId, [FromBody] CredencialEntrada entrada, CancellationToken ct) =>
        Ok(await cofre.SalvarAsync(await UsuarioAsync(ct), contaId, entrada, ct));

    [HttpPatch("ativo")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> AlterarAtivo(long contaId, [FromBody] AtivoCredencialEntrada entrada, CancellationToken ct) =>
        Ok(await cofre.AlterarAtivoAsync(await UsuarioAsync(ct), contaId, entrada.Ativo, ct));

    /// <summary>Registra o pedido de teste (execução real no MD 0.5, via fila do MD 0.4).</summary>
    [HttpPost("testar")]
    public async Task<IActionResult> Testar(long contaId, CancellationToken ct)
    {
        await cofre.SolicitarTesteAsync(await UsuarioAsync(ct), contaId, ct);
        return StatusCode(StatusCodes.Status202Accepted, new { mensagem = "Teste registrado. Ele será executado quando o coletor estiver ativo." });
    }

    [HttpGet("auditoria")]
    public async Task<IActionResult> Auditoria(long contaId, CancellationToken ct) =>
        Ok(await cofre.AuditoriaAsync(await UsuarioAsync(ct), contaId, ct));
}
