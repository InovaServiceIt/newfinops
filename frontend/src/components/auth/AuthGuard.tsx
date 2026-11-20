import React, { createContext, useContext, useEffect, useState } from 'react';
import './AuthGuard.css';

export interface CurrentUser {
  name: string;
  email?: string;
  tenantId?: string;
}

const UserContext = createContext<CurrentUser | null>(null);

/** Usuário logado (disponível dentro do AuthGuard). */
export function useCurrentUser(): CurrentUser | null {
  return useContext(UserContext);
}

export const AuthGuard: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<CurrentUser | null>(null);

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch('/api/auth/me');
        if (response.ok) {
          setUser(await response.json());
        } else {
          // 401 (sem sessão) ou qualquer outra falha: tela de login.
          window.location.href = '/login';
        }
      } catch (error) {
        console.error('Falha ao verificar autenticação:', error);
        window.location.href = '/login';
      }
    };

    checkAuth();
  }, []);

  if (!user) {
    return (
      <div className="auth-loading">
        <div className="auth-spinner" />
        <p>Verificando acesso corporativo...</p>
      </div>
    );
  }

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
