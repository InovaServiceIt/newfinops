using System.Text.Json;
using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Npgsql;
using NpgsqlTypes;

namespace Finops.Backend.Coletas;

public static class ColetasSql
{
    public const string Parametro = "SELECT valor FROM parametro WHERE chave = @chave";

    public const string ContaParaColeta = @"
SELECT cc.cliente_id, cc.ativo, c.ativo, cr.status
FROM cloud_conta cc
JOIN cliente c ON c.id = cc.cliente_id
LEFT JOIN credencial cr ON cr.cloud_conta_id = cc.id
WHERE cc.id = @conta_id";

    public const string UltimaColetaManual = @"
SELECT max(criado_em)
FROM job_execucao
WHERE cloud_conta_id = @conta_id AND origem = 'MANUAL' AND tipo_job = ANY(@tipos)";

    /// <summary>ON CONFLICT DO NOTHING: coleta da mesma conta+tipo em andamento → sem linha = 409.</summary>
    public const string Enfileirar = @"
INSERT INTO job_execucao (tipo_job, cliente_id, cloud_conta_id, origem, solicitado_por, prioridade, escopo)
VALUES (@tipo_job, @cliente_id, @conta_id, 'MANUAL', @usuario_id, 10, CAST(@escopo AS jsonb))
ON CONFLICT DO NOTHING
RETURNING id";

    private const string FiltroColetas = @"
FROM job_execucao j
LEFT JOIN cliente c      ON c.id = j.cliente_id
LEFT JOIN cloud_conta cc ON cc.id = j.cloud_conta_id
LEFT JOIN usuario u      ON u.id = j.solicitado_por
WHERE j.criado_em >= @de AND j.criado_em < @ate
  AND (@cliente_id IS NULL OR j.cliente_id = @cliente_id)
  AND (@conta_id IS NULL OR j.cloud_conta_id = @conta_id)
  AND (@status IS NULL OR j.status = @status)
  AND (@tipo_job IS NULL OR j.tipo_job = @tipo_job)";

    public const string ContarColetas = "SELECT count(*) " + FiltroColetas;

    public const string ListarColetas = @"
SELECT j.id, j.tipo_job, c.nome, cc.nome, cc.provedor, j.origem, u.nome, j.status, j.tentativas,
       j.criado_em, j.iniciado_em, j.finalizado_em,
       CASE WHEN j.iniciado_em IS NULL THEN NULL
            -- Duração da coleta = tempo do coletor (coletado_em): reimportar não a altera.
            ELSE EXTRACT(EPOCH FROM (COALESCE(j.coletado_em, j.finalizado_em, now()) - j.iniciado_em))::float8 END,
       j.erro, j.resumo::text, j.coletor_versao,
       (j.status IN ('IMPORTADO', 'ERRO') AND j.coletado_em IS NOT NULL
        AND j.tipo_job IN ('COLETA_COMPLETA', 'COLETA_CUSTO', 'COLETA_INVENTARIO')) AS pode_reimportar
" + FiltroColetas + @"
ORDER BY j.criado_em DESC, j.id DESC
LIMIT @limite OFFSET @deslocamento";

    /// <summary>Função SECURITY DEFINER (DDL do HLGDR): valida perfil/status/bruta e devolve o status anterior.</summary>
    public const string Reimportar = "SELECT reimportar_job(@id)";

    public const string ListarAgenda = @"
SELECT a.id, a.nome, a.tipo_job, a.cliente_id, c.nome, a.cloud_conta_id, cc.nome, a.cron, a.fuso,
       CASE WHEN a.parametros->>'cost_days' ~ '^[0-9]+$' THEN (a.parametros->>'cost_days')::int END,
       a.ativo, a.atualizado_em, a.atualizado_por
FROM job_agenda a
LEFT JOIN cliente c      ON c.id = a.cliente_id
LEFT JOIN cloud_conta cc ON cc.id = a.cloud_conta_id
ORDER BY a.nome, a.id";

    public const string CriarAgenda = @"
INSERT INTO job_agenda (nome, tipo_job, cliente_id, cloud_conta_id, cron, fuso, parametros, ativo, atualizado_por)
VALUES (@nome, @tipo_job, @cliente_id, @conta_id, @cron, @fuso, CAST(@parametros AS jsonb), true, @por)
RETURNING id";

