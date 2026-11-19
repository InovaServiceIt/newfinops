using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Predict.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Predict.Backend.Scratch
{
    public class CheckData
    {
        public static void Run(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            Console.WriteLine("--- Database Check ---");
            
            try {
                var planCount = context.ActionPlans.Count();
                Console.WriteLine($"Vw ActionPlans Count: {planCount}");
            } catch (Exception ex) {
                Console.WriteLine($"Error querying ActionPlans: {ex.Message}");
            }

            try {
                var narrativeCount = context.ActionPlanNarratives.Count();
                Console.WriteLine($"Narratives Count: {narrativeCount}");
            } catch (Exception ex) {
                Console.WriteLine($"Error querying Narratives: {ex.Message}");
            }
            
            // Try to check if there are any entities in the DB that might be feeding the view
            // Assuming there might be some raw SQL check if I knew the table names
        }
    }
}
