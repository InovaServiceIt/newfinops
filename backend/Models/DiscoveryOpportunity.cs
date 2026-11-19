using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    [Table("predict_discovery_opportunities")]
    public class PredictDiscoveryOpportunity
    {
        [Key]
        [Column("opportunity_id")]
        public int Id { get; set; }
        
        // Identificação
        [Column("client_code")]
        public string ClientCode { get; set; } = string.Empty;

        [NotMapped]
        public string? ClientName { get; set; }
        
        [Column("opportunity_code")]
        public string OpportunityCode { get; set; } = string.Empty;
        
        [Column("process_id")]
        public int? ProcessId { get; set; }
        public PredictDiscoveryProcess? Process { get; set; }
        
        [Column("title")]
        public string Title { get; set; } = string.Empty;
        
        [Column("opportunity_category")]
        public string? OpportunityCategory { get; set; }
        
        [Column("operational_area")]
        public string? OperationalArea { get; set; }

        [Column("origin_module")]
        public string OriginModule { get; set; } = "predict_radar";

        // Contexto
        [Column("summary")]
        public string? Summary { get; set; }
        
        [Column("requested_by")]
        public string? RequestedBy { get; set; }
        
        [Column("owner_name")]
        public string? OwnerName { get; set; }
        
        [Column("source_reference")]
        public string? SourceReference { get; set; }
        
        [Column("origin_type")]
        public string? OriginType { get; set; }
        
        [Column("radar_pattern_reference")]
        public string? RadarPatternReference { get; set; }

        // Classificação
        [Column("expected_gain_classification")]
        public string? ExpectedGainClassification { get; set; }
        
        [Column("implementation_effort_classification")]
        public string? ImplementationEffortClassification { get; set; }
        
        [Column("operational_risk_classification")]
        public string? OperationalRiskClassification { get; set; }
        
        [Column("priority_score")]
        public double PriorityScore { get; set; }
        
        [Column("priority_rank")]
        public int PriorityRank { get; set; }

        // Volumetria
        [Column("estimated_volume")]
        public decimal? EstimatedVolume { get; set; }
        
        [Column("volume_unit")]
        public string? VolumeUnit { get; set; }
        
        [Column("frequency_value")]
        public decimal? FrequencyValue { get; set; }
        
        [Column("frequency_unit")]
        public string? FrequencyUnit { get; set; }
        
        [Column("estimated_time_saved_minutes")]
        public int? EstimatedTimeSavedMinutes { get; set; }
        
        [Column("estimated_cost_avoidance")]
        public decimal? EstimatedCostAvoidance { get; set; }
        
        [Column("automation_potential_percent")]
        public double? AutomationPotentialPercent { get; set; }

        // Controle
        [Column("status")]
        public string Status { get; set; } = "Identificada";
        
        [Column("discard_reason")]
        public string? DiscardReason { get; set; }
        
        [Column("builder_reference")]
        public string? BuilderReference { get; set; }
        
        [Column("converted_to_builder")]
        public bool ConvertedToBuilder { get; set; }
        
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
        
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Column("created_by")]
        public string? CreatedBy { get; set; }
        
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        
        [Column("updated_by")]
        public string? UpdatedBy { get; set; }

        // Catálogo de Serviços — associação semântica (Etapa 7 do Discovery)
        [Column("catalog_id")]
        public int? CatalogId { get; set; }

        [Column("catalog_score")]
        public double? CatalogScore { get; set; }

        [Column("catalog_matched_at")]
        public DateTime? CatalogMatchedAt { get; set; }

        // Campos do catálogo carregados via JOIN no repositório (não persistidos)
        [NotMapped]
        public string? CatalogActivity { get; set; }

        [NotMapped]
        public string? CatalogAgentAction { get; set; }

        [NotMapped]
        public string? CatalogClassification { get; set; }
    }
}
