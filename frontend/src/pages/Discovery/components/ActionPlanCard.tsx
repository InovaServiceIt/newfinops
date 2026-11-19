import React from 'react';
import { ActionPlan, ActionPlanNarrative, ROTULO_ACAO_MAP, PRIORIDADE_MAP } from '../../../types/actionPlan';
import { AlertTriangle, Eye, Clock, Zap, Sparkles } from 'lucide-react';
import './ActionPlanCard.css';

interface ActionPlanCardProps {
  plan: ActionPlan;
  narrative?: ActionPlanNarrative;
  maxScore: number;
  onViewDetail: () => void;
}

export default function ActionPlanCard({ plan, narrative, maxScore, onViewDetail }: ActionPlanCardProps) {
  const prioridade = plan.prioridadeSugerida || 'P4';
  const prioridadeStyle = PRIORIDADE_MAP[prioridade] || PRIORIDADE_MAP['P4'];
  const rotuloStyle = plan.rotuloAcao ? ROTULO_ACAO_MAP[plan.rotuloAcao] : null;
  const score = plan.scoreHostname || 0;
  const scorePercent = maxScore > 0 ? (score / maxScore) * 100 : 0;
  const isHighScore = score > 3000;
  const isCritical = prioridade === 'P2';
  const has100PctGraves = (plan.pctGraves || 0) >= 100;

  const formatDate = (d?: string) => {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('pt-BR');
  };

  const getUrgencyStyle = (urgency?: string) => {
    switch (urgency) {
      case 'IMEDIATA': return { bg: '#FFEBEE', color: '#B71C1C', border: '#E53935' };
      case 'ALTA': return { bg: '#FFF3E0', color: '#E65100', border: '#F57C00' };
      case 'MÉDIA': return { bg: '#F3F8FF', color: '#1565C0', border: '#1E88E5' };
      default: return null;
    }
  };

  const urgencyStyle = getUrgencyStyle(narrative?.urgencia);

  return (
    <div className={`action-plan-card ${isHighScore ? 'high-score' : ''} ${isCritical ? 'critical' : ''}`}>
      {/* Badges */}
      <div className="card-badges">
        <span
          className="badge-prioridade"
          style={{ background: prioridadeStyle.bg, color: prioridadeStyle.color }}
        >
          {prioridade}
        </span>
        {rotuloStyle && (
          <span
            className="badge-rotulo"
            style={{ background: rotuloStyle.bg, color: rotuloStyle.color }}
          >
            {rotuloStyle.label}
          </span>
        )}
        {plan.nuRequisicaoItsm && (
          <span className="badge-chamado">
            Chamado #{plan.nuRequisicaoItsm}
          </span>
        )}
      </div>

      {/* Título + Score */}
      <div className="card-header-row">
        <div className="card-title-block">
          <h3 className="card-host-name">{plan.nmHost || '—'}</h3>
          <span className="card-category">{plan.cdCategoria || ''}</span>
        </div>
        <div className="card-score-block">
          <span className="score-value">{score.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}</span>
          <div className="score-bar-bg">
            <div 
              className="score-bar-fill"
              style={{ 
                width: `${Math.min(scorePercent, 100)}%`,
                background: score > 3000 ? '#E74C3C' : score > 1000 ? '#F39C12' : '#2ECC71'
              }}
            />
          </div>
        </div>
      </div>

      {/* AI Narrative Section (only if exists) */}
      {narrative && (
        <div className="card-narrative">
          <div className="narrative-meta">
            <Sparkles size={14} className="sparkle-icon" />
            <span>Análise IA · Haiku 3.5 · {formatDate(narrative.dtGeracao)}</span>
          </div>
          
          <div className="narrative-section">
            <span className="narrative-label">PROBLEMA</span>
            <p className="narrative-text">{narrative.problema}</p>
          </div>

          <div className="narrative-section">
            <span className="narrative-label">IMPACTO</span>
            <p className="narrative-text">{narrative.impacto}</p>
          </div>

          {urgencyStyle && (
            <div 
              className="narrative-urgency"
              style={{ background: urgencyStyle.bg, color: urgencyStyle.color, borderColor: urgencyStyle.border }}
            >
              <AlertTriangle size={14} />
              <strong>URGÊNCIA: {narrative.urgencia}</strong>
              <span className="urgency-justification">{narrative.justificativaUrgencia}</span>
            </div>
          )}
        </div>
      )}

      {/* Description from predictive model */}
      <p className="card-desc">{plan.justificativaResumida || '—'}</p>

      {/* Linha de métricas */}
      <div className="card-metrics">
        <span className="metric-item">
          <Clock size={14} />
          {plan.diasComEvento ?? '—'} dias em alerta
        </span>
        <span className={`metric-item ${has100PctGraves ? 'metric-danger' : ''}`}>
          {has100PctGraves
            ? <AlertTriangle size={14} />
            : <Zap size={14} />}
          {plan.pctGraves?.toFixed(0) ?? '0'}% eventos graves
        </span>
      </div>

      {/* Label Predict Radar */}
      <div className="card-radar-label">
        🔴 Gerado pelo Predict Radar — {formatDate(plan.dtInicioAnalise)} a {formatDate(plan.dtFimAnalise)}
      </div>

      {/* Ações */}
      <div className="card-actions">
        {!plan.nuRequisicaoItsm && (
          <button className="btn-open-ticket">
            Abrir chamado
          </button>
        )}
        <button className="btn-detail" onClick={() => onViewDetail(plan)}>
          <Eye size={16} /> Ver detalhe
        </button>
      </div>
    </div>
  );
}
