import React, { useEffect, useState } from 'react';
import { Outlet } from 'react-router-dom';
import GlobalHeader from './GlobalHeader';
import Sidebar from './Sidebar';
import './AppShell.css';

const COLLAPSED_KEY = 'finops.sidebar.collapsed';

function readCollapsed(): boolean {
  try {
    return window.localStorage.getItem(COLLAPSED_KEY) === '1';
  } catch {
    return false;
  }
}

/** Casca da aplicação: header fixo + sidebar recolhível + área central. */
const AppShell: React.FC = () => {
  const [collapsed, setCollapsed] = useState<boolean>(readCollapsed);

  useEffect(() => {
    try {
      window.localStorage.setItem(COLLAPSED_KEY, collapsed ? '1' : '0');
    } catch {
      /* sem persistência */
    }
  }, [collapsed]);

  return (
    <div className={`app-shell${collapsed ? ' sidebar-collapsed' : ''}`}>
      <GlobalHeader />
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  );
};

export default AppShell;
