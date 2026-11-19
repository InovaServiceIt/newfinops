import React, { useState, useEffect } from 'react';
import { PageHeader } from '../../components/common/PageHeader';
import { FilterCard } from '../../components/forms/FilterCard';
import { DataGrid, Column } from '../../components/common/DataGrid';
import { BadgeStatus } from '../../components/common/BadgeStatus';
import { SideFormDrawer } from '../../components/layout/SideFormDrawer';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { DecisionRuleForm } from './DecisionRuleForm';
import { decisionRulesService } from '../../services/decisionRulesService';
import { DecisionRule } from '../../types/decisionRules';
import { Plus, Pencil, Eye, Power, PowerOff } from 'lucide-react';

export default function DecisionRulesPage() {
  const [rules, setRules] = useState<DecisionRule[]>([]);
  const [filteredRules, setFilteredRules] = useState<DecisionRule[]>([]);
  const [loading, setLoading] = useState(true);
  
  // Drawer State
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<'create' | 'edit' | 'view'>('create');
  const [activeRule, setActiveRule] = useState<DecisionRule | null>(null);
  
  // Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [ruleToToggle, setRuleToToggle] = useState<DecisionRule | null>(null);

  // Filters State
  const [searchCode, setSearchCode] = useState('');
  const [searchName, setSearchName] = useState('');

  useEffect(() => {
    loadRules();
  }, []);

  const loadRules = async () => {
    setLoading(true);
    try {
      const data = await decisionRulesService.getRules();
      setRules(data);
      setFilteredRules(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    const filtered = rules.filter(r => 
      (!searchCode || r.rule_code.toLowerCase().includes(searchCode.toLowerCase())) &&
      (!searchName || r.rule_name.toLowerCase().includes(searchName.toLowerCase()))
    );
    setFilteredRules(filtered);
  };

  const handleClearForm = () => {
    setSearchCode('');
    setSearchName('');
    setFilteredRules(rules);
  };

  const handleCreate = () => {
    setActiveRule(null);
    setDrawerMode('create');
    setDrawerOpen(true);
  };

  const handleEdit = (rule: DecisionRule) => {
    setActiveRule(rule);
    setDrawerMode('edit');
    setDrawerOpen(true);
  };

  const handleView = (rule: DecisionRule) => {
    setActiveRule(rule);
    setDrawerMode('view');
    setDrawerOpen(true);
  };

  const requestToggleStatus = (rule: DecisionRule) => {
    setRuleToToggle(rule);
    setDialogOpen(true);
  };

  const confirmToggleStatus = async () => {
    if (!ruleToToggle || !ruleToToggle.id) return;
    try {
      await decisionRulesService.toggleActiveStatus(ruleToToggle.id, !ruleToToggle.is_active);
      await loadRules();
    } catch(err) {
      console.error(err);
    } finally {
      setDialogOpen(false);
      setRuleToToggle(null);
    }
  };

  const handleSaveForm = async (data: Omit<DecisionRule, 'id'>) => {
    if (drawerMode === 'create') {
      await decisionRulesService.createRule(data);
    } else if (drawerMode === 'edit' && activeRule?.id) {
      await decisionRulesService.updateRule(activeRule.id, data);
    }
    setDrawerOpen(false);
    loadRules();
  };

  const getDrawerTitle = () => {
    if (drawerMode === 'create') return 'Nova Regra de Decisão';
    if (drawerMode === 'edit') return `Editar Regra: ${activeRule?.rule_code}`;
    return `Visualizar Regra: ${activeRule?.rule_code}`;
  };

  const columns: Column<DecisionRule>[] = [
    { header: 'Prioridade', accessor: 'priority_order', width: '10%' },
    { header: 'Código', accessor: 'rule_code', width: '20%' },
    { header: 'Nome', accessor: 'rule_name', width: '20%' },
    { header: 'Status', accessor: (row) => <BadgeStatus status={row.decision_status} />, width: '15%' },
    { 
      header: 'Incidente?', 
      accessor: (row) => row.open_incident ? <span style={{color: 'var(--status-error)', fontWeight: 'bold'}}>Sim</span> : 'Não',
      width: '10%' 
    },
    { 
      header: 'Ativa', 
      accessor: (row) => row.is_active ? 
        <span style={{color: 'var(--status-success)'}}>Sim</span> : 
        <span style={{color: 'var(--text-muted)'}}>Não</span>,
      width: '10%' 
    }
  ];

  const actions = (row: DecisionRule) => (
    <>
      <button className="btn-secondary" onClick={() => handleView(row)} title="Visualizar" style={{padding: '0.4rem'}}>
        <Eye size={16} />
      </button>
      <button className="btn-secondary" onClick={() => handleEdit(row)} title="Editar" style={{padding: '0.4rem'}}>
        <Pencil size={16} />
      </button>
      <button 
        className="btn-secondary" 
        onClick={() => requestToggleStatus(row)} 
        title={row.is_active ? "Inativar" : "Ativar"}
        style={{padding: '0.4rem', color: row.is_active ? 'var(--status-error)' : 'var(--status-success)'}}
      >
        {row.is_active ? <PowerOff size={16} /> : <Power size={16} />}
      </button>
    </>
  );

  return (
    <div>
      <PageHeader 
        title="Decision Rules" 
        subtitle="Cadastro e manutenção das regras que orientam o motor de decisão do Predict."
        action={<button className="btn-primary" onClick={handleCreate}><Plus size={20} /> Nova Regra</button>}
      />

      <FilterCard onSearch={handleSearch} onClear={handleClearForm}>
        <div className="filter-field">
          <label>Código da regra</label>
          <input 
            type="text" 
            className="filter-input" 
            placeholder="Ex: R1_IGNORE..."
            value={searchCode}
            onChange={(e) => setSearchCode(e.target.value)}
          />
        </div>
        <div className="filter-field">
          <label>Nome da regra</label>
          <input 
            type="text" 
            className="filter-input" 
            placeholder="Ex: Ignorar eventos"
            value={searchName}
            onChange={(e) => setSearchName(e.target.value)}
          />
        </div>
      </FilterCard>

      {loading ? (
        <div style={{ textAlign: 'center', padding: '3rem' }}>Carregando regras...</div>
      ) : (
        <DataGrid 
          data={filteredRules} 
          columns={columns} 
          keyExtractor={(row) => row.id!} 
          actions={actions}
        />
      )}

      <SideFormDrawer 
        isOpen={drawerOpen} 
        onClose={() => setDrawerOpen(false)} 
        title={getDrawerTitle()}
      >
        <DecisionRuleForm 
          initialData={activeRule || undefined}
          onSave={handleSaveForm}
          onCancel={() => setDrawerOpen(false)}
          isReadOnly={drawerMode === 'view'}
        />
      </SideFormDrawer>

      <ConfirmDialog 
        isOpen={dialogOpen}
        title={ruleToToggle?.is_active ? "Inativar Regra" : "Ativar Regra"}
        message={`Deseja realmente ${ruleToToggle?.is_active ? 'inativar' : 'ativar'} a regra "${ruleToToggle?.rule_code}"?`}
        confirmLabel="Sim, continuar"
        onConfirm={confirmToggleStatus}
        onCancel={() => setDialogOpen(false)}
        isDestructive={ruleToToggle?.is_active}
      />
    </div>
  );
}
