using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Contas;

/// <summary>Leitura das contas de cloud: qualquer perfil autorizado (o RLS filtra o que cada um vê).</summary>
[ApiController]
[Route("api/contas")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class ContasController(ContasRepositorio repo, UsuarioAtualServico usuarios) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? clienteId, [FromQuery] string? provedor,
        [FromQuery] bool incluirInativas, CancellationToken ct)
    {
        var usuario = await usuarios.ObterAsync(ct)
            ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

        var p = string.IsNullOrWhiteSpace(provedor) ? null : provedor.Trim().ToUpperInvariant();
        if (p is not null && !Provedores.Valido(p))
            throw ErroNegocioException.Validacao("Provedor: use OCI, AWS, AZURE ou GCP.");

        return Ok(await repo.ListarAsync(usuario, clienteId, p, incluirInativas, ct));
    }
}

/// <summary>Cadastro de contas de cloud: só ADMINISTRADOR.</summary>
[ApiController]
[Route("api/admin/contas")]
[Authorize(Policy = Politicas.Administrador)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class AdminContasController(ContasRepositorio repo, UsuarioAtualServico usuarios) : ControllerBase
{
    private async Task<UsuarioAtual> AtorAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] ContaCriacao entrada, CancellationToken ct)
    {
        var dados = ContaRegras.ValidarCriacao(entrada);
        return StatusCode(StatusCodes.Status201Created, await repo.CriarAsync(await AtorAsync(ct), dados, ct));
    }

    /// <summary>Provedor e identificador não mudam: só nome, escopo (compatível) e região padrão.</summary>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(long id, [FromBody] ContaAlteracao entrada, CancellationToken ct)
    {
        ContaRegras.ValidarNome(entrada.Nome);
        ContaRegras.ValidarRegiao(entrada.RegiaoPadrao);
        return Ok(await repo.AtualizarAsync(await AtorAsync(ct), id, entrada, ct));
    }

    [HttpPatch("{id:long}/ativo")]
    public async Task<IActionResult> AlterarAtivo(long id, [FromBody] AtivoEntrada entrada, CancellationToken ct) =>
        Ok(await repo.AlterarAtivoAsync(await AtorAsync(ct), id, entrada.Ativo, ct));
}
