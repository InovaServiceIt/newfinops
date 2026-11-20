import React from 'react';
import { ChevronRight } from 'lucide-react';
import './PageHeader.css';

interface PageHeaderProps {
  title: string;
  subtitle?: string;
}

/** Breadcrumb "FinOps / <página>" + título. */
const PageHeader: React.FC<PageHeaderProps> = ({ title, subtitle }) => (
  <div className="page-header">
    <nav className="page-breadcrumb" aria-label="Breadcrumb">
      <span>FinOps</span>
      <ChevronRight size={14} />
      <span className="page-breadcrumb-current">{title}</span>
    </nav>
    <h1 className="page-title">{title}</h1>
    {subtitle && <p className="page-subtitle">{subtitle}</p>}
  </div>
);

export default PageHeader;
