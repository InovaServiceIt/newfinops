using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Predict.Backend.Models
{
    [Table("vw_predict_plano_acao_hostname")]
    public class ActionPlan
    {
        [Key]
        [Column("id_plano")]
        public int IdPlano { get; set; }

        [Column("id_contrato")]
        public int? IdContrato { get; set; }

        [Column("nome_entidade")]
        public string? NomeEntidade { get; set; }

        [Column("ranking_hostname")]
        public int? RankingHostname { get; set; }

        [Column("nm_host")]
        public string? NmHost { get; set; }

        [Column("nm_completo")]
        public string? NmCompleto { get; set; }

        [Column("descricao_chave")]
        public string? DescricaoChave { get; set; }

        [Column("description_exemplo")]
        public string? DescriptionExemplo { get; set; }

        [Column("rotulo_acao")]
        public string? RotuloAcao { get; set; }

        [Column("tx_acao_inicial_suger")]
        public string? TxAcaoInicialSuger { get; set; }

        [Column("prioridade_sugerida")]
        public string? PrioridadeSugerida { get; set; }

        [Column("justificativa_resumida")]
        public string? JustificativaResumida { get; set; }

        [Column("titulo_melhoria")]
        public string? TituloMelhoria { get; set; }

        [Column("score_hostname")]
        public decimal? ScoreHostname { get; set; }

        [Column("qt_eventos")]
        public int? QtEventos { get; set; }

        [Column("pct_volume_contrato")]
        public decimal? PctVolumeContrato { get; set; }

        [Column("dias_com_evento")]
        public int? DiasComEvento { get; set; }

        [Column("qt_disaster")]
        public int? QtDisaster { get; set; }

        [Column("qt_high")]
        public int? QtHigh { get; set; }

        [Column("qt_average")]
        public int? QtAverage { get; set; }

        [Column("qt_warning")]
        public int? QtWarning { get; set; }

        [Column("pct_graves")]
        public decimal? PctGraves { get; set; }

        [Column("pct_nao_graves")]
        public decimal? PctNaoGraves { get; set; }

        [Column("ultima_ocorrencia")]
        public DateTime? UltimaOcorrencia { get; set; }

        [Column("dt_inicio_analise")]
        public DateTime? DtInicioAnalise { get; set; }

        [Column("dt_fim_analise")]
        public DateTime? DtFimAnalise { get; set; }

        [Column("dt_registro_sistema")]
        public DateTime? DtRegistroSistema { get; set; }

        [Column("nu_requisicao_itsm")]
        public int? NuRequisicaoItsm { get; set; }

        [Column("cd_agente")]
        public string? CdAgente { get; set; }

        [Column("cd_categoria")]
        public string? CdCategoria { get; set; }

        [Column("nu_evm")]
        public long? NuEvm { get; set; }
    }
}
