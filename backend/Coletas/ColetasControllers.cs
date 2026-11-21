using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Coletas;

public abstract class ColetasControllerBase(UsuarioAtualServico usuarios) : ControllerBase
{
    protected async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");
}

/// <summary>"Coletar agora" (perfis do parâmetro coleta.manual.perfis_permitidos; o RLS também barra).</summary>
[ApiController]
[Route("api/contas/{contaId:long}/coletar")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class ColetarController(ColetasRepositorio repo, UsuarioAtualServico usuarios) : ColetasControllerBase(usuarios)
{
    [HttpPost]
    public async Task<IActionResult> Coletar(long contaId, [FromBody] ColetarEntrada entrada, CancellationToken ct)
    {
        var tipo = ColetarRegras.ValidarTipoColeta(entrada.TipoJob);
        var dias = ColetarRegras.ValidarCostDays(entrada.CostDays);
        var jobId = await repo.ColetarAsync(await UsuarioAsync(ct), contaId, tipo, dias, ct);
        return StatusCode(StatusCodes.Status202Accepted, new { jobId });
    }
}

/// <summary>Tela Coletas (ADMINISTRADOR e ANALISTA_FINOPS; RLS filtra pelos clientes visíveis).</summary>
[ApiController]
[Route("api/coletas")]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class ColetasController(ColetasRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ColetasControllerBase(usuarios)
{
    public const int TamanhoPadrao = 50;
    public const int TamanhoMaximo = 200;

    [HttpGet]
    [Authorize(Policy = Politicas.Analista)]
    public async Task<IActionResult> Listar(
        [FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? status, [FromQuery] string? tipoJob,
        [FromQuery] DateTimeOffset? de, [FromQuery] DateTimeOffset? ate, [FromQuery] int pagina = 1, [FromQuery] int tamanho = TamanhoPadrao,
        CancellationToken ct = default)
    {
        var s = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        if (s is not null && !TiposJob.Status.Contains(s)) throw ErroNegocioException.Validacao("Status inválido.");
        var t = string.IsNullOrWhiteSpace(tipoJob) ? null : tipoJob.Trim().ToUpperInvariant();
        if (t is not null && !TiposJob.Todos.Contains(t)) throw ErroNegocioException.Validacao("Tipo inválido.");

        var agora = relogio.GetUtcNow();
        var fim = ate ?? agora.AddMinutes(1);
        var inicio = de ?? fim.AddDays(-7);
        if (inicio >= fim) throw ErroNegocioException.Validacao("Período: a data inicial deve ser anterior à final.");

        var filtro = new FiltroColetas(clienteId, contaId, s, t, inicio, fim,
            Math.Max(pagina, 1), Math.Clamp(tamanho, 1, TamanhoMaximo));
        return Ok(await repo.ListarAsync(await UsuarioAsync(ct), filtro, ct));
    }

    /// <summary>Reimporta uma coleta IMPORTADO/ERRO que tenha coleta_bruta (só ADMINISTRADOR; o banco também valida).</summary>
    [HttpPost("{id:long}/reimportar")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Reimportar(long id, CancellationToken ct)
    {
        var anterior = await repo.ReimportarAsync(await UsuarioAsync(ct), id, ct);
        return StatusCode(StatusCodes.Status202Accepted, new { jobId = id, statusAnterior = anterior });
    }

    /// <summary>Se o usuário pode disparar "Coletar agora" (lê o parâmetro do banco).</summary>
    [HttpGet("permissao")]
    [Authorize(Policy = Politicas.Autorizado)]
    public async Task<IActionResult> Permissao(CancellationToken ct) =>
        Ok(new { podeColetar = await repo.PodeColetarAsync(await UsuarioAsync(ct), ct) });
}

/// <summary>Agenda de coletas: ADMINISTRADOR e ANALISTA leem; só ADMINISTRADOR grava.</summary>
[ApiController]
[Route("api/admin/agenda")]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class AgendaController(ColetasRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ColetasControllerBase(usuarios)
{
    [HttpGet]
    [Authorize(Policy = Politicas.Analista)]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(new { itens = await repo.ListarAgendaAsync(await UsuarioAsync(ct), ct), atalhos = CronRegras.Atalhos.Select(a => new { rotulo = a.Rotulo, cron = a.Cron }) });

    /// <summary>Valida o cron (Quartz) e devolve as próximas 3 execuções no fuso escolhido.</summary>
    [HttpPost("previa")]
    [Authorize(Policy = Politicas.Analista)]
    public IActionResult Previa([FromBody] PreviaCronEntrada entrada) =>
        Ok(CronRegras.Previa(entrada.Cron, entrada.Fuso, relogio.GetUtcNow()));

    [HttpPost]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Criar([FromBody] AgendaEntrada entrada, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, new { id = await repo.SalvarAgendaAsync(await UsuarioAsync(ct), null, entrada, ct) });

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> Atualizar(long id, [FromBody] AgendaEntrada entrada, CancellationToken ct) =>
        Ok(new { id = await repo.SalvarAgendaAsync(await UsuarioAsync(ct), id, entrada, ct) });

    [HttpPatch("{id:long}/ativo")]
    [Authorize(Policy = Politicas.Administrador)]
    public async Task<IActionResult> AlterarAtivo(long id, [FromBody] AtivoEntrada entrada, CancellationToken ct)
    {
        await repo.AlterarAtivoAgendaAsync(await UsuarioAsync(ct), id, entrada.Ativo, ct);
        return NoContent();
    }
}

/// <summary>Parâmetros de negócio/operação (só ADMINISTRADOR; edição, sem criar/apagar chaves).</summary>
[ApiController]
[Route("api/admin/parametros")]
[Authorize(Policy = Politicas.Administrador)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class ParametrosController(ColetasRepositorio repo, UsuarioAtualServico usuarios) : ColetasControllerBase(usuarios)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await repo.ListarParametrosAsync(await UsuarioAsync(ct), ct));

    [HttpPut("{chave}")]
    public async Task<IActionResult> Atualizar(string chave, [FromBody] ParametroEntrada entrada, CancellationToken ct)
    {
        await repo.AtualizarParametroAsync(await UsuarioAsync(ct), chave, entrada.Valor, ct);
        return NoContent();
    }
}
