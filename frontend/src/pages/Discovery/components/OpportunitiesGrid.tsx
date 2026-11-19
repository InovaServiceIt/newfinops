import React from 'react';
import { PredictDiscoveryOpportunity } from '../../../types/opportunity';
import { Eye, Edit2, Trash2, Bot } from 'lucide-react';

interface OpportunitiesGridProps {
  data: PredictDiscoveryOpportunity[];
  onView: (opp: PredictDiscoveryOpportunity) => void;
  onEdit: (opp: PredictDiscoveryOpportunity) => void;
  onInactivate: (opp: PredictDiscoveryOpportunity) => void;
}

export default function OpportunitiesGrid({ data, onView, onEdit, onInactivate }: OpportunitiesGridProps) {
  if (!data || data.length === 0) {
    return (
      <div className="empty-state">
        <div className="empty-state-icon">Nenhuma oportunidade encontrada</div>
        <p>Ajuste os filtros ou cadastre uma nova oportunidade.</p>
      </div>
    );
  }

  const getClassificationClass = (cls?: string) => {
    switch (cls?.toLowerCase()) {
      case 'alto': return 'alto';
      case 'médio': return 'médio';
      case 'baixo': return 'baixo';
      default: return '';
    }
  };

  const getStatusClass = (status?: string) => {
    if (!status) return 'draft';
    return status.toLowerCase().replace(' ', '-');
  };

  const formatScore = (score?: number) => {
    if (score == null) return '—';
    return score.toLocaleString('pt-BR');
  };

  return (
    <div className="opportunities-grid-container">
      <table className="opportunities-table">
        <thead>
          <tr>
            <th>Código</th>
            <th>Título</th>
            <th>Cliente</th>
            <th>Processo</th>
            <th>Ganho</th>
            <th>Esforço</th>
            <th>Risco</th>
            <th>Score</th>
            <th>Rank</th>
            <th>Status</th>
            <th>Responsável</th>
            <th title="Associação semântica ao catálogo de serviços"><Bot size={13} style={{ verticalAlign: 'middle' }} /> Catálogo</th>
            <th style={{ textAlign: 'center' }}>Ações</th>
          </tr>
        </thead>
        <tbody>
          {data.map((opp) => (
            <tr key={opp.id} style={{ opacity: opp.isActive ? 1 : 0.6 }}>
              <td><strong>{opp.opportunityCode}</strong></td>
              <td>{opp.title}</td>
              <td style={{ fontSize: '0.8rem', color: '#666' }}>{opp.clientName || opp.clientCode}</td>
              <td>{opp.processId || '—'}</td>
              <td>
                {opp.expectedGainClassification ? (
                  <span className={`class-badge ${getClassificationClass(opp.expectedGainClassification)}`}>
                    {opp.expectedGainClassification}
                  </span>
                ) : '—'}
              </td>
              <td>
                {opp.implementationEffortClassification ? (
                  <span className={`class-badge ${getClassificationClass(opp.implementationEffortClassification)}`}>
                    {opp.implementationEffortClassification}
                  </span>
                ) : '—'}
              </td>
              <td>
                {opp.operationalRiskClassification ? (
                  <span className={`class-badge ${getClassificationClass(opp.operationalRiskClassification)}`}>
                    {opp.operationalRiskClassification}
                  </span>
                ) : '—'}
              </td>
              <td><strong>{formatScore(opp.priorityScore)}</strong></td>
              <td>{opp.priorityRank ? `#${opp.priorityRank}` : '—'}</td>
              <td>
                <span className={`status-badge ${!opp.isActive ? 'inativo' : getStatusClass(opp.status)}`}>
                  {!opp.isActive ? 'Inativo' : (opp.status || 'Draft')}
                </span>
              </td>
              <td>{opp.ownerName || '—'}</td>
              <td>
                {opp.catalogId ? (
                  <span
                    className={`catalog-badge ${opp.catalogClassification === 'Objetiva' ? 'objetiva' : 'generica'}`}
                    title={`Atividade: ${opp.catalogActivity}\nAção: ${opp.catalogAgentAction}\nScore: ${opp.catalogScore ? (opp.catalogScore * 100).toFixed(0) + '%' : 'N/A'}`}
                  >
                    <Bot size={10} />
                    {opp.catalogClassification || 'Associada'}
                  </span>
                ) : (
                  <span className="catalog-badge none">—</span>
                )}
              </td>
              <td style={{ textAlign: 'center' }}>
                <div style={{ display: 'flex', gap: '0.4rem', justifyContent: 'center' }}>
                  <button 
                    onClick={() => onView(opp)}
                    title="Visualizar"
                    style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6c757d' }}
                  >
                    <Eye size={16} />
                  </button>
                  <button 
                    onClick={() => onEdit(opp)}
                    title="Editar"
                    style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#1565C0' }}
                  >
                    <Edit2 size={16} />
                  </button>
                  {opp.isActive && (
                    <button 
                      onClick={() => onInactivate(opp)}
                      title="Inativar"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#E63946' }}
                    >
                      <Trash2 size={16} />
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
