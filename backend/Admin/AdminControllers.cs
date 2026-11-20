using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Finops.Backend.Admin;

/// <summary>Converte erros de negócio e de banco em respostas amigáveis (detalhe técnico só no log).</summary>
public sealed class ErrosCloudVistaFilter : IExceptionFilter
{
    private readonly ILogger<ErrosCloudVistaFilter> _logger;

    public ErrosCloudVistaFilter(ILogger<ErrosCloudVistaFilter> logger) => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case ErroNegocioException e:
                context.Result = new ObjectResult(new { erro = e.Message }) { StatusCode = e.Status };
                break;
            case CloudVistaNaoConfiguradoException:
                context.Result = new ObjectResult(new { erro = "Banco CloudVista não configurado." }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
                break;
            case OperationCanceledException:
                return;
            default:
                _logger.LogError(context.Exception, "Falha em {Rota}.", context.HttpContext.Request.Path);
                context.Result = new ObjectResult(new { erro = "Não foi possível concluir a operação." }) { StatusCode = StatusCodes.Status500InternalServerError };
                break;
        }
        context.ExceptionHandled = true;
    }
}

/// <summary>Base dos endpoints de administração: perfil ADMINISTRADOR (lido do banco).</summary>
[ApiController]
[Authorize(Policy = Politicas.Administrador)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public abstract class AdminControllerBase : ControllerBase
{
    protected readonly AdminRepositorio Repo;
    private readonly UsuarioAtualServico _usuarios;

    protected AdminControllerBase(AdminRepositorio repo, UsuarioAtualServico usuarios)
    {
        Repo = repo;
        _usuarios = usuarios;
    }

    /// <summary>Administrador que faz a requisição (garantido pela política).</summary>
    protected async Task<UsuarioAtual> AtorAsync(CancellationToken ct) =>
        await _usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");
}

[Route("api/admin/clientes")]
public sealed class AdminClientesController(AdminRepositorio repo, UsuarioAtualServico usuarios) : AdminControllerBase(repo, usuarios)
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool incluirInativos, CancellationToken ct) =>
        Ok(await Repo.ListarClientesAsync(await AtorAsync(ct), incluirInativos, ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] ClienteEntrada entrada, CancellationToken ct)
    {
        var dados = ClienteRegras.Validar(entrada);
        var criado = await Repo.CriarClienteAsync(await AtorAsync(ct), dados, ct);
        return StatusCode(StatusCodes.Status201Created, criado);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] ClienteEntrada entrada, CancellationToken ct)
    {
        var dados = ClienteRegras.Validar(entrada);
        return Ok(await Repo.AtualizarClienteAsync(await AtorAsync(ct), id, dados, ct));
    }

    [HttpPatch("{id:long}/ativo")]
    public async Task<IActionResult> AlterarAtivo(long id, [FromBody] AtivoEntrada entrada, CancellationToken ct) =>
        Ok(await Repo.AlterarAtivoClienteAsync(await AtorAsync(ct), id, entrada.Ativo, ct));
}

[Route("api/admin/usuarios")]
public sealed class AdminUsuariosController(AdminRepositorio repo, UsuarioAtualServico usuarios) : AdminControllerBase(repo, usuarios)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await Repo.ListarUsuariosAsync(await AtorAsync(ct), ct));

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] UsuarioCriacao entrada, CancellationToken ct)
    {
        var email = UsuarioRegras.ValidarEmail(entrada.Email);
        var nome = UsuarioRegras.ValidarNome(entrada.Nome);
        var perfil = UsuarioRegras.ValidarPerfil(entrada.Perfil);
        var criado = await Repo.CriarUsuarioAsync(await AtorAsync(ct), email, nome, perfil, entrada.ClienteIds, ct);
        return StatusCode(StatusCodes.Status201Created, criado);
    }

    /// <summary>E-mail não muda depois de criado.</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] UsuarioAlteracao entrada, CancellationToken ct)
    {
        var nome = UsuarioRegras.ValidarNome(entrada.Nome);
        var perfil = UsuarioRegras.ValidarPerfil(entrada.Perfil);
        return Ok(await Repo.AtualizarUsuarioAsync(await AtorAsync(ct), id, nome, perfil, entrada.ClienteIds, ct));
    }

    [HttpPatch("{id:long}/ativo")]
    public async Task<IActionResult> AlterarAtivo(long id, [FromBody] AtivoEntrada entrada, CancellationToken ct) =>
        Ok(await Repo.AlterarAtivoUsuarioAsync(await AtorAsync(ct), id, entrada.Ativo, ct));
}
