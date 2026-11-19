using Microsoft.Extensions.Configuration;
using Predict.Backend.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Predict.Backend.Services.Implementations
{
    public class OllamaEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _http;
        private readonly ILogger<OllamaEmbeddingService> _logger;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaEmbeddingService(IConfiguration config, HttpClient http, ILogger<OllamaEmbeddingService> logger)
        {
            _http = http;
            _logger = logger;
            
            var configUrl = config["Ollama:BaseUrl"];
            _model = config["Ollama:Model"] ?? "qwen3-embedding:0.6b";

            // Fallback preventivo: se estiver vazio ou for localhost, forçamos o IP correto
            if (string.IsNullOrEmpty(configUrl) || configUrl.Contains("localhost"))
            {
                _baseUrl = "http://204.168.147.122:11434";
                _logger.LogWarning("Ollama BaseUrl não encontrada ou configurada como localhost. Usando fallback: {Url}", _baseUrl);
            }
            else
            {
                _baseUrl = configUrl;
                _logger.LogInformation("Ollama configurado em: {Url}", _baseUrl);
            }
        }

        public async Task<float[]> GerarAsync(string texto, CancellationToken ct = default)
        {
            var payload = new { model = _model, prompt = texto };
            var targetUrl = $"{_baseUrl.TrimEnd('/')}/api/embeddings";

            try
            {
                _logger.LogDebug("Chamando Ollama: {Url} com modelo {Model}", targetUrl, _model);
                var response = await _http.PostAsJsonAsync(targetUrl, payload, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: ct);
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao chamar Ollama em {Url}. Verifique conectividade e firewall.", targetUrl);
                throw;
            }
        }

        public async Task<List<float[]>> GerarEmbeddingsEmLoteAsync(List<string> textos, CancellationToken ct = default)
        {
            // Ollama processa um texto por request — paralelizar com limite de concorrência
            using var semaforo = new SemaphoreSlim(5); // máximo 5 requests simultâneos

            var tasks = textos.Select(async texto =>
            {
                await semaforo.WaitAsync(ct);
                try
                {
                    return await GerarAsync(texto, ct);
                }
                finally
                {
                    semaforo.Release();
                }
            });

            var resultados = await Task.WhenAll(tasks);
            return resultados.ToList();
        }
    }

    public record OllamaEmbeddingResponse(
        [property: JsonPropertyName("embedding")] float[] Embedding
    );
}
