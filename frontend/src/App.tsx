import { BrowserRouter, Routes, Route, NavLink, Outlet } from 'react-router-dom';
import { Activity, Search, ShieldCheck, Wrench, CheckSquare, Zap, Target, Settings } from 'lucide-react';
import './App.css';

import Dashboard from './pages/Dashboard/Dashboard';
import DecisionRulesPage from './pages/DecisionRules/DecisionRulesPage';
import RadarPage from './pages/Radar/RadarPage';
import OpportunitiesPage from './pages/Discovery/OpportunitiesPage';
import DiscoveryPanelPage from './pages/Discovery/DiscoveryPanelPage';
import MatrixPage from './pages/Discovery/MatrixPage';
import CatalogPage from './pages/Discovery/CatalogPage';
import ApprovalPage from './pages/Approval/ApprovalPage';
import AttentionCapacityPage from './pages/AttentionCapacity/AttentionCapacityPage';

import GlobalHeader from './components/layout/GlobalHeader';

function MainLayout() {
  return (
    <div className="layout-root">
      <GlobalHeader />
      <main className="content-area">
        <Outlet />
      </main>
    </div>
  );
}

import { AuthGuard } from './components/auth/AuthGuard';

import LoginPage from './pages/Login/LoginPage';

console.log('%cPredict v1.0.1 - SSO Fix Applied', 'color: white; background: #ce1212; padding: 4px; border-radius: 4px;');

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<AuthGuard><MainLayout /></AuthGuard>}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/radar" element={<RadarPage />} />
          <Route path="/discovery" element={<OpportunitiesPage />} />
          <Route path="/discovery/opportunities" element={<OpportunitiesPage />} />
          <Route path="/discovery/panel" element={<DiscoveryPanelPage />} />
          <Route path="/discovery/matrix" element={<MatrixPage />} />
          <Route path="/discovery/catalog" element={<CatalogPage />} />
          <Route path="/decision-rules" element={<DecisionRulesPage />} />
          <Route path="/approval" element={<ApprovalPage />} />
          <Route path="/regras" element={<AttentionCapacityPage />} />
          <Route path="*" element={<div style={{padding: '2rem'}}>Página em construção</div>} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;

