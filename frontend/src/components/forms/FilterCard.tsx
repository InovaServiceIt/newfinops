import React, { ReactNode } from 'react';
import { Filter, X } from 'lucide-react';
import './FilterCard.css';

interface FilterCardProps {
  onSearch: () => void;
  onClear: () => void;
  children: ReactNode;
}

export function FilterCard({ onSearch, onClear, children }: FilterCardProps) {
  return (
    <div className="filter-card">
      <div className="filter-card-header">
        <h3 className="filter-card-title">
          <Filter size={18} /> Filtros de Pesquisa
        </h3>
      </div>
      <div className="filter-card-body">
        <div className="filter-grid">
          {children}
        </div>
      </div>
      <div className="filter-card-footer">
        <button className="btn-secondary" onClick={onClear}>Limpar</button>
        <button className="btn-primary" onClick={onSearch}>Pesquisar</button>
      </div>
    </div>
  );
}
