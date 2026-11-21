using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Compartilhado;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Governanca;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record PoliticaEntrada(long? ClienteId, string? Chave, IReadOnlyList<string>? ValoresPermitidos, string? Descricao, bool? Ativa);

public sealed record PoliticaChave(long Id, string Chave, IReadOnlyList<string>? ValoresPermitidos, string? Descricao, bool Ativa,
    long? Presentes, long? Ausentes, long? ForaDaLista, decimal? PresentesPct, DateTimeOffset? AtualizadoEm, string? AtualizadoPor);

public sealed record PoliticaResumo(long ClienteId, long Total, long Conformes, decimal ConformidadePct, long ComAlgumaTag,
    IReadOnlyList<PoliticaChave> Chaves);

public sealed record PontoEvolucao(DateOnly Dia, long Total, long Conformes, long ComAlgumaTag, decimal ConformidadePct, IReadOnlyList<string> Chaves);

// ── Regras ───────────────────────────────────────────────────────────────────

public static class PoliticaRegras
{
    public const int MaxValores = 100;
    public const int MaxDiasEvolucao = 366;
    public const decimal MetaPct = 80m;   // fixa nesta fase (vira configuração depois)

    public sealed record Valida(string? Chave, string[]? Valores, string? Descricao, bool? Ativa);

    public static Valida Validar(PoliticaEntrada e, bool criacao)
    {
        string? chave = null;
        if (criacao)
        {
            if (e.ClienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
            chave = GovernancaRegras.Chave(e.Chave);
        }

        string[]? valores = null;
        if (e.ValoresPermitidos is { Count: > 0 } lista)
        {
            valores = lista.Select(v => (v ?? "").Trim()).Where(v => v.Length > 0)
                .DistinctBy(GovernancaRegras.Normalizar).ToArray();
            if (valores.Length > MaxValores) throw ErroNegocioException.Validacao($"Valores permitidos: no máximo {MaxValores}.");
            if (valores.Any(v => v.Length > 200)) throw ErroNegocioException.Validacao("Valores permitidos: no máximo 200 caracteres cada.");
            if (valores.Length == 0) valores = null;   // lista vazia = qualquer valor não vazio
        }

        var descricao = string.IsNullOrWhiteSpace(e.Descricao) ? null : e.Descricao.Trim();
        if (descricao is { Length: > 500 }) throw ErroNegocioException.Validacao("Descrição: no máximo 500 caracteres.");
        return new Valida(chave, valores, descricao, e.Ativa);
    }

    public static (DateOnly De, DateOnly Ate) Periodo(DateOnly? de, DateOnly? ate, DateOnly hoje)
    {
        var fim = ate ?? hoje;
        var ini = de ?? fim.AddDays(-89);
        if (ini > fim) throw ErroNegocioException.Validacao("Período: a data inicial deve ser anterior ou igual à final.");
        if (fim.DayNumber - ini.DayNumber + 1 > MaxDiasEvolucao) throw ErroNegocioException.Validacao($"Evolução: no máximo {MaxDiasEvolucao} dias.");
        return (ini, fim);
    }
}

// ── SQL / repositório ────────────────────────────────────────────────────────

public static class PoliticaTagsSql
{
    public const string Listar = @"
SELECT id, chave, valores_permitidos, descricao, ativa, atualizado_em, atualizado_por
FROM tag_politica
WHERE cliente_id = @cliente_id
ORDER BY ativa DESC, chave";

    public const string ChavesAtivas = "SELECT chave FROM tag_politica WHERE cliente_id = @cliente_id AND ativa ORDER BY chave";

    public const string Criar = @"
INSERT INTO tag_politica (cliente_id, chave, valores_permitidos, descricao, ativa, criado_por, atualizado_em, atualizado_por)
VALUES (@cliente_id, @chave, @valores, @descricao, true, @por, now(), @por)
RETURNING id";

    public const string ParaAlterar = @"
SELECT cliente_id, chave, valores_permitidos, descricao, ativa FROM tag_politica WHERE id = @id FOR UPDATE";

    public const string Atualizar = @"
UPDATE tag_politica
SET valores_permitidos = @valores, descricao = @descricao, ativa = @ativa, atualizado_em = now(), atualizado_por = @por
WHERE id = @id";

