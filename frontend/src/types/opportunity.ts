export type RiskClassification = 'Alto' | 'Médio' | 'Baixo';
export type EffortClassification = 'Alto' | 'Médio' | 'Baixo';
export type GainClassification = 'Alto' | 'Médio' | 'Baixo';
export type OpportunityStatus = 'Draft' | 'Aprovado' | 'Em Análise' | 'Descartado' | 'Implementação';

export interface PredictDiscoveryOpportunity {
  id: number;
  opportunityCode: string;
  clientCode: string;
  clientName?: string;
  title: string;
  processId?: number;
  opportunityCategory?: string;
  operationalArea?: string;
  summary?: string;
  requestedBy?: string;
  ownerName?: string;
  sourceReference?: string;
  originType?: string;
  radarPatternReference?: string;
  expectedGainClassification?: GainClassification;
  implementationEffortClassification?: EffortClassification;
  operationalRiskClassification?: RiskClassification;
  priorityScore?: number;
  priorityRank?: number;
  estimatedVolume?: number;
  volumeUnit?: string;
  frequencyValue?: number;
  frequencyUnit?: string;
  estimatedTimeSavedMinutes?: number;
  estimatedCostAvoidance?: number;
  automationPotentialPercent?: number;
  status?: OpportunityStatus;
  discardReason?: string;
  builderReference?: string;
  convertedToBuilder?: boolean;
  isActive: boolean;
  createdAt: string;
  createdBy?: string;
  updatedAt: string;
  updatedBy?: string;
  // Catálogo semântico — Etapa 7 (preenchido quando catalog_id != null)
  catalogId?: number;
  catalogScore?: number;
  catalogMatchedAt?: string;
  catalogActivity?: string;
  catalogAgentAction?: string;
  catalogClassification?: string;
}

export interface PredictDiscoveryProcess {
  id: number;
  name: string;
  description?: string;
  isActive: boolean;
}
