import React from 'react';
import { NavLink } from 'react-router-dom';
import { Palette, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { MENU } from '../../navigation/menu';
import { useCurrentUser } from '../auth/AuthGuard';
import './Sidebar.css';

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

const Sidebar: React.FC<SidebarProps> = ({ collapsed, onToggle }) => {
  const perfil = useCurrentUser()?.perfil;
  const ehAdmin = perfil === 'ADMINISTRADOR';
  const secoes = MENU
    .map((s) => ({ ...s, items: s.items.filter((i) => (!i.adminOnly || ehAdmin) && (!i.perfis || (!!perfil && i.perfis.includes(perfil)))) }))
    .filter((s) => s.items.length > 0);

  return (
  <aside className={`sidebar${collapsed ? ' is-collapsed' : ''}`} aria-label="Menu principal">
    <nav className="sidebar-nav">
      {secoes.map((section) => (
        <div key={section.title} className="sidebar-section">
          <div className="sidebar-section-title">{section.title}</div>
          {section.items.map(({ title, path, icon: Icon }) => (
            <NavLink
              key={path}
              to={path}
              className={({ isActive }) => `sidebar-item${isActive ? ' is-active' : ''}`}
              title={collapsed ? title : undefined}
            >
              <Icon size={18} className="sidebar-item-icon" />
              <span className="sidebar-item-label">{title}</span>
            </NavLink>
          ))}
        </div>
      ))}
    </nav>

    <div className="sidebar-footer">
      <NavLink
        to="/_tokens"
        className={({ isActive }) => `sidebar-footer-link${isActive ? ' is-active' : ''}`}
        title="Amostras de tokens"
      >
        <Palette size={14} />
        <span className="sidebar-item-label">Tokens</span>
      </NavLink>
      <button
        type="button"
        className="sidebar-toggle"
        onClick={onToggle}
        aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
        title={collapsed ? 'Expandir menu' : 'Recolher menu'}
      >
        {collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
      </button>
    </div>
  </aside>
  );
};

export default Sidebar;
