import React from 'react';
import { LogOut, Settings, ShieldOff } from 'lucide-react';
import './AcessoNaoAutorizado.css';

interface Props {
  /** E-mail da conta Microsoft usada (quando conhecido). */
  email?: string | null;
  /** Quando presente, mostra uma falha técnica em vez de "sem acesso". */
  erro?: string;
}

/** Tela para usuário autenticado no Entra mas sem cadastro ativo no CloudVista (sem loop de redirecionamento). */
const AcessoNaoAutorizado: React.FC<Props> = ({ email, erro }) => (
  <div className="cv-acesso-root">
    <div className="cv-acesso-card">
      <div className="cv-acesso-faixa">
        <img src="/service-it-logo.png" alt="Service IT" className="cv-acesso-logo" />
      </div>
      <div className="cv-acesso-corpo">
        <div className="cv-acesso-marca">
          <Settings size={24} className="cv-acesso-marca-icone" />
          <span>CloudVista</span>
        </div>
        <ShieldOff size={36} className="cv-acesso-icone" />
        {erro ? (
          <>
            <h1>Não foi possível validar o acesso</h1>
            <p className="cv-acesso-texto">{erro}</p>
          </>
        ) : (
          <>
            <h1>Acesso não autorizado</h1>
            <p className="cv-acesso-texto">Seu usuário ainda não tem acesso ao CloudVista. Solicite ao administrador.</p>
            {email && <p className="cv-acesso-email">Conta: {email}</p>}
          </>
        )}
        <div className="cv-acesso-acoes">
          {erro && (
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => window.location.reload()}>
              Tentar novamente
            </button>
          )}
          <a className="ui-btn ui-btn-primary" href="/MicrosoftIdentity/Account/SignOut">
            <LogOut size={16} /> Sair
          </a>
        </div>
      </div>
    </div>
  </div>
);

export default AcessoNaoAutorizado;