    public const string Evolucao = @"
SELECT dia, total, conformes, com_alguma_tag, chaves
FROM tag_conformidade_dia
WHERE cliente_id = @cliente_id AND dia BETWEEN @de AND @ate
ORDER BY dia";
}

public sealed class PoliticaTagsRepositorio(CloudVistaDb db, ILogger<PoliticaTagsRepositorio> logger)
{
    public Task<PoliticaResumo> ResumoAsync(UsuarioAtual u, long clienteId, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, (conn, tx) => CarregarAsync(conn, tx, clienteId, ct), ct);

    public Task<string[]> ChavesAtivasAsync(UsuarioAtual u, long clienteId, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            await using var cmd = Cmd(conn, tx, PoliticaTagsSql.ChavesAtivas, clienteId);
            var l = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) l.Add(r.GetString(0));
            return l.ToArray();
        }, ct);

    public async Task<PoliticaResumo> CriarAsync(UsuarioAtual u, PoliticaEntrada e, CancellationToken ct)
    {
        var v = PoliticaRegras.Validar(e, criacao: true);
        try
        {
            return await db.ExecutarComContextoAsync(u, async (conn, tx) =>
            {
                await using (var cmd = Cmd(conn, tx, PoliticaTagsSql.Criar, e.ClienteId!.Value))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = v.Chave! });
                    Valores(cmd, v.Valores);
                    cmd.Parameters.Add(new NpgsqlParameter("descricao", NpgsqlDbType.Varchar) { Value = (object?)v.Descricao ?? DBNull.Value });
                    cmd.Parameters.Add(new NpgsqlParameter("por", NpgsqlDbType.Varchar) { Value = u.Email });
                    await cmd.ExecuteScalarAsync(ct);
                }
                logger.LogInformation("Política de tags: usuário {Usuario} criou chave {Chave} no cliente {Cliente} (valores: {Valores}).",
                    u.Id, v.Chave, e.ClienteId, v.Valores?.Length ?? 0);
                return await CarregarAsync(conn, tx, e.ClienteId.Value, ct);
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ErroNegocioException.Conflito("Esta chave já está na política do cliente (reative-a se estiver inativa).");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw ErroNegocioException.Validacao("Chave de tag inválida.");
        }
    }

    /// <summary>Altera valores permitidos, descrição e ativa (a chave não muda; sem DELETE).</summary>
    public Task<PoliticaResumo> AtualizarAsync(UsuarioAtual u, long id, PoliticaEntrada e, CancellationToken ct)
    {
        var v = PoliticaRegras.Validar(e, criacao: false);
        return db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            long clienteId;
            string chave;
            bool ativaAntes;
            int valoresAntes;
            await using (var cmd = new NpgsqlCommand(PoliticaTagsSql.ParaAlterar, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw ErroNegocioException.NaoEncontrado("Chave da política não encontrada.");
                clienteId = r.GetInt64(0);
                chave = r.GetString(1);
                valoresAntes = r.IsDBNull(2) ? 0 : r.GetFieldValue<string[]>(2).Length;
                ativaAntes = r.GetBoolean(4);
            }
            if (e.ClienteId is { } c && c != clienteId) throw ErroNegocioException.Validacao("Cliente: não pode ser alterado.");
            if (e.Chave is { } k && k.Trim() != chave) throw ErroNegocioException.Validacao("Chave: não pode ser alterada; cadastre outra.");

            await using (var cmd = new NpgsqlCommand(PoliticaTagsSql.Atualizar, conn, tx))
            {
                Valores(cmd, v.Valores);
                cmd.Parameters.Add(new NpgsqlParameter("descricao", NpgsqlDbType.Varchar) { Value = (object?)v.Descricao ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("ativa", NpgsqlDbType.Boolean) { Value = v.Ativa ?? ativaAntes });
                cmd.Parameters.Add(new NpgsqlParameter("por", NpgsqlDbType.Varchar) { Value = u.Email });
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                await cmd.ExecuteNonQueryAsync(ct);
            }
            logger.LogInformation(
                "Política de tags: usuário {Usuario} alterou chave {Chave} do cliente {Cliente}: ativa {AtivaAntes}→{AtivaDepois}, valores {VAntes}→{VDepois}.",
                u.Id, chave, clienteId, ativaAntes, v.Ativa ?? ativaAntes, valoresAntes, v.Valores?.Length ?? 0);
            return await CarregarAsync(conn, tx, clienteId, ct);
        }, ct);
    }

    public Task<List<PontoEvolucao>> EvolucaoAsync(UsuarioAtual u, long clienteId, DateOnly de, DateOnly ate, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            await using var cmd = Cmd(conn, tx, PoliticaTagsSql.Evolucao, clienteId);
            cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = de });
            cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = ate });
            var l = new List<PontoEvolucao>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var total = r.GetInt64(1);
                var conformes = r.GetInt64(2);
                l.Add(new PontoEvolucao(r.GetFieldValue<DateOnly>(0), total, conformes, r.GetInt64(3),
                    GovernancaRegras.Pct(conformes, total), r.IsDBNull(4) ? [] : r.GetFieldValue<string[]>(4)));
            }
            return l;
        }, ct);

    // ── auxiliares ──────────────────────────────────────────────────────────

    /// <summary>Política + status calculado agora sobre o inventário (mesma SQL do snapshot do worker).</summary>
    private static async Task<PoliticaResumo> CarregarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long clienteId, CancellationToken ct)
    {
        long total = 0, conformes = 0, comTag = 0;
        var porChave = new Dictionary<string, (long Presentes, long Fora)>(StringComparer.Ordinal);
        await using (var cmd = Cmd(conn, tx, PoliticaSql.Resumo, clienteId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct))
            {
                total = r.GetInt64(0);
                conformes = r.GetInt64(1);
                comTag = r.GetInt64(2);
                using var json = JsonDocument.Parse(r.GetString(3));
                foreach (var p in json.RootElement.EnumerateObject())
                    porChave[p.Name] = (p.Value.GetProperty("presentes").GetInt64(), p.Value.GetProperty("fora_da_lista").GetInt64());
            }
        }

        var chaves = new List<PoliticaChave>();
        await using (var cmd = Cmd(conn, tx, PoliticaTagsSql.Listar, clienteId))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var chave = r.GetString(1);
                var ativa = r.GetBoolean(4);
                var st = ativa && porChave.TryGetValue(chave, out var x) ? x : ((long, long)?)null;
                chaves.Add(new PoliticaChave(r.GetInt64(0), chave, r.IsDBNull(2) ? null : r.GetFieldValue<string[]>(2),
                    r.IsDBNull(3) ? null : r.GetString(3), ativa,
                    st?.Item1, st is null ? null : total - st.Value.Item1, st?.Item2,
                    st is null ? null : GovernancaRegras.Pct(st.Value.Item1, total),
                    r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }
        return new PoliticaResumo(clienteId, total, conformes, GovernancaRegras.Pct(conformes, total), comTag, chaves);
    }

    private static void Valores(NpgsqlCommand cmd, string[]? valores) =>
        cmd.Parameters.Add(new NpgsqlParameter("valores", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = (object?)valores ?? DBNull.Value });

    private static NpgsqlCommand Cmd(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, long clienteId)
    {
        var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
        return cmd;
    }
}

