using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Predict.Backend.Infrastructure.Data;
using Npgsql;

namespace Predict.Backend.Scratch
{
    public static class CheckPgVector
    {
        public static async Task Run(IServiceProvider services)
        {
            var config = services.GetRequiredService<IConfiguration>();
            var defaultConnStr = config.GetConnectionString("DefaultConnection");
            var prodConnStr = config.GetConnectionString("ProductionViews");
            
            Console.WriteLine("=====================================================");
            Console.WriteLine("CONFIRMAÇÃO DOS BANCOS DE DADOS E PGVECTOR:");
            Console.WriteLine("=====================================================");

            await CheckDb(defaultConnStr, "DB Principal (DefaultConnection)");
            await CheckDb(prodConnStr, "DB Analítico (ProductionViews)");
            
            Console.WriteLine("=====================================================");
        }

        private static async Task CheckDb(string connStr, string label)
        {
            try
            {
                using var conn = new NpgsqlConnection(connStr);
                await conn.OpenAsync();
                
                string currentDb = "";
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT current_database();";
                    currentDb = (string)await cmd.ExecuteScalarAsync();
                }

                Console.WriteLine($"\n-> Acessando: {label}");
                Console.WriteLine($"-> Nome real do banco (current_database): {currentDb}");

                // Check pgvector
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT default_version FROM pg_available_extensions WHERE name = 'vector';";
                    var availableVersion = await cmd.ExecuteScalarAsync();
                    
                    if (availableVersion != null)
                    {
                        Console.WriteLine($"   [OK] Binários do 'pgvector' estão DISPONÍVEIS no catálogo (v{availableVersion}).");
                        
                        cmd.CommandText = "SELECT extversion FROM pg_extension WHERE extname = 'vector';";
                        var installedVersion = await cmd.ExecuteScalarAsync();
                        
                        if (installedVersion != null)
                            Console.WriteLine($"   [OK] Extensão INSTALADA neste banco (v{installedVersion}).");
                        else
                            Console.WriteLine($"   [AVISO] Extensão ainda NÃO habilitada via 'CREATE EXTENSION' neste banco.");
                    }
                    else
                    {
                        Console.WriteLine($"   [ERRO] Binários do 'pgvector' não encontrados neste contexto.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO] Falha ao verificar {label}: {ex.Message}");
            }
        }
    }
}
