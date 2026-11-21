using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Custos;

/// <summary>Visão Geral sobre o custo agregado do CloudVista (custo_fato/previsão/crédito). RLS filtra por cliente.</summary>
[ApiController]
[Route("api/visao-geral")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class VisaoGeralController(CustosRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] bool comFator, CancellationToken ct)
    {
        var usuario = await usuarios.ObterAsync(ct)
            ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        if (contaId is <= 0) throw ErroNegocioException.Validacao("Conta inválida.");

        var hoje = VisaoGeralCustos.HojeEmSaoPaulo(relogio);
        var dados = await repo.CarregarAsync(usuario, clienteId.Value, contaId, hoje, ct);

        // ADMINISTRADOR/ANALISTA escolhem (toggle). CLIENTE: sempre com fator, aplicado aqui; o fator
        // e o custo bruto nunca saem na resposta; período sem fator vigente = "em apuração".
        var cliente = !ExploradorController.PodeFator(usuario.Perfil);
        return Ok(VisaoGeralCustos.Montar(clienteId.Value, contaId, hoje, dados, comFator, cliente));
    }
}

/// <summary>Fator do cliente: leitura ADMINISTRADOR/ANALISTA; cadastro só ADMINISTRADOR.</summary>
[ApiController]
[Route("api/fator-cliente")]
[Authorize(Policy = Politicas.Analista)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class FatorClienteController(CustosRepositorio repo, UsuarioAtualServico usuarios) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? clienteId, CancellationToken ct) =>
        Ok(await repo.ListarFatoresAsync(await UsuarioAsync(ct), clienteId, ct));

    [HttpPost]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Criar([FromBody] FatorEntrada entrada, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await repo.CriarFatorAsync(await UsuarioAsync(ct), entrada, ct));

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Atualizar(long id, [FromBody] FatorEntrada entrada, CancellationToken ct) =>
        Ok(await repo.AtualizarFatorAsync(await UsuarioAsync(ct), id, entrada, ct));
}
