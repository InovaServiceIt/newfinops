import { BrowserRouter, Routes, Route, Outlet } from 'react-router-dom';
import './App.css';

import GlobalHeader from './components/layout/GlobalHeader';
import { AuthGuard } from './components/auth/AuthGuard';
import LoginPage from './pages/Login/LoginPage';
import HelloWorldPage from './pages/HelloWorld/HelloWorldPage';

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

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<AuthGuard><MainLayout /></AuthGuard>}>
          <Route path="/" element={<HelloWorldPage />} />
          <Route path="*" element={<HelloWorldPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;
