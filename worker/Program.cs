using CloudVista.Worker;
using Quartz;

// cloudvista-worker: lê a agenda (job_agenda) e enfileira job_execucao; zela pela fila.
// Sem HTTP, sem chave mestra, sem credenciais de cloud. Quartz em memória (1 instância);
// a fonte da verdade da agenda é o banco.
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WorkerDb>();
builder.Services.AddSingleton<ParametroRepositorio>();
builder.Services.AddSingleton<AgendaRepositorio>();
builder.Services.AddSingleton<FilaRepositorio>();

builder.Services.AddQuartz(q =>
{
    q.UseInMemoryStore();
    q.SchedulerName = "cloudvista-worker";

    // Disparo das agendas: um único job durável; cada linha da agenda vira um trigger.
    q.AddJob<DispararAgendaJob>(j => j.WithIdentity(DispararAgendaJob.Chave).StoreDurably());

    q.AddJob<SincronizarAgendaJob>(j => j.WithIdentity(SincronizarAgendaJob.Chave));
    q.AddTrigger(t => t
        .WithIdentity(SincronizarAgendaJob.Gatilho)
        .ForJob(SincronizarAgendaJob.Chave)
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInSeconds(Parametros.SincronizarAgendaSegPadrao).RepeatForever()));

    q.AddJob<ZeladorJob>(j => j.WithIdentity(ZeladorJob.Chave));
    q.AddTrigger(t => t
        .WithIdentity("zelador", "sistema")
        .ForJob(ZeladorJob.Chave)
        .StartNow()
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(1).RepeatForever()));
});

builder.Services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

var host = builder.Build();

// Sem banco o worker não tem função: encerra com código 1 (o Coolify mostra o erro no log).
if (!host.Services.GetRequiredService<WorkerDb>().Configurado)
{
    Environment.ExitCode = 1;
    return;
}

host.Run();
