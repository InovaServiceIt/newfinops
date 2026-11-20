import { lazy, Suspense, type ReactElement } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';

import { AuthGuard } from './components/auth/AuthGuard';
import AppShell from './components/layout/AppShell';
import { HOME_PATH, MENU_ITEMS } from './navigation/menu';
import LoginPage from './pages/Login/LoginPage';
import PlaceholderPage from './pages/Placeholder/PlaceholderPage';
import TokensPage from './pages/Tokens/TokensPage';

// Carregada sob demanda: os gráficos (recharts) ficam fora do bundle inicial.
const VisaoGeralPage = lazy(() => import('./pages/VisaoGeral/VisaoGeralPage'));

/** Páginas já implementadas; os demais itens do menu usam o placeholder. */
const PAGINAS: Record<string, ReactElement> = {
  '/visao-geral': (
    <Suspense fallback={<div className="page-content"><div className="ui-skeleton" style={{ height: 320 }} /></div>}>
      <VisaoGeralPage />
    </Suspense>
  ),
};

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<AuthGuard><AppShell /></AuthGuard>}>
          <Route path="/" element={<Navigate to={HOME_PATH} replace />} />
          {MENU_ITEMS.map((item) => (
            <Route
              key={item.path}
              path={item.path}
              element={PAGINAS[item.path] ?? <PlaceholderPage title={item.title} />}
            />
          ))}
          <Route path="/_tokens" element={<TokensPage />} />
          <Route path="*" element={<Navigate to={HOME_PATH} replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
