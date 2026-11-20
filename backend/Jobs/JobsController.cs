using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Jobs;

public sealed record JobSituacao(long Id, string TipoJob, string Status, DateTimeOffset CriadoEm, DateTimeOffset? IniciadoEm,
    DateTimeOffset? FinalizadoEm, string? Erro, JsonElement? Resumo);

public static class JobsSql
{
    public const string PorId = @"
SELECT id, tipo_job, status, criado_em, iniciado_em, finalizado_em, erro, resumo::text
FROM job_execucao
WHERE id = @id";
}

/// <summary>Situação de um job da fila (para a tela acompanhar por polling). O RLS filtra pelo cliente visível.</summary>
[ApiController]
[Route("api/jobs")]
[Authorize(Policy = Politicas.Autorizado)]
[TypeFilter(typeof(ErrosCloudVistaFilter))]
public sealed class JobsController(CloudVistaDb db, UsuarioAtualServico usuarios) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obter(long id, CancellationToken ct)
    {
        var usuario = await usuarios.ObterAsync(ct)
            ?? throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Acesso negado.");

        var job = await db.ExecutarComContextoAsync(usuario, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(JobsSql.PorId, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;

            JsonElement? resumo = null;
            if (!r.IsDBNull(7))
            {
                using var doc = JsonDocument.Parse(r.GetString(7));
                resumo = doc.RootElement.Clone();
            }
            return new JobSituacao(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4),
                r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                resumo);
        }, ct);

        return job is null ? NotFound(new { erro = "Job não encontrado." }) : Ok(job);
    }
}
