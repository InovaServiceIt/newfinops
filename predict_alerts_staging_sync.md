# Predict — Sincronização de Alertas para Staging

> Tarefa para: \*\*Antigravity\*\*  
> Branch: `feature/predict-discovery`  
> Stack: .NET / C# — Npgsql

\---

## Objetivo

Implementar o método `AlertsStagingSync` que sincroniza a tabela `alerts\_alert` (origem) para a tabela `stg\_alerts\_alert` (destino), cobrindo os últimos **90 dias** de dados.

O processo segue esta sequência:

1. Conectar na origem e ler `alerts\_alert` WHERE `open\_datetime >= CURRENT\_DATE - 90 dias`
2. Guardar os registros em memória
3. Conectar no destino e executar `TRUNCATE` em `stg\_alerts\_alert`
4. Inserir os registros via **COPY BINARY** (Bulk Insert)

\---

## Strings de Conexão

Usar as variáveis de ambiente do **Coolify** que vocês já conhecem:

|Papel|Variável Coolify|
|-|-|
|Origem|`ConnectionStrings\_\_SourceConnection`|
|Destino|`ConnectionStrings\_\_DefaultConnection`|

Ambos os bancos são **PostgreSQL**. Utilizar **Npgsql** para os dois.

\---

## Detalhes Técnicos

### Volume esperado

\~341.000 linhas por execução.  
Por isso o insert **obrigatoriamente** deve ser via `COPY BINARY` do Npgsql (`BeginBinaryImportAsync`) — INSERT em lote seria inviável nesse volume.

### Estrutura das tabelas

`alerts\_alert` (origem) e `stg\_alerts\_alert` (destino) são **idênticas** — mesmos nomes de colunas, mesmos tipos. O mapeamento pode ser feito dinamicamente via `reader.GetName(i)` e `reader.GetValues()`.

### Dependência

Apenas `Npgsql` (versão 7+). Sem dependências adicionais.

\---

## Código de Referência

O código abaixo é a implementação de referência. Adaptar conforme os padrões do projeto (injeção de dependência, logging, etc.).

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace Predict.Infra.Staging
{
    public class AlertsStagingSync
    {
        private readonly string \_originConnectionString;
        private readonly string \_destinationConnectionString;

        public AlertsStagingSync(string originConnectionString, string destinationConnectionString)
        {
            \_originConnectionString = originConnectionString;
            \_destinationConnectionString = destinationConnectionString;
        }

        /// <summary>
        /// Lê alerts\_alert dos últimos 90 dias na origem,
        /// trunca stg\_alerts\_alert no destino e realiza BULK INSERT via COPY.
        /// </summary>
        public async Task SyncAsync()
        {
            Console.WriteLine("\[AlertsStagingSync] Iniciando leitura da origem...");

            var rows = await ReadFromOriginAsync();

            Console.WriteLine($"\[AlertsStagingSync] {rows.Count} registros lidos.");

            await WriteToDestinationAsync(rows);

            Console.WriteLine("\[AlertsStagingSync] Sincronização concluída com sucesso.");
        }

        private async Task<List<AlertRow>> ReadFromOriginAsync()
        {
            var rows = new List<AlertRow>();

            await using var conn = new NpgsqlConnection(\_originConnectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT \*
                FROM alerts\_alert
                WHERE open\_datetime >= (CURRENT\_DATE - INTERVAL '90 days')";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var columnNames = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
                columnNames.Add(reader.GetName(i));

            while (await reader.ReadAsync())
            {
                var values = new object\[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(new AlertRow(columnNames, values));
            }

            return rows;
        }

        private async Task WriteToDestinationAsync(List<AlertRow> rows)
        {
            await using var conn = new NpgsqlConnection(\_destinationConnectionString);
            await conn.OpenAsync();

            Console.WriteLine("\[AlertsStagingSync] Executando TRUNCATE em stg\_alerts\_alert...");
            await using (var truncateCmd = new NpgsqlCommand("TRUNCATE TABLE stg\_alerts\_alert", conn))
                await truncateCmd.ExecuteNonQueryAsync();

            if (rows.Count == 0)
            {
                Console.WriteLine("\[AlertsStagingSync] Nenhum registro para inserir.");
                return;
            }

            var columns = string.Join(", ", rows\[0].ColumnNames);
            var copySql = $"COPY stg\_alerts\_alert ({columns}) FROM STDIN (FORMAT BINARY)";

            Console.WriteLine("\[AlertsStagingSync] Iniciando COPY BINARY...");

            await using var writer = await conn.BeginBinaryImportAsync(copySql);

            foreach (var row in rows)
            {
                await writer.StartRowAsync();

                for (int i = 0; i < row.Values.Length; i++)
                {
                    if (row.Values\[i] == DBNull.Value || row.Values\[i] == null)
                        await writer.WriteNullAsync();
                    else
                        await writer.WriteAsync(row.Values\[i], NpgsqlTypes.NpgsqlDbType.Unknown);
                }
            }

            await writer.CompleteAsync();
            Console.WriteLine($"\[AlertsStagingSync] {rows.Count} registros inseridos via COPY.");
        }
    }

    internal class AlertRow
    {
        public List<string> ColumnNames { get; }
        public object\[] Values { get; }

        public AlertRow(List<string> columnNames, object\[] values)
        {
            ColumnNames = columnNames;
            Values = values;
        }
    }
}
```

\---

## Como Instanciar

```csharp
var originConn = Environment.GetEnvironmentVariable("ConnectionStrings\_\_SourceConnection");
var destConn   = Environment.GetEnvironmentVariable("ConnectionStrings\_\_DefaultConnection");

var sync = new AlertsStagingSync(originConn, destConn);
await sync.SyncAsync();
```

> ⚠️ Se o projeto já usa `IConfiguration` para resolver connection strings, adaptar a leitura das variáveis conforme o padrão existente.

\---

## Observações

* O `NpgsqlDbType.Unknown` no `WriteAsync` funciona para os tipos padrão do PostgreSQL (int, text, timestamp, bool, numeric, etc.). Se houver colunas com tipos especiais (`jsonb`, arrays, enums customizados), informar para revisarmos o mapeamento explicitamente.
* O `TRUNCATE` não é transacionado com o `COPY` nesta versão. Se quiserem garantir atomicidade (rollback do truncate caso o COPY falhe), envolver os dois em uma `NpgsqlTransaction`.
* O método é `async` ponta a ponta — não usar `.Result` ou `.Wait()` na chamada.

