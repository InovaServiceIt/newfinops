import React, { useState, useEffect } from 'react';
import { Target, PlusCircle, Download, Zap, TrendingUp } from 'lucide-react';
import './components/Opportunities.css';
import { opportunityService } from '../../services/opportunityService';
import { PredictDiscoveryOpportunity, PredictDiscoveryProcess } from '../../types/opportunity';
import OpportunitiesGrid from './components/OpportunitiesGrid';
import OpportunitiesFilter from './components/OpportunitiesFilter';
import OpportunityDrawer from './components/OpportunityDrawer';

export default function OpportunitiesPage() {
  const [data, setData] = useState<PredictDiscoveryOpportunity[]>([]);
  const [processes, setProcesses] = useState<PredictDiscoveryProcess[]>([]);
  const [clients, setClients] = useState<{code: string, name: string}[]>([]);
  const [loading, setLoading] = useState(true);

  // Drawer state
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [drawerMode, setDrawerMode] = useState<'create' | 'edit' | 'view'>('create');
  const [selectedOpp, setSelectedOpp] = useState<Partial<PredictDiscoveryOpportunity> | null>(null);

  useEffect(() => {
    loadData();
    loadProcesses();
  }, []);

  const loadData = async (filters?: any) => {
    setLoading(true);
    try {
      const opps = await opportunityService.getAll(filters);
      setData(opps);
      
      // Extract unique clients with names
      const clientMap = new Map<string, string>();
      opps.forEach(o => {
        if (o.clientCode && !clientMap.has(o.clientCode)) {
          clientMap.set(o.clientCode, o.clientName || o.clientCode);
        }
      });
      
      if (clients.length === 0) {
        const uniqueClients = Array.from(clientMap.entries()).map(([code, name]) => ({ code, name }));
        setClients(uniqueClients);
      }
    } catch (error) {
      console.error('Error loading opportunities', error);
    } finally {
      setLoading(false);
    }
  };

  const loadProcesses = async () => {
    try {
      const procs = await opportunityService.getProcesses();
      setProcesses(procs);
    } catch (error) {
      console.error('Error loading processes', error);
    }
  };

  const handleCreate = () => {
    setSelectedOpp(null);
    setDrawerMode('create');
    setDrawerOpen(true);
  };

  const handleEdit = (opp: PredictDiscoveryOpportunity) => {
    setSelectedOpp(opp);
    setDrawerMode('edit');
    setDrawerOpen(true);
  };

  const handleView = (opp: PredictDiscoveryOpportunity) => {
    setSelectedOpp(opp);
    setDrawerMode('view');
    setDrawerOpen(true);
  };

  const handleInactivate = async (opp: PredictDiscoveryOpportunity) => {
    if (window.confirm(`Deseja realmente inativar a oportunidade ${opp.opportunityCode}?`)) {
      try {
        await opportunityService.inactivate(opp.id);
        loadData();
      } catch (e) {
        alert('Erro ao inativar oportunidade');
      }
    }
  };

  const handleSaveDrawer = async (opp: Partial<PredictDiscoveryOpportunity>) => {
    try {
      if (drawerMode === 'create') {
        await opportunityService.create(opp as any);
      } else if (drawerMode === 'edit' && opp.id) {
        await opportunityService.update(opp.id, opp as any);
      }
      setDrawerOpen(false);
      loadData();
    } catch (e) {
      alert('Erro ao salvar oportunidade. Verifique os dados ou contate o suporte.');
    }
  };

  const handleExport = () => {
    alert("ExportaÃ§Ã£o para CSV em desenvolvimento.");
  };

  return (
    <div className="opportunities-container" style={{ padding: '0 2rem 2rem' }}>
      {/* MODULE CONTEXT HEADER */}
      {/* HEADER SPACE */}
      <div style={{ paddingTop: '1.5rem' }}></div>

      <div className="opportunities-main-card">
        <div className="opportunities-header">
          <div className="opportunities-title-box">
            <div className="opportunities-icon-wrapper">
              <Target size={20} />
            </div>
            <div className="opportunities-title-content">
              <small>Predict Discovery</small>
              <h2>Cadastro de oportunidades</h2>
            </div>
          </div>
          
          <div className="opportunities-actions">
            <button className="btn-opp-action primary" onClick={handleCreate}>
              <PlusCircle size={16} /> Nova oportunidade
            </button>
            <button className="btn-opp-action secondary" onClick={() => loadData()}>
              Atualizar
            </button>
            <button className="btn-opp-action secondary" onClick={handleExport}>
              <Download size={16} /> Exportar
            </button>
          </div>
        </div>

        <OpportunitiesFilter onFilter={loadData} clients={clients} />

        {loading ? (
          <div style={{ textAlign: 'center', padding: '3rem', color: '#6c757d' }}>
            Carregando oportunidades...
          </div>
        ) : (
          <OpportunitiesGrid 
            data={data} 
            onView={handleView}
            onEdit={handleEdit}
            onInactivate={handleInactivate}
          />
        )}
      </div>

      <OpportunityDrawer 
        isOpen={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        mode={drawerMode}
        opportunity={selectedOpp}
        onSave={handleSaveDrawer}
        processes={processes}
      />
    </div>
  );
}
