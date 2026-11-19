using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Services.Interfaces;

namespace Predict.Backend.Services
{
    public class NarrativeGenerationJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NarrativeGenerationJob> _logger;

        public NarrativeGenerationJob(IServiceProvider serviceProvider, ILogger<NarrativeGenerationJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Narrative Generation Job scheduled (Every Monday 06:00)");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogInformation("Next run scheduled for {Time}. Waiting {Delay}...", nextRun, delay);
                    await Task.Delay(delay, stoppingToken);
                }

                if (stoppingToken.IsCancellationRequested) break;

                await RunGenerationProcess();
            }
        }

        private async Task RunGenerationProcess()
        {
            _logger.LogInformation("Starting weekly narrative generation process...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var narrativeService = scope.ServiceProvider.GetRequiredService<INarrativeService>();

                var hosts = context.ActionPlans.ToList();
                _logger.LogInformation("Found {Count} hosts to process.", hosts.Count);

                foreach (var host in hosts)
                {
                    var result = await narrativeService.GenerateNarrativeHostAsync(host);

                    var existing = context.ActionPlanNarratives
                        .FirstOrDefault(n => n.IdPlano == host.IdPlano);

                    if (existing != null)
                    {
                        existing.Problema = result.Problema;
                        existing.Impacto = result.Impacto;
                        existing.Acoes = result.Acoes;
                        existing.Urgencia = result.Urgencia;
                        existing.JustificativaUrgencia = result.JustificativaUrgencia;
                        existing.DtGeracao = DateTime.UtcNow;
                        existing.Status = result.Status;
                        existing.Erro = result.Erro;
                    }
                    else
                    {
                        context.ActionPlanNarratives.Add(result);
                    }

                    // Save periodically to avoid huge transactions
                    if (hosts.IndexOf(host) % 10 == 0)
                    {
                        await context.SaveChangesAsync();
                    }
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Weekly narrative generation process completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in weekly narrative generation job.");
            }
        }

        private DateTime GetNextRunTime(DateTime now)
        {
            // Calculate next Monday at 06:00
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            
            // If it's already Monday and past 06:00, move to next week
            if (daysUntilMonday == 0 && now.Hour >= 6)
            {
                daysUntilMonday = 7;
            }

            var nextMonday = now.Date.AddDays(daysUntilMonday).AddHours(6);
            return nextMonday;
        }
    }
}
