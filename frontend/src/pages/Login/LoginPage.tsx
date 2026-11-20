import React from 'react';
import { Cloud, Globe, Shield, Zap } from 'lucide-react';
import './LoginPage.css';

const LoginPage: React.FC = () => {
  const handleLogin = () => {
    // Redireciona para o fluxo oficial do Entra ID configurado no backend
    window.location.href = '/MicrosoftIdentity/Account/SignIn';
  };

  return (
    <div className="login-root">
      <div className="login-background" aria-hidden="true">
        <div className="login-shape login-shape-1" />
        <div className="login-shape login-shape-2" />
      </div>

      <div className="login-card">
        {/* Faixa escura com o logo: o logo Service IT fica legível em qualquer skin */}
        <div className="login-card-band">
          <img src="/service-it-logo.png" alt="Service IT" className="login-logo" />
        </div>

        <div className="login-card-body">
          <div className="login-brand">
            <Zap size={28} className="login-brand-icon" />
            <span className="login-brand-name">FINOPS</span>
          </div>
          <h1>Bem-vindo ao FinOps!</h1>
          <p className="login-text">
            Faça login com sua conta corporativa para acessar a plataforma FinOps da Service IT.
          </p>

          <button type="button" onClick={handleLogin} className="login-button">
            <span className="ms-icon" aria-hidden="true">
              <span className="ms-square ms-red" />
              <span className="ms-square ms-green" />
              <span className="ms-square ms-blue" />
              <span className="ms-square ms-yellow" />
            </span>
            Entrar com Microsoft Service IT
          </button>

          <p className="login-footer">© 2026 Service IT • FinOps</p>
        </div>
      </div>

      <div className="login-features">
        <div className="login-feature"><Shield size={18} /><span>Acesso Seguro</span></div>
        <div className="login-feature"><Cloud size={18} /><span>Custos de Nuvem</span></div>
        <div className="login-feature"><Globe size={18} /><span>Multicloud</span></div>
      </div>
    </div>
  );
};

export default LoginPage;
