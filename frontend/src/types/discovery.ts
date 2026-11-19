export interface PredictDiscoveryOpportunity {
    id: number;
    clientCode: string;
    opportunityCode: string;
    processId?: number;
    process?: PredictDiscoveryProcess;
    title: string;
    opportunityCategory?: string;
    operationalArea?: string;
    summary?: string;
    requestedBy?: string;
    ownerName?: string;
    sourceReference?: string;
    originType?: string;
    radarPatternReference?: string;
    expectedGainClassification?: string;
    implementationEffortClassification?: string;
    operationalRiskClassification?: string;
    priorityScore: number;
    priorityRank: number;
    estimatedVolume?: number;
    volumeUnit?: string;
    frequencyValue?: number;
    frequencyUnit?: string;
    estimatedTimeSavedMinutes?: number;
    estimatedCostAvoidance?: number;
    automationPotentialPercent?: number;
    status: string;
    discardReason?: string;
    builderReference?: string;
    convertedToBuilder: boolean;
    isActive: boolean;
    createdAt: string;
    createdBy?: string;
    // Catálogo semântico (Etapa 7 — preenchido quando catalog_id != null)
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

export interface PredictDiscoveryTask {
    id: number;
    opportunityId: number;
    taskCode: string;
    name: string;
    description?: string;
    sequence: number;
    estimatedVolume?: number;
    volumeUnit?: string;
    frequencyValue?: number;
    frequencyUnit?: string;
    averageTimeInMinutes?: number;
    automationCandidate: boolean;
    status: string;
}

export interface PredictDiscoveryPainPoint {
    id: number;
    opportunityId: number;
    painPointCode: string;
    title: string;
    description?: string;
    impactType?: string;
    impactLevel?: string;
    evidence?: string;
    recurrenceNotes?: string;
    status: string;
}

export interface BulkRankRequest {
    clientCode: string;
    updates: BulkRankUpdateItem[];
    rationale?: string;
    contextPayload?: string;
}

export interface BulkRankUpdateItem {
    opportunityId: number;
    priorityRank: number;
    priorityScore: number;
    status: string;
    scoreOverridden: boolean;
    oldScore?: number;
    overrideJustification?: string;
}

export interface MatrixOpportunity extends PredictDiscoveryOpportunity {
    tempScore?: number;
    tempRank?: number;
    overridden?: boolean;
    justification?: string;
}
