using System;

namespace Predict.Backend.Models
{
    public class VwPlanoAcaoHostname
    {
        public int IdPlano { get; set; }
        public int IdContrato { get; set; }
        public string NmHost { get; set; } = string.Empty;
        public string DescricaoChave { get; set; } = string.Empty;
        public string TituloMelhoria { get; set; } = string.Empty;
        public string JustificativaResumida { get; set; } = string.Empty;
        public string CdCategoria { get; set; } = string.Empty;
        public string RotuloAcao { get; set; } = string.Empty;
        public double ScoreHostname { get; set; }
        public int RankingHostname { get; set; }
        public int QtEventos { get; set; }
    }

    public class AnalisarPadroesResult
    {
        public int Inseridas { get; set; }
        public int Ignoradas { get; set; }
        public int TotalView { get; set; }
    }

    public class BulkRankRequest
    {
        public string ClientCode { get; set; } = string.Empty;
        public List<BulkRankUpdateItem> Updates { get; set; } = new();
        public string? Rationale { get; set; }
        public string? ContextPayload { get; set; }
    }

    public class BulkRankUpdateItem
    {
        public int OpportunityId { get; set; }
        public int PriorityRank { get; set; }
        public double PriorityScore { get; set; }
        public string Status { get; set; } = "prioritized";
        public bool ScoreOverridden { get; set; }
        public double? OldScore { get; set; }
        public string? OverrideJustification { get; set; }
    }
}
