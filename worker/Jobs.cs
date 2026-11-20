using Quartz;

namespace CloudVista.Worker;

/// <summary>
/// Lê job_agenda ativa e recria/atualiza os triggers Quartz (cron + fuso). Remove os de agendas
/// desativadas ou excluídas. Cron/fuso inválido → aviso com o id da agenda (o worker segue).
/// Também reprograma o próprio intervalo se agenda.sincronizar_seg mudar.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SincronizarAgendaJob(AgendaRepositorio agendas, ParametroRepositorio parametros, ILogger<SincronizarAgendaJob> logger) : IJob
{
    public static readonly JobKey Chave = new("sincronizar-agenda", "sistema");
    public static readonly TriggerKey Gatilho = new("sincronizar-agenda", "sistema");
    public const string GrupoAgenda = "agenda";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var scheduler = context.Scheduler;
        try
        {
            var ativas = await agendas.AtivasAsync(ct);
            var mantidas = new HashSet<TriggerKey>();

            foreach (var agenda in ativas)
            {
                var conv = ConversaoAgenda.Converter(agenda.Cron, agenda.Fuso);
                if (!conv.Valida)
                {
                    logger.LogWarning("Agenda {AgendaId} ignorada: {Motivo}.", agenda.Id, conv.Motivo);
                    continue;
                }

                var triggerKey = new TriggerKey($"agenda-{agenda.Id}", GrupoAgenda);
                mantidas.Add(triggerKey);
                var assinatura = ConversaoAgenda.Assinatura(agenda);

                var atual = await scheduler.GetTrigger(triggerKey, ct);
                if (atual is not null && atual.JobDataMap.GetString("assinatura") == assinatura)
                    continue;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(DispararAgendaJob.Chave)
                    .UsingJobData(DispararAgendaJob.ChaveAgendaId, agenda.Id)
                    .UsingJobData("assinatura", assinatura)
                    .WithCronSchedule(agenda.Cron.Trim(), c => c.InTimeZone(conv.Fuso!).WithMisfireHandlingInstructionDoNothing())
                    .Build();

                if (atual is null)
                    await scheduler.ScheduleJob(trigger, ct);
                else
                    await scheduler.RescheduleJob(triggerKey, trigger, ct);

                logger.LogInformation("Agenda {AgendaId} ({Nome}) programada: {Cron} [{Fuso}].",
                    agenda.Id, agenda.Nome, agenda.Cron, conv.Fuso!.Id);
            }

            // Remove triggers de agendas que não estão mais ativas.
            foreach (var key in await scheduler.GetTriggerKeys(Quartz.Impl.Matchers.GroupMatcher<TriggerKey>.GroupEquals(GrupoAgenda), ct))
            {
                if (mantidas.Contains(key)) continue;
                await scheduler.UnscheduleJob(key, ct);
                logger.LogInformation("Agenda removida do Quartz: {Trigger}.", key.Name);
            }

            await AjustarIntervaloAsync(scheduler, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem detalhes que possam conter dados: tipo da exceção (e SQLSTATE quando houver).
            logger.LogError("Falha ao sincronizar a agenda. Tipo: {Tipo}. SQLSTATE: {SqlState}.",
                ex.GetType().Name, (ex as Npgsql.PostgresException)?.SqlState ?? "-");
        }
    }

    private async Task AjustarIntervaloAsync(IScheduler scheduler, CancellationToken ct)
    {
        var segundos = await parametros.InteiroAsync(Parametros.SincronizarAgendaSeg, Parametros.SincronizarAgendaSegPadrao, 10, 3600, ct);
        if (await scheduler.GetTrigger(Gatilho, ct) is ISimpleTrigger t && t.RepeatInterval == TimeSpan.FromSeconds(segundos))
            return;

        var novo = TriggerBuilder.Create()
            .WithIdentity(Gatilho)
            .ForJob(Chave)
            .StartAt(DateTimeOffset.UtcNow.AddSeconds(segundos))
            .WithSimpleSchedule(s => s.WithIntervalInSeconds(segundos).RepeatForever().WithMisfireHandlingInstructionNextWithRemainingCount())
            .Build();
        await scheduler.RescheduleJob(Gatilho, novo, ct);
        logger.LogInformation("Sincronização da agenda a cada {Segundos} s.", segundos);
    }
}

/// <summary>Disparo de uma linha da agenda: 1 job_execucao (AGENDADA, prioridade 100) por conta alvo.</summary>
[DisallowConcurrentExecution]
public sealed class DispararAgendaJob(AgendaRepositorio agendas, ILogger<DispararAgendaJob> logger) : IJob
{
    public static readonly JobKey Chave = new("disparar-agenda", "sistema");
    public const string ChaveAgendaId = "agendaId";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var agendaId = context.MergedJobDataMap.GetLong(ChaveAgendaId);
        try
        {
            // Relê a agenda: pode ter sido desativada/alterada desde a última sincronização.
            var agenda = await agendas.AtivaPorIdAsync(agendaId, ct);
            if (agenda is null)
            {
                logger.LogInformation("Agenda {AgendaId} não está mais ativa; disparo ignorado.", agendaId);
                return;
            }

            var (alvo, criados) = await agendas.EnfileirarAsync(agenda, ct);
            var pulados = alvo - criados;
            logger.LogInformation("Agenda {AgendaId}: {Criados} job(s) {Tipo} enfileirado(s) de {Alvo} conta(s) alvo; {Pulados} já em andamento.",
                agendaId, criados, agenda.TipoJob, alvo, pulados);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Falha ao disparar a agenda {AgendaId}. Tipo: {Tipo}. SQLSTATE: {SqlState}.",
                agendaId, ex.GetType().Name, (ex as Npgsql.PostgresException)?.SqlState ?? "-");
        }
    }
}

/// <summary>A cada minuto: jobs EXECUTANDO sem sinal além do timeout voltam para PENDENTE ou viram ERRO.</summary>
[DisallowConcurrentExecution]
public sealed class ZeladorJob(FilaRepositorio fila, ParametroRepositorio parametros, TimeProvider relogio, ILogger<ZeladorJob> logger) : IJob
{
    public static readonly JobKey Chave = new("zelador", "sistema");

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        try
        {
            var timeoutMin = await parametros.InteiroAsync(Parametros.TimeoutExecucaoMin, Parametros.TimeoutExecucaoMinPadrao, 1, 24 * 60, ct);
            var maxTentativas = await parametros.InteiroAsync(Parametros.MaxTentativas, Parametros.MaxTentativasPadrao, 1, 20, ct);

            var (repostos, erros) = await fila.ZelarAsync(relogio.GetUtcNow(), TimeSpan.FromMinutes(timeoutMin), maxTentativas, ct);
            if (repostos + erros > 0)
                logger.LogWarning("Zelador: {Repostos} job(s) devolvido(s) à fila e {Erros} marcado(s) com erro (timeout {Timeout} min).",
                    repostos, erros, timeoutMin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Falha no zelador da fila. Tipo: {Tipo}. SQLSTATE: {SqlState}.",
                ex.GetType().Name, (ex as Npgsql.PostgresException)?.SqlState ?? "-");
        }
    }
}
