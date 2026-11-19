using System;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Predict.Backend.Scratch
{
    public class ReadProductionView
    {
        public static async System.Threading.Tasks.Task Run(IServiceProvider services)
        {
            // Same server, but database = postgres
            var config = services.GetRequiredService<IConfiguration>();
            var baseConn = config.GetConnectionString("DefaultConnection")!;
            
            // Replace the database name
            var postgresConn = baseConn.Replace("Database=Service_Infra_DB_DEV", "Database=postgres");
            
            Console.WriteLine($"=== Connecting to postgres database ===");
            Console.WriteLine($"Connection: {postgresConn.Substring(0, postgresConn.IndexOf("Password="))}...");

            try
            {
                using var conn = new NpgsqlConnection(postgresConn);
                conn.Open();
                Console.WriteLine("Connected successfully to postgres database!");

                // 1. Check if view exists
                using (var cmd = new NpgsqlCommand(@"
                    SELECT table_type 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                      AND table_name = 'vw_predict_plano_acao_hostname';", conn))
                {
                    var result = cmd.ExecuteScalar();
                    Console.WriteLine($"Object type: {result ?? "NOT FOUND"}");
                }

                // 2. Get view definition
                using (var cmd = new NpgsqlCommand(@"
                    SELECT definition 
                    FROM pg_views 
                    WHERE schemaname = 'public' 
                      AND viewname = 'vw_predict_plano_acao_hostname';", conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        Console.WriteLine($"\n--- VIEW DEFINITION ---");
                        Console.WriteLine(result.ToString());
                        Console.WriteLine("--- END DEFINITION ---\n");
                    }
                    else
                    {
                        Console.WriteLine("No view definition found - checking if it's a table...");
                    }
                }

                // 3. Count rows
                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM vw_predict_plano_acao_hostname;", conn))
                {
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"Row count: {count}");
                }

                // 4. Show first 5 rows
                using (var cmd = new NpgsqlCommand(@"
                    SELECT id_plano, id_contrato, nome_entidade, nm_host, prioridade_sugerida, score_hostname
                    FROM vw_predict_plano_acao_hostname 
                    ORDER BY id_plano 
                    LIMIT 5;", conn))
                {
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("\n--- First 5 rows ---");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  id={reader.GetValue(0)}, contrato={reader.GetValue(1)}, entidade={reader.GetValue(2)}, host={reader.GetValue(3)}, prioridade={reader.GetValue(4)}, score={reader.GetValue(5)}");
                    }
                }

                // 5. Distinct clients
                using (var cmd = new NpgsqlCommand(@"
                    SELECT DISTINCT nome_entidade 
                    FROM vw_predict_plano_acao_hostname 
                    WHERE nome_entidade IS NOT NULL 
                    ORDER BY nome_entidade;", conn))
                {
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("\n--- Real Clients ---");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  - {reader.GetString(0)}");
                    }
                }

                // 6. List all columns
                using (var cmd = new NpgsqlCommand(@"
                    SELECT column_name, data_type, is_nullable
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' 
                      AND table_name = 'vw_predict_plano_acao_hostname'
                    ORDER BY ordinal_position;", conn))
                {
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("\n--- Columns ---");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  {reader.GetString(0)} ({reader.GetString(1)}) nullable={reader.GetString(2)}");
                    }
                }

                Console.WriteLine("\n=== Done ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }
}
