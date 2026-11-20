import React from 'react';
import { Link } from 'react-router-dom';
import { Settings } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import { useEhAdministrador } from '../../components/auth/AuthGuard';
import ContasLista from './ContasLista';

/** Contas de cloud (somente leitura, todos os perfis; o RLS define o que cada um vê). */
const ContasPage: React.FC = () => {
  const ehAdmin = useEhAdministrador();

  return (
    <div className="page-content" style={{ maxWidth: 'none' }}>
      <PageHeader
        title="Contas de cloud"
        actions={ehAdmin && (
          <Link className="ui-btn ui-btn-secondary" to="/configuracoes?aba=contas">
            <Settings size={16} /> Gerenciar
          </Link>
        )}
      />
      <ContasLista />
    </div>
  );
};

export default ContasPage;
