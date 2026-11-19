import React, { ReactNode } from 'react';
import './ui.css';

interface PageHeaderProps {
  title: string;
  subtitle: string;
  action?: ReactNode;
}

export function PageHeader({ title, subtitle, action }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div className="page-header-info">
        <h1>{title}</h1>
        <p>{subtitle}</p>
      </div>
      {action && (
        <div className="page-header-action">
          {action}
        </div>
      )}
    </header>
  );
}
