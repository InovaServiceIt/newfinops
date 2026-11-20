import React from 'react';
import { Construction } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import './PlaceholderPage.css';

/** Área central vazia usada por todos os itens do menu nesta fase. */
const PlaceholderPage: React.FC<{ title: string }> = ({ title }) => (
  <div className="page-content">
    <PageHeader title={title} />
    <div className="placeholder-area">
      <Construction size={28} />
      <span>Em construção</span>
    </div>
  </div>
);

export default PlaceholderPage;
