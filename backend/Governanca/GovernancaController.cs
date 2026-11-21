using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Governanca;

/// <summary>Governança de tags: leitura sobre o inventário (todos os perfis; RLS filtra). Nada é persistido.</summary>
[ApiController]
[Route("api/governanca-tags")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class GovernancaController(GovernancaRepositorio repo, PoliticaTagsRepositorio politicas, UsuarioAtualServico usuarios, TimeProvider relogio) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao,
        [FromQuery] string? tipo, [FromQuery] string? compartimento, [FromQuery] bool automaticas, CancellationToken ct)
    {
        var f = GovernancaRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, automaticas);
        return Ok(await repo.ResumoAsync(await UsuarioAsync(ct), f, ct));
    }

    /// <summary>Chaves da avaliação: as da URL (ad hoc) ou, com politica=true, as chaves ativas da política do cliente.</summary>
    private async Task<string[]> ChavesAsync(UsuarioAtual u, long clienteId, string? chaves, bool politica, CancellationToken ct)
    {
        if (!politica) return GovernancaRegras.Chaves(chaves);
        var ativas = await politicas.ChavesAtivasAsync(u, clienteId, ct);
        return ativas.Length > 0 ? ativas : throw ErroNegocioException.Validacao("Este cliente não tem política de tags ativa.");
    }

    [HttpGet("avaliacao")]
    public async Task<IActionResult> Avaliacao([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao,
        [FromQuery] string? tipo, [FromQuery] string? compartimento, [FromQuery] string? chaves, [FromQuery] bool politica, CancellationToken ct)
    {
        var f = GovernancaRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, true);
        var u = await UsuarioAsync(ct);
        return Ok(await repo.AvaliarAsync(u, f, await ChavesAsync(u, f.ClienteId, chaves, politica, ct), ct, politica));
    }

    [HttpGet("variacoes")]
    public async Task<IActionResult> Variacoes([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao,
        [FromQuery] string? tipo, [FromQuery] string? compartimento, [FromQuery] string? chave, CancellationToken ct)
    {
        var f = GovernancaRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, true);
        return Ok(await repo.VariacoesAsync(await UsuarioAsync(ct), f, GovernancaRegras.Chave(chave), ct));
    }

    [HttpGet("nao-conformes")]
    public async Task<IActionResult> NaoConformes([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao,
        [FromQuery] string? tipo, [FromQuery] string? compartimento, [FromQuery] string? chaves, [FromQuery] string? faltando,
        [FromQuery] bool politica, [FromQuery] int pagina = 1, CancellationToken ct = default)
    {
        var f = GovernancaRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, true);
        var u = await UsuarioAsync(ct);
        var lista = await ChavesAsync(u, f.ClienteId, chaves, politica, ct);
        return Ok(await repo.NaoConformesAsync(u, f, lista, Faltando(faltando, lista), Math.Max(pagina, 1), ct, politica));
    }

    [HttpGet("nao-conformes/exportar")]
    public async Task<IActionResult> Exportar([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao,
        [FromQuery] string? tipo, [FromQuery] string? compartimento, [FromQuery] string? chaves, [FromQuery] string? faltando,
        [FromQuery] bool politica, CancellationToken ct = default)
    {
        var f = GovernancaRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, true);
        var u = await UsuarioAsync(ct);
        var lista = await ChavesAsync(u, f.ClienteId, chaves, politica, ct);
        var linhas = await repo.ExportarAsync(u, f, lista, Faltando(faltando, lista), ct, politica);
        var bytes = InventarioRegras.CodificacaoCsv.GetPreamble()
            .Concat(InventarioRegras.CodificacaoCsv.GetBytes(string.Join("\r\n", linhas) + "\r\n")).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"nao_conformes_{relogio.GetUtcNow():yyyyMMdd_HHmm}.csv");
    }

    /// <summary>Filtro opcional "só quem não tem a chave X" (X precisa estar entre as avaliadas).</summary>
    private static string? Faltando(string? faltando, string[] chaves)
    {
        if (string.IsNullOrWhiteSpace(faltando)) return null;
        var c = GovernancaRegras.Chave(faltando);
        return chaves.Contains(c) ? c : throw ErroNegocioException.Validacao("A chave filtrada precisa estar entre as avaliadas.");
    }
}
