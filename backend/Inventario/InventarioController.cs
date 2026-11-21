using System.Text;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Inventario;

/// <summary>Inventário multicloud: todos os perfis autorizados; o RLS filtra os clientes visíveis.</summary>
[ApiController]
[Route("api/inventario")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class InventarioController(InventarioRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    /// <summary>KPIs, sinais rápidos, facetas de filtro e proveniência por conta.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo([FromQuery] long? clienteId, [FromQuery] long? contaId, CancellationToken ct)
    {
        var f = InventarioRegras.Validar(clienteId, contaId, null, null, null, null, null, null, null, false);
        return Ok(await repo.ResumoAsync(await UsuarioAsync(ct), f.ClienteId, f.ContaId, ct));
    }

    [HttpGet("recursos")]
    public async Task<IActionResult> Recursos(
        [FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao, [FromQuery] string? tipo,
        [FromQuery] string? compartimento, [FromQuery] string? estado, [FromQuery] string? busca, [FromQuery] string? tag,
        [FromQuery] string? sinal, [FromQuery] bool removidos, [FromQuery] string? ordem, [FromQuery] string? direcao,
        [FromQuery] int pagina = 1, CancellationToken ct = default)
    {
        var f = InventarioRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, estado, busca, tag, sinal, removidos);
        var (expr, desc) = InventarioRegras.Ordem(ordem, direcao);
        return Ok(await repo.ListarAsync(await UsuarioAsync(ct), f, expr, desc, Math.Max(pagina, 1), ct));
    }

    /// <summary>Detalhe (chave por query: a de bucket contém "/").</summary>
    [HttpGet("recurso")]
    public async Task<IActionResult> Recurso([FromQuery] long? contaId, [FromQuery] string? chave, CancellationToken ct)
    {
        if (contaId is not > 0) throw ErroNegocioException.Validacao("Conta inválida.");
        var c = InventarioRegras.Limpo(chave) ?? throw ErroNegocioException.Validacao("Informe a chave do recurso.");
        return Ok(await repo.DetalheAsync(await UsuarioAsync(ct), contaId.Value, c, ct));
    }

    /// <summary>CSV do filtro atual (até 50 mil linhas; células sanitizadas contra fórmula).</summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? regiao, [FromQuery] string? tipo,
        [FromQuery] string? compartimento, [FromQuery] string? estado, [FromQuery] string? busca, [FromQuery] string? tag,
        [FromQuery] string? sinal, [FromQuery] bool removidos, CancellationToken ct = default)
    {
        var f = InventarioRegras.Validar(clienteId, contaId, regiao, tipo, compartimento, estado, busca, tag, sinal, removidos);
        var linhas = await repo.ExportarAsync(await UsuarioAsync(ct), f, ct);
        var bytes = InventarioRegras.CodificacaoCsv.GetPreamble()
            .Concat(InventarioRegras.CodificacaoCsv.GetBytes(string.Join("\r\n", linhas) + "\r\n")).ToArray();
        var nome = $"inventario_{relogio.GetUtcNow():yyyyMMdd_HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", nome);
    }
}