// ── Controller ───────────────────────────────────────────────────────────────

/// <summary>Política de tags: todos leem (RLS); só ADMINISTRADOR/ANALISTA escrevem (403 antes do banco; RLS é a 2ª barreira).</summary>
[ApiController]
[Route("api/politica-tags")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class PoliticaTagsController(PoliticaTagsRepositorio repo, UsuarioAtualServico usuarios, TimeProvider relogio) : ControllerBase
{
    private async Task<UsuarioAtual> UsuarioAsync(CancellationToken ct) =>
        await usuarios.ObterAsync(ct) ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? clienteId, CancellationToken ct)
    {
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        return Ok(await repo.ResumoAsync(await UsuarioAsync(ct), clienteId.Value, ct));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.Analista)]
    public async Task<IActionResult> Criar([FromBody] PoliticaEntrada entrada, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await repo.CriarAsync(await UsuarioAsync(ct), entrada, ct));

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.Analista)]
    public async Task<IActionResult> Atualizar(long id, [FromBody] PoliticaEntrada entrada, CancellationToken ct) =>
        Ok(await repo.AtualizarAsync(await UsuarioAsync(ct), id, entrada, ct));

    [HttpGet("evolucao")]
    public async Task<IActionResult> Evolucao([FromQuery] long? clienteId, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, CancellationToken ct)
    {
        if (clienteId is not > 0) throw ErroNegocioException.Validacao("Cliente: selecione o cliente.");
        var (ini, fim) = PoliticaRegras.Periodo(de, ate, Custos.VisaoGeralCustos.HojeEmSaoPaulo(relogio));
        return Ok(new { meta = PoliticaRegras.MetaPct, pontos = await repo.EvolucaoAsync(await UsuarioAsync(ct), clienteId.Value, ini, fim, ct) });
    }
}
