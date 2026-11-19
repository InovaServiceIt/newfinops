import React, { useState, useEffect, useMemo } from 'react';
import { Radio, Filter, RefreshCw } from 'lucide-react';
import { ActionPlan, ActionPlanWithNarrative, ROTULO_ACAO_MAP } from '../../types/actionPlan';
import { actionPlansService, ActionPlanFilters } from '../../services/actionPlansService';
import ActionPlanCard from './components/ActionPlanCard';
import ActionPlanDetailDrawer from './components/ActionPlanDetailDrawer';
import './DiscoveryPanelPage.css';

const PRIORIDADES = ['P2', 'P3', 'P4'];
const STATUS_OPTIONS = [
  { value: '', label: 'Todos' },
  { value: 'sem_chamado', label: 'Sem chamado' },
  { value: 'com_chamado', label: 'Com chamado' },
];

export default function DiscoveryPanelPage() {
  const [plans, setPlans] = useState<ActionPlanWithNarrative[]>([]);
  const [total, setTotal] = useState(0);
  const [clientes, setClientes] = useState<string[]>([]);
  const [categorias, setCategorias] = useState<string[]>([]);
  const [dtUltimoRegistro, setDtUltimoRegistro] = useState<string | undefined>();
  const [loading, setLoading] = useState(true);
  const [selectedItem, setSelectedItem] = useState<ActionPlanWithNarrative | null>(null);

  const [filters, setFilters] = useState<ActionPlanFilters>({});

  const loadData = async (f: ActionPlanFilters = {}) => {
    setLoading(true);
    try {
      const result = await actionPlansService.getAll(f);
      setPlans(result.data || []);
      setTotal(result.total);
      setClientes(result.clientes || []);
      setCategorias(result.categorias || []);
      setDtUltimoRegistro(result.dtUltimoRegistro);
    } catch (err) {
      console.error('Erro ao carregar painel:', err);
      setPlans([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData(filters);
  }, []);

  const handleFilterChange = (key: keyof ActionPlanFilters, value: string) => {
    const newFilters = { ...filters, [key]: value || undefined };
    setFilters(newFilters);
    loadData(newFilters);
  };

  const maxScore = useMemo(() => {
    if (!plans.length) return 1;
    return Math.max(...plans.map(p => p.plan.scoreHostname || 0));
  }, [plans]);

  const formatDate = (d?: string) => {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('pt-BR');
  };

  return (
    <div className="panel-page">

      {/* TOP BADGE */}
      <div className="panel-top-badge">
        <Radio size={16} />
        <span>Gerado pelo Predict Radar</span>
        {dtUltimoRegistro && (
          <span className="badge-date">Atualizado em {formatDate(dtUltimoRegistro)}</span>
        )}
      </div>

      {/* HEADER */}
      <div className="panel-header">
        <div>
          <span className="panel-super-title">PREDICT DISCOVERY</span>
          <h1 className="panel-title">Painel de Oportunidades</h1>
          <p className="panel-subtitle">
            {loading ? 'Carregando...' : (
              <><strong>{total.toLocaleString('pt-BR')}</strong> oportunidades identificadas pelo modelo preditivo</>
            )}
          </p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <button className="btn-refresh" onClick={() => loadData(filters)} disabled={loading}>
            <RefreshCw size={16} className={loading ? 'spinning' : ''} />
            Atualizar
          </button>
        </div>
      </div>

      {/* FILTROS */}
      <div className="panel-filters">
        <div className="filter-icon-label">
          <Filter size={16} /> Filtros
        </div>

        <select
          className="filter-select"
          value={filters.nomeEntidade || ''}
          onChange={e => handleFilterChange('nomeEntidade', e.target.value)}
        >
          <option value="">Todos os clientes</option>
          {clientes.map(c => <option key={c} value={c}>{c}</option>)}
        </select>

        <select
          className="filter-select"
          value={filters.prioridade || ''}
          onChange={e => handleFilterChange('prioridade', e.target.value)}
        >
          <option value="">Todas as prioridades</option>
          {PRIORIDADES.map(p => <option key={p} value={p}>{p}</option>)}
        </select>

        <select
          className="filter-select"
          value={filters.rotuloAcao || ''}
          onChange={e => handleFilterChange('rotuloAcao', e.target.value)}
        >
          <option value="">Todos os tipos</option>
          {Object.entries(ROTULO_ACAO_MAP).map(([key, val]) => (
            <option key={key} value={key}>{val.label}</option>
          ))}
        </select>

        <select
          className="filter-select"
          value={filters.cdCategoria || ''}
          onChange={e => handleFilterChange('cdCategoria', e.target.value)}
        >
          <option value="">Todas as categorias</option>
          {categorias.map(c => <option key={c} value={c}>{c}</option>)}
        </select>

        <select
          className="filter-select"
          value={filters.statusChamado || ''}
          onChange={e => handleFilterChange('statusChamado', e.target.value)}
        >
          {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>

      {/* GRID */}
      {loading ? (
        <div className="panel-loading">
          <div className="loading-spinner" />
          <span>Consultando o modelo preditivo...</span>
        </div>
      ) : plans.length === 0 ? (
        <div className="panel-empty">
          <Radio size={48} strokeWidth={1} />
          <h3>Nenhuma oportunidade encontrada</h3>
          <p>Tente remover os filtros aplicados</p>
        </div>
      ) : (
        <div className="plans-grid">
          {plans.map(item => (
            <ActionPlanCard
              key={item.plan.idPlano}
              plan={item.plan}
              narrative={item.narrative}
              maxScore={maxScore}
              onViewDetail={() => setSelectedItem(item)}
            />
          ))}
        </div>
      )}

      {/* DRAWER DE DETALHE */}
      <ActionPlanDetailDrawer
        item={selectedItem}
        onClose={() => setSelectedItem(null)}
        onRegenerate={(newNarrative) => {
          if (selectedItem) {
            setPlans(prev => prev.map(p => 
              p.plan.idPlano === selectedItem.plan.idPlano 
                ? { ...p, narrative: newNarrative } 
                : p
            ));
            setSelectedItem({ ...selectedItem, narrative: newNarrative });
          }
        }}
      />
    </div>
  );
}
