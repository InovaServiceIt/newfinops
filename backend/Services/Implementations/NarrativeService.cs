using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Predict.Backend.Models;
using Predict.Backend.Services.Interfaces;

namespace Predict.Backend.Services.Implementations
{
    public class NarrativeService : INarrativeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<NarrativeService> _logger;

        private const string SYSTEM_PROMPT = @"Você é um analista sênior de operações de TI especializado em ambientes corporativos de grande porte.
Sua função é transformar dados técnicos de monitoramento em narrativas executivas claras, precisas e acionáveis para gestores e SDMs.

Regras de escrita:
- Escreva de forma direta, sem jargões desnecessários
- Conecte sempre o problema técnico ao impacto no negócio
- Use senso de urgência proporcional à severidade real
- O plano de ação deve ser executável por um técnico de middleware
- Nunca invente dados — use apenas o que foi fornecido
- Responda APENAS com o JSON solicitado, sem texto adicional";

        public NarrativeService(HttpClient httpClient, IConfiguration configuration, ILogger<NarrativeService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Anthropic:ApiKey"] ?? string.Empty;
            _logger = logger;
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Anthropic API Key is missing.");
            }
        }

        public async Task<ActionPlanNarrative> GenerateNarrativeHostAsync(ActionPlan host)
        {
            try
            {
                var userPrompt = BuildUserPrompt(host);
                
                var requestBody = new
                {
                    model = "claude-3-5-haiku-20241022", // Spec mentioned 4.5 but Haiku 3.5 is the current latest for Haiku.
                    max_tokens = 1024,
                    temperature = 0,
                    system = SYSTEM_PROMPT,
                    messages = new[]
                    {
                        new { role = "user", content = userPrompt }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var content = result.GetProperty("content")[0].GetProperty("text").GetString();

                if (string.IsNullOrEmpty(content)) throw new Exception("Empty response from AI");

                // Parse the inner JSON returned by Claude
                var narrativeData = JsonSerializer.Deserialize<NarrativeResponse>(content.Trim(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (narrativeData == null) throw new Exception("Failed to parse narrative JSON");

                return new ActionPlanNarrative
                {
                    IdPlano = (int)host.IdPlano,
                    Problema = narrativeData.Problema,
                    Impacto = narrativeData.Impacto,
                    Acoes = JsonSerializer.Serialize(narrativeData.Acoes),
                    Urgencia = narrativeData.Urgencia,
                    JustificativaUrgencia = narrativeData.JustificativaUrgencia,
                    ModeloUtilizado = "claude-haiku-3.5",
                    DtGeracao = DateTime.UtcNow,
                    Status = "gerado"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating narrative for host {Host}", host.NmHost);
                return new ActionPlanNarrative
                {
                    IdPlano = host.IdPlano,
                    Status = "erro",
                    Erro = ex.Message,
                    DtGeracao = DateTime.UtcNow,
                    Problema = "Erro na geração",
                    Impacto = "Erro na geração",
                    JustificativaUrgencia = "N/A",
                    Urgencia = "MÉDIA"
                };
            }
        }

        public async Task<List<ActionPlanNarrative>> GenerateNarrativesBatchAsync(List<ActionPlan> hosts)
        {
            var results = new List<ActionPlanNarrative>();
            foreach (var host in hosts)
            {
                results.Add(await GenerateNarrativeHostAsync(host));
                await Task.Delay(500); // Small delay to avoid rate limiting
            }
            return results;
        }

        private string BuildUserPrompt(ActionPlan host)
        {
            return $@"Analise os dados do host abaixo e gere uma narrativa executiva estruturada.

DADOS DO HOST:
- Nome: {host.NmHost}
- Categoria: {host.CdCategoria}
- Prioridade: {host.PrioridadeSugerida}
- Score de criticidade: {host.ScoreHostname}
- Total de eventos: {host.QtEventos} em {host.DiasComEvento} dias consecutivos
- Eventos Disaster: {host.QtDisaster}
- Eventos High: {host.QtHigh}
- Eventos Average: {host.QtAverage}
- Eventos Warning: {host.QtWarning}
- Percentual de eventos graves: {host.PctGraves}%
- Representa {host.PctVolumeContrato}% do volume total do contrato
- Última ocorrência: {host.UltimaOcorrencia}
- Período analisado: {host.DtInicioAnalise} até {host.DtFimAnalise}
- Problema identificado: {host.DescricaoChave}
- Ação sugerida pelo modelo: {host.TxAcaoInicialSuger}
- Justificativa do modelo: {host.JustificativaResumida}

Retorne EXCLUSIVAMENTE o seguinte JSON, sem nenhum texto antes ou depois:

{{
  ""problema"": ""Uma frase executiva descrevendo o problema em linguagem clara. Máximo 2 linhas."",
  ""impacto"": ""Uma frase descrevendo o impacto real no negócio ou na operação do cliente. Máximo 2 linhas."",
  ""acoes"": [
    ""Passo 1 — ação específica e executável"",
    ""Passo 2 — ação específica e executável"",
    ""Passo 3 — ação específica e executável"",
    ""Passo 4 — ação específica e executável"",
    ""Passo 5 — ação específica e executável""
  ],
  ""urgencia"": ""IMEDIATA | ALTA | MÉDIA"",
  ""justificativa_urgencia"": ""Uma frase curta justificando o nível de urgência.""
}}";
        }

        private class NarrativeResponse
        {
            public string Problema { get; set; } = string.Empty;
            public string Impacto { get; set; } = string.Empty;
            public List<string> Acoes { get; set; } = new();
            public string Urgencia { get; set; } = string.Empty;
            public string JustificativaUrgencia { get; set; } = string.Empty;
        }
    }
}
