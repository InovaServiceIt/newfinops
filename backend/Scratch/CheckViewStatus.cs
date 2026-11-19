using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;

namespace Predict.Backend.Scratch
{
    public class CheckViewStatus
    {
        public static async System.Threading.Tasks.Task Run(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            Console.WriteLine("=== Checking vw_predict_plano_acao_hostname status ===");

            try
            {
                using var conn = context.Database.GetDbConnection();
                conn.Open();

                // 1. Check if it's a VIEW
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT table_type 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                          AND table_name = 'vw_predict_plano_acao_hostname';";
                    var result = cmd.ExecuteScalar();
                    Console.WriteLine($"Object type: {result ?? "NOT FOUND"}");
                }

                // 2. Count rows
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM vw_predict_plano_acao_hostname;";
                    var count = cmd.ExecuteScalar();
                    Console.WriteLine($"Row count: {count}");
                }

                // 3. Show first 5 rows (real data check)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT id_plano, id_contrato, nome_entidade, nm_host, prioridade_sugerida, score_hostname
                        FROM vw_predict_plano_acao_hostname 
                        ORDER BY id_plano 
                        LIMIT 5;";
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("\n--- First 5 rows ---");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  id_plano={reader.GetValue(0)}, contrato={reader.GetValue(1)}, entidade={reader.GetValue(2)}, host={reader.GetValue(3)}, prioridade={reader.GetValue(4)}, score={reader.GetValue(5)}");
                    }
                }

                // 4. Show distinct clientes
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT DISTINCT nome_entidade 
                        FROM vw_predict_plano_acao_hostname 
                        WHERE nome_entidade IS NOT NULL 
                        ORDER BY nome_entidade;";
                    using var reader = cmd.ExecuteReader();
                    Console.WriteLine("\n--- Distinct Clients ---");
                    while (reader.Read())
                    {
                        Console.WriteLine($"  - {reader.GetString(0)}");
                    }
                }

                Console.WriteLine("\n=== Check complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }
}
