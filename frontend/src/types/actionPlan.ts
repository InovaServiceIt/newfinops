export interface ActionPlan {
  idPlano: number;
  idContrato?: number;
  nomeEntidade?: string;
  rankingHostname?: number;
  nmHost?: string;
  nmCompleto?: string;
  descricaoChave?: string;
  descriptionExemplo?: string;
  rotuloAcao?: string;
  txAcaoInicialSuger?: string;
  prioridadeSugerida?: string;
  justificativaResumida?: string;
  tituloMelhoria?: string;
  scoreHostname?: number;
  qtEventos?: number;
  pctVolumeContrato?: number;
  diasComEvento?: number;
  qtDisaster?: number;
  qtHigh?: number;
  qtAverage?: number;
  qtWarning?: number;
  pctGraves?: number;
  pctNaoGraves?: number;
  ultimaOcorrencia?: string;
  dtInicioAnalise?: string;
  dtFimAnalise?: string;
  dtRegistroSistema?: string;
  nuRequisicaoItsm?: number;
  cdAgente?: string;
  cdCategoria?: string;
  nuEvm?: number;
}

export interface ActionPlanNarrative {
  id: number;
  idPlano: number;
  problema: string;
  impacto: string;
  acoes: string[];
  urgencia: string;
  justificativaUrgencia: string;
  modeloUtilizado: string;
  dtGeracao: string;
  status: 'gerado' | 'erro';
  erro?: string;
}

export interface ActionPlanWithNarrative {
  plan: ActionPlan;
  narrative?: ActionPlanNarrative;
}

export interface ActionPlansResponse {
  total: number;
  dtUltimoRegistro?: string;
  clientes: string[];
  categorias: string[];
  data: ActionPlanWithNarrative[];
}

export const ROTULO_ACAO_MAP: Record<string, { label: string; color: string; bg: string }> = {
  INVESTIGAR_CAUSA_RAIZ_DE_EVENTO_CRITICO_RECORRENTE: { label: 'Causa Raiz', color: '#fff', bg: '#E74C3C' },
  REVISAR_CAPACIDADE_DO_HOST: { label: 'Capacidade', color: '#fff', bg: '#E67E22' },
  REVISAR_COLETA_E_CONECTIVIDADE_DO_MONITORAMENTO: { label: 'Conectividade', color: '#333', bg: '#F39C12' },
  REVISAR_LIMIARES_E_SUPRESSAO_DE_RUIDO: { label: 'Limiares', color: '#fff', bg: '#2980B9' },
  ANALISE_ESPECIALIZADA_DO_EVENTO: { label: 'Análise Especializada', color: '#fff', bg: '#8E44AD' },
  REVISAR_REGRA_DE_NEGOCIO_OU_INTEGRACAO: { label: 'Regra / Integração', color: '#fff', bg: '#2C3E50' },
  REVISAR_DISPONIBILIDADE_DE_SERVICO_OU_PORTA: { label: 'Disponibilidade', color: '#fff', bg: '#922B21' },
};

export const PRIORIDADE_MAP: Record<string, { color: string; bg: string }> = {
  P2: { color: '#fff', bg: '#C0392B' },
  P3: { color: '#333', bg: '#F39C12' },
  P4: { color: '#555', bg: '#BDC3C7' },
};
