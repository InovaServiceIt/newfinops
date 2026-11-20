using Npgsql;
using NpgsqlTypes;

namespace CloudVista.Worker;

/// <summary>Banco cloudvista com o usuário svc_cv_worker (sem chave mestra, sem credenciais de cloud).</summary>
public sealed class WorkerDb : IAsyncDisposable
{
    public const string ConnectionStringName = "CloudVista";
    private readonly NpgsqlDataSource? _dataSource;

    public WorkerDb(IConfiguration configuracao, ILogger<WorkerDb> logger)
    {
        var cs = configuracao.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(cs) || cs.Contains("YOUR_CONNECTION_STRING_HERE"))
        {
            logger.LogCritical("ConnectionStrings:{Name} não configurada. O worker não tem o que fazer.", ConnectionStringName);
            return;
        }
        try
        {
            var builder = new NpgsqlDataSourceBuilder(cs);
            builder.ConnectionStringBuilder.ApplicationName = "CloudVista-Worker";
            _dataSource = builder.Build();
        }
        catch (Exception ex)
        {
            // Nunca logar a exceção: a de parse ecoa trechos da connection string (senha).
            logger.LogCritical("ConnectionStrings:{Name} inválida (formato). Tipo do erro: {Tipo}.", ConnectionStringName, ex.GetType().Name);
        }
    }

    public bool Configurado => _dataSource is not null;

    public async Task<NpgsqlConnection> AbrirAsync(CancellationToken ct) =>
        _dataSource is null ? throw new InvalidOperationException("Banco não configurado.") : await _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>SQL do worker (parametrizado, sem SELECT *).</summary>
public static class WorkerSql
{
    public const string Parametro = "SELECT valor FROM parametro WHERE chave = @chave";

    public const string AgendasAtivas = @"
SELECT id, nome, tipo_job, cliente_id, cloud_conta_id, cron, fuso, parametros::text
FROM job_agenda
WHERE ativo
ORDER BY id";

    public const string AgendaPorId = @"
SELECT id, nome, tipo_job, cliente_id, cloud_conta_id, cron, fuso, parametros::text
FROM job_agenda
WHERE id = @id AND ativo";

    /// <summary>Contas alvo: ativas, de cliente ativo, com credencial ATIVA, filtradas pela agenda.</summary>
    private const string ContasAlvo = @"
FROM cloud_conta cc
JOIN cliente c     ON c.id = cc.cliente_id
JOIN credencial cr ON cr.cloud_conta_id = cc.id AND cr.status = 'ATIVA'
WHERE cc.ativo
  AND c.ativo
  AND (@cliente_id IS NULL OR cc.cliente_id = @cliente_id)
  AND (@cloud_conta_id IS NULL OR cc.id = @cloud_conta_id)";

    public const string ContarContasAlvo = "SELECT count(*) " + ContasAlvo;

    /// <summary>
    /// 1 job por conta alvo. ON CONFLICT DO NOTHING: o índice único parcial (conta, tipo) em
    /// PENDENTE/EXECUTANDO impede duplicar job em andamento; a conta é pulada.
    /// </summary>
    public const string EnfileirarDaAgenda = @"
INSERT INTO job_execucao (tipo_job, cliente_id, cloud_conta_id, origem, agenda_id, prioridade, escopo)
SELECT @tipo_job, cc.cliente_id, cc.id, 'AGENDADA', @agenda_id, @prioridade, CAST(@escopo AS jsonb)
" + ContasAlvo + @"
ON CONFLICT DO NOTHING
RETURNING id";

    public const string ExecutandoParaZelador = @"
SELECT id, tentativas, heartbeat_em, iniciado_em, criado_em
FROM job_execucao
WHERE status = 'EXECUTANDO'
FOR UPDATE SKIP LOCKED";

    public const string VoltarParaPendente = @"
UPDATE job_execucao
SET status = 'PENDENTE', executor = NULL, iniciado_em = NULL, heartbeat_em = NULL
WHERE id = @id AND status = 'EXECUTANDO'";

    public const string MarcarErroTimeout = @"
UPDATE job_execucao
SET status = 'ERRO', erro = @erro, finalizado_em = now()
WHERE id = @id AND status = 'EXECUTANDO'";
}

/// <summary>Leitura de parâmetros (sem cache longo: lidos a cada ciclo) com defaults seguros.</summary>
public sealed class ParametroRepositorio(WorkerDb db)
{
    public async Task<int> InteiroAsync(string chave, int padrao, int min, int max, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var cmd = new NpgsqlCommand(WorkerSql.Parametro, conn);
        cmd.Parameters.Add(new NpgsqlParameter("chave", NpgsqlDbType.Varchar) { Value = chave });
        var valor = await cmd.ExecuteScalarAsync(ct) as string;
        return Parametros.Inteiro(valor, padrao, min, max);
    }
}

