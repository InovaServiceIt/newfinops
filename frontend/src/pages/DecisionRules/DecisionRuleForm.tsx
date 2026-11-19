import React, { useState, useEffect } from 'react';
import { DecisionRule, DecisionStatus } from '../../types/decisionRules';
import { ToggleSwitch } from '../../components/forms/ToggleSwitch';
import { FormSection } from '../../components/forms/FormSection';
import { TooltipHelper } from '../../components/common/TooltipHelper';
import { Save } from 'lucide-react';

interface DecisionRuleFormProps {
  initialData?: DecisionRule;
  onSave: (rule: Omit<DecisionRule, 'id'>) => Promise<void>;
  onCancel: () => void;
  isReadOnly?: boolean;
}

export function DecisionRuleForm({ initialData, onSave, onCancel, isReadOnly = false }: DecisionRuleFormProps) {
  const [formData, setFormData] = useState<Partial<DecisionRule>>({
    is_active: true,
    open_incident: false,
    close_event: false,
    decision_status: 'ignored',
    priority_order: 10,
    ...initialData
  });
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Update effect if initialData changes
  useEffect(() => {
    if (initialData) {
      setFormData(initialData);
    }
  }, [initialData]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const handleToggle = (name: keyof DecisionRule) => (checked: boolean) => {
    setFormData(prev => ({ ...prev, [name]: checked }));
  };

  const handleNumChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value ? parseInt(value, 10) : undefined }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    
    // Basic validations
    if (!formData.rule_code || !formData.rule_name || !formData.decision_status || !formData.priority_order) {
      setError('Preencha todos os campos obrigatórios.');
      return;
    }
    
    if (formData.severity_min && formData.severity_max && formData.severity_max < formData.severity_min) {
      setError('A severidade máxima não pode ser menor que a mínima.');
      return;
    }

    try {
      setLoading(true);
      await onSave(formData as Omit<DecisionRule, 'id'>);
    } catch (err: any) {
      setError(err.message || 'Erro ao salvar a regra.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {error && (
        <div style={{ backgroundColor: 'var(--status-error)', color: 'white', padding: '1rem', borderRadius: '4px', marginBottom: '1rem' }}>
          {error}
        </div>
      )}
      
      <div style={{ flex: 1 }}>
        <FormSection title="1. Identificação">
          <div className="form-grid">
            <div className="form-group">
              <label>Código da Regra *</label>
              <input 
                type="text" 
                className="form-control" 
                name="rule_code" 
                value={formData.rule_code || ''} 
                onChange={handleChange} 
                disabled={isReadOnly || !!initialData?.id} 
                required 
              />
            </div>
            <div className="form-group">
              <label>Nome da Regra *</label>
              <input 
                type="text" 
                className="form-control" 
                name="rule_name" 
                value={formData.rule_name || ''} 
                onChange={handleChange} 
                disabled={isReadOnly} 
                required 
              />
            </div>
            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
              <label>Descrição</label>
              <textarea 
                className="form-control" 
                name="rule_description" 
                value={formData.rule_description || ''} 
                onChange={handleChange} 
                disabled={isReadOnly} 
              />
            </div>
          </div>
        </FormSection>

        <FormSection title="2. Escopo do Evento">
          <div className="form-grid">
            <div className="form-group">
              <label>Origem <TooltipHelper content="Vazio = Qualquer Origem" /></label>
              <select className="form-control" name="event_origin" value={formData.event_origin || ''} onChange={handleChange} disabled={isReadOnly}>
                <option value="">(Qualquer)</option>
                <option value="Datadog">Datadog</option>
                <option value="Zabbix">Zabbix</option>
                <option value="SolarWinds">SolarWinds</option>
                <option value="GoogleChat">GoogleChat</option>
              </select>
            </div>
            <div className="form-group">
              <label>Categoria <TooltipHelper content="Vazio = Qualquer Categoria" /></label>
              <select className="form-control" name="event_category" value={formData.event_category || ''} onChange={handleChange} disabled={isReadOnly}>
                <option value="">(Qualquer)</option>
                <option value="infrastructure">Infrastructure</option>
                <option value="application">Application</option>
                <option value="network">Network</option>
              </select>
            </div>
            <div className="form-group">
              <label>Ambiente</label>
              <select className="form-control" name="environment" value={formData.environment || ''} onChange={handleChange} disabled={isReadOnly}>
                <option value="">(Qualquer)</option>
                <option value="prod">Produção</option>
                <option value="hml">Homologação</option>
                <option value="dev">Desenvolvimento</option>
              </select>
            </div>
          </div>
        </FormSection>

        <FormSection title="3. Critérios de Severidade e Volume">
          <div className="form-grid">
            <div className="form-group">
              <label>Severidade Mínima</label>
              <input type="number" min="1" max="5" className="form-control" name="severity_min" value={formData.severity_min || ''} onChange={handleNumChange} disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Severidade Máxima</label>
              <input type="number" min="1" max="5" className="form-control" name="severity_max" value={formData.severity_max || ''} onChange={handleNumChange} disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Contagem Mín. de Eventos <TooltipHelper content="Qtd mínima no intervalo para a regra disparar" /></label>
              <input type="number" min="1" className="form-control" name="event_count_min" value={formData.event_count_min || ''} onChange={handleNumChange} disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Janela (minutos) <TooltipHelper content="Janela de observação para a contagem" /></label>
              <input type="number" min="1" className="form-control" name="event_count_window_minutes" value={formData.event_count_window_minutes || ''} onChange={handleNumChange} disabled={isReadOnly} />
            </div>
          </div>
        </FormSection>

        <FormSection title="4. Critérios Textuais">
          <div className="form-grid full">
            <div className="form-group">
              <label>Texto Contém</label>
              <textarea className="form-control" name="text_contains" value={formData.text_contains || ''} onChange={handleChange} disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Texto NÃO Contém</label>
              <textarea className="form-control" name="text_not_contains" value={formData.text_not_contains || ''} onChange={handleChange} disabled={isReadOnly} />
            </div>
          </div>
        </FormSection>

        <FormSection title="5. Resultado da Decisão">
          <div className="form-grid">
            <div className="form-group">
              <label>Decision Status * <TooltipHelper content="O impacto determinado para o evento pelo motor" /></label>
              <select className="form-control" name="decision_status" value={formData.decision_status || ''} onChange={handleChange} required disabled={isReadOnly}>
                <option value="ignored">Ignored</option>
                <option value="monitored">Monitored</option>
                <option value="alert">Alert</option>
                <option value="incident_candidate">Incident Candidate</option>
                <option value="incident_opened">Incident Opened</option>
                <option value="closed">Closed</option>
              </select>
            </div>
            <div className="form-group">
              <label>Ordem de Prioridade (1 = mais alta) *</label>
              <input type="number" min="1" className="form-control" name="priority_order" value={formData.priority_order || ''} onChange={handleNumChange} required disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Abre Incidente? <TooltipHelper content="Força a abertura de incidente no ITSM" /></label>
              <ToggleSwitch checked={!!formData.open_incident} onChange={handleToggle('open_incident')} disabled={isReadOnly} />
            </div>
            <div className="form-group">
              <label>Fecha Evento?</label>
              <ToggleSwitch checked={!!formData.close_event} onChange={handleToggle('close_event')} disabled={isReadOnly} />
            </div>
          </div>
        </FormSection>

        <FormSection title="6. Situação">
          <div className="form-grid">
            <div className="form-group">
              <label>Regra Ativa?</label>
              <ToggleSwitch checked={!!formData.is_active} onChange={handleToggle('is_active')} disabled={isReadOnly} />
            </div>
            <div className="form-group" style={{ gridColumn: '1 / -1' }}>
              <label>Notas adicionais</label>
              <textarea className="form-control" name="notes" value={formData.notes || ''} onChange={handleChange} disabled={isReadOnly} />
            </div>
          </div>
        </FormSection>
      </div>
      
      {/* Footer is injected by SideFormDrawer, but we need buttons to submit */}
      {!isReadOnly && (
        <div style={{ marginTop: '2rem', display: 'flex', gap: '1rem', justifyContent: 'flex-end', paddingBottom: '2rem' }}>
           <button type="button" className="btn-secondary" onClick={onCancel} disabled={loading}>Cancelar</button>
           <button type="submit" className="btn-primary" disabled={loading}>
             <Save size={16} /> {loading ? 'Salvando...' : 'Salvar Regra'}
           </button>
        </div>
      )}
    </form>
  );
}
