import React, { useEffect, useState } from 'react';
import { 
  ShieldCheck, CheckCircle2, XCircle, Clock, Info, 
  Search, Filter, ChevronRight, AlertCircle, Sparkles,
  ArrowRight, Shield
} from 'lucide-react';
import { approvalService, Suggestion } from '../../services/approvalService';
import './ApprovalPage.css';

export default function ApprovalPage() {
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [processingId, setProcessingId] = useState<number | null>(null);
  const [observation, setObservation] = useState('');

  const selectedSuggestion = suggestions.find(s => s.id === selectedId);

  useEffect(() => {
    loadSuggestions();
  }, []);

  const loadSuggestions = async () => {
    setLoading(true);
    try {
      const data = await approvalService.getPendingSuggestions();
      setSuggestions(data);
      if (data.length > 0 && !selectedId) {
        setSelectedId(data[0].id);
      }
    } catch (err) {
      console.error('Erro ao carregar sugestões:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (id: number) => {
    setProcessingId(id);
    try {
      await approvalService.approve(id, observation);
      setSuggestions(prev => prev.filter(s => s.id !== id));
      setSelectedId(null);
      setObservation('');
    } catch (err) {
      console.error('Erro ao aprovar:', err);
    } finally {
      setProcessingId(null);
    }
  };

  const handleReject = async (id: number) => {
    setProcessingId(id);
    try {
      await approvalService.reject(id, observation);
      setSuggestions(prev => prev.filter(s => s.id !== id));
      setSelectedId(null);
      setObservation('');
    } catch (err) {
      console.error('Erro ao rejeitar:', err);
    } finally {
      setProcessingId(null);
    }
  };

  const getPriorityColor = (p: string) => {
    switch (p) {
      case 'P1': return '#ef4444';
      case 'P2': return '#f97316';
      case 'P3': return '#eab308';
      default: return '#94a3b8';
    }
  };

  return (
    <div className="approval-page">
      {/* Sidebar - Lista de Sugestões */}
      <div className="approval-sidebar">
        <div className="sidebar-header">
          <div className="flex items-center gap-2 mb-4">
            <ShieldCheck size={24} className="text-blue-500" />
            <h2 className="!mb-0">Fila de Aprovação</h2>
          </div>
          <div className="search-box">
            <Search size={16} />
            <input type="text" placeholder="Buscar sugestão..." />
          </div>
        </div>

        <div className="suggestions-list">
          {loading ? (
            <div className="p-8 text-center text-gray-500">Carregando fila...</div>
          ) : suggestions.length === 0 ? (
            <div className="p-8 text-center">
              <CheckCircle2 size={40} className="text-green-500/20 mx-auto mb-4" />
              <p className="text-gray-400">Tudo limpo! Nenhuma sugestão pendente.</p>
            </div>
          ) : (
            suggestions.map(s => (
              <div 
                key={s.id} 
                className={`suggestion-item ${selectedId === s.id ? 'active' : ''}`}
                onClick={() => setSelectedId(s.id)}
              >
                <div className="flex justify-between items-start mb-2">
                  <span className="priority-dot" style={{ background: getPriorityColor(s.cdPrioridade) }} />
                  <span className="suggestion-date">{new Date(s.dtSugestao).toLocaleDateString()}</span>
                </div>
                <h4 className="suggestion-host">{s.nmHost || 'Global'}</h4>
                <p className="suggestion-preview">{s.descricaoRepresentativa.substring(0, 80)}...</p>
                <div className="suggestion-meta">
                  <span className="cluster-badge">{s.qtAlertasCluster} alertas</span>
                  <ChevronRight size={16} />
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* Main Content - Detalhe e Ação */}
      <div className="approval-main">
        {selectedSuggestion ? (
          <div className="suggestion-detail-container">
            <div className="detail-header">
              <div className="flex items-center gap-3">
                <div className="ia-icon-pulse">
                  <Sparkles size={20} className="text-purple-400" />
                </div>
                <div>
                  <div className="text-xs text-purple-400 font-bold tracking-widest uppercase mb-1">Sugestão Gerada por IA</div>
                  <h1 className="detail-title">Análise de Padrão: {selectedSuggestion.nmHost || 'Global'}</h1>
                </div>
              </div>
              <div className="flex gap-3">
                <button 
                  className="btn-reject" 
                  onClick={() => handleReject(selectedSuggestion.id)}
                  disabled={processingId !== null}
                >
                  <XCircle size={18} /> Rejeitar
                </button>
                <button 
                  className="btn-approve" 
                  onClick={() => handleApprove(selectedSuggestion.id)}
                  disabled={processingId !== null}
                >
                  <CheckCircle2 size={18} /> Aprovar Regra
                </button>
              </div>
            </div>

            <div className="detail-grid">
              <div className="detail-column-main">
                {/* Seção 1: Justificativa */}
                <div className="detail-card ia-justification">
                  <h3><Info size={18} /> Justificativa da IA</h3>
                  <p>{selectedSuggestion.justificativaIa}</p>
                </div>

                {/* Seção 2: A Regra Proposta */}
                <div className="detail-card rule-definition">
                  <div className="flex justify-between items-center mb-4">
                    <h3><Shield size={18} /> Configuração da Regra</h3>
                    <span className="priority-badge" style={{ borderColor: getPriorityColor(selectedSuggestion.cdPrioridade), color: getPriorityColor(selectedSuggestion.cdPrioridade) }}>
                      Prioridade {selectedSuggestion.cdPrioridade}
                    </span>
                  </div>
                  
                  <div className="rule-visualizer">
                    <div className="rule-step">
                      <span className="step-label">SE campo</span>
                      <span className="step-value">{selectedSuggestion.tpAlvo}</span>
                    </div>
                    <div className="rule-step">
                      <span className="step-label">OPERADOR</span>
                      <span className="step-value">{selectedSuggestion.tpOperador}</span>
                    </div>
                    <div className="rule-step">
                      <span className="step-label">PADRÃO</span>
                      <code className="step-code">{selectedSuggestion.txPadrao}</code>
                    </div>
                    <div className="rule-connector">
                      <ArrowRight size={24} className="text-gray-600" />
                    </div>
                    <div className="rule-step outcome">
                      <span className="step-label">ENTÃO</span>
                      <span className="step-value">{selectedSuggestion.rotuloAcao}</span>
                    </div>
                  </div>
                </div>

                {/* Seção 3: Ação Sugerida */}
                <div className="detail-card action-suger">
                  <h3>Ação Recomendada (Especialista)</h3>
                  <div className="action-box">
                    {selectedSuggestion.txAcaoInicialSuger}
                  </div>
                </div>
              </div>

              <div className="detail-column-side">
                <div className="detail-card meta-info">
                  <h3>Contexto do Cluster</h3>
                  <div className="meta-row">
                    <span className="label">Volume</span>
                    <span className="value">{selectedSuggestion.qtAlertasCluster} ocorrências</span>
                  </div>
                  <div className="meta-row">
                    <span className="label">Contrato</span>
                    <span className="value">{selectedSuggestion.idContrato}</span>
                  </div>
                  <div className="meta-row">
                    <span className="label">Identificado em</span>
                    <span className="value">{new Date(selectedSuggestion.dtSugestao).toLocaleString()}</span>
                  </div>
                </div>

                <div className="detail-card observation-box">
                  <h3>Observações de Aprovação</h3>
                  <textarea 
                    placeholder="Adicione um comentário (opcional)..."
                    value={observation}
                    onChange={(e) => setObservation(e.target.value)}
                  />
                  <p className="text-[10px] text-gray-500 mt-2">
                    Ao aprovar, esta regra será integrada ao motor de decisão e passará a processar novos alertas automaticamente.
                  </p>
                </div>
              </div>
            </div>
          </div>
        ) : (
          <div className="empty-state">
            <div className="empty-icon">
              <ShieldCheck size={64} />
            </div>
            <h2>Sala de Decisão Predict</h2>
            <p>Selecione uma sugestão na fila lateral para revisar e autorizar.</p>
          </div>
        )}
      </div>
    </div>
  );
}
