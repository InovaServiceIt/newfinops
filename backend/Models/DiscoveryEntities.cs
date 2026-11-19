using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    public class PredictDiscoveryProcess
    {
        public int Id { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class PredictDiscoveryTask
    {
        public int Id { get; set; }
        public int OpportunityId { get; set; }
        public string TaskCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Sequence { get; set; }
        public decimal? EstimatedVolume { get; set; }
        public string? VolumeUnit { get; set; }
        public decimal? FrequencyValue { get; set; }
        public string? FrequencyUnit { get; set; }
        public int? AverageTimeInMinutes { get; set; }
        public bool AutomationCandidate { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class PredictDiscoveryPainPoint
    {
        public int Id { get; set; }
        public int OpportunityId { get; set; }
        public string PainPointCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImpactType { get; set; }
        public string? ImpactLevel { get; set; }
        public string? Evidence { get; set; }
        public string? RecurrenceNotes { get; set; }
        public string Status { get; set; } = "Identified";
    }

    [Table("integration_entities")]
    public class IntegrationEntity
    {
        [Column("idcontrato4biz")]
        public int? IdContrato4Biz { get; set; }

        [Key]
        [Column("entities_id")]
        public int EntitiesId { get; set; }

        [Column("nome")]
        public string? Nome { get; set; }
    }
}
