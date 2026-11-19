using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    public class ActionPlanNarrative
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int IdPlano { get; set; }

        [Required]
        public string Problema { get; set; } = string.Empty;

        [Required]
        public string Impacto { get; set; } = string.Empty;

        [Required]
        public string Acoes { get; set; } = "[]";

        [Required]
        [MaxLength(10)]
        public string Urgencia { get; set; } = string.Empty; // IMEDIATA | ALTA | MÉDIA

        [Required]
        public string JustificativaUrgencia { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ModeloUtilizado { get; set; } = "claude-haiku-4-5";

        public DateTime DtGeracao { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "gerado"; // gerado | erro

        public string? Erro { get; set; }
    }
}
