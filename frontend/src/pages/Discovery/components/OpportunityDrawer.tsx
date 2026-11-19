import React, { useState, useEffect } from 'react';
import { X, Save, ArrowRight, Bot, Zap } from 'lucide-react';
import { PredictDiscoveryOpportunity, PredictDiscoveryProcess } from '../../../types/opportunity';

interface DrawerProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (opp: Partial<PredictDiscoveryOpportunity>) => void;
  mode: 'create' | 'edit' | 'view';
  opportunity: Partial<PredictDiscoveryOpportunity> | null;
  processes: PredictDiscoveryProcess[];
}

export default function OpportunityDrawer({ isOpen, onClose, onSave, mode, opportunity, processes }: DrawerProps) {
  const [formData, setFormData] = useState<Partial<PredictDiscoveryOpportunity>>({});

  useEffect(() => {
    if (opportunity && (mode === 'edit' || mode === 'view')) {
      setFormData(opportunity);
    } else {
      // Defaults for create
      setFormData({
        status: 'Draft',
        isActive: true,
      });
    }
  }, [opportunity, mode]);

  const handleChange = (field: keyof PredictDiscoveryOpportunity, value: any) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  const readOnly = mode === 'view';

  return (
    <div className={`opp-drawer-overlay ${isOpen ? 'open' : ''}`} onClick={onClose}>
      <div className="opp-drawer" onClick={e => e.stopPropagation()}>
        <div className="opp-drawer-header">
          <h3>
            {mode === 'create' ? 'Nova Oportunidade' : mode === 'edit' ? 'Editar Oportunidade' : 'Detalhe da Oportunidade'}
          </h3>
          <button className="btn-close-drawer" onClick={onClose}><X size={18} /></button>
        </div>

        <div className="opp-drawer-body">
          <form id="opp-form" onSubmit={handleSave}>
            {/* BLOCO 1: Identificação */}
            <div className="form-section">
              <h4>1. Identificação</h4>
              <div className="form-grid">
                <div className="form-group">
                  <label className="required">Cliente</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    required 
                    disabled={readOnly || mode === 'edit'}
                    value={formData.clientCode || ''}
                    onChange={e => handleChange('clientCode', e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label className="required">Código</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    required 
                    disabled={readOnly || mode === 'edit'}
                    value={formData.opportunityCode || ''}
                    onChange={e => handleChange('opportunityCode', e.target.value)}
                  />
                </div>
                <div className="form-group full">
                  <label className="required">Título</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    required 
                    disabled={readOnly}
                    value={formData.title || ''}
                    onChange={e => handleChange('title', e.target.value)}
                  />
                </div>

                {/* SUGESTÃO DE AGENTE (INTEGRADA) */}
                {opportunity?.catalogId && (
                  <div className="form-group full">
                    <div className="catalog-suggestion-integrated">
                       <div className="suggestion-badge">
                          <Bot size={14} /> Sugestão de Agente Autômato
                       </div>
                       <div className="suggestion-body">
                          <div className="suggestion-title">{opportunity.catalogActivity}</div>
                          <div className="suggestion-action">
                             <code>{opportunity.catalogAgentAction}</code>
                          </div>
                          <div className="suggestion-footer">
                             <span>Confiança: <strong>{Math.round((opportunity.catalogScore || 0) * 100)}%</strong></span>
                             <span className={`classification ${opportunity.catalogClassification === 'Objetiva' ? 'success' : 'warning'}`}>
                                {opportunity.catalogClassification}
                             </span>
                          </div>
                       </div>
                    </div>
                  </div>
                )}
                <div className="form-group">
                  <label>Processo Relacionado</label>
                  <select 
                    className="form-control" 
                    disabled={readOnly}
                    value={formData.processId || ''}
                    onChange={e => handleChange('processId', e.target.value ? parseInt(e.target.value) : undefined)}
                  >
                    <option value="">Nenhum processo</option>
                    {processes.map(p => (
                      <option key={p.id} value={p.id}>{p.name}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label>Categoria</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    disabled={readOnly}
                    value={formData.opportunityCategory || ''}
                    onChange={e => handleChange('opportunityCategory', e.target.value)}
                  />
                </div>
                <div className="form-group">
                  <label>Score (Radar)</label>
                  <input type="number" className="form-control" disabled={readOnly} value={formData.priorityScore || ''} onChange={e => handleChange('priorityScore', parseFloat(e.target.value))} />
                </div>
                <div className="form-group">
                  <label>Ranking Radar</label>
                  <input type="number" className="form-control" disabled={readOnly} value={formData.priorityRank || ''} onChange={e => handleChange('priorityRank', parseInt(e.target.value))} />
                </div>
              </div>
            </div>

            {/* BLOCO 2: Contexto */}
            <div className="form-section">
              <h4>2. Contexto Operacional</h4>
              <div className="form-grid full">
                <div className="form-group">
                  <label>Resumo Executivo</label>
                  <textarea 
                    className="form-control" 
                    disabled={readOnly}
                    value={formData.summary || ''}
                    onChange={e => handleChange('summary', e.target.value)}
                  />
                </div>
              </div>
              <div className="form-grid" style={{ marginTop: '1.25rem' }}>
                <div className="form-group">
                  <label>Solicitante</label>
                  <input type="text" className="form-control" disabled={readOnly} value={formData.requestedBy || ''} onChange={e => handleChange('requestedBy', e.target.value)} />
                </div>
                <div className="form-group">
                  <label>Responsável</label>
                  <input type="text" className="form-control" disabled={readOnly} value={formData.ownerName || ''} onChange={e => handleChange('ownerName', e.target.value)} />
                </div>
              </div>
            </div>

            {/* BLOCO 3: Classificação */}
            <div className="form-section">
              <h4>3. Priorização</h4>
              <div className="form-grid">
                <div className="form-group">
                  <label>Ganho Esperado</label>
                  <select className="form-control" disabled={readOnly} value={formData.expectedGainClassification || ''} onChange={e => handleChange('expectedGainClassification', e.target.value)}>
                    <option value="">Selecione...</option>
                    <option value="Alto">Alto</option>
                    <option value="Médio">Médio</option>
                    <option value="Baixo">Baixo</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Esforço de Implantação</label>
                  <select className="form-control" disabled={readOnly} value={formData.implementationEffortClassification || ''} onChange={e => handleChange('implementationEffortClassification', e.target.value)}>
                    <option value="">Selecione...</option>
                    <option value="Alto">Alto</option>
                    <option value="Médio">Médio</option>
                    <option value="Baixo">Baixo</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Risco Operacional</label>
                  <select className="form-control" disabled={readOnly} value={formData.operationalRiskClassification || ''} onChange={e => handleChange('operationalRiskClassification', e.target.value)}>
                    <option value="">Selecione...</option>
                    <option value="Alto">Alto</option>
                    <option value="Médio">Médio</option>
                    <option value="Baixo">Baixo</option>
                  </select>
                </div>
              </div>
            </div>

            {/* BLOCO 4: Volume e Controle */}
            <div className="form-section">
              <h4>4. Volumetria & Controle</h4>
              <div className="form-grid">
                <div className="form-group">
                  <label>Status</label>
                  <select className="form-control" disabled={readOnly} value={formData.status || 'Draft'} onChange={e => handleChange('status', e.target.value)}>
                    <option value="Draft">Draft</option>
                    <option value="Em Análise">Em Análise</option>
                    <option value="Aprovado">Aprovado</option>
                    <option value="Implementação">Implementação</option>
                    <option value="Descartado">Descartado</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Motivo de Descarte</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    disabled={readOnly || formData.status !== 'Descartado'} 
                    value={formData.discardReason || ''} 
                    onChange={e => handleChange('discardReason', e.target.value)} 
                    placeholder={formData.status === 'Descartado' ? "Obrigatório" : ""}
                    required={formData.status === 'Descartado'}
                  />
                </div>
              </div>
            </div>
          </form>
        </div>



        <div className="opp-drawer-footer">
          <button type="button" className="btn-opp-action secondary" onClick={onClose}>
            {readOnly ? 'Fechar' : 'Cancelar'}
          </button>
          {!readOnly && (
            <button type="submit" form="opp-form" className="btn-opp-action primary">
              <Save size={16} /> Salvar Oportunidade
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
