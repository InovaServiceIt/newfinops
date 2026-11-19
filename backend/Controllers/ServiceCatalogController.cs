using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Predict.Backend.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Predict.Backend.Controllers
{
    [ApiController]
    [Route("api/servicecatalog")]
    public class ServiceCatalogController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ServiceCatalogController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/servicecatalog?search=disco
        [HttpGet]
        public async Task<IActionResult> GetCatalog([FromQuery] string? search)
        {
            var items = new List<CatalogItemDto>();

            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                var whereClause = string.IsNullOrWhiteSpace(search)
                    ? "WHERE is_active = TRUE"
                    : @"WHERE is_active = TRUE
                          AND (
                            activity    ILIKE @search
                            OR agent_action ILIKE @search
                            OR classification ILIKE @search
                          )";

                cmd.CommandText = $@"
                    SELECT
                        id,
                        activity,
                        classification,
                        agent_action,
                        (embedding IS NOT NULL) AS has_embedding
                    FROM public.predict_service_catalog
                    {whereClause}
                    ORDER BY portfolio, service, weight
                    LIMIT 500";

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@search";
                    p.Value = $"%{search}%";
                    cmd.Parameters.Add(p);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new CatalogItemDto
                    {
                        Id            = reader.GetInt32(0),
                        Activity      = reader.GetString(1),
                        Classification = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        AgentAction   = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        HasEmbedding  = reader.GetBoolean(4)
                    });
                }

                return Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CATALOG] Erro ao buscar catálogo: {ex.Message}");
                return StatusCode(500, new { erro = ex.Message });
            }
        }

        // GET /api/servicecatalog/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        COUNT(*)                                          AS total,
                        COUNT(*) FILTER (WHERE embedding IS NOT NULL)    AS indexed,
                        COUNT(*) FILTER (WHERE embedding IS NULL)        AS pending
                    FROM public.predict_service_catalog
                    WHERE is_active = TRUE";

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        total   = reader.GetInt64(0),
                        indexed = reader.GetInt64(1),
                        pending = reader.GetInt64(2)
                    });
                }

                return Ok(new { total = 0, indexed = 0, pending = 0 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CATALOG] Erro ao buscar stats: {ex.Message}");
                return StatusCode(500, new { erro = ex.Message });
            }
        }
    }

    // DTO interno — pode ficar no mesmo arquivo ou em Models/
    public class CatalogItemDto
    {
        public int    Id             { get; set; }
        public string Activity       { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string AgentAction    { get; set; } = string.Empty;
        public bool   HasEmbedding   { get; set; }
    }
}
