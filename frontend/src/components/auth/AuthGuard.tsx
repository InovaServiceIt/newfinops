import React, { createContext, useContext, useEffect, useState } from 'react';
import { CODIGO_NAO_AUTORIZADO } from '../../services/api';
import AcessoNaoAutorizado from './AcessoNaoAutorizado';
import './AuthGuard.css';

export type Perfil = 'ADMINISTRADOR' | 'ANALISTA_FINOPS' | 'CLIENTE';

export const NOME_PERFIL: Record<Perfil, string> = {
  ADMINISTRADOR: 'Administrador',
  ANALISTA_FINOPS: 'Analista FinOps',
  CLIENTE: 'Cliente',
};

export interface ClienteResumo {
  id: number;
  nome: string;
  apelido: string;
}

/** Usuário CloudVista autorizado (perfil lido do banco). */
export interface CurrentUser {
  usuarioId: number;
  nome: string;
  email: string;
  perfil: Perfil;
  clientes: ClienteResumo[];
}

interface MeResposta {
  autorizado: boolean;
  nome: string | null;
  email: string | null;
  perfil: Perfil | null;
  usuarioId: number | null;
  clientes: ClienteResumo[];
}

type Estado =
  | { tipo: 'carregando' }
  | { tipo: 'ok'; usuario: CurrentUser }
  | { tipo: 'nao_autorizado'; nome: string | null; email: string | null }
  | { tipo: 'erro'; mensagem: string };

const UserContext = createContext<CurrentUser | null>(null);

/** Usuário logado (disponível dentro do AuthGuard). */
export function useCurrentUser(): CurrentUser | null {
  return useContext(UserContext);
}

export function useEhAdministrador(): boolean {
  return useCurrentUser()?.perfil === 'ADMINISTRADOR';
}

export const AuthGuard: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [estado, setEstado] = useState<Estado>({ tipo: 'carregando' });

  useEffect(() => {
    const verificar = async () => {
      try {
        const res = await fetch('/api/auth/me', { headers: { Accept: 'application/json' } });
        if (res.status === 401) {
          window.location.href = '/login';
          return;
        }
        const body = await res.json().catch(() => null);

        if (res.status === 403 && body?.codigo === CODIGO_NAO_AUTORIZADO) {
          setEstado({ tipo: 'nao_autorizado', nome: null, email: null });
          return;
        }
        if (!res.ok) {
          setEstado({ tipo: 'erro', mensagem: body?.erro ?? 'Não foi possível validar o acesso.' });
          return;
        }

        const me = body as MeResposta;
        if (!me.autorizado || !me.perfil || me.usuarioId === null) {
          setEstado({ tipo: 'nao_autorizado', nome: me.nome, email: me.email });
          return;
        }
        setEstado({
          tipo: 'ok',
          usuario: { usuarioId: me.usuarioId, nome: me.nome ?? me.email ?? '', email: me.email ?? '', perfil: me.perfil, clientes: me.clientes ?? [] },
        });
      } catch (error) {
        console.error('Falha ao verificar autenticação:', error);
        setEstado({ tipo: 'erro', mensagem: 'Não foi possível validar o acesso. Verifique a conexão.' });
      }
    };

    verificar();
  }, []);

  switch (estado.tipo) {
    case 'carregando':
      return (
        <div className="auth-loading">
          <div className="auth-spinner" />
          <p>Verificando acesso corporativo...</p>
        </div>
      );
    case 'nao_autorizado':
      return <AcessoNaoAutorizado email={estado.email} />;
    case 'erro':
      return <AcessoNaoAutorizado erro={estado.mensagem} />;
    case 'ok':
      return <UserContext.Provider value={estado.usuario}>{children}</UserContext.Provider>;
  }
};
