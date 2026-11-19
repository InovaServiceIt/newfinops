using Microsoft.EntityFrameworkCore;
using Npgsql;
using Predict.Backend.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace Predict.Backend.Scratch
{
    public class Onda2Migration
    {
        public static async Task Run(AppDbContext db)
        {
            var conn = db.Database.GetDbConnection() as NpgsqlConnection;
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

            Console.WriteLine("🚀 Iniciando Migração Onda 2...");

            using var cmd = conn.CreateCommand();
            
            // 1. Truncate existing fake embeddings
            Console.WriteLine("🗑️ Limpando embeddings da Onda 1...");
            cmd.CommandText = "TRUNCATE TABLE predict_alert_embeddings;";
            await cmd.ExecuteNonQueryAsync();

            // 2. Drop old index if exists
            cmd.CommandText = "DROP INDEX IF EXISTS idx_embeddings_vector;";
            await cmd.ExecuteNonQueryAsync();

            // 3. Create HNSW Index
            Console.WriteLine("🏗️ Criando Índice HNSW (vector_cosine_ops)...");
            cmd.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_hnsw_alert_embeddings 
                ON predict_alert_embeddings 
                USING hnsw (embedding vector_cosine_ops) 
                WITH (m = 16, ef_construction = 64);
            ";
            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine("✅ Migração de Banco (Onda 2) concluída!");
        }
    }
}
