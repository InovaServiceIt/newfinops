import React, { useState } from 'react';
import { Search, X } from 'lucide-react';

interface FilterProps {
  onFilter: (filters: { clientCode?: string; status?: string; search?: string }) => void;
  clients: { code: string; name: string }[];
}

export default function OpportunitiesFilter({ onFilter, clients }: FilterProps) {
  const [clientCode, setClientCode] = useState('');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');

  const handleApply = () => {
    onFilter({
      clientCode: clientCode || undefined,
      status: status || undefined,
      search: search || undefined
    });
  };

  const handleClear = () => {
    setClientCode('');
    setStatus('');
    setSearch('');
    onFilter({});
  };

  return (
    <div className="opportunities-filters">
      <div className="filter-group">
        <label>Cliente</label>
        <select 
          className="filter-select"
          value={clientCode}
          onChange={(e) => setClientCode(e.target.value)}
        >
          <option value="">Todos os clientes</option>
          {clients.map(c => (
            <option key={c.code} value={c.code}>{c.name}</option>
          ))}
        </select>
      </div>

      <div className="filter-group">
        <label>Status</label>
        <select 
          className="filter-select"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
        >
          <option value="">Todos os status</option>
          <option value="Draft">Draft</option>
          <option value="Em Análise">Em Análise</option>
          <option value="Aprovado">Aprovado</option>
          <option value="Implementação">Implementação</option>
          <option value="Descartado">Descartado</option>
        </select>
      </div>

      <div className="filter-group" style={{ flex: 2 }}>
        <label>Buscar</label>
        <div style={{ position: 'relative', display: 'flex' }}>
          <input 
            type="text" 
            className="filter-input" 
            placeholder="Título, código ou resumo..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: '100%', paddingLeft: '2.2rem' }}
          />
          <Search size={16} style={{ position: 'absolute', left: '0.6rem', top: '0.65rem', color: '#adb5bd' }} />
        </div>
      </div>

      <div className="filter-actions">
        <button className="btn-opp-action primary" onClick={handleApply}>
          Pesquisar
        </button>
        <button className="btn-opp-action secondary" onClick={handleClear} title="Limpar filtros">
          <X size={16} />
        </button>
      </div>
    </div>
  );
}
