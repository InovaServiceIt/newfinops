import React, { useState, useEffect } from 'react';
import { X, Save, CheckCircle, Info, TrendingUp } from 'lucide-react';
import { discoveryService } from '../../../services/discoveryService';
import { PredictDiscoveryOpportunity } from '../../../types/discovery';
import './OpportunityFormDrawer.css';

interface OpportunityFormDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  opportunity?: PredictDiscoveryOpportunity;
}

export default function OpportunityFormDrawer({ isOpen, onClose, onSuccess, opportunity }: OpportunityFormDrawerProps) {
  const [mounted, setMounted] = useState(false);
  const [formData, setFormData] = useState<Partial<PredictDiscoveryOpportunity>>({});

  useEffect(() => {
    if (isOpen) {
      setMounted(true);
      setFormData(opportunity || {
        clientCode: '',
        opportunityCode: '',
        title: '',
        status: 'Identificada',
        isActive: true,
        priorityScore: 0,
        priorityRank: 0,
        convertedToBuilder: false,
        expectedGainClassification: 'Médio',
        implementationEffortClassification: 'Médio',
        operationalRiskClassification: 'Baixo'
      });
    } else {
      setMounted(false);
    }
  }, [isOpen, opportunity]);

  const [saving, setSaving] = useState(false);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value, type } = e.target;
    const val = type === 'checkbox' ? (e.target as HTMLInputElement).checked : value;
    setFormData(prev => ({ ...prev, [name]: val }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (opportunity?.id) {
        await discoveryService.updateOpportunity(opportunity.id, formData);
      } else {
        await discoveryService.createOpportunity(formData);
      }
      onSuccess();
      onClose();
    } catch (error) {
      console.error('Erro ao salvar:', error);
      alert('Erro ao salvar oportunidade.');
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen && !mounted) return null;

  return (
    <div className={`drawer-overlay ${isOpen ? 'active' : ''}`} onClick={onClose}>
      <div className={`drawer-content ${isOpen ? 'active' : ''}`} onClick={e => e.stopPropagation()}>
        <div className="drawer-header">
          <div className="header-title-box">
             <h2>{opportunity ? 'Editar Oportunidade' : 'Nova Oportunidade'}</h2>
             <span className="drawer-subtitle">Discovery Operations</span>
          </div>
          <button className="close-btn" onClick={onClose}><X /></button>
        </div>

        <form onSubmit={handleSubmit} className="drawer-form">
          <div className="form-sections">
            {/* BLOCO 1: IDENTIFICAÇÃO */}
            <section className="form-section">
              <div className="section-title">
                <Info size={18} /> <h3>Identificação</h3>
              </div>
              <div className="form-grid">
                <div className="form-field full">
                  <label>Título da oportunidade</label>
                  <input type="text" name="title" value={formData.title} onChange={handleChange} placeholder="Ex: Automação de Reset de Senha" required />
                </div>
                <div className="form-field">
                  <label>Cliente</label>
                  <input type="text" name="clientCode" value={formData.clientCode} onChange={handleChange} placeholder="Código do Cliente" required />
                </div>
                <div className="form-field">
                  <label>Código Op.</label>
                  <input type="text" name="opportunityCode" value={formData.opportunityCode} onChange={handleChange} placeholder="Ex: OP-001" required />
                </div>
                <div className="form-field">
                  <label>Processo</label>
                   <select name="processId" value={formData.processId || ''} onChange={handleChange}>
                     <option value="">Selecione um processo...</option>
                   </select>
                </div>
                <div className="form-field">
                  <label>Categoria</label>
                  <input type="text" name="opportunityCategory" value={formData.opportunityCategory} onChange={handleChange} />
                </div>
              </div>
            </section>

            {/* BLOCO 2: CONTEXTO */}
            <section className="form-section">
              <div className="section-title">
                <Info size={18} /> <h3>Contexto</h3>
              </div>
              <div className="form-grid">
                <div className="form-field full">
                  <label>Resumo</label>
                  <textarea name="summary" value={formData.summary} onChange={handleChange} rows={3}></textarea>
                </div>
                <div className="form-field">
                  <label>Solicitado por</label>
                  <input type="text" name="requestedBy" value={formData.requestedBy} onChange={handleChange} />
                </div>
                <div className="form-field">
                  <label>Responsável</label>
                  <input type="text" name="ownerName" value={formData.ownerName} onChange={handleChange} />
                </div>
              </div>
            </section>

            {/* BLOCO 3: CLASSIFICAÇÃO */}
            <section className="form-section">
              <div className="section-title">
                <TrendingUp size={18} /> <h3>Classificação</h3>
              </div>
              <div className="form-grid">
                <div className="form-field">
                  <label>Ganho Esperado</label>
                   <select name="expectedGainClassification" value={formData.expectedGainClassification} onChange={handleChange}>
                     <option value="Baixo">Baixo</option>
                     <option value="Médio">Médio</option>
                     <option value="Alto">Alto</option>
                   </select>
                </div>
                <div className="form-field">
                  <label>Esforço</label>
                   <select name="implementationEffortClassification" value={formData.implementationEffortClassification} onChange={handleChange}>
                     <option value="Baixo">Baixo</option>
                     <option value="Médio">Médio</option>
                     <option value="Alto">Alto</option>
                   </select>
                </div>
                <div className="form-field">
                  <label>Score</label>
                  <input type="number" name="priorityScore" value={formData.priorityScore} onChange={handleChange} />
                </div>
              </div>
            </section>
          </div>

          <div className="drawer-footer">
            <button type="button" className="btn-cancel" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn-save" disabled={saving}>
              {saving ? 'Salvando...' : (
                <><Save size={18} /> Salvar Oportunidade</>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
