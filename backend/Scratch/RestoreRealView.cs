using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;

namespace Predict.Backend.Scratch
{
    public class RestoreRealView
    {
        public static async System.Threading.Tasks.Task Run(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            Console.WriteLine("=== Restoring Real View vw_predict_plano_acao_hostname ===");

            try
            {
                using var conn = context.Database.GetDbConnection();
                conn.Open();

                // 1. Check current state
                string currentType = "";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT table_type 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                          AND table_name = 'vw_predict_plano_acao_hostname';";
                    var result = cmd.ExecuteScalar();
                    currentType = result?.ToString() ?? "NOT FOUND";
                    Console.WriteLine($"Current type: {currentType}");
                }

                if (currentType == "BASE TABLE")
                {
                    Console.WriteLine("DETECTED: Mock table is blocking the real view. Dropping it...");
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "DROP TABLE IF EXISTS vw_predict_plano_acao_hostname CASCADE;";
                        cmd.ExecuteNonQuery();
                    }
                    Console.WriteLine("Mock table dropped successfully.");
                }

                // 2. Check if the view now exists (it might auto-resolve if it was a dependency)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT table_type 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                          AND table_name = 'vw_predict_plano_acao_hostname';";
                    var result = cmd.ExecuteScalar();
                    var newType = result?.ToString() ?? "NOT FOUND";
                    Console.WriteLine($"After drop - type: {newType}");

                    if (newType == "VIEW")
                    {
                        Console.WriteLine("SUCCESS! The real VIEW has been restored!");
                        
                        // Count rows
                        using var cmd2 = conn.CreateCommand();
                        cmd2.CommandText = "SELECT COUNT(*) FROM vw_predict_plano_acao_hostname;";
                        var count = cmd2.ExecuteScalar();
                        Console.WriteLine($"Real data row count: {count}");

                        // Show sample
                        using var cmd3 = conn.CreateCommand();
                        cmd3.CommandText = @"
                            SELECT id_plano, id_contrato, nome_entidade, nm_host, prioridade_sugerida
                            FROM vw_predict_plano_acao_hostname 
                            ORDER BY id_plano 
                            LIMIT 5;";
                        using var reader = cmd3.ExecuteReader();
                        Console.WriteLine("\n--- First 5 real rows ---");
                        while (reader.Read())
                        {
                            Console.WriteLine($"  id={reader.GetValue(0)}, contrato={reader.GetValue(1)}, entidade={reader.GetValue(2)}, host={reader.GetValue(3)}, prioridade={reader.GetValue(4)}");
                        }

                        // Show distinct clientes
                        using var cmd4 = conn.CreateCommand();
                        cmd4.CommandText = @"
                            SELECT DISTINCT nome_entidade 
                            FROM vw_predict_plano_acao_hostname 
                            WHERE nome_entidade IS NOT NULL 
                            ORDER BY nome_entidade;";
                        using var reader2 = cmd4.ExecuteReader();
                        Console.WriteLine("\n--- Real Clients ---");
                        while (reader2.Read())
                        {
                            Console.WriteLine($"  - {reader2.GetString(0)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: After dropping mock table, object is '{newType}'. View may need to be recreated manually.");
                        
                        // Check if there's a view definition stored in pg_views
                        using var cmd5 = conn.CreateCommand();
                        cmd5.CommandText = @"
                            SELECT viewname, definition 
                            FROM pg_views 
                            WHERE schemaname = 'public' 
                              AND viewname LIKE '%predict%plano%';";
                        using var reader3 = cmd5.ExecuteReader();
                        if (reader3.Read())
                        {
                            Console.WriteLine($"Found view definition: {reader3.GetString(0)}");
                            Console.WriteLine($"Definition: {reader3.GetString(1)}");
                        }
                        else
                        {
                            Console.WriteLine("No view definition found in pg_views. The original view definition needs to be executed by the DBA.");
                        }
                    }
                }

                Console.WriteLine("\n=== Restore complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}
