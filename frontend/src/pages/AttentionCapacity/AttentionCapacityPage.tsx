import React, { useEffect, useState } from 'react';
import { ShieldAlert, Info, Save, CheckCircle } from 'lucide-react';
import './AttentionCapacityPage.css';

interface Rule {
  id: number;
  level: string;
  priority: string;
  description: string;
  criteria: string;
  sortOrder: number;
  groupKey: string;
  maxSlots: number;
}

interface GroupedRule {
  groupKey: string;
  title: string;
  description: string;
  priorities: string[];
  maxSlots: number;
  badgeColors: { [key: string]: { bg: string; text: string } };
}

const AttentionCapacityPage: React.FC = () => {
  const [rules, setRules] = useState<Rule[]>([]);
  const [groupedRules, setGroupedRules] = useState<GroupedRule[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Mapeamento visual das prioridades
  const priorityMetadata: { [key: string]: { label: string; bg: string; text: string } } = {
    ACN: { label: 'Crítico (ACN)', bg: '#FADBD8', text: '#C0392B' },
    P1: { label: 'Alto (P1)', bg: '#FDEBD0', text: '#D35400' },
    P2: { label: 'Médio (P2)', bg: '#FCF3CF', text: '#D4AC0D' }
  };

  useEffect(() => {
    fetchRules();
  }, []);

  const fetchRules = async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/attention/rules');
      if (!response.ok) throw new Error('Falha ao carregar regras.');
      const data: Rule[] = await response.ok ? await response.json() : [];
      setRules(data);
      groupRules(data);
    } catch (error) {
      console.error(error);
      setMessage({ text: 'Erro ao conectar com o servidor para carregar regras.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const groupRules = (flatRules: Rule[]) => {
    // Agrupar regras por 'groupKey'
    const groups: { [key: string]: Rule[] } = {};
    flatRules.forEach(rule => {
      if (!groups[rule.groupKey]) {
        groups[rule.groupKey] = [];
      }
      groups[rule.groupKey].push(rule);
    });

    const grouped: GroupedRule[] = Object.keys(groups).map(key => {
      const items = groups[key];
      const priorities = items.map(i => i.priority);
      const maxSlots = items[0].maxSlots; // O limite é compartilhado pelo grupo

      // Gerar título e descrição amigável com base no conceito do grupo
      let title = priorities.map(p => priorityMetadata[p]?.label || p).join(' ou ');
      let description = items.length > 1 
        ? 'Slots de atendimento dedicados compartilhados' 
        : 'Slots de atendimento dedicados independentes';

      // Cores para os Badges
      const badgeColors: { [key: string]: { bg: string; text: string } } = {};
      priorities.forEach(p => {
        badgeColors[p] = priorityMetadata[p] || { bg: '#EAECEE', text: '#2C3E50' };
      });

      return {
        groupKey: key,
        title,
        description,
        priorities,
        maxSlots,
        badgeColors
      };
    });

    // Ordenar de acordo com a prioridade crítica
    grouped.sort((a, b) => {
      if (a.groupKey === 'ACN_P1') return -1;
      if (b.groupKey === 'ACN_P1') return 1;
      return 0;
    });

    setGroupedRules(grouped);
  };

  const handleSlotChange = (groupKey: string, val: number) => {
    const safeVal = Math.max(1, Math.min(10, isNaN(val) ? 1 : val));
    setGroupedRules(prev => prev.map(g => {
      if (g.groupKey === groupKey) {
        return { ...g, maxSlots: safeVal };
      }
      return g;
    }));
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      setMessage(null);

      // Desmembrar o estado agrupado de volta para o payload plano da API (UPSERT)
      const payload: { priority: string; maxSlots: number }[] = [];
      groupedRules.forEach(g => {
        g.priorities.forEach(p => {
          payload.push({
            priority: p,
            maxSlots: g.maxSlots
          });
        });
      });

      const response = await fetch('/api/attention/rules', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!response.ok) throw new Error('Erro ao salvar configurações.');

      setMessage({ text: 'Configurações de capacidade por grupo salvas com sucesso!', type: 'success' });
      
      // Limpar mensagem após 5 segundos
      setTimeout(() => setMessage(null), 5000);
    } catch (error) {
      console.error(error);
      setMessage({ text: 'Erro ao salvar as configurações de regras.', type: 'error' });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="rules-loading-container">
        <div className="spinner"></div>
        <p>Carregando regras de capacidade...</p>
      </div>
    );
  }

  return (
    <div className="capacity-rules-container">
      {/* Toast Notification */}
      {message && (
        <div className={`notification-toast ${message.type}`}>
          {message.type === 'success' ? <CheckCircle size={20} /> : <ShieldAlert size={20} />}
          <span>{message.text}</span>
        </div>
      )}

      {/* Header */}
      <div className="capacity-rules-header">
        <div className="header-info">
          <h1 className="main-title">Capacidade de Atendimento (Regras)</h1>
          <p className="subtitle">
            Defina a quantidade máxima de chamados simultâneos que um analista pode atender baseado na estrutura de grupos.
          </p>
        </div>
        <button 
          className={`btn-save-config ${saving ? 'loading' : ''}`} 
          onClick={handleSave}
          disabled={saving}
        >
          <Save size={18} />
          {saving ? 'Salvando...' : 'Salvar Configurações'}
        </button>
      </div>

      {/* Cards List */}
      <div className="rules-cards-list">
        {groupedRules.map(group => (
          <div key={group.groupKey} className="rules-card-item">
            {/* Left side: Priorities badge group */}
            <div className="card-priority-group">
              {group.priorities.map((p, idx) => (
                <React.Fragment key={p}>
                  {idx > 0 && <span className="priority-connector">ou</span>}
                  <div 
                    className="priority-badge"
                    style={{ 
                      backgroundColor: group.badgeColors[p].bg, 
                      color: group.badgeColors[p].text 
                    }}
                  >
                    <span className="badge-abbr">{p}</span>
                    <span className="badge-name">{priorityMetadata[p]?.label.split(' ')[0]}</span>
                  </div>
                </React.Fragment>
              ))}
              <div className="priority-description-block">
                <div className="priority-title-container">
                  <span className="priority-desc-title">{group.title}</span>
                  <span className="group-key-badge">Grupo: {group.groupKey}</span>
                </div>
                <span className="priority-desc-subtitle">{group.description}</span>
              </div>
            </div>

            {/* Right side: Max inputs */}
            <div className="card-slots-input-group">
              <span className="input-label">Máximo:</span>
              <div className="number-input-wrapper">
                <button 
                  className="btn-counter-adjust minus"
                  onClick={() => handleSlotChange(group.groupKey, group.maxSlots - 1)}
                >-</button>
                <input 
                  type="number" 
                  className="slots-number-input"
                  value={group.maxSlots}
                  onChange={(e) => handleSlotChange(group.groupKey, parseInt(e.target.value))}
                  min={1}
                  max={10}
                />
                <button 
                  className="btn-counter-adjust plus"
                  onClick={() => handleSlotChange(group.groupKey, group.maxSlots + 1)}
                >+</button>
              </div>
              <span className="input-unit">chamados</span>
            </div>
          </div>
        ))}
      </div>

      {/* Info Alert Box */}
      <div className="rules-info-alert-box">
        <div className="alert-icon-wrapper">
          <Info size={22} className="alert-info-icon" />
        </div>
        <div className="alert-text-content">
          <p className="alert-rule-title">Lógica de Slot Grupo Ativa:</p>
          <ul className="alert-rules-list">
            <li>
              <strong>ACN e P1</strong> compartilham a mesma chave de grupo (<code>ACN_P1</code>). Um analista alocado a 1 chamado ACN automaticamente preenche o slot P1 do mesmo grupo (dedicação exclusiva).
            </li>
            <li>
              Prioridades <strong>P2 em diante</strong> operam de forma independente sob chaves exclusivas de grupo, com slots independentes que não sobrecarregam a escala de crise.
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
};

export default AttentionCapacityPage;
