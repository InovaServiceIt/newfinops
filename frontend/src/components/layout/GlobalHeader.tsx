import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Moon, Settings, Sun } from 'lucide-react';
import { useTheme } from '../../theme/ThemeProvider';
import { THEMES } from '../../theme/themes';
import { NOME_PERFIL, useCurrentUser } from '../auth/AuthGuard';
import './GlobalHeader.css';

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  const first = parts[0][0];
  const last = parts.length > 1 ? parts[parts.length - 1][0] : '';
  return (first + last).toUpperCase();
}

const GlobalHeader: React.FC = () => {
  const navigate = useNavigate();
  const user = useCurrentUser();
  const { theme, cycleTheme } = useTheme();

  const currentIndex = Math.max(0, THEMES.findIndex((t) => t.id === theme));
  const current = THEMES[currentIndex];
  const next = THEMES[(currentIndex + 1) % THEMES.length];
  const ThemeIcon = current.mode === 'dark' ? Moon : Sun;

  return (
    <header className="global-header">
      <div className="header-left" onClick={() => navigate('/')}>
        <div className="header-logo-icon">
          <Settings size={20} />
        </div>
        <div className="header-title-group">
          <span className="header-brand">CloudVista</span>
          <span className="header-tagline">Custos multicloud sob controle. Decisões mais inteligentes.</span>
        </div>
      </div>

      <div className="header-right">
        <button
          type="button"
          className="header-theme-toggle"
          onClick={cycleTheme}
          title={`Skin atual: ${current.name}. Clique para usar ${next.name}.`}
          aria-label={`Trocar skin para ${next.name}`}
        >
          <ThemeIcon size={16} />
          <span className="header-theme-name">{current.name}</span>
        </button>

        {user && (
          <div className="header-user" title={`${user.nome} · ${user.email}`}>
            <span className="header-user-avatar">{initials(user.nome)}</span>
            <span className="header-user-info">
              <span className="header-user-name">{user.email}</span>
              <span className="header-user-perfil">{NOME_PERFIL[user.perfil]}</span>
            </span>
          </div>
        )}

        <img src="/service-it-logo.png" alt="Service IT" className="service-it-logo-img" />
      </div>
    </header>
  );
};

export default GlobalHeader;
