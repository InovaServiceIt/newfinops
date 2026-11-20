import React from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import PageHeader from '../../components/layout/PageHeader';
import { useEhAdministrador } from '../../components/auth/AuthGuard';
import { HOME_PATH } from '../../navigation/menu';
import ClientesAba from './ClientesAba';
import ContasAba from './ContasAba';
import AgendaAba from './AgendaAba';
import ParametrosAba from './ParametrosAba';
import UsuariosAba from './UsuariosAba';
import './ConfiguracoesPage.css';

type Aba = 'clientes' | 'usuarios' | 'contas' | 'agenda' | 'parametros';

const ABAS: { id: Aba; rotulo: string }[] = [
  { id: 'clientes', rotulo: 'Clientes' },
  { id: 'usuarios', rotulo: 'Usuários' },
  { id: 'contas', rotulo: 'Contas de cloud' },
  { id: 'agenda', rotulo: 'Agenda' },
  { id: 'parametros', rotulo: 'Parâmetros' },
];

/** Configurações (só ADMINISTRADOR; o backend também exige a política Administrador). */
const ConfiguracoesPage: React.FC = () => {
  const ehAdmin = useEhAdministrador();
  const [params, setParams] = useSearchParams();
  const aba: Aba = ABAS.some((a) => a.id === params.get('aba')) ? (params.get('aba') as Aba) : 'clientes';
  const setAba = (a: Aba) => setParams({ aba: a }, { replace: true });

  if (!ehAdmin) return <Navigate to={HOME_PATH} replace />;

  return (
    <div className="page-content cfg-pagina">
      <PageHeader title="Configurações" />
      <div className="ui-tabs" role="tablist">
        {ABAS.map((a) => (
          <button key={a.id} type="button" role="tab" className="ui-tab" aria-selected={aba === a.id} onClick={() => setAba(a.id)}>
            {a.rotulo}
          </button>
        ))}
      </div>
      {aba === 'clientes' && <ClientesAba />}
      {aba === 'usuarios' && <UsuariosAba />}
      {aba === 'contas' && <ContasAba />}
      {aba === 'agenda' && <AgendaAba />}
      {aba === 'parametros' && <ParametrosAba />}
    </div>
  );
};

export default ConfiguracoesPage;