public sealed class AgendaRepositorio(WorkerDb db)
{
    public async Task<List<AgendaItem>> AtivasAsync(CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var cmd = new NpgsqlCommand(WorkerSql.AgendasAtivas, conn);
        return await LerAsync(cmd, ct);
    }

    public async Task<AgendaItem?> AtivaPorIdAsync(long id, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var cmd = new NpgsqlCommand(WorkerSql.AgendaPorId, conn);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        return (await LerAsync(cmd, ct)).FirstOrDefault();
    }

    /// <summary>Enfileira 1 job por conta alvo. Retorna (contas alvo, jobs criados).</summary>
    public async Task<(long Alvo, int Criados)> EnfileirarAsync(AgendaItem a, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        long alvo;
        await using (var cnt = new NpgsqlCommand(WorkerSql.ContarContasAlvo, conn, tx))
        {
            AdicionarFiltros(cnt, a);
            alvo = (long)(await cnt.ExecuteScalarAsync(ct))!;
        }

        var criados = 0;
        await using (var ins = new NpgsqlCommand(WorkerSql.EnfileirarDaAgenda, conn, tx))
        {
            AdicionarFiltros(ins, a);
            ins.Parameters.Add(new NpgsqlParameter("tipo_job", NpgsqlDbType.Varchar) { Value = a.TipoJob });
            ins.Parameters.Add(new NpgsqlParameter("agenda_id", NpgsqlDbType.Bigint) { Value = a.Id });
            ins.Parameters.Add(new NpgsqlParameter("prioridade", NpgsqlDbType.Smallint) { Value = (short)100 });
            ins.Parameters.Add(new NpgsqlParameter("escopo", NpgsqlDbType.Text) { Value = Json.ObjetoOuVazio(a.ParametrosJson) });
            await using var r = await ins.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) criados++;
        }

        await tx.CommitAsync(ct);
        return (alvo, criados);
    }

    private static void AdicionarFiltros(NpgsqlCommand cmd, AgendaItem a)
    {
        cmd.Parameters.Add(new NpgsqlParameter("cliente_id", NpgsqlDbType.Bigint) { Value = (object?)a.ClienteId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("cloud_conta_id", NpgsqlDbType.Bigint) { Value = (object?)a.CloudContaId ?? DBNull.Value });
    }

    private static async Task<List<AgendaItem>> LerAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<AgendaItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            lista.Add(new AgendaItem(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetInt64(4),
                r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? "{}" : r.GetString(7)));
        }
        return lista;
    }
}

public sealed class FilaRepositorio(WorkerDb db)
{
    /// <summary>Aplica a regra do zelador aos jobs EXECUTANDO (com trava SKIP LOCKED). Retorna (repostos, com erro).</summary>
    public async Task<(int Repostos, int Erros)> ZelarAsync(DateTimeOffset agora, TimeSpan timeout, int maxTentativas, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var jobs = new List<JobEmExecucao>();
        await using (var sel = new NpgsqlCommand(WorkerSql.ExecutandoParaZelador, conn, tx))
        await using (var r = await sel.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                jobs.Add(new JobEmExecucao(
                    r.GetInt64(0), r.GetInt16(1),
                    r.IsDBNull(2) ? null : r.GetFieldValue<DateTimeOffset>(2),
                    r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3),
                    r.GetFieldValue<DateTimeOffset>(4)));
            }
        }

        int repostos = 0, erros = 0;
        foreach (var job in jobs)
        {
            var decisao = RegraZelador.Decidir(job, agora, timeout, maxTentativas);
            if (decisao == DecisaoZelador.Manter) continue;

            var sql = decisao == DecisaoZelador.VoltarParaPendente ? WorkerSql.VoltarParaPendente : WorkerSql.MarcarErroTimeout;
            await using var upd = new NpgsqlCommand(sql, conn, tx);
            upd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = job.Id });
            if (decisao == DecisaoZelador.MarcarErro)
                upd.Parameters.Add(new NpgsqlParameter("erro", NpgsqlDbType.Varchar) { Value = RegraZelador.MensagemTimeout });
            await upd.ExecuteNonQueryAsync(ct);

            if (decisao == DecisaoZelador.VoltarParaPendente) repostos++; else erros++;
        }

        await tx.CommitAsync(ct);
        return (repostos, erros);
    }
}
