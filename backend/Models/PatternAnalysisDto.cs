using System;
using System.Collections.Generic;

namespace Predict.Backend.Models
{
    public class PatternAnalysisStatusResponse
    {
        public bool PodeExecutar { get; set; }
        public int DiasDesde { get; set; }
        public int DiasRestantes { get; set; }
        public DateTime? UltimaExecucao { get; set; }
        public int QtClusters { get; set; }
        public int QtSugestoes { get; set; }
        public int QtPendentes { get; set; }
        public bool Bloqueado { get; set; }
        public int QtAvaliados { get; set; }
        public int QtFaltaAvaliar { get; set; }
    }

    public class ProgressJob
    {
        public int Etapa { get; set; }
        public string Status { get; set; } = string.Empty; // "iniciando", "em_andamento", "concluido", "erro"
        public int Total { get; set; }
        public int Processados { get; set; }
        public string Erro { get; set; } = string.Empty;

        public int QtAlertas { get; set; }
        public int QtClusters { get; set; }
        public int QtSugestoes { get; set; }
        public AnalisarPadroesResult? DiscoveryResult { get; set; }
        public string DetalheEtapa { get; set; } = string.Empty;
        public Dictionary<int, string> DetalhesPorEtapa { get; set; } = new();

    }

    public class ClusterData
    {
        public int Id { get; set; }
        public string IdContrato { get; set; } = string.Empty;
        public string NmHost { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int QtAlertas { get; set; }
    }

    public class ProcessLogDto
    {
        public int Id { get; set; }
        public string JobId { get; set; } = string.Empty;
        public DateTime DtLog { get; set; }
        public string TpLog { get; set; } = string.Empty;
        public int Etapa { get; set; }
        public int? AlertaId { get; set; }
        public string Mensagem { get; set; } = string.Empty;
    }

    public class SuggestionDto
    {
        public int Id { get; set; }
        public string IdContrato { get; set; } = string.Empty;
        public string NmHost { get; set; } = string.Empty;
        public int QtAlertasCluster { get; set; }
        public string DescricaoRepresentativa { get; set; } = string.Empty;
        public string TpAlvo { get; set; } = string.Empty;
        public string TpOperador { get; set; } = string.Empty;
        public string TxPadrao { get; set; } = string.Empty;
        public string RotuloAcao { get; set; } = string.Empty;
        public string CdPrioridade { get; set; } = string.Empty;
        public string TxAcaoInicialSuger { get; set; } = string.Empty;
        public string JustificativaIa { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DtSugestao { get; set; }
    }
}
