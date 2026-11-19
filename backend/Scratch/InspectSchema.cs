using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;

namespace Predict.Backend.Scratch
{
    public class InspectSchema
    {
        public static async System.Threading.Tasks.Task Run(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            Console.WriteLine("--- Inspecting Database Schema ---");

            var sql = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = 'public' 
                ORDER BY table_name;
            ";

            try {
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;
                context.Database.OpenConnection();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    Console.WriteLine($"Table: {reader.GetString(0)}");
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error inspecting schema: {ex.Message}");
            }
        }
    }
}
