using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    [Table("predict_att_assignment_rules")]
    public class PredictAttAssignmentRule
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("level")]
        [Required]
        [MaxLength(10)]
        public string Level { get; set; } = "R4";

        [Column("priority")]
        [Required]
        [MaxLength(20)]
        public string Priority { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("criteria", TypeName = "jsonb")]
        public string Criteria { get; set; } = "{}"; // Ex: {"maxSlots": 1}

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("group_key")]
        [Required]
        [MaxLength(50)]
        public string GroupKey { get; set; } = string.Empty; // Ex: ACN_P1, P2

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
