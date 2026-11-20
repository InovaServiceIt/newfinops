import React, { useState } from 'react';
import { Navigate } from 'react-router-dom';
import PageHeader from '../../components/layout/PageHeader';
import { useEhAdministrador } from '../../components/auth/AuthGuard';
import { HOME_PATH } from '../../navigation/menu';
import ClientesAba from './ClientesAba';
import UsuariosAba from './UsuariosAba';
import './ConfiguracoesPage.css';

type Aba = 'clientes' | 'usuarios';

/** Configurações (só ADMINISTRADOR; o backend também exige a política Administrador). */
const ConfiguracoesPage: React.FC = () => {
  const ehAdmin = useEhAdministrador();
  const [aba, setAba] = useState<Aba>('clientes');

  if (!ehAdmin) return <Navigate to={HOME_PATH} replace />;

  return (
    <div className="page-content cfg-pagina">
      <PageHeader title="Configurações" />
      <div className="ui-tabs" role="tablist">
        <button type="button" role="tab" className="ui-tab" aria-selected={aba === 'clientes'} onClick={() => setAba('clientes')}>
          Clientes
        </button>
        <button type="button" role="tab" className="ui-tab" aria-selected={aba === 'usuarios'} onClick={() => setAba('usuarios')}>
          Usuários
        </button>
      </div>
      {aba === 'clientes' ? <ClientesAba /> : <UsuariosAba />}
    </div>
  );
};

export default ConfiguracoesPage;
