import React, { useState, useEffect } from 'react';

interface User {
  name: string;
  email: string;
  tenantId: string;
}

export const AuthGuard: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean | null>(null);
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch('/api/auth/me');
        if (response.ok) {
          const data = await response.json();
          setUser(data);
          setIsAuthenticated(true);
        } else {
          // Redireciona para nossa Landing Page customizada
          window.location.href = '/login';
        }
      } catch (error) {
        console.error('Falha ao verificar autenticação:', error);
        window.location.href = '/login';
      }
    };

    checkAuth();
  }, []);

  if (isAuthenticated === null) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-[#0a0a0b] text-white">
        <div className="flex flex-col items-center gap-4">
          <div className="w-8 h-8 border-4 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
          <p className="text-gray-400 font-medium">Verificando acesso corporativo...</p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
};
