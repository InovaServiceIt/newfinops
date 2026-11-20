import React from 'react';
import { ChevronRight } from 'lucide-react';
import './PageHeader.css';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
  /** Ações da página (ex.: Exportar, filtros), alinhadas à direita na mesma linha. */
  actions?: React.ReactNode;
}

/** Cabeçalho compacto da página em uma linha: "FinOps › <título>" + ações à direita. */
const PageHeader: React.FC<PageHeaderProps> = ({ title, subtitle, actions }) => (
  <div className="page-header">
    <div className="page-header-main">
      <nav className="page-breadcrumb" aria-label="Breadcrumb">
        <span>FinOps</span>
        <ChevronRight size={14} className="page-breadcrumb-sep" aria-hidden="true" />
      </nav>
      <h1 className="page-title">{title}</h1>
      {subtitle && <p className="page-subtitle">{subtitle}</p>}
    </div>
    {actions && <div className="page-header-actions">{actions}</div>}
  </div>
);

export default PageHeader;
