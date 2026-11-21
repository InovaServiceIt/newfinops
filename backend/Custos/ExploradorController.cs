using System.Globalization;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finops.Backend.Custos;

/// <summary>Explorador de custos sobre custo_fato (todos os perfis; RLS). Fator só para ADMINISTRADOR/ANALISTA.</summary>
[ApiController]
[Route("api/explorador")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class ExploradorController(ExploradorRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    public static bool PodeFator(string perfil) => perfil is Perfis.Administrador or Perfis.AnalistaFinOps;

    [HttpGet("consulta")]
    public async Task<IActionResult> Consulta([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? granularidade,
        [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, [FromQuery] string? dimensao, [FromQuery] string? filtroChave,
        [FromQuery] string? tagChave, [FromQuery] string? moeda, [FromQuery] bool comFator, [FromQuery] int? top, CancellationToken ct)
    {
        var u = await UsuarioAsync(ct);
        var c = ExploradorRegras.Validar(clienteId, contaId, granularidade, de, ate, dimensao, filtroChave, tagChave, moeda, comFator, top);
        return Ok(await repo.ConsultarAsync(u, c, PodeFator(u.Perfil), VisaoGeralCustos.HojeEmSaoPaulo(relogio), ct));
    }

    /// <summary>Autocomplete do filtro (até 50). Com dimensao=TAG e sem tagChave: devolve as chaves de tag.</summary>
    [HttpGet("valores")]
    public async Task<IActionResult> Valores([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? dimensao,
        [FromQuery] string? tagChave, [FromQuery] string? busca, CancellationToken ct)
    {
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        var d = (dimensao ?? "").Trim().ToUpperInvariant();
        if (!ExploradorRegras.Dimensoes.Contains(d)) throw ErroNegocioException.Validacao("Dimensão inválida.");
        return Ok(await repo.ValoresAsync(await UsuarioAsync(ct), clienteId.Value, contaId is > 0 ? contaId : null, d,
            InventarioRegras.Limpo(tagChave, 500), InventarioRegras.Limpo(busca, 200), ct));
    }

    /// <summary>CSV longo (período × chave × valor), moeda e indicação de fator em cada linha.</summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar([FromQuery] long? clienteId, [FromQuery] long? contaId, [FromQuery] string? granularidade,
        [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, [FromQuery] string? dimensao, [FromQuery] string? filtroChave,
        [FromQuery] string? tagChave, [FromQuery] string? moeda, [FromQuery] bool comFator, CancellationToken ct)
    {
        var u = await UsuarioAsync(ct);
        var c = ExploradorRegras.Validar(clienteId, contaId, granularidade, de, ate, dimensao, filtroChave, tagChave, moeda, comFator, 50);
        var r = await repo.ConsultarAsync(u, c, PodeFator(u.Perfil), VisaoGeralCustos.HojeEmSaoPaulo(relogio), ct);
        var linhas = ExploradorCsv.Linhas(r);
        var bytes = InventarioRegras.CodificacaoCsv.GetPreamble()
            .Concat(InventarioRegras.CodificacaoCsv.GetBytes(string.Join("\r\n", linhas) + "\r\n")).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"explorador_{relogio.GetUtcNow():yyyyMMdd_HHmm}.csv");
    }
}

public static class ExploradorCsv
{
    public static List<string> Linhas(RespostaExplorador r)
    {
        var linhas = new List<string> { InventarioRegras.LinhaCsv(ExploradorRegras.CabecalhoCsv) };
        var fator = r.ComFator ? "sim" : "não";
        foreach (var t in r.Tabela)
            for (var i = 0; i < r.Serie.Count && i < t.Serie.Count; i++)
                if (t.Serie[i] != 0)
                    linhas.Add(InventarioRegras.LinhaCsv([r.Serie[i].Periodo, t.Chave, t.Rotulo,
                        t.Serie[i].ToString("0.######", CultureInfo.InvariantCulture), r.Moeda, fator]));
        return linhas;
    }
}
