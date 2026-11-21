using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Quartz;

namespace CloudVista.Worker;

public static class ImportadorSql
{
    public const string ChaveIntervalo = "importador.intervalo_seg";
    public const int IntervaloPadrao = 30;
    public const int MinutosPreso = 30;

    /// <summary>Job IMPORTANDO sem andamento há 30 min (worker reiniciado no meio) volta para COLETADO.</summary>
    public const string RecuperarPresos = @"
UPDATE job_execucao SET status = 'COLETADO'
WHERE status = 'IMPORTANDO' AND heartbeat_em < now() - make_interval(mins => @minutos)";

    public const string Claim = @"
UPDATE job_execucao SET status = 'IMPORTANDO', heartbeat_em = now()
WHERE id = (SELECT id FROM job_execucao
            WHERE status = 'COLETADO'
            ORDER BY coletado_em NULLS LAST, id
            FOR UPDATE SKIP LOCKED
            LIMIT 1)
RETURNING id, tipo_job, cliente_id, cloud_conta_id, coletado_em";

    public const string Bruta = "SELECT sha256, conteudo FROM coleta_bruta WHERE job_execucao_id = @job";
    public const string Provedor = "SELECT provedor FROM cloud_conta WHERE id = @conta";

    public const string ApagarFato = @"
DELETE FROM custo_fato
WHERE cloud_conta_id = @conta AND dimensao = @dimensao AND granularidade = @granularidade
  AND periodo BETWEEN @de AND @ate
  AND (@chave2 IS NULL OR chave2 = @chave2)";

    public const string InserirFato = @"
INSERT INTO custo_fato (cloud_conta_id, cliente_id, granularidade, periodo, dimensao, chave, chave2, valor, moeda, job_execucao_id, atualizado_em)
SELECT @conta, @cliente, @granularidade, t.periodo, @dimensao, t.chave, t.chave2, t.valor, t.moeda, @job, now()
FROM unnest(@periodos, @chaves, @chaves2, @valores, @moedas) AS t(periodo, chave, chave2, valor, moeda)
ON CONFLICT (cloud_conta_id, granularidade, periodo, dimensao, chave, chave2, moeda)
DO UPDATE SET valor = EXCLUDED.valor, cliente_id = EXCLUDED.cliente_id, job_execucao_id = EXCLUDED.job_execucao_id, atualizado_em = now()";

    public const string ApagarPrevisao = "DELETE FROM custo_previsao WHERE cloud_conta_id = @conta AND dia BETWEEN @de AND @ate";
    public const string InserirPrevisao = @"
INSERT INTO custo_previsao (cloud_conta_id, cliente_id, dia, moeda, valor, job_execucao_id, gerada_em)
SELECT @conta, @cliente, t.periodo, t.moeda, t.valor, @job, now()
FROM unnest(@periodos, @moedas, @valores) AS t(periodo, moeda, valor)
ON CONFLICT (cloud_conta_id, dia, moeda)
DO UPDATE SET valor = EXCLUDED.valor, cliente_id = EXCLUDED.cliente_id, job_execucao_id = EXCLUDED.job_execucao_id, gerada_em = now()";

    public const string ApagarCredito = "DELETE FROM custo_credito WHERE cloud_conta_id = @conta AND mes BETWEEN @de AND @ate";
    public const string InserirCredito = @"
INSERT INTO custo_credito (cloud_conta_id, cliente_id, mes, moeda, valor, job_execucao_id, atualizado_em)
SELECT @conta, @cliente, t.periodo, t.moeda, t.valor, @job, now()
FROM unnest(@periodos, @moedas, @valores) AS t(periodo, moeda, valor)
ON CONFLICT (cloud_conta_id, mes, moeda)
DO UPDATE SET valor = EXCLUDED.valor, cliente_id = EXCLUDED.cliente_id, job_execucao_id = EXCLUDED.job_execucao_id, atualizado_em = now()";

    public const string Concluir = @"
UPDATE job_execucao
SET status = @status, finalizado_em = now(), erro = @erro,
    resumo = COALESCE(resumo, '{}'::jsonb) || jsonb_build_object('importacao', CAST(@importacao AS jsonb))
WHERE id = @job AND status = 'IMPORTANDO'";
}

public sealed record JobImportacao(long Id, string TipoJob, long ClienteId, long ContaId, DateTimeOffset? ColetadoEm = null);