    public const string AtualizarAgenda = @"
UPDATE job_agenda
SET nome = @nome, tipo_job = @tipo_job, cliente_id = @cliente_id, cloud_conta_id = @conta_id,
    cron = @cron, fuso = @fuso, parametros = CAST(@parametros AS jsonb), atualizado_por = @por, atualizado_em = now()
WHERE id = @id";

    public const string AtivarAgenda = "UPDATE job_agenda SET ativo = @ativo, atualizado_por = @por, atualizado_em = now() WHERE id = @id";

    public const string ListarParametros = "SELECT chave, valor, tipo, descricao, atualizado_em, atualizado_por FROM parametro ORDER BY chave";
    public const string TipoParametro = "SELECT tipo FROM parametro WHERE chave = @chave FOR UPDATE";
    public const string AtualizarParametro = "UPDATE parametro SET valor = @valor, atualizado_por = @por, atualizado_em = now() WHERE chave = @chave";
}

public sealed class ColetasRepositorio(CloudVistaDb db, TimeProvider relogio, ILogger<ColetasRepositorio> logger)
{
    // ── Permissão ────────────────────────────────────────────────────────────

    /// <summary>Lê coleta.manual.perfis_permitidos (a cada chamada). JSON inválido → nega e loga aviso.</summary>
    private async Task<bool> PodeColetarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, UsuarioAtual u, CancellationToken ct)
    {
        var json = await ParametroAsync(conn, tx, ColetarRegras.ChavePerfis, ct);
        var permitidos = ColetarRegras.PerfisPermitidos(json);
        if (permitidos is null)
            logger.LogWarning("Parâmetro {Chave} ausente ou com JSON inválido: coleta manual negada a todos.", ColetarRegras.ChavePerfis);
        return ColetarRegras.PodeColetar(u.Perfil, permitidos);
    }

    public Task<bool> PodeColetarAsync(UsuarioAtual u, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, (conn, tx) => PodeColetarAsync(conn, tx, u, ct), ct);

    // ── Coletar agora ────────────────────────────────────────────────────────

    public Task<long> ColetarAsync(UsuarioAtual u, long contaId, string tipoJob, int? costDays, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            if (!await PodeColetarAsync(conn, tx, u, ct))
                throw new ErroNegocioException(StatusCodes.Status403Forbidden, "Seu perfil não pode disparar coletas.");

            long clienteId;
            await using (var cmd = new NpgsqlCommand(ColetasSql.ContaParaColeta, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw ErroNegocioException.NaoEncontrado("Conta não encontrada.");
                clienteId = r.GetInt64(0);
                if (!r.GetBoolean(1) || !r.GetBoolean(2))
                    throw ErroNegocioException.Validacao("A conta e o cliente precisam estar ativos para coletar.");
                if (r.IsDBNull(3) || r.GetString(3) != "ATIVA")
                    throw ErroNegocioException.Validacao("Teste a credencial antes de coletar.");
            }

            var intervalo = ColetarRegras.IntervaloMinimo(await ParametroAsync(conn, tx, ColetarRegras.ChaveIntervalo, ct));
            DateTimeOffset? ultima;
            await using (var cmd = new NpgsqlCommand(ColetasSql.UltimaColetaManual, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                cmd.Parameters.Add(new NpgsqlParameter("tipos", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = TiposJob.Coletas });
                var v = await cmd.ExecuteScalarAsync(ct);
                ultima = v is DateTime dt ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)) : v as DateTimeOffset?;
            }
            var restantes = ColetarRegras.MinutosRestantes(ultima, relogio.GetUtcNow(), intervalo);
            if (restantes > 0)
                throw new ErroNegocioException(StatusCodes.Status429TooManyRequests, $"Aguarde {restantes} min para coletar esta conta novamente.");

            var escopo = costDays is { } d ? JsonSerializer.Serialize(new { cost_days = d }) : "{}";
            await using (var cmd = new NpgsqlCommand(ColetasSql.Enfileirar, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("tipo_job", NpgsqlDbType.Varchar) { Value = tipoJob });
                cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = clienteId });
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = contaId });
                cmd.Parameters.Add(new NpgsqlParameter("usuario_id", NpgsqlDbType.Bigint) { Value = u.Id });
                cmd.Parameters.Add(new NpgsqlParameter("escopo", NpgsqlDbType.Text) { Value = escopo });
                return await cmd.ExecuteScalarAsync(ct) is long id
                    ? id
                    : throw ErroNegocioException.Conflito("Já existe uma coleta em andamento para esta conta.");
            }
        }, ct);

    // ── Lista de coletas ─────────────────────────────────────────────────────

    public Task<PaginaColetas> ListarAsync(UsuarioAtual u, FiltroColetas f, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            long total;
            await using (var cnt = new NpgsqlCommand(ColetasSql.ContarColetas, conn, tx))
            {
                ParametrosFiltro(cnt, f);
                total = (long)(await cnt.ExecuteScalarAsync(ct))!;
            }

            var itens = new List<ColetaItem>();
            await using (var cmd = new NpgsqlCommand(ColetasSql.ListarColetas, conn, tx))
            {
                ParametrosFiltro(cmd, f);
                cmd.Parameters.Add(new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = f.Tamanho });
                cmd.Parameters.Add(new NpgsqlParameter("deslocamento", NpgsqlDbType.Integer) { Value = (f.Pagina - 1) * f.Tamanho });
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    JsonElement? resumo = null;
                    if (!r.IsDBNull(14))
                    {
                        using var doc = JsonDocument.Parse(r.GetString(14));
                        resumo = doc.RootElement.Clone();
                    }
                    itens.Add(new ColetaItem(
                        r.GetInt64(0), r.GetString(1), Txt(r, 2), Txt(r, 3), Txt(r, 4), r.GetString(5), Txt(r, 6),
                        r.GetString(7), r.GetInt16(8), r.GetFieldValue<DateTimeOffset>(9), Data(r, 10), Data(r, 11),
                        r.IsDBNull(12) ? null : Math.Round(r.GetDouble(12), 1), Txt(r, 13), resumo, Txt(r, 15), r.GetBoolean(16)));
                }
            }
            return new PaginaColetas(itens, total, f.Pagina, f.Tamanho);
        }, ct);

    private static void ParametrosFiltro(NpgsqlCommand cmd, FiltroColetas f)
    {
        cmd.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.TimestampTz) { Value = f.De.ToUniversalTime() });
        cmd.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.TimestampTz) { Value = f.Ate.ToUniversalTime() });
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = (object?)f.ClienteId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)f.ContaId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = (object?)f.Status ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("tipo_job", NpgsqlDbType.Varchar) { Value = (object?)f.TipoJob ?? DBNull.Value });
    }

    // ── Reimportar ───────────────────────────────────────────────────────────

    /// <summary>Volta o job para COLETADO (o importador pega no próximo ciclo). Devolve o status anterior.</summary>
    public async Task<string> ReimportarAsync(UsuarioAtual u, long id, CancellationToken ct)
    {
        try
        {
            var anterior = await db.ExecutarComContextoAsync(u, async (conn, tx) =>
            {
                await using var cmd = new NpgsqlCommand(ColetasSql.Reimportar, conn, tx);
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
                return await cmd.ExecuteScalarAsync(ct) as string ?? "";
            }, ct);
            logger.LogInformation("Reimportação solicitada: usuário {Usuario}, job {Job}, status anterior {Status}.", u.Id, id, anterior);
            return anterior;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.RaiseException)
        {
            logger.LogInformation("Reimportação recusada: usuário {Usuario}, job {Job}, motivo {Motivo}.", u.Id, id, ex.MessageText);
            throw ReimportarRegras.Mapear(ex.MessageText);
        }
    }

    // ── Agenda ───────────────────────────────────────────────────────────────

    public Task<List<AgendaSaida>> ListarAgendaAsync(UsuarioAtual u, CancellationToken ct) =>
        db.ExecutarComContextoAsync(u, async (conn, tx) =>
        {
            var lista = new List<AgendaSaida>();
            await using var cmd = new NpgsqlCommand(ColetasSql.ListarAgenda, conn, tx);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                lista.Add(new AgendaSaida(
                    r.GetInt64(0), r.GetString(1), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetInt64(3), Txt(r, 4),
                    r.IsDBNull(5) ? null : r.GetInt64(5), Txt(r, 6),
                    r.GetString(7), Txt(r, 8) ?? CronRegras.FusoPadrao,
                    r.IsDBNull(9) ? null : r.GetInt32(9), r.GetBoolean(10), Data(r, 11), Txt(r, 12)));
            }
            return lista;
        }, ct);

    public async Task<long> SalvarAgendaAsync(UsuarioAtual admin, long? id, AgendaEntrada entrada, CancellationToken ct)
    {
        var v = AgendaRegras.Validar(entrada, relogio.GetUtcNow());
        var parametros = v.CostDays is { } d ? JsonSerializer.Serialize(new { cost_days = d }) : "{}";
        try
        {
            return await db.ExecutarComContextoAsync(admin, async (conn, tx) =>
            {
                await using var cmd = new NpgsqlCommand(id is null ? ColetasSql.CriarAgenda : ColetasSql.AtualizarAgenda, conn, tx);
                cmd.Parameters.Add(new NpgsqlParameter("nome", NpgsqlDbType.Varchar) { Value = v.Nome });
                cmd.Parameters.Add(new NpgsqlParameter("tipo_job", NpgsqlDbType.Varchar) { Value = v.Tipo });
                cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = (object?)v.ClienteId ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("conta_id", NpgsqlDbType.Bigint) { Value = (object?)v.ContaId ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("cron", NpgsqlDbType.Varchar) { Value = v.Cron });
                cmd.Parameters.Add(new NpgsqlParameter("fuso", NpgsqlDbType.Varchar) { Value = v.Fuso });
                cmd.Parameters.Add(new NpgsqlParameter("parametros", NpgsqlDbType.Text) { Value = parametros });
                cmd.Parameters.Add(new NpgsqlParameter("por", NpgsqlDbType.Varchar) { Value = admin.Email });
                if (id is null)
                    return (long)(await cmd.ExecuteScalarAsync(ct))!;

                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id.Value });
                return await cmd.ExecuteNonQueryAsync(ct) == 1 ? id.Value : throw ErroNegocioException.NaoEncontrado("Agenda não encontrada.");
            }, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw ErroNegocioException.Validacao("Alvo: cliente ou conta inexistente.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw ErroNegocioException.Validacao("Dados da agenda inválidos.");
        }
    }

    public Task<bool> AlterarAtivoAgendaAsync(UsuarioAtual admin, long id, bool ativo, CancellationToken ct) =>
        db.ExecutarComContextoAsync(admin, async (conn, tx) =>
        {
            await using var cmd = new NpgsqlCommand(ColetasSql.AtivarAgenda, conn, tx);
            cmd.Parameters.Add(new NpgsqlParameter("ativo", NpgsqlDbType.Boolean) { Value = ativo });
            cmd.Parameters.Add(new NpgsqlParameter("por", NpgsqlDbType.Varchar) { Value = admin.Email });
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
            return await cmd.ExecuteNonQueryAsync(ct) == 1 ? true : throw ErroNegocioException.NaoEncontrado("Agenda não encontrada.");
        }, ct);

    // ── Parâmetros ───────────────────────────────────────────────────────────

    public Task<List<ParametroSaida>> ListarParametrosAsync(UsuarioAtual admin, CancellationToken ct) =>
        db.ExecutarComContextoAsync(admin, async (conn, tx) =>
        {
            var lista = new List<ParametroSaida>();
            await using var cmd = new NpgsqlCommand(ColetasSql.ListarParametros, conn, tx);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lista.Add(new ParametroSaida(r.GetString(0), Txt(r, 1), r.GetString(2), Txt(r, 3), Data(r, 4), Txt(r, 5)));
            return lista;
        }, ct);

    public Task<bool> AtualizarParametroAsync(UsuarioAtual admin, string chave, string? valor, CancellationToken ct) =>
        db.ExecutarComContextoAsync(admin, async (conn, tx) =>
        {
            string tipo;
            await using (var cmd = new NpgsqlCommand(ColetasSql.TipoParametro, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = chave });
                tipo = await cmd.ExecuteScalarAsync(ct) as string ?? throw ErroNegocioException.NaoEncontrado("Parâmetro não encontrado.");
            }
            var normalizado = ParametroRegras.Validar(tipo, valor);
            await using (var cmd = new NpgsqlCommand(ColetasSql.AtualizarParametro, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("valor", NpgsqlDbType.Text) { Value = normalizado });
                cmd.Parameters.Add(new NpgsqlParameter("por", NpgsqlDbType.Varchar) { Value = admin.Email });
                cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = chave });
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return true;
        }, ct);

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static async Task<string?> ParametroAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string chave, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(ColetasSql.Parametro, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = chave });
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    private static string? Txt(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static DateTimeOffset? Data(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetFieldValue<DateTimeOffset>(i);
}
