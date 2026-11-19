using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    [Table("predict_audit_history")]
    public class PredictAuditHistory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("module_name")]
        [Required]
        [MaxLength(50)]
        public string ModuleName { get; set; } = string.Empty;

        [Column("entity_type")]
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        [Column("entity_id")]
        public string EntityId { get; set; } = string.Empty;

        [Column("event_type")]
        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [Column("field_name")]
        [MaxLength(50)]
        public string? FieldName { get; set; }

        [Column("old_value")]
        public string? OldValue { get; set; }

        [Column("new_value")]
        public string? NewValue { get; set; }

        [Column("justification")]
        public string? Justification { get; set; }

        [Column("changed_by")]
        [MaxLength(100)]
        public string ChangedBy { get; set; } = string.Empty;

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [Table("predict_decision_log")]
    public class PredictDecisionLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("module_name")]
        [Required]
        [MaxLength(50)]
        public string ModuleName { get; set; } = string.Empty;

        [Column("entity_type")]
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        [Column("entity_id")]
        public string EntityId { get; set; } = string.Empty;

        [Column("decision_type")]
        [Required]
        [MaxLength(50)]
        public string DecisionType { get; set; } = string.Empty;

        [Column("rationale")]
        public string Rationale { get; set; } = string.Empty;

        [Column("context_payload", TypeName = "jsonb")]
        public string? ContextPayload { get; set; }

        [Column("decided_by")]
        [MaxLength(100)]
        public string DecidedBy { get; set; } = string.Empty;

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