/// <summary>Lê coleta_bruta, valida o sha256 e grava custo_fato/custo_previsao/custo_credito numa transação.</summary>
public sealed class ImportadorRepositorio(WorkerDb db, ILogger<ImportadorRepositorio> logger)
{
    public async Task<int> RecuperarPresosAsync(CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var cmd = new NpgsqlCommand(ImportadorSql.RecuperarPresos, conn);
        cmd.Parameters.Add(new NpgsqlParameter("minutos", NpgsqlDbType.Integer) { Value = ImportadorSql.MinutosPreso });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<JobImportacao?> PegarAsync(CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var cmd = new NpgsqlCommand(ImportadorSql.Claim, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new JobImportacao(r.GetInt64(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3),
                r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    /// <summary>Importa um job IMPORTANDO. Lança ImportacaoException (mensagem segura) em falha esperada.</summary>
    public async Task<Dictionary<string, object?>> ImportarAsync(JobImportacao job, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        string? provedor;
        await using (var cmd = new NpgsqlCommand(ImportadorSql.Provedor, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
            provedor = await cmd.ExecuteScalarAsync(ct) as string;
        }

        Dictionary<string, object?> resumo;
        if (provedor != "OCI")
        {
            resumo = ResumoSemDados("importador do provedor ainda não disponível");
        }
        else
        {
            using var doc = await LerDocumentoAsync(conn, tx, job, ct);
            var raiz = doc.RootElement;

            // Ordem: compartimentos → recursos → custo (tudo na mesma transação).
            Dictionary<string, object?>? inventario = null;
            if (ImportaInventario(job.TipoJob))
            {
                var planoInv = ParserInventarioOci.Planejar(raiz);
                inventario = await InventarioGravacao.GravarAsync(conn, tx, job, job.ColetadoEm ?? DateTimeOffset.UtcNow, planoInv, ct);
                foreach (var aviso in (List<string>)inventario["avisos"]!)
                    logger.LogWarning("Inventário do job {Job}: {Aviso}.", job.Id, aviso);
            }

            if (job.TipoJob != "COLETA_INVENTARIO" && raiz.TryGetProperty("cost", out var cost) && cost.ValueKind == JsonValueKind.Object)
            {
                var plano = ParserCustoOci.Planejar(cost);
                await GravarAsync(conn, tx, job, plano, ct);
                resumo = plano.Resumo();
            }
            else
            {
                resumo = ResumoSemDados("sem custo");
            }
            if (inventario is not null) resumo["inventario"] = inventario;
        }

        await ConcluirAsync(conn, tx, job.Id, "IMPORTADO", null, resumo, ct);
        await tx.CommitAsync(ct);
        return resumo;
    }

    /// <summary>Marca ERRO (transação própria; a da importação já foi desfeita).</summary>
    public async Task MarcarErroAsync(long jobId, string erro, CancellationToken ct)
    {
        await using var conn = await db.AbrirAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await ConcluirAsync(conn, tx, jobId, "ERRO", erro.Length <= 1000 ? erro : erro[..999] + "…",
            new Dictionary<string, object?> { ["erro"] = true }, ct);
        await tx.CommitAsync(ct);
    }

    private static Dictionary<string, object?> ResumoSemDados(string aviso) => new()
    {
        ["linhas"] = new Dictionary<string, int>(),
        ["periodo_custo"] = null,
        ["moedas"] = Array.Empty<string>(),
        ["avisos"] = new[] { aviso },
    };

    /// <summary>Inventário: só COLETA_INVENTARIO e COLETA_COMPLETA.</summary>
    public static bool ImportaInventario(string tipoJob) => tipoJob is "COLETA_INVENTARIO" or "COLETA_COMPLETA";

    /// <summary>JSON completo da coleta (sha256 conferido).</summary>
    private static async Task<JsonDocument> LerDocumentoAsync(NpgsqlConnection conn, NpgsqlTransaction tx, JobImportacao job, CancellationToken ct)
    {
        string? sha;
        byte[] conteudo;
        await using (var cmd = new NpgsqlCommand(ImportadorSql.Bruta, conn, tx))
        {
            cmd.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = job.Id });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new ImportacaoException("coleta_bruta não encontrada para o job");
            sha = r.IsDBNull(0) ? null : r.GetString(0);
            conteudo = r.GetFieldValue<byte[]>(1);
        }

        if (!ColetaBruta.Sha256Confere(conteudo, sha))
            throw new ImportacaoException("sha256 da coleta_bruta não confere (conteúdo corrompido)");

        byte[] json;
        try
        {
            json = ColetaBruta.Descomprimir(conteudo);
        }
        catch (InvalidDataException)
        {
            throw new ImportacaoException("coleta_bruta não é um gzip válido");
        }

        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                doc.Dispose();
                throw new ImportacaoException("coleta_bruta não contém um objeto JSON");
            }
            return doc;
        }
        catch (JsonException)
        {
            throw new ImportacaoException("coleta_bruta não contém JSON válido");
        }
    }

    private async Task GravarAsync(NpgsqlConnection conn, NpgsqlTransaction tx, JobImportacao job, PlanoImportacao plano, CancellationToken ct)
    {
        foreach (var bloco in plano.Fatos.Where(b => b.Aplicar && b.Linhas.Count > 0))
        {
            await using (var del = new NpgsqlCommand(ImportadorSql.ApagarFato, conn, tx))
            {
                del.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
                del.Parameters.Add(new NpgsqlParameter("dimensao", NpgsqlDbType.Varchar) { Value = bloco.Dimensao });
                del.Parameters.Add(new NpgsqlParameter("granularidade", NpgsqlDbType.Varchar) { Value = bloco.Granularidade });
                del.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = bloco.De!.Value });
                del.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = bloco.Ate!.Value });
                del.Parameters.Add(new NpgsqlParameter("chave2", NpgsqlDbType.Varchar) { Value = (object?)bloco.FiltroChave2 ?? DBNull.Value });
                await del.ExecuteNonQueryAsync(ct);
            }

            await using var ins = new NpgsqlCommand(ImportadorSql.InserirFato, conn, tx);
            ins.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
            ins.Parameters.Add(new NpgsqlParameter("cliente", NpgsqlDbType.Bigint) { Value = job.ClienteId });
            ins.Parameters.Add(new NpgsqlParameter("granularidade", NpgsqlDbType.Varchar) { Value = bloco.Granularidade });
            ins.Parameters.Add(new NpgsqlParameter("dimensao", NpgsqlDbType.Varchar) { Value = bloco.Dimensao });
            ins.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = job.Id });
            ins.Parameters.Add(new NpgsqlParameter("periodos", NpgsqlDbType.Array | NpgsqlDbType.Date) { Value = bloco.Linhas.Select(l => l.Periodo).ToArray() });
            ins.Parameters.Add(new NpgsqlParameter("chaves", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = bloco.Linhas.Select(l => l.Chave).ToArray() });
            ins.Parameters.Add(new NpgsqlParameter("chaves2", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = bloco.Linhas.Select(l => l.Chave2).ToArray() });
            ins.Parameters.Add(new NpgsqlParameter("valores", NpgsqlDbType.Array | NpgsqlDbType.Numeric) { Value = bloco.Linhas.Select(l => l.Valor).ToArray() });
            ins.Parameters.Add(new NpgsqlParameter("moedas", NpgsqlDbType.Array | NpgsqlDbType.Char) { Value = bloco.Linhas.Select(l => l.Moeda).ToArray() });
            await ins.ExecuteNonQueryAsync(ct);
        }

        await GravarSerieAsync(conn, tx, job, plano.Previsao, ImportadorSql.ApagarPrevisao, ImportadorSql.InserirPrevisao, ct);
        await GravarSerieAsync(conn, tx, job, plano.Credito, ImportadorSql.ApagarCredito, ImportadorSql.InserirCredito, ct);

        foreach (var aviso in plano.Avisos)
            logger.LogWarning("Importação do job {Job}: {Aviso}.", job.Id, aviso);
    }

    private static async Task GravarSerieAsync(NpgsqlConnection conn, NpgsqlTransaction tx, JobImportacao job, BlocoSerie serie,
        string sqlApagar, string sqlInserir, CancellationToken ct)
    {
        if (!serie.Aplicar || serie.Linhas.Count == 0) return;

        await using (var del = new NpgsqlCommand(sqlApagar, conn, tx))
        {
            del.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
            del.Parameters.Add(new NpgsqlParameter("de", NpgsqlDbType.Date) { Value = serie.De!.Value });
            del.Parameters.Add(new NpgsqlParameter("ate", NpgsqlDbType.Date) { Value = serie.Ate!.Value });
            await del.ExecuteNonQueryAsync(ct);
        }

        await using var ins = new NpgsqlCommand(sqlInserir, conn, tx);
        ins.Parameters.Add(new NpgsqlParameter("conta", NpgsqlDbType.Bigint) { Value = job.ContaId });
        ins.Parameters.Add(new NpgsqlParameter("cliente", NpgsqlDbType.Bigint) { Value = job.ClienteId });
        ins.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = job.Id });
        ins.Parameters.Add(new NpgsqlParameter("periodos", NpgsqlDbType.Array | NpgsqlDbType.Date) { Value = serie.Linhas.Select(l => l.Periodo).ToArray() });
        ins.Parameters.Add(new NpgsqlParameter("moedas", NpgsqlDbType.Array | NpgsqlDbType.Char) { Value = serie.Linhas.Select(l => l.Moeda).ToArray() });
        ins.Parameters.Add(new NpgsqlParameter("valores", NpgsqlDbType.Array | NpgsqlDbType.Numeric) { Value = serie.Linhas.Select(l => l.Valor).ToArray() });
        await ins.ExecuteNonQueryAsync(ct);
    }

    private static async Task ConcluirAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long jobId, string status, string? erro,
        Dictionary<string, object?> resumo, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(ImportadorSql.Concluir, conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = status });
        cmd.Parameters.Add(new NpgsqlParameter("erro", NpgsqlDbType.Varchar) { Value = (object?)erro ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("importacao", NpgsqlDbType.Text) { Value = JsonSerializer.Serialize(resumo) });
        cmd.Parameters.Add(new NpgsqlParameter("job", NpgsqlDbType.Bigint) { Value = jobId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Falha esperada da importação: a mensagem é segura para gravar em job_execucao.erro.</summary>
public sealed class ImportacaoException(string mensagem) : Exception(mensagem);

/// <summary>
/// A cada importador.intervalo_seg: devolve à fila importações presas e importa os jobs COLETADO
/// (um por vez, até esvaziar ou 20 por ciclo).
/// </summary>
[DisallowConcurrentExecution]
public sealed class ImportarColetasJob(ImportadorRepositorio repo, ParametroRepositorio parametros, ILogger<ImportarColetasJob> logger) : IJob
{
    public static readonly JobKey Chave = new("importar-coletas", "sistema");
    public static readonly TriggerKey Gatilho = new("importar-coletas", "sistema");
    private const int MaxPorCiclo = 20;

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        try
        {
            var presos = await repo.RecuperarPresosAsync(ct);
            if (presos > 0) logger.LogWarning("Importador: {Presos} job(s) preso(s) em IMPORTANDO devolvido(s) para COLETADO.", presos);

            for (var i = 0; i < MaxPorCiclo && !ct.IsCancellationRequested; i++)
            {
                var job = await repo.PegarAsync(ct);
                if (job is null) break;
                await ImportarAsync(job, ct);
            }
            await AjustarIntervaloAsync(context.Scheduler, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Falha no importador. Tipo: {Tipo}. SQLSTATE: {SqlState}. Detalhe: {Detalhe}.",
                ex.GetType().Name, (ex as PostgresException)?.SqlState ?? "-", Diagnostico.De(ex));
        }
    }

    private async Task ImportarAsync(JobImportacao job, CancellationToken ct)
    {
        try
        {
            var resumo = await repo.ImportarAsync(job, ct);
            logger.LogInformation("Job {Job} importado: {Resumo}", job.Id, JsonSerializer.Serialize(resumo["linhas"]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var mensagem = ex is ImportacaoException
                ? ex.Message
                : $"Falha na importação ({ex.GetType().Name}{((ex as PostgresException)?.SqlState is { } s ? $" {s}" : "")}): {Diagnostico.De(ex)}";
            logger.LogError("Job {Job}: importação falhou: {Mensagem}", job.Id, mensagem);
            await repo.MarcarErroAsync(job.Id, mensagem, ct);
        }
    }

    private async Task AjustarIntervaloAsync(IScheduler scheduler, CancellationToken ct)
    {
        var segundos = await parametros.InteiroAsync(ImportadorSql.ChaveIntervalo, ImportadorSql.IntervaloPadrao, 5, 3600, ct);
        if (await scheduler.GetTrigger(Gatilho, ct) is ISimpleTrigger t && t.RepeatInterval == TimeSpan.FromSeconds(segundos))
            return;
        await scheduler.RescheduleJob(Gatilho, TriggerBuilder.Create()
            .WithIdentity(Gatilho).ForJob(Chave)
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(segundos))
            .WithSimpleSchedule(s => s.WithIntervalInSeconds(segundos).RepeatForever().WithMisfireHandlingInstructionNextWithRemainingCount())
            .Build(), ct);
    }
}
