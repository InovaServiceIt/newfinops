using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Predict.Backend.Services.Interfaces;

namespace Predict.Backend.Services.Implementations
{
    public class AlertsStagingSyncService : IAlertsStagingSyncService
    {
        private readonly string _originConnectionString;
        private readonly string _destinationConnectionString;
        private readonly ILogger<AlertsStagingSyncService> _logger;

        public AlertsStagingSyncService(IConfiguration configuration, ILogger<AlertsStagingSyncService> logger)
        {
            _originConnectionString = configuration.GetConnectionString("SourceConnection") 
                ?? throw new ArgumentNullException("SourceConnection string is missing in configuration.");
            _destinationConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection string is missing in configuration.");
            _logger = logger;
        }

        public async Task SyncAsync(string? jobId = null)
        {
            _logger.LogInformation("[AlertsStagingSync] Iniciando sincronização de alertas (90 dias)...");

            try
            {
                var rows = await ReadFromOriginAsync();
                _logger.LogInformation("[AlertsStagingSync] {Count} registros lidos da origem.", rows.Count);

                await WriteToDestinationAsync(rows);
                _logger.LogInformation("[AlertsStagingSync] Sincronização concluída com sucesso.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SYNC-01] Erro crítico durante a sincronização.");
                throw new Exception($"[SYNC-01] Erro ao sincronizar dados: {ex.Message}", ex);
            }
        }

        private async Task<List<AlertRow>> ReadFromOriginAsync()
        {
            var rows = new List<AlertRow>();

            await using var conn = new NpgsqlConnection(_originConnectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT *
                FROM alerts_alert
                WHERE open_datetime >= (CURRENT_DATE - INTERVAL '90 days')";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.CommandTimeout = 300;

            await using var reader = await cmd.ExecuteReaderAsync();

            var columnNames = new List<string>();
            var columnTypes = new List<Type>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
                columnTypes.Add(reader.GetFieldType(i));
            }

            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(new AlertRow(columnNames, columnTypes, values));
            }

            return rows;
        }

        private async Task WriteToDestinationAsync(List<AlertRow> rows)
        {
            await using var conn = new NpgsqlConnection(_destinationConnectionString);
            await conn.OpenAsync();

            _logger.LogInformation("[AlertsStagingSync] Executando TRUNCATE em stg_alerts_alert...");
            await using (var truncateCmd = new NpgsqlCommand("TRUNCATE TABLE stg_alerts_alert", conn))
                await truncateCmd.ExecuteNonQueryAsync();

            if (rows.Count == 0)
            {
                _logger.LogWarning("[AlertsStagingSync] Nenhum registro encontrado para sincronizar.");
                return;
            }

            var columns = string.Join(", ", rows[0].ColumnNames);
            // Mudamos para FORMAT TEXT para maior resiliência contra erros de protocolo (08P01)
            var copySql = $"COPY stg_alerts_alert ({columns}) FROM STDIN";

            _logger.LogInformation("[AlertsStagingSync] Iniciando COPY TEXT para {Count} linhas...", rows.Count);

            await using var writer = await conn.BeginTextImportAsync(copySql);

            foreach (var row in rows)
            {
                var lineValues = new List<string>();

                for (int i = 0; i < row.Values.Length; i++)
                {
                    var val = row.Values[i];
                    var colName = row.ColumnNames[i].ToLower();
                    var colType = row.ColumnTypes[i];

                    if (val == DBNull.Value || val == null)
                    {
                        // Proteção Global de IDs: apenas para tipos NUMÉRICOS
                        if ((colName.StartsWith("id_") || colName.EndsWith("_id") || colName == "id")
                            && (colType == typeof(int) || colType == typeof(long) || colType == typeof(decimal)))
                        {
                            lineValues.Add("0");
                        }
                        else
                        {
                            lineValues.Add("\\N");
                        }
                    }
                    else
                    {
                        if (val is DateTime dt)
                        {
                            lineValues.Add(dt.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        }
                        else if (val is bool b)
                        {
                            lineValues.Add(b ? "true" : "false");
                        }
                        else
                        {
                            // Escapar caracteres especiais se necessário (tab, newline)
                            var s = val.ToString()?.Replace("\t", " ").Replace("\n", " ").Replace("\r", " ") ?? "";
                            lineValues.Add(s);
                        }
                    }
                }

                await writer.WriteLineAsync(string.Join("\t", lineValues));
            }

            _logger.LogInformation("[AlertsStagingSync] Finalizando importação...");
        }
    }

    internal class AlertRow
    {
        public List<string> ColumnNames { get; }
        public List<Type> ColumnTypes { get; }
        public object[] Values { get; }

        public AlertRow(List<string> columnNames, List<Type> columnTypes, object[] values)
        {
            ColumnNames = columnNames;
            ColumnTypes = columnTypes;
            Values = values;
        }
    }
}
