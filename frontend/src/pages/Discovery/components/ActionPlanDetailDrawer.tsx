import React from 'react';
import { ActionPlan, ActionPlanWithNarrative, ActionPlanNarrative, ROTULO_ACAO_MAP, PRIORIDADE_MAP } from '../../../types/actionPlan';
import { X, AlertTriangle, Activity, Clock, BarChart2, Terminal, Sparkles, RefreshCcw, CheckCircle } from 'lucide-react';
import { actionPlansService } from '../../../services/actionPlansService';
import './ActionPlanDetailDrawer.css';

interface ActionPlanDetailDrawerProps {
  item: ActionPlanWithNarrative | null;
  onClose: () => void;
  onRegenerate: (newNarrative: ActionPlanNarrative) => void;
}

export default function ActionPlanDetailDrawer({ item, onClose, onRegenerate }: ActionPlanDetailDrawerProps) {
  const [isRegenerating, setIsRegenerating] = React.useState(false);
  const plan = item?.plan;
  const narrative = item?.narrative;
  const isOpen = !!item;

  const formatDate = (d?: string) => {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('pt-BR');
  };

  const formatDateTime = (d?: string) => {
    if (!d) return '—';
    return new Date(d).toLocaleString('pt-BR');
  };

  const handleRegenerate = async () => {
    if (!plan?.idPlano) return;
    setIsRegenerating(true);
    try {
      const newNarrative = await actionPlansService.generateNarrative(plan.idPlano);
      onRegenerate(newNarrative);
    } catch (err) {
      console.error('Erro ao regenerar:', err);
      alert('Falha ao regenerar narrativa');
    } finally {
      setIsRegenerating(false);
    }
  };

  if (!isOpen) return null;

  const prioridade = plan.prioridadeSugerida || 'P4';
  const prioridadeStyle = PRIORIDADE_MAP[prioridade] || PRIORIDADE_MAP['P4'];
  const rotuloStyle = plan.rotuloAcao ? ROTULO_ACAO_MAP[plan.rotuloAcao] : null;
  const pctGraves = plan.pctGraves || 0;
  const pctNaoGraves = plan.pctNaoGraves || 0;

  return (
    <div className={`detail-overlay active`} onClick={onClose}>
      <div className={`detail-drawer active`} onClick={e => e.stopPropagation()}>
        <button className="detail-close" onClick={onClose}><X size={20} /></button>

        {/* SEÇÃO 1: CABEÇALHO */}
        <div className="detail-header">
          <div className="detail-badges">
            <span className="badge-prioridade-lg" style={{ background: prioridadeStyle.bg, color: prioridadeStyle.color }}>
              {prioridade}
            </span>
            {rotuloStyle && (
              <span className="badge-rotulo-lg" style={{ background: rotuloStyle.bg, color: rotuloStyle.color }}>
                {rotuloStyle.label}
              </span>
            )}
          </div>
          <h2 className="detail-title">{plan.nmCompleto || plan.nmHost || '—'}</h2>
          <span className="detail-categoria">{plan.cdCategoria}</span>

          <div className="detail-score-block">
            <span className="detail-score-number">{(plan.scoreHostname || 0).toLocaleString('pt-BR', { maximumFractionDigits: 0 })}</span>
            <span className="detail-score-label">Score de criticidade</span>
          </div>

          <div className="detail-radar-badge">
            <Sparkles size={14} />
            <span>{narrative ? 'Narrativa gerada por Claude Haiku 3.5' : 'Dados gerados pelo Predict Radar'}</span>
          </div>
        </div>

        <div className="detail-body">
          {/* SEÇÃO 2: PROBLEMA / NARRATIVA */}
          <section className="detail-section highlight">
            <div className="section-header-row">
              <h3 className="detail-section-title">
                <Sparkles size={16} /> Problema detectado
              </h3>
              <button 
                className={`btn-regenerate ${isRegenerating ? 'loading' : ''}`}
                onClick={handleRegenerate}
                disabled={isRegenerating}
              >
                <RefreshCcw size={14} /> {isRegenerating ? 'Gerando...' : 'Gerar narrativa IA'}
              </button>
            </div>

            {/* Real data from the view - always shown */}
            <p className="detail-text-ai">{plan!.descricaoChave || plan!.justificativaResumida || '—'}</p>

            {plan!.descriptionExemplo && (
              <>
                <h3 className="detail-section-title sub">
                  <Activity size={16} /> Log de evidência
                </h3>
                <pre className="detail-code-block">{plan!.descriptionExemplo}</pre>
              </>
            )}

            {/* AI Narrative - only if generated */}
            {narrative && (
              <>
                <h3 className="detail-section-title sub" style={{ marginTop: '1rem' }}>
                  <Sparkles size={16} /> Análise IA — Impacto no negócio
                </h3>
                <p className="detail-text-ai">{narrative.impacto}</p>

                <h3 className="detail-section-title sub">
                  <CheckCircle size={16} /> Plano de ação sugerido (IA)
                </h3>
                <ol className="detail-action-list">
                  {narrative.acoes.map((acao, idx) => (
                    <li key={idx}>{acao}</li>
                  ))}
                </ol>

                <div className={`urgency-callout urgency-${narrative.urgencia.toLowerCase()}`}>
                  <AlertTriangle size={16} />
                  <div>
                    <strong>URGÊNCIA {narrative.urgencia}</strong>
                    <p>{narrative.justificativaUrgencia}</p>
                  </div>
                </div>
              </>
            )}

            {/* Recommended action from the view - always shown */}
            {plan!.txAcaoInicialSuger && (
              <div className="recommended-action-box">
                <h4 className="action-box-title">Ação recomendada</h4>
                <p>{plan!.txAcaoInicialSuger}</p>
              </div>
            )}
          </section>

          {/* SEÇÃO 3: MÉTRICAS */}
          <section className="detail-section">
            <h3 className="detail-section-title">
              <BarChart2 size={16} /> Métricas do período
            </h3>
            <p className="detail-janela">
              <Clock size={13} /> Análise de {formatDate(plan!.dtInicioAnalise)} a {formatDate(plan!.dtFimAnalise)}
            </p>
            <div className="metrics-grid">
              <div className="metric-box neutral">
                <span className="metric-val">{plan!.qtEventos ?? '—'}</span>
                <span className="metric-lbl">Total de eventos</span>
              </div>
              <div className="metric-box orange">
                <span className="metric-val">{plan!.diasComEvento ?? '—'}</span>
                <span className="metric-lbl">Dias em alerta</span>
              </div>
              <div className="metric-box red">
                <span className="metric-val">{plan!.qtDisaster ?? 0}</span>
                <span className="metric-lbl">Disaster</span>
              </div>
              <div className="metric-box orange">
                <span className="metric-val">{plan!.qtHigh ?? 0}</span>
                <span className="metric-lbl">High</span>
              </div>
              <div className="metric-box yellow">
                <span className="metric-val">{plan!.qtAverage ?? 0}</span>
                <span className="metric-lbl">Average</span>
              </div>
              <div className="metric-box blue">
                <span className="metric-val">{plan!.qtWarning ?? 0}</span>
                <span className="metric-lbl">Warning</span>
              </div>
            </div>

            {/* Barra de severidade */}
            <div className="severity-bar-container">
              <div className="severity-bar">
                <div className="severity-graves" style={{ width: `${pctGraves}%` }} title={`${pctGraves.toFixed(1)}% graves`} />
                <div className="severity-nao-graves" style={{ width: `${pctNaoGraves}%` }} title={`${pctNaoGraves.toFixed(1)}% não graves`} />
              </div>
              <div className="severity-labels">
                <span className="sev-red">{pctGraves.toFixed(1)}% graves</span>
                <span className="sev-green">{pctNaoGraves.toFixed(1)}% não graves</span>
              </div>
            </div>

            <p className="detail-meta">Representa {plan!.pctVolumeContrato?.toFixed(2) ?? '—'}% do volume total do contrato</p>
            <p className="detail-meta">Última ocorrência: {formatDateTime(plan!.ultimaOcorrencia)}</p>
          </section>

          {/* SEÇÃO 4: AÇÃO RECOMENDADA */}
          <section className="detail-section">
            <h3 className="detail-section-title">
              <Activity size={16} /> Ação recomendada
            </h3>
            <div className="action-callout">
              <p>{plan!.txAcaoInicialSuger || '—'}</p>
            </div>
            {plan!.nuRequisicaoItsm ? (
              <div className="chamado-badge-lg">
                ✅ Chamado já aberto: <strong>#{plan!.nuRequisicaoItsm}</strong>
              </div>
            ) : null}
          </section>

          {/* SEÇÃO 5: RASTREABILIDADE */}
          <section className="detail-section detail-trace">
            <h3 className="detail-section-title">
              <Terminal size={16} /> Rastreabilidade técnica
            </h3>
            <div className="trace-grid">
              <span className="trace-key">ID Plano</span>
              <span className="trace-val">{plan.idPlano}</span>
              <span className="trace-key">ID Contrato</span>
              <span className="trace-val">{plan.idContrato ?? '—'}</span>
              <span className="trace-key">EVM</span>
              <span className="trace-val">{plan.nuEvm ?? '—'}</span>
              <span className="trace-key">Agente</span>
              <span className="trace-val">{plan.cdAgente ?? '—'}</span>
            </div>
          </section>
        </div>

        <div className="detail-footer">
          <button className="btn-glpi">Abrir chamado no GLPI</button>
        </div>
      </div>
    </div>
  );
}
