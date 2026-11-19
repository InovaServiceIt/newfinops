import React, { useState, useEffect } from 'react';
import { Library, Search, Database, CheckCircle, Clock, Bot } from 'lucide-react';
import axios from 'axios';

interface CatalogItem {
  id: number;
  activity: string;
  classification: string;
  agentAction: string;
  hasEmbedding: boolean;
}

export default function CatalogPage() {
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [stats, setStats] = useState({ total: 0, indexed: 0, pending: 0 });

  useEffect(() => {
    fetchStats();
    fetchData();
  }, []);

  const fetchStats = async () => {
    try {
      const res = await axios.get('/api/servicecatalog/stats');
      setStats(res.data);
    } catch (e) { console.error(e); }
  };

  const fetchData = async (query = '') => {
    setLoading(true);
    try {
      const res = await axios.get(`/api/servicecatalog?search=${query}`);
      setItems(res.data);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    fetchData(search);
  };

  return (
    <div className="opportunities-container" style={{ padding: '0 2rem 2rem' }}>
      <div style={{ paddingTop: '1.5rem' }}></div>

      <div className="opportunities-main-card">
        <div className="opportunities-header">
          <div className="opportunities-title-box">
            <div className="opportunities-icon-wrapper">
              <Library size={20} />
            </div>
            <div className="opportunities-title-content">
              <small>Predict Discovery</small>
              <h2>Catálogo de Serviços</h2>
            </div>
          </div>

          <div style={{ display: 'flex', gap: '1rem' }}>
             <div className="catalog-stat-card">
                <Database size={14} />
                <span>Total: <strong>{stats.total}</strong></span>
             </div>
             <div className="catalog-stat-card success">
                <CheckCircle size={14} />
                <span>Indexados: <strong>{stats.indexed}</strong></span>
             </div>
             {stats.pending > 0 && (
                <div className="catalog-stat-card warning">
                    <Clock size={14} />
                    <span>Pendentes: <strong>{stats.pending}</strong></span>
                </div>
             )}
          </div>
        </div>

        <div className="opportunities-filter-bar">
          <form onSubmit={handleSearch} style={{ display: 'flex', gap: '0.5rem', width: '100%' }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <Search size={16} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: '#adb5bd' }} />
              <input
                type="text"
                placeholder="Pesquisar por atividade, classificação ou instrução..."
                className="filter-input"
                style={{ paddingLeft: '38px', width: '100%' }}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <button type="submit" className="btn-opp-action primary">Pesquisar</button>
          </form>
        </div>

        <div className="opportunities-grid-wrapper">
          <table className="opportunities-table">
            <thead>
              <tr>
                <th style={{ width: '60px' }}>ID</th>
                <th>Atividade</th>
                <th style={{ width: '120px' }}>Classificação</th>
                <th>Instrução Executável</th>
                <th style={{ width: '100px', textAlign: 'center' }}>Vetor</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={5} style={{ textAlign: 'center', padding: '3rem' }}>Carregando catálogo...</td></tr>
              ) : items.length === 0 ? (
                <tr><td colSpan={5} style={{ textAlign: 'center', padding: '3rem' }}>Nenhum item encontrado.</td></tr>
              ) : (
                items.map(item => (
                  <tr key={item.id}>
                    <td style={{ color: '#adb5bd', fontSize: '0.8rem' }}>#{item.id}</td>
                    <td style={{ fontWeight: 600 }}>{item.activity}</td>
                    <td>
                      <span className={`catalog-badge ${item.classification === 'Objetiva' ? 'objetiva' : 'generica'}`}>
                        {item.classification}
                      </span>
                    </td>
                    <td>
                      <div style={{ 
                        fontSize: '0.75rem', 
                        fontFamily: 'monospace', 
                        background: '#f8f9fa', 
                        padding: '0.5rem', 
                        borderRadius: '4px',
                        maxHeight: '60px',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        color: '#495057'
                      }}>
                        {item.agentAction}
                      </div>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                       {item.hasEmbedding ? (
                         <div title="Vetorizado e pronto para match" style={{ color: '#10b981' }}><Bot size={18} /></div>
                       ) : (
                         <div title="Pendente de vetorização" style={{ color: '#dee2e6' }}><Bot size={18} /></div>
                       )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
