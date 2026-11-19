using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Predict.Backend.Infrastructure.Data;
using Predict.Backend.Models;
using Predict.Backend.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Predict.Backend.Services.Implementations
{
    public class PatternAnalysisService : IPatternAnalysisService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<PatternAnalysisService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceCatalogEmbeddingService _catalogEmbeddingService;

        private const double LIMIAR_SIMILARIDADE = 0.90; // Tolerância para cluster
        private const double LIMIAR_CATALOG_MATCH = 0.80; // Threshold semântico para associação ao catálogo
        private const int MINIMO_ALERTAS_CLUSTER = 3;

        private static readonly ConcurrentDictionary<string, ProgressJob> _jobs = new();

        private readonly IAlertsStagingSyncService _syncService;

        public PatternAnalysisService(
            IServiceScopeFactory scopeFactory, 
            HttpClient httpClient, 
            IConfiguration configuration, 
            IEmbeddingService embeddingService, 
            ILogger<PatternAnalysisService> logger,
            IAlertsStagingSyncService syncService,
            IServiceCatalogEmbeddingService catalogEmbeddingService)
        {
            _scopeFactory = scopeFactory;
            _httpClient = httpClient;
            _apiKey = configuration["Anthropic:ApiKey"] ?? string.Empty;
            _embeddingService = embeddingService;
            _logger = logger;
            _configuration = configuration;
            _syncService = syncService;
            _catalogEmbeddingService = catalogEmbeddingService;
        }

        public async Task<PatternAnalysisStatusResponse> GetStatusAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT dt_execucao, qt_clusters, qt_sugestoes FROM predict_controle_execucao WHERE tp_rotina = 'ANALISE_PADROES' AND status = 'SUCESSO' ORDER BY dt_execucao DESC LIMIT 1";
            
            DateTime? ultimaExecucao = null;
            int qtClusters = 0, qtSugestoes = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    ultimaExecucao = reader.GetDateTime(0);
                    qtClusters = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    qtSugestoes = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                }
            }

            if (ultimaExecucao == null) return new PatternAnalysisStatusResponse { PodeExecutar = true };

            int diasDesde = (int)(DateTime.UtcNow - ultimaExecucao.Value).TotalDays;
            
            int qtPendentes = 0;
            using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = "SELECT COUNT(*) FROM predict_sugestao_regra WHERE status = 'AGUARDANDO_APROVACAO'";
                var obj = await cmd2.ExecuteScalarAsync();
                qtPendentes = obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
            }

            int qtAvaliados = 0;
            using (var cmd3 = conn.CreateCommand())
            {
                cmd3.CommandText = "SELECT COUNT(*) FROM predict_alert_embeddings";
                var obj = await cmd3.ExecuteScalarAsync();
                qtAvaliados = obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
            }

            int qtFaltaAvaliar = 0;
            var prodConnString = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("ProductionViews");
            if (!string.IsNullOrEmpty(prodConnString))
            {
                using var pConn = new Npgsql.NpgsqlConnection(prodConnString);
                await pConn.OpenAsync();
                using var cmd4 = pConn.CreateCommand();
                cmd4.CommandText = @"SELECT COUNT(*) FROM vw_predict_plano_acao_hostname v 
                                    WHERE v.description_exemplo IS NOT NULL 
                                    AND NOT EXISTS (SELECT 1 FROM predict_alert_embeddings e WHERE e.alert_id = v.id_plano)";
                var obj = await cmd4.ExecuteScalarAsync();
                qtFaltaAvaliar = obj == null || obj == DBNull.Value ? 0 : Convert.ToInt32(obj);
            }

            return new PatternAnalysisStatusResponse
            {
                PodeExecutar = diasDesde >= 7,
                DiasDesde = diasDesde,
                DiasRestantes = Math.Max(0, 7 - diasDesde),
                UltimaExecucao = ultimaExecucao,
                QtClusters = qtClusters,
                QtSugestoes = qtSugestoes,
                QtPendentes = qtPendentes,
                QtAvaliados = qtAvaliados,
                QtFaltaAvaliar = qtFaltaAvaliar
            };
        }

        public string StartAnalysis(string executor, bool force)
        {
            string jobId = $"job_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            _jobs[jobId] = new ProgressJob { Etapa = 0, Status = "iniciando" };
            Task.Run(async () => await RunAnalysisAsync(jobId, executor, force));
            return jobId;
        }

        public ProgressJob? GetProgress(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }


        private async Task RunAnalysisAsync(string jobId, string executor, bool force)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            
            int execId = 0;
            try
            {
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO predict_controle_execucao (tp_rotina, status, executado_por, forcado) VALUES ('ANALISE_PADROES', 'EM_ANDAMENTO', @exec, @force) RETURNING id";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "exec"; p1.Value = executor; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "force"; p2.Value = force; cmd.Parameters.Add(p2);
                    execId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                await GravarLogBancoAsync(jobId, "INFO", 0, $"Iniciando análise de padrões (Executor: {executor}, Force: {force})");

                // PRÉ-ETAPA: Indexação delta do catálogo de serviços
                _jobs[jobId].DetalheEtapa = "Verificando embeddings pendentes no catálogo de serviços...";
                await GravarLogBancoAsync(jobId, "INFO", 0, "[PA-00] Iniciando indexação delta do predict_service_catalog");
                try
                {
                    int catalogIndexados = await _catalogEmbeddingService.IndexarCatalogoAsync(jobId);
                    await GravarLogBancoAsync(jobId, "INFO", 0,
                        $"[PA-00] Indexação do catálogo concluída: {catalogIndexados} atividades indexadas nesta execução.");
                }
                catch (Exception exCat)
                {
                    _logger.LogError(exCat, "[PA-00] Falha na indexação do catálogo. Pipeline continua.");
                    await GravarLogBancoAsync(jobId, "WARN", 0,
                        $"[PA-00] Falha na indexação do catálogo: {exCat.Message}. Pipeline continua.");
                }

                // ETAPA 0: Sincronização de Alertas
                _jobs[jobId].Etapa = 0;
                _jobs[jobId].Status = "Sincronizando dados";
                _jobs[jobId].DetalheEtapa = "Sincronizando alertas dos últimos 90 dias...";
                await _syncService.SyncAsync(jobId);
                var t0 = sw.Elapsed;
                _jobs[jobId].DetalheEtapa = $"Sincronização concluída com sucesso ({FormatDuration(t0)}).";
                _jobs[jobId].DetalhesPorEtapa[0] = _jobs[jobId].DetalheEtapa;
                await GravarLogBancoAsync(jobId, "INFO", 0, $"[PA-01] Sincronização finalizada em {FormatDuration(t0)}");

                // ETAPA 1
                _jobs[jobId].Etapa = 1;
                await GravarLogBancoAsync(jobId, "INFO", 1, "[PA-02] Iniciando Etapa 1: Vetorização de alertas pendentes");
                int qtAlerts = await GerarEmbeddingsAsync(dbContext, _embeddingService, jobId);
                var t1 = sw.Elapsed - t0;
                _jobs[jobId].QtAlertas = qtAlerts;
                _jobs[jobId].DetalheEtapa = $"{qtAlerts} alertas convertidos em vetores ({FormatDuration(t1)}).";
                _jobs[jobId].DetalhesPorEtapa[1] = _jobs[jobId].DetalheEtapa;
                await GravarLogBancoAsync(jobId, "INFO", 1, $"Etapa 1 finalizada. Total: {qtAlerts} em {FormatDuration(t1)}");

                // ETAPA 2
                _jobs[jobId].Etapa = 2;
                await GravarLogBancoAsync(jobId, "INFO", 2, "[PA-03] Iniciando Etapa 2: Identificação de Clusters de similaridade");
                var clusters = await IdentificarClustersAsync(dbContext, jobId, execId);
                var t2 = sw.Elapsed - (t0 + t1);
                _jobs[jobId].QtClusters = clusters.Count;
                _jobs[jobId].DetalheEtapa = $"{clusters.Count} grupos de alertas similares detectados ({FormatDuration(t2)}).";
                _jobs[jobId].DetalhesPorEtapa[2] = _jobs[jobId].DetalheEtapa;
                await GravarLogBancoAsync(jobId, "INFO", 2, $"Etapa 2 finalizada. Total: {clusters.Count} em {FormatDuration(t2)}");

                // ETAPA 3
                _jobs[jobId].Etapa = 3;
                await GravarLogBancoAsync(jobId, "INFO", 3, "[PA-04] Iniciando Etapa 3: Geração de sugestões de regras via IA Cognitiva");
                _jobs[jobId].Total = clusters.Count;
                _jobs[jobId].Processados = 0;
                int qtSugestoes = await SugerirRegrasAsync(dbContext, jobId, clusters, execId);
                var t3 = sw.Elapsed - (t0 + t1 + t2);
                _jobs[jobId].QtSugestoes = qtSugestoes;
                _jobs[jobId].DetalheEtapa = $"{qtSugestoes} sugestões de automação geradas ({FormatDuration(t3)}).";
                _jobs[jobId].DetalhesPorEtapa[3] = _jobs[jobId].DetalheEtapa;
                await GravarLogBancoAsync(jobId, "INFO", 3, $"Etapa 3 finalizada. Total: {qtSugestoes} em {FormatDuration(t3)}");

                // ETAPA 4
                _jobs[jobId].Etapa = 4;
                _jobs[jobId].DetalheEtapa = "Padrões e sugestões consolidados e salvos com sucesso.";
                _jobs[jobId].DetalhesPorEtapa[4] = _jobs[jobId].DetalheEtapa;
                await GravarLogBancoAsync(jobId, "INFO", 4, "[PA-05] Etapa 4: Finalizando processamento de IA");
                
                // ETAPA 5: Populando Discovery
                _jobs[jobId].Etapa = 6;
                _jobs[jobId].Status = "Populando base Discovery";
                var result = await AnalisarPadroesAsync(jobId, executor, force, sw);
                _jobs[jobId].DiscoveryResult = result;
                await GravarLogBancoAsync(jobId, "INFO", 5, $"[PA-06] Etapa 5 finalizada. Oportunidades: {result.Inseridas} inseridas, {result.Ignoradas} ignoradas.");
                _jobs[jobId].DetalhesPorEtapa[5] = $"Processamento Radar finalizado ({result.Inseridas} ops).";

                // FINALIZAÇÃO
                _jobs[jobId].Status = "concluido";
                _jobs[jobId].QtAlertas = qtAlerts;
                _jobs[jobId].QtClusters = clusters.Count;
                _jobs[jobId].QtSugestoes = qtSugestoes;
                
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE predict_controle_execucao SET status='SUCESSO', qt_alertas=@qta, qt_clusters=@qtc, qt_sugestoes=@qts WHERE id=@id";
                    var p = cmd.CreateParameter(); p.ParameterName="qta"; p.Value=qtAlerts; cmd.Parameters.Add(p);
                    p = cmd.CreateParameter(); p.ParameterName="qtc"; p.Value=clusters.Count; cmd.Parameters.Add(p);
                    p = cmd.CreateParameter(); p.ParameterName="qts"; p.Value=qtSugestoes; cmd.Parameters.Add(p);
                    p = cmd.CreateParameter(); p.ParameterName="id"; p.Value=execId; cmd.Parameters.Add(p);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "[PA-ERR] Erro na analise");
                await GravarLogBancoAsync(jobId, "ERROR", _jobs[jobId].Etapa, $"[PA-ERR] Erro crítico na análise: {ex.Message}");
                if (_jobs.ContainsKey(jobId))
                {
                    _jobs[jobId].Status = "erro";
                    _jobs[jobId].Erro = $"[PA-ERR] {ex.Message}";
                }
                
                if (execId > 0)
                {
                    try {
                        using var cmd = dbContext.Database.GetDbConnection().CreateCommand();
                        cmd.CommandText = "UPDATE predict_controle_execucao SET status='ERRO', erro=@err WHERE id=@id";
                        var p = cmd.CreateParameter(); p.ParameterName="err"; p.Value=ex.Message; cmd.Parameters.Add(p);
                        p = cmd.CreateParameter(); p.ParameterName="id"; p.Value=execId; cmd.Parameters.Add(p);
                        await cmd.ExecuteNonQueryAsync();
                    } catch { /* ignore */ }
                }
            }
        }

        private async Task<int> GerarEmbeddingsAsync(AppDbContext dbContext, IEmbeddingService embeddingService, string jobId)
        {
            var prodConnString = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("ProductionViews")!;
            
            long totalFaltantes = 0;
            using (var pConn = new Npgsql.NpgsqlConnection(prodConnString))
            {
                await pConn.OpenAsync();
                using (var cmd = pConn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT COUNT(*) FROM vw_predict_plano_acao_hostname v 
                                        WHERE v.description_exemplo IS NOT NULL 
                                        AND NOT EXISTS (SELECT 1 FROM predict_alert_embeddings e WHERE e.alert_id = v.id_plano)";
                    totalFaltantes = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
                }
            }

            _jobs[jobId].Total = (int)totalFaltantes;
            _jobs[jobId].Processados = 0;

            _logger.LogInformation("🚀 Iniciando processamento de {Total} alertas pendentes de vetorização.", totalFaltantes);

            if (totalFaltantes == 0) return 0;

            int proc = 0;
            var conn = dbContext.Database.GetDbConnection();
            
            while (true)
            {
                var alertBatch = new List<Tuple<int, string, string>>();
                using (var pConn = new Npgsql.NpgsqlConnection(prodConnString))
                {
                    await pConn.OpenAsync();
                    using (var cmd = pConn.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT id_plano, id_contrato::varchar, description_exemplo 
                                            FROM vw_predict_plano_acao_hostname v 
                                            WHERE v.description_exemplo IS NOT NULL 
                                            AND NOT EXISTS (SELECT 1 FROM predict_alert_embeddings e WHERE e.alert_id = v.id_plano) 
                                            ORDER BY id_plano LIMIT 1000";
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            // Pular registros com id_plano NULL (view pode retornar linhas incompletas)
                            if (reader.IsDBNull(0)) continue;
                            alertBatch.Add(Tuple.Create(reader.GetInt32(0), reader.IsDBNull(1) ? "0" : reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
                        }
                    }
                }

                if (alertBatch.Count == 0) break; 

                // Processar em sublotes menores para atualização frequente do progresso na UI
                var subLotes = alertBatch.Chunk(10);

                foreach (var chunk in subLotes)
                {
                    var textos = chunk.Select(a => a.Item3.Length > 500 ? a.Item3.Substring(0, 500) : a.Item3).ToList();
                    
                    // Chamada real para Ollama (via Interface)
                    var embeddings = await embeddingService.GerarEmbeddingsEmLoteAsync(textos);

                    for (int i = 0; i < chunk.Length; i++)
                    {
                        var alert = chunk[i];
                        var emb = embeddings[i];
                        string arrayStr = "[" + string.Join(",", emb.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

                        try 
                        {
                            using (var cmdins = conn.CreateCommand())
                            {
                                cmdins.CommandText = "INSERT INTO predict_alert_embeddings (alert_id, id_contrato, description_flat, embedding) VALUES (@id, @c, @d, @emb::vector) ON CONFLICT (alert_id) DO NOTHING";
                                var p1 = cmdins.CreateParameter(); p1.ParameterName = "id"; p1.Value = alert.Item1; cmdins.Parameters.Add(p1);
                                var p2 = cmdins.CreateParameter(); p2.ParameterName = "c"; p2.Value = alert.Item2; cmdins.Parameters.Add(p2);
                                var p3 = cmdins.CreateParameter(); p3.ParameterName = "d"; p3.Value = textos[i]; cmdins.Parameters.Add(p3);
                                var p4 = cmdins.CreateParameter(); p4.ParameterName = "emb"; p4.Value = arrayStr; cmdins.Parameters.Add(p4);
                                await cmdins.ExecuteNonQueryAsync();
                            }

                            proc++;
                            _jobs[jobId].Processados = proc;
                            
                            var descCurta = textos[i].Length > 60 ? textos[i].Substring(0, 60) + "..." : textos[i];
                            var msg = $"Alerta vetorizado com sucesso: {descCurta}";
                            _logger.LogInformation("[VETORIZAÇÃO {Proc}/{Total}] Alerta ID: {Id} | Texto: \"{Desc}\"", proc, totalFaltantes, alert.Item1, descCurta);
                            await GravarLogBancoAsync(jobId, "INFO", 1, msg, alert.Item1);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao salvar embedding do alerta {Id}", alert.Item1);
                            await GravarLogBancoAsync(jobId, "ERROR", 1, $"Falha ao salvar embedding: {ex.Message}", alert.Item1);
                        }
                    }
                }
            }

            _logger.LogInformation("✅ Etapa de vetorização concluída. {Total} alertas processados.", proc);
            return proc;
        }

        private async Task<List<ClusterData>> IdentificarClustersAsync(AppDbContext dbContext, string jobId, int execId)
        {
            var clusters = new List<ClusterData>();
            var conn = dbContext.Database.GetDbConnection();
            
            // Lógica de Semântica usando PGVector puro:
            // Vamos agrupar alertas por contrato pegando 1 representante e buscando similares
            
            var basesUnicas = new List<Tuple<int, string, string, string>>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT alert_id, id_contrato, description_flat, embedding::text FROM predict_alert_embeddings ORDER BY alert_id DESC";
                using var reader = await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync())
                    basesUnicas.Add(Tuple.Create(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
            
            var processados = new HashSet<int>();

            foreach(var baseAlert in basesUnicas)
            {
                if (processados.Contains(baseAlert.Item1)) continue;

                // PGVector Cosine Similarity Search (<=>)
                using var cmdBusca = conn.CreateCommand();
                cmdBusca.CommandText = @"
                    SELECT alert_id, description_flat, 1 - (embedding <=> @vetor::vector) AS similaridade 
                    FROM predict_alert_embeddings 
                    WHERE id_contrato = @c AND alert_id != @bid AND (1 - (embedding <=> @vetor::vector)) >= @limiar
                    ORDER BY similaridade DESC
                ";
                var p1 = cmdBusca.CreateParameter(); p1.ParameterName = "vetor"; p1.Value = baseAlert.Item4; cmdBusca.Parameters.Add(p1);
                var p2 = cmdBusca.CreateParameter(); p2.ParameterName = "c"; p2.Value = baseAlert.Item2; cmdBusca.Parameters.Add(p2);
                var p3 = cmdBusca.CreateParameter(); p3.ParameterName = "bid"; p3.Value = baseAlert.Item1; cmdBusca.Parameters.Add(p3);
                var p4 = cmdBusca.CreateParameter(); p4.ParameterName = "limiar"; p4.Value = LIMIAR_SIMILARIDADE; cmdBusca.Parameters.Add(p4);

                var clusterMembers = new List<int> { baseAlert.Item1 };
                double simSum = 1.0; // Self
                
                using (var reader = await cmdBusca.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        int simId = reader.GetInt32(0);
                        if (!processados.Contains(simId))
                        {
                            clusterMembers.Add(simId);
                            simSum += reader.GetDouble(2);
                        }
                    }
                }

                if (clusterMembers.Count >= MINIMO_ALERTAS_CLUSTER)
                {
                    // Formou um Cluster! Persist.
                    double mediaSim = simSum / clusterMembers.Count;
                    foreach(var m in clusterMembers) processados.Add(m);

                    using var cmdIns = conn.CreateCommand();
                    cmdIns.CommandText = "INSERT INTO predict_alert_clusters (id_contrato, nm_host, descricao_cluster, qt_alertas, similaridade_media, id_execucao) VALUES (@c, 'VARIOUS', @d, @q, @s, @ex) RETURNING id";
                    var ic = cmdIns.CreateParameter(); ic.ParameterName = "c"; ic.Value = baseAlert.Item2; cmdIns.Parameters.Add(ic);
                    var idc = cmdIns.CreateParameter(); idc.ParameterName = "d"; idc.Value = baseAlert.Item3; cmdIns.Parameters.Add(idc);
                    var iq = cmdIns.CreateParameter(); iq.ParameterName = "q"; iq.Value = clusterMembers.Count; cmdIns.Parameters.Add(iq);
                    var ism = cmdIns.CreateParameter(); ism.ParameterName = "s"; ism.Value = mediaSim; cmdIns.Parameters.Add(ism);
                    var iex = cmdIns.CreateParameter(); iex.ParameterName = "ex"; iex.Value = execId; cmdIns.Parameters.Add(iex);

                    int clusterId = (int)(await cmdIns.ExecuteScalarAsync() ?? 0);
                    
                    clusters.Add(new ClusterData { 
                        Id = clusterId,
                        IdContrato = baseAlert.Item2,
                        NmHost = "VARIOUS",
                        Descricao = baseAlert.Item3,
                        QtAlertas = clusterMembers.Count
                    });
                }
                else
                {
                    processados.Add(baseAlert.Item1); // Mark single as processed
                }
            }

            return clusters;
        }

        private async Task<int> SugerirRegrasAsync(AppDbContext dbContext, string jobId, List<ClusterData> clusters, int execId)
        {
            int proc = 0;
            var conn = dbContext.Database.GetDbConnection();
            using var localHttpClient = new HttpClient();
            localHttpClient.DefaultRequestHeaders.Clear();
            localHttpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            localHttpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            foreach(var cluster in clusters)
            {
                string prompt = $@"Você é o assistente Predict IA. Analise o seguinte padrão de falha corporativa e sugira os parâmetros para uma Regra de Decisão que automatize o fechamento inteligente destes alertas.
Alertas do Padrão ({cluster.QtAlertas} ocorrencias na semana):
""{cluster.Descricao}""
No sistema, temos as opções:
- tp_alvo: 'HOSTNAME' ou 'CONTRATO'
- tp_operador: 'EQUALS', 'CONTAINS' ou 'STARTS_WITH'

Responda ÚNICA E EXCLUSIVAMENTE em JSON válido, sem tags markdown e sem explicações externas, com esta estrutura exata:
{{
  ""tp_alvo"": ""HOSTNAME"",
  ""tp_operador"": ""CONTAINS"",
  ""tx_padrao"": ""(escreva o texto chave que deve ser filtrado com base na string acima)"",
  ""rotulo_acao"": ""(ex: Backup Falho, Disco Cheio)"",
  ""cd_prioridade"": ""(A1, A2 ou A3)"",
  ""tx_acao_inicial_suger"": ""(uma instrução executiva para o operador L1)"",
  ""justificativa_ia"": ""(uma breve explicação da IA de por que este padrao foi sugerido)""
}}";

                var body = new {
                    model = "claude-3-5-haiku-20241022",
                    max_tokens = 500,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                try
                {
                    var response = await localHttpClient.PostAsJsonAsync("https://api.anthropic.com/v1/messages", body);
                    if (response.IsSuccessStatusCode)
                    {
                        var resultString = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(resultString);
                        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "{}";
                        
                        // Clean markdown if present
                        text = Regex.Replace(text, @"```json|```", "").Trim();
                        var sugg = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                        
                        if (sugg != null && sugg.ContainsKey("tx_padrao"))
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = @"INSERT INTO predict_sugestao_regra 
                                (id_contrato, nm_host, qt_alertas_cluster, descricao_representativa, id_cluster, tp_alvo, tp_operador, tx_padrao, rotulo_acao, cd_prioridade, tx_acao_inicial_suger, justificativa_ia, id_execucao) 
                                VALUES (@c, @h, @qa, @d, @idc, @ta, @to, @tx, @ra, @pr, @ai, @jia, @iex)";
                            
                            var p = cmd.CreateParameter(); p.ParameterName="c"; p.Value=cluster.IdContrato; cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="h"; p.Value=cluster.NmHost; cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="qa"; p.Value=cluster.QtAlertas; cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="d"; p.Value=cluster.Descricao; cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="idc"; p.Value=cluster.Id; cmd.Parameters.Add(p);
                            
                            p = cmd.CreateParameter(); p.ParameterName="ta"; p.Value=sugg.GetValueOrDefault("tp_alvo", "HOSTNAME"); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="to"; p.Value=sugg.GetValueOrDefault("tp_operador", "CONTAINS"); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="tx"; p.Value=sugg.GetValueOrDefault("tx_padrao", "Unknown"); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="ra"; p.Value=sugg.GetValueOrDefault("rotulo_acao", "Rule"); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="pr"; p.Value=sugg.GetValueOrDefault("cd_prioridade", "A2"); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="ai"; p.Value=sugg.GetValueOrDefault("tx_acao_inicial_suger", ""); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="jia"; p.Value=sugg.GetValueOrDefault("justificativa_ia", ""); cmd.Parameters.Add(p);
                            p = cmd.CreateParameter(); p.ParameterName="iex"; p.Value=execId; cmd.Parameters.Add(p);

                            await cmd.ExecuteNonQueryAsync();
                            proc++;
                        }
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Erro na api Anthropic para o cluster {id}", cluster.Id);
                }

                _jobs[jobId].Processados = proc;
            }

            return proc;
        }
        public async Task<List<ProcessLogDto>> GetLogsAsync(string jobId)
        {
            var logs = new List<ProcessLogDto>();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT id, job_id, dt_log, tp_log, etapa, alerta_id, mensagem FROM predict_process_logs WHERE job_id = @j ORDER BY dt_log ASC";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "j"; p1.Value = jobId; cmd.Parameters.Add(p1);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    logs.Add(new ProcessLogDto
                    {
                        Id = reader.GetInt32(0),
                        JobId = reader.GetString(1),
                        DtLog = reader.GetDateTime(2),
                        TpLog = reader.GetString(3),
                        Etapa = reader.GetInt32(4),
                        AlertaId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        Mensagem = reader.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar logs do banco para o job {JobId}", jobId);
            }
            return logs;
        }

        public async Task<List<SuggestionDto>> GetPendingSuggestionsAsync()
        {
            var suggestions = new List<SuggestionDto>();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT id, id_contrato, nm_host, qt_alertas_cluster, descricao_representativa, 
                                          tp_alvo, tp_operador, tx_padrao, rotulo_acao, cd_prioridade, 
                                          tx_acao_inicial_suger, justificativa_ia, status, dt_sugestao 
                                   FROM predict_sugestao_regra 
                                   WHERE status = 'AGUARDANDO_APROVACAO' 
                                   ORDER BY dt_sugestao DESC";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    suggestions.Add(new SuggestionDto
                    {
                        Id = reader.GetInt32(0),
                        IdContrato = reader.GetString(1),
                        NmHost = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        QtAlertasCluster = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        DescricaoRepresentativa = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        TpAlvo = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        TpOperador = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        TxPadrao = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        RotuloAcao = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        CdPrioridade = reader.IsDBNull(9) ? "" : reader.GetString(9),
                        TxAcaoInicialSuger = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        JustificativaIa = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        Status = reader.GetString(12),
                        DtSugestao = reader.GetDateTime(13)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sugestões pendentes");
            }
            return suggestions;
        }

        public async Task<bool> ApproveSuggestionAsync(int id, string approvedBy, string? observation)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE predict_sugestao_regra 
                                   SET status = 'APROVADA', aprovado_por = @u, dt_aprovacao = NOW(), observacao = @o 
                                   WHERE id = @id";
                
                var p1 = cmd.CreateParameter(); p1.ParameterName = "u"; p1.Value = approvedBy; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "o"; p2.Value = (object?)observation ?? DBNull.Value; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "id"; p3.Value = id; cmd.Parameters.Add(p3);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aprovar sugestão {Id}", id);
                return false;
            }
        }

        public async Task<bool> RejectSuggestionAsync(int id, string rejectedBy, string? observation)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"UPDATE predict_sugestao_regra 
                                   SET status = 'REJEITADA', aprovado_por = @u, dt_aprovacao = NOW(), observacao = @o 
                                   WHERE id = @id";
                
                var p1 = cmd.CreateParameter(); p1.ParameterName = "u"; p1.Value = rejectedBy; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "o"; p2.Value = (object?)observation ?? DBNull.Value; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "id"; p3.Value = id; cmd.Parameters.Add(p3);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao rejeitar sugestão {Id}", id);
                return false;
            }
        }

        public async Task<AnalisarPadroesResult> AnalisarPadroesAsync(string? jobId = null, string? executor = "System", bool force = false, System.Diagnostics.Stopwatch? sw = null)
        {
            var result = new AnalisarPadroesResult();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                // Passo 0: Executar a Function de processamento do banco (Radar -> Predict)
                // Usando uma conexão dedicada direto do IConfiguration para garantir que a senha (password) seja enviada
                var dbConnString = _configuration.GetConnectionString("DefaultConnection");
                using (var dedicatedConn = new Npgsql.NpgsqlConnection(dbConnString))
                {
                    await dedicatedConn.OpenAsync();
                    using (var cmdRefresh = dedicatedConn.CreateCommand())
                    {
                        if (jobId != null && _jobs.ContainsKey(jobId)) {
                            _jobs[jobId].Etapa = 5;
                            _jobs[jobId].Status = "Processando inteligência no banco (Radar)";
                            _jobs[jobId].DetalheEtapa = "Aguardando processamento pesado do banco...";
                        }

                        _logger.LogInformation("Etapa 5: Executando processamento de padrões no banco (Function)...");
                        cmdRefresh.CommandText = "SELECT public.create_predict_plano_acao_hostname()";
                        cmdRefresh.CommandTimeout = 600; // 10 minutos de limite
                        var t5Start = sw?.Elapsed ?? TimeSpan.Zero;
                        await cmdRefresh.ExecuteNonQueryAsync();
                        var t5 = (sw?.Elapsed ?? TimeSpan.Zero) - t5Start;
                        
                        if (jobId != null && _jobs.ContainsKey(jobId)) {
                            _jobs[jobId].DetalheEtapa = $"Inteligência Radar atualizada ({FormatDuration(t5)}).";
                            _jobs[jobId].DetalhesPorEtapa[5] = _jobs[jobId].DetalheEtapa;
                        }
                    }
                }

                // Seta o timeout do context principal também para os próximos passos
                dbContext.Database.SetCommandTimeout(600);

                // FIX: Ativar registros que foram inseridos sem is_active nas execuções anteriores
                using (var cmdFix = conn.CreateCommand())
                {
                    cmdFix.CommandText = "UPDATE public.predict_discovery_opportunities SET is_active = true WHERE is_active IS NULL OR is_active = false";
                    await cmdFix.ExecuteNonQueryAsync();
                }

                // Passo 1: Contar total de registros na view (para o progresso x/y)
                int totalRegistros = 0;
                using (var cmdCount = conn.CreateCommand())
                {
                    cmdCount.CommandText = "SELECT COUNT(*) FROM public.vw_predict_plano_acao_hostname";
                    cmdCount.CommandTimeout = 120;
                    totalRegistros = Convert.ToInt32(await cmdCount.ExecuteScalarAsync());
                }

                result.TotalView = totalRegistros;

                // Atualiza o progress job com o total
                if (jobId != null && _jobs.ContainsKey(jobId))
                {
                    _jobs[jobId].Etapa = 6;
                    _jobs[jobId].Total = totalRegistros;
                    _jobs[jobId].Processados = 0;
                }

                if (totalRegistros == 0)
                {
                    _logger.LogInformation("Etapa 5: Nenhum registro encontrado na view. Nada a processar.");
                    return result;
                }

                // Passo 2: Ler a view em lotes e popular discovery_opportunities
                int offset = 0;
                const int BATCH_SIZE = 50;
                int processados = 0;

                while (offset < totalRegistros)
                {
                    var oportunidades = new List<VwPlanoAcaoHostname>();
                    using (var cmdRead = conn.CreateCommand())
                    {
                        cmdRead.CommandTimeout = 120;
                        cmdRead.CommandText = $"SELECT id_plano, id_contrato, nm_host, descricao_chave, titulo_melhoria, justificativa_resumida, cd_categoria, rotulo_acao, score_hostname, ranking_hostname, qt_eventos FROM public.vw_predict_plano_acao_hostname ORDER BY id_plano LIMIT {BATCH_SIZE} OFFSET {offset}";
                        using var reader = await cmdRead.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            // Pular registros com colunas obrigatórias NULL
                            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2)) continue;
                            oportunidades.Add(new VwPlanoAcaoHostname
                            {
                                IdPlano = reader.GetInt32(0),
                                IdContrato = reader.GetInt32(1),
                                NmHost = reader.GetString(2),
                                DescricaoChave = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                TituloMelhoria = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                JustificativaResumida = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                CdCategoria = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                RotuloAcao = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                ScoreHostname = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                                RankingHostname = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                QtEventos = reader.IsDBNull(10) ? 0 : reader.GetInt32(10)
                            });
                        }
                    }

                    // Inserir cada oportunidade do lote
                    foreach (var item in oportunidades)
                    {
                        var opportunityCode = $"RAD-{item.IdPlano}";
                        var clientCode = item.IdContrato.ToString();

                        try
                        {
                            // Lógica de classificação inicial baseada no score do Radar (Novos Limites: 500 e 100)
                            string initialGain = item.ScoreHostname >= 400 ? "high" : (item.ScoreHostname >= 100 ? "medium" : "low");
                            string initialEffort = item.ScoreHostname >= 500 ? "low" : (item.ScoreHostname >= 100 ? "medium" : "high");

                            using var cmdInsert = conn.CreateCommand();
                            cmdInsert.CommandText = @"
                                INSERT INTO public.predict_discovery_opportunities (
                                    client_code, opportunity_code, origin_module, origin_type,
                                    source_reference, radar_pattern_reference, title, summary,
                                    opportunity_category, priority_score, priority_rank,
                                    estimated_volume, volume_unit, status, created_by, created_at, is_active,
                                    expected_gain_classification, implementation_effort_classification
                                ) 
                                SELECT @cc, @oc, @om, @ot, @sr, @rpr, @ti, @su, @cat, @ps, @pr, @ev, @vu, @st, @cb, NOW(), true, @gain, @eff
                                WHERE NOT EXISTS (
                                    SELECT 1 FROM public.predict_discovery_opportunities 
                                    WHERE client_code = @cc 
                                      AND title = @ti 
                                      AND COALESCE(source_reference, '') = COALESCE(@sr, '')
                                      AND status != 'Concluída' AND status != 'Descartada'
                                );";
                            
                            var p = cmdInsert.CreateParameter(); p.ParameterName = "cc"; p.Value = clientCode; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "oc"; p.Value = opportunityCode; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "om"; p.Value = "predict_radar"; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "ot"; p.Value = (object?)item.RotuloAcao ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "sr"; p.Value = (object?)item.NmHost ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "rpr"; p.Value = (object?)item.DescricaoChave ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "ti"; p.Value = (object?)item.TituloMelhoria ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "su"; p.Value = (object?)item.JustificativaResumida ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "cat"; p.Value = (object?)item.CdCategoria ?? DBNull.Value; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "ps"; p.Value = item.ScoreHostname; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "pr"; p.Value = item.RankingHostname; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "ev"; p.Value = (decimal)item.QtEventos; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "vu"; p.Value = "eventos"; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "st"; p.Value = "Identificada"; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "cb"; p.Value = "predict_radar"; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "gain"; p.Value = initialGain; cmdInsert.Parameters.Add(p);
                            p = cmdInsert.CreateParameter(); p.ParameterName = "eff"; p.Value = initialEffort; cmdInsert.Parameters.Add(p);

                            int rows = await cmdInsert.ExecuteNonQueryAsync();
                            if (rows > 0) result.Inseridas++;
                            else result.Ignoradas++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro ao inserir oportunidade RAD-{Id}", item.IdPlano);
                            result.Ignoradas++;
                        }

                        processados++;
                    }

                    offset += BATCH_SIZE;

                    // Atualiza progresso no job para o frontend exibir x/y
                    if (jobId != null && _jobs.ContainsKey(jobId))
                    {
                        _jobs[jobId].Processados = processados;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar padrões e popular oportunidades");
                throw;
            }

            // ETAPA 7: Associação semântica ao catálogo de serviços
            if (jobId != null && _jobs.ContainsKey(jobId))
            {
                _jobs[jobId].Status = "Associando ao catálogo de serviços";
                _jobs[jobId].DetalheEtapa = "Executando match semântico com predict_service_catalog...";
            }
            try
            {
                await ExecutarEtapa7CatalogMatchAsync(jobId);
            }
            catch (Exception exEtapa7)
            {
                _logger.LogError(exEtapa7, "[PA-07] Falha na Etapa 7. Pipeline finalizado sem match de catálogo.");
                if (jobId != null)
                    await GravarLogBancoAsync(jobId, "ERROR", 7,
                        $"[PA-07] Falha crítica na Etapa 7: {exEtapa7.Message}");
            }

            // Finaliza o job informando o resultado
            if (jobId != null && _jobs.ContainsKey(jobId))
            {
                var totalTime = sw?.Elapsed ?? TimeSpan.Zero; // Aproximado
                _jobs[jobId].DiscoveryResult = new AnalisarPadroesResult { Inseridas = result.Inseridas, Ignoradas = result.Ignoradas, TotalView = result.TotalView };
                _jobs[jobId].DetalheEtapa = $"{result.Inseridas} novas oportunidades disponibilizadas ({FormatDuration(totalTime - (totalTime*0.9))})."; 
                _jobs[jobId].DetalhesPorEtapa[6] = _jobs[jobId].DetalheEtapa;
            }

            return result;
        }

        /// <summary>
        /// Etapa 7 — Para cada oportunidade com catalog_id IS NULL, gera embedding de
        /// "title | summary", chama find_catalog_match (threshold 0.80) e atualiza
        /// catalog_id, catalog_score e catalog_matched_at. Sem match → loga e mantém NULL.
        /// </summary>
        private async Task ExecutarEtapa7CatalogMatchAsync(string? jobId)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            if (jobId != null)
                await GravarLogBancoAsync(jobId, "INFO", 7, "[PA-07] Iniciando Etapa 7: Associação semântica ao catálogo de serviços.");

            var sw7 = System.Diagnostics.Stopwatch.StartNew();

            // Buscar todas as oportunidades sem catalog_id (inclui backlog)
            var oportunidades = new List<(int Id, string Title, string Summary)>();
            using (var cmdRead = conn.CreateCommand())
            {
                cmdRead.CommandText = @"
                    SELECT opportunity_id, title, COALESCE(summary, '')
                    FROM public.predict_discovery_opportunities
                    WHERE catalog_id IS NULL AND is_active = TRUE
                    ORDER BY opportunity_id";
                cmdRead.CommandTimeout = 120;
                using var reader = await cmdRead.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    oportunidades.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
            }

            _logger.LogInformation("[PA-07] {Total} oportunidades sem catalog_id encontradas.", oportunidades.Count);
            if (jobId != null)
                await GravarLogBancoAsync(jobId, "INFO", 7,
                    $"[PA-07] {oportunidades.Count} oportunidades elegíveis para match com o catálogo.");

            int comMatch = 0;
            int semMatch = 0;

            foreach (var op in oportunidades)
            {
                // Texto para embedding: "title | summary"
                var textoOp = $"{op.Title} | {op.Summary}";

                float[] embedding;
                try
                {
                    embedding = await _embeddingService.GerarAsync(textoOp);
                }
                catch (Exception exEmb)
                {
                    _logger.LogWarning(exEmb, "[PA-07] Falha ao gerar embedding para oportunidade id={Id}. Pulando.", op.Id);
                    if (jobId != null)
                        await GravarLogBancoAsync(jobId, "WARN", 7,
                            $"[PA-07] Sem embedding para oportunidade id={op.Id}: {exEmb.Message}");
                    semMatch++;
                    continue;
                }

                if (embedding.Length == 0)
                {
                    semMatch++;
                    continue;
                }

                string embStr = "[" + string.Join(",",
                    embedding.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

                // Chamar find_catalog_match via SQL
                try
                {
                    using var cmdMatch = conn.CreateCommand();
                    cmdMatch.CommandText = @"
                        SELECT catalog_id, score
                        FROM public.find_catalog_match(@emb::vector(1024), @threshold, 1)";
                    cmdMatch.CommandTimeout = 30;

                    var p1 = cmdMatch.CreateParameter();
                    p1.ParameterName = "emb";
                    p1.Value = embStr;
                    cmdMatch.Parameters.Add(p1);

                    var p2 = cmdMatch.CreateParameter();
                    p2.ParameterName = "threshold";
                    p2.Value = LIMIAR_CATALOG_MATCH;
                    cmdMatch.Parameters.Add(p2);

                    using var matchReader = await cmdMatch.ExecuteReaderAsync();
                    if (await matchReader.ReadAsync())
                    {
                        int catalogId = matchReader.GetInt32(0);
                        double score = matchReader.GetDouble(1);
                        await matchReader.CloseAsync();

                        // Atualizar oportunidade com o match
                        using var cmdUpd = conn.CreateCommand();
                        cmdUpd.CommandText = @"
                            UPDATE public.predict_discovery_opportunities
                            SET catalog_id = @cid,
                                catalog_score = @score,
                                catalog_matched_at = NOW()
                            WHERE opportunity_id = @oid";

                        var u1 = cmdUpd.CreateParameter(); u1.ParameterName = "cid"; u1.Value = catalogId; cmdUpd.Parameters.Add(u1);
                        var u2 = cmdUpd.CreateParameter(); u2.ParameterName = "score"; u2.Value = score; cmdUpd.Parameters.Add(u2);
                        var u3 = cmdUpd.CreateParameter(); u3.ParameterName = "oid"; u3.Value = op.Id; cmdUpd.Parameters.Add(u3);
                        await cmdUpd.ExecuteNonQueryAsync();

                        comMatch++;
                        _logger.LogDebug("[PA-07] Oportunidade id={OpId} → catalog_id={CatId} (score={Score:F4}).",
                            op.Id, catalogId, score);
                    }
                    else
                    {
                        await matchReader.CloseAsync();
                        semMatch++;
                        _logger.LogDebug("[PA-07] Oportunidade id={OpId}: sem match no catálogo (threshold={Thr}).",
                            op.Id, LIMIAR_CATALOG_MATCH);
                        if (jobId != null)
                            await GravarLogBancoAsync(jobId, "INFO", 7,
                                $"[PA-07] Oportunidade id={op.Id} sem match no catálogo (score abaixo de {LIMIAR_CATALOG_MATCH}).");
                    }
                }
                catch (Exception exMatch)
                {
                    _logger.LogError(exMatch, "[PA-07] Erro ao fazer match para oportunidade id={Id}.", op.Id);
                    semMatch++;
                }
            }

            sw7.Stop();
            var resumo = $"[PA-07] Etapa 7 concluída em {FormatDuration(sw7.Elapsed)}. " +
                         $"Com match: {comMatch} | Sem match: {semMatch} | Total processado: {oportunidades.Count}.";
            _logger.LogInformation(resumo);
            if (jobId != null)
                await GravarLogBancoAsync(jobId, "INFO", 7, resumo);
        }

        private async Task GravarLogBancoAsync(string jobId, string tpLog, int etapa, string mensagem, int? alertaId = null)
        {
            try
            {

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO predict_process_logs (job_id, tp_log, etapa, alerta_id, mensagem) 
                                   VALUES (@j, @t, @e, @a, @m)";
                
                var p1 = cmd.CreateParameter(); p1.ParameterName = "j"; p1.Value = jobId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "t"; p2.Value = tpLog; cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter(); p3.ParameterName = "e"; p3.Value = etapa; cmd.Parameters.Add(p3);
                var p4 = cmd.CreateParameter(); p4.ParameterName = "a"; p4.Value = (object?)alertaId ?? DBNull.Value; cmd.Parameters.Add(p4);
                var p5 = cmd.CreateParameter(); p5.ParameterName = "m"; p5.Value = mensagem; cmd.Parameters.Add(p5);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gravar log no banco para o job {JobId}", jobId);
            }
        }
        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalSeconds < 60)
                return $"{(int)ts.TotalSeconds}s";
            
            return $"{(int)ts.TotalMinutes}m {(int)ts.Seconds}s";
        }
    }
}
