import React, { useState, useEffect } from 'react';
import { 
  BarChart2, GripVertical, Edit3, CheckCircle, 
  AlertCircle, ChevronRight, Info, Search 
} from 'lucide-react';
import axios from 'axios';
import { MatrixOpportunity, BulkRankRequest, BulkRankUpdateItem } from '../../types/discovery';
import './MatrixPage.css';
import OpportunityDrawer from './components/OpportunityDrawer';
import { opportunityService } from '../../services/opportunityService';

const API_BASE = '/api'; // Use relative path for production/Coolify

const getStatusColor = (score: number) => {
  if (score >= 500) return '#e24b4a'; // Risco Alto (Vermelho)
  if (score >= 100) return '#f6ad55'; // Risco Médio (Laranja)
  return '#38b2ac'; // Risco Baixo (Verde)
};

export default function MatrixPage() {
  const [opportunities, setOpportunities] = useState<MatrixOpportunity[]>([]);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showScoreModal, setShowScoreModal] = useState(false);
  const [overrideScore, setOverrideScore] = useState<number>(0);
  const [justification, setJustification] = useState('');
  const [clientCode, setClientCode] = useState('SERVICE_IT_01'); 
  const [availableClients, setAvailableClients] = useState<{code: string, name: string}[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selectedOpp, setSelectedOpp] = useState<any>(null);
  const [processes, setProcesses] = useState<any[]>([]);

  useEffect(() => {
    fetchMatrixData();
  }, [clientCode]);

  useEffect(() => {
    fetchClients();
    fetchProcesses();
  }, []);

  const fetchProcesses = async () => {
    try {
      const procs = await opportunityService.getProcesses();
      setProcesses(procs);
    } catch (e) {
      console.error("Error fetching processes", e);
    }
  };

  const fetchClients = async () => {
    try {
      const response = await axios.get(`${API_BASE}/opportunities`);
      const allOpps = response.data;
      
      const clientMap = new Map<string, string>();
      allOpps.forEach((o: any) => {
        if (o.clientCode && !clientMap.has(o.clientCode)) {
          clientMap.set(o.clientCode, o.clientName || o.clientCode);
        }
      });

      const uniqueClients = Array.from(clientMap.entries())
        .map(([code, name]) => ({ code, name }))
        .sort((a, b) => a.name.localeCompare(b.name));
      
      if (uniqueClients.length > 0) {
        setAvailableClients(uniqueClients);
        // Se o atual não estiver na lista, pega o primeiro da lista real
        if (!uniqueClients.some(c => c.code === clientCode)) {
           setClientCode(uniqueClients[0].code);
        }
      }
    } catch (err) {
      console.error('Failed to fetch clients', err);
    }
  };

  const fetchMatrixData = async () => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_BASE}/opportunities/matrix?clientCode=${clientCode}`);
      // Initialize temp values
      const data = response.data.map((o: MatrixOpportunity) => ({
        ...o,
        tempScore: o.priorityScore,
        tempRank: o.priorityRank
      }));
      setOpportunities(data);
    } catch (err: any) {
      setError('Falha ao carregar matriz de priorização.');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const toggleSelection = (id: number) => {
    setSelectedIds(prev => 
      prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
    );
  };

  const handleBubbleDragStart = (e: React.DragEvent, id: number) => {
    e.dataTransfer.setData('bubbleId', id.toString());
  };

  const handleQuadrantDrop = (e: React.DragEvent) => {
    const bubbleId = parseInt(e.dataTransfer.getData('bubbleId'));
    if (isNaN(bubbleId)) return;

    const container = e.currentTarget.getBoundingClientRect();
    const x = ((e.clientX - container.left) / container.width) * 100;
    const y = 100 - (((e.clientY - container.top) / container.height) * 100);

    const getClassification = (val: number) => {
      if (val >= 66) return 'high';
      if (val >= 33) return 'medium';
      return 'low';
    };

    const newGain = getClassification(y);
    const newEffort = getClassification(x);

    setOpportunities(prev => prev.map(o => {
      if (o.id === bubbleId) {
        return {
          ...o,
          expectedGainClassification: newGain,
          implementationEffortClassification: newEffort,
          overridden: true
        };
      }
      return o;
    }));
  };

  const handleDragStart = (e: React.DragEvent, index: number) => {
    e.dataTransfer.setData('draggedIndex', index.toString());
  };

  const handleDrop = (e: React.DragEvent, dropIndex: number) => {
    const dragIndex = parseInt(e.dataTransfer.getData('draggedIndex'));
    if (dragIndex === dropIndex) return;

    const newItems = [...opportunities];
    const [removed] = newItems.splice(dragIndex, 1);
    newItems.splice(dropIndex, 0, removed);

    const updatedItems = newItems.map((item, index) => ({
      ...item,
      tempRank: index + 1
    }));

    setOpportunities(updatedItems);
  };

  const handleScoreOverride = () => {
    if (selectedIds.length === 0) return;
    
    const lastSelected = opportunities.find(o => o.id === selectedIds[selectedIds.length - 1]);
    if (lastSelected) {
      setOverrideScore(lastSelected.tempScore || lastSelected.priorityScore);
      setJustification('');
      setShowScoreModal(true);
    }
  };

  const saveScoreOverride = () => {
    if (!justification.trim()) {
      alert('Justificativa é obrigatória');
      return;
    }

    const updatedOpportunities = opportunities.map(o => {
      if (selectedIds.includes(o.id)) {
        return {
          ...o,
          tempScore: overrideScore,
          overridden: true,
          justification: justification
        };
      }
      return o;
    });

    // Re-sort by score
    const sorted = [...updatedOpportunities].sort((a, b) => (b.tempScore || 0) - (a.tempScore || 0));
    
    // Update ranks
    const final = sorted.map((item, index) => ({
      ...item,
      tempRank: index + 1
    }));

    setOpportunities(final);
    setShowScoreModal(false);
  };

  const confirmPrioritization = async () => {
    if (selectedIds.length === 0) return;

    const updates: BulkRankUpdateItem[] = opportunities.map(o => ({
      opportunityId: o.id,
      priorityRank: o.tempRank || o.priorityRank,
      priorityScore: o.tempScore || o.priorityScore,
      status: selectedIds.includes(o.id) ? 'prioritized' : 'new',
      scoreOverridden: o.overridden || false,
      oldScore: o.overridden ? o.priorityScore : undefined,
      overrideJustification: o.justification
    }));

    const request: BulkRankRequest = {
      clientCode,
      updates,
      contextPayload: JSON.stringify(opportunities)
    };

    try {
      await axios.patch(`${API_BASE}/opportunities/bulk-rank`, request);
      alert('Priorização confirmada com sucesso!');
      fetchMatrixData(); // Refresh list (prioritized items will disappear)
      setSelectedIds([]);
    } catch (err) {
      console.error(err);
      alert('Erro ao confirmar priorização.');
    }
  };

  // Coordinate mapping for quadrant
  const getCoordinates = (item: MatrixOpportunity) => {
    const mapY: Record<string, number> = { 
      'high': 80, 'alta': 80, 
      'medium': 55, 'media': 55, 'média': 55, 'medio': 55, 'médio': 55,
      'low': 25, 'baixa': 25 
    };
    const mapX: Record<string, number> = { 
      'high': 75, 'alta': 75, 
      'medium': 50, 'media': 50, 'média': 50, 'medio': 50, 'médio': 50,
      'low': 25, 'baixa': 25 
    };
    
    const yVal = item.expectedGainClassification?.toLowerCase() || 'low';
    const xVal = item.implementationEffortClassification?.toLowerCase() || 'low';
    
    let y = mapY[yVal] || 25;
    let x = mapX[xVal] || 25;
    
    // Add small random jitter based on ID to avoid perfect overlap
    const jitterX = (item.id * 13 % 20) - 10;
    const jitterY = (item.id * 7 % 20) - 10;
    
    return { x: x + jitterX, y: 100 - (y + jitterY) };
  };

  const handleItemClick = (opp: MatrixOpportunity) => {
    toggleSelection(opp.id);
    setSelectedOpp(opp);
    setDrawerOpen(true);
  };

  if (loading) return <div className="matrix-page"><div className="loading">Carregando...</div></div>;

  return (
    <div className="matrix-page animate-fade-in">
      <header className="matrix-header">
        <div className="opportunities-title-box">
          <div className="opportunities-icon-wrapper">
            <BarChart2 size={20} />
          </div>
          <div className="opportunities-title-content">
            <small>Predict Discovery</small>
            <h2>Matriz de Priorização</h2>
          </div>
        </div>
        <div className="matrix-actions">
          <div className="client-selector" style={{ marginRight: '10px', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '14px', fontWeight: 600, color: '#666' }}>Cliente:</span>
            <select 
              value={clientCode} 
              onChange={(e) => setClientCode(e.target.value)}
              style={{ padding: '8px', borderRadius: '6px', border: '1px solid #ddd' }}
            >
              {availableClients.map(c => <option key={c.code} value={c.code}>{c.name}</option>)}
            </select>
          </div>
          <button 
            className="btn-adjust-score" 
            onClick={handleScoreOverride}
            disabled={selectedIds.length === 0}
          >
            <Edit3 size={18} /> Ajustar Score
          </button>
          <button 
            className="btn-confirm-prioritization"
            onClick={confirmPrioritization}
            disabled={selectedIds.length === 0}
          >
            <CheckCircle size={18} /> Confirmar Priorização
          </button>
        </div>
      </header>

      <div className="matrix-risk-legend">
        <div className="legend-item"><span className="dot dot-high"></span> Risco Alto</div>
        <div className="legend-item"><span className="dot dot-medium"></span> Risco Médio</div>
        <div className="legend-item"><span className="dot dot-low"></span> Risco Baixo</div>
      </div>

      <div className="matrix-container">
        {/* Left Panel: Quadrant */}
        <div className="matrix-quadrant-panel">
          <div className="panel-header-simple">
            <h3 className="panel-title">Distribuição ganho x esforço</h3>
          </div>
          
          <div 
            className="quadrant-container"
            onDragOver={(e) => e.preventDefault()}
            onDrop={handleQuadrantDrop}
          >
            <div className="quadrant-labels">
              <span className="q-label top-left">Quick Win</span>
              <span className="q-label top-right">Estratégico</span>
              <span className="q-label bottom-left">Baixo Valor</span>
              <span className="q-label bottom-right">Reavaliar</span>
            </div>
            
            <div className="quadrant-grid">
              <div className="grid-line-h"></div>
              <div className="grid-line-v"></div>
            </div>

            <div className="axis-label y-axis">GANHO EXPECTADO</div>
            <div className="axis-label x-axis">ESFORÇO DE IMPLEMENTAÇÃO</div>

            {opportunities.length > 0 ? opportunities.map(o => {
              const { x, y } = getCoordinates(o);
              // Clamp size between 24 and 60
              const calculatedSize = 24 + ((o.tempScore || 0) / 100) * 18;
              const size = Math.min(Math.max(calculatedSize, 24), 60);
              const score = o.priorityScore || 0;
              let riskColorClass = 'low';
              
              if (score >= 500) {
                riskColorClass = 'high';
              } else if (score >= 100) {
                riskColorClass = 'medium';
              } else {
                const riskClass = o.operationalRiskClassification?.toLowerCase() || 'low';
                if (riskClass.includes('alt') || riskClass === 'high') riskColorClass = 'high';
                else if (riskClass.includes('med') || riskClass === 'medium') riskColorClass = 'medium';
              }
              
              return (
                <div 
                  key={o.id}
                  className={`matrix-bubble bubble-${riskColorClass} ${selectedIds.includes(o.id) ? 'selected' : ''}`}
                  style={{ 
                    left: `${x}%`, 
                    top: `${y}%`,
                    width: `${size}px`,
                    height: `${size}px`
                  }}
                  draggable
                  onDragStart={(e) => handleBubbleDragStart(e, o.id)}
                  onClick={() => handleItemClick(o)}
                  title={`${o.title} | Gain: ${o.expectedGainClassification}, Effort: ${o.implementationEffortClassification}`}
                >
                  {o.tempRank}
                </div>
              );
            }) : (
              <div className="empty-state">
                <Search size={48} className="empty-state-icon" />
                <p>Não há oportunidades elegíveis para priorização.</p>
              </div>
            )}
          </div>
        </div>

        {/* Right Panel: Ranking */}
        <div className="matrix-ranking-panel">
          <div className="panel-header">
            <h3>Ranking de Oportunidades</h3>
          </div>
          <div className="ranking-list">
            {opportunities.map((o, index) => (
              <div 
                key={o.id}
                className={`ranking-item ${selectedIds.includes(o.id) ? 'selected' : ''}`}
                draggable
                onDragStart={(e) => handleDragStart(e, index)}
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => handleDrop(e, index)}
                onClick={() => handleItemClick(o)}
                style={{ cursor: 'pointer' }}
              >
                <div className="status-indicator" style={{ 
                    width: '8px', 
                    height: '8px', 
                    borderRadius: '50%', 
                    backgroundColor: getStatusColor(o.priorityScore || 0),
                    marginRight: '8px',
                    flexShrink: 0
                  }}></div>
                <div className="rank-number">#{o.tempRank}</div>
                <div className="item-info">
                  <div className="item-code-row">
                    <span className="item-opportunity-code">{o.opportunityCode}</span>
                    {o.overridden && <span className="score-adjusted-badge">score ajustado</span>}
                  </div>
                  <span className="item-title">{o.title}</span>
                </div>
                <div className="item-score">{Math.round(o.tempScore || 0)}</div>
                <div className="drag-handle">
                  <GripVertical size={18} />
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Score Adjust Modal */}
      {showScoreModal && (
        <div className="matrix-modal-overlay">
          <div className="matrix-modal animate-scale-in">
            <div className="modal-header">
              <h2>Ajustar Score Manual</h2>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>Novo Score (0-100)</label>
                <input 
                  type="number" 
                  min="0" 
                  max="100" 
                  value={overrideScore}
                  onChange={(e) => setOverrideScore(parseInt(e.target.value))}
                />
              </div>
              <div className="form-group">
                <label>Justificativa da Alteração</label>
                <textarea 
                  rows={4} 
                  placeholder="Explique o motivo do ajuste manual de score..."
                  value={justification}
                  onChange={(e) => setJustification(e.target.value)}
                ></textarea>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn-cancel" onClick={() => setShowScoreModal(false)}>Cancelar</button>
              <button className="btn-save" onClick={saveScoreOverride}>Aplicar Ajuste</button>
            </div>
          </div>
        </div>
      )}
      {/* Opportunity Detail Drawer */}
      <OpportunityDrawer 
        isOpen={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        mode="view"
        opportunity={selectedOpp}
        onSave={() => {}}
        processes={processes}
      />
    </div>
  );
}
