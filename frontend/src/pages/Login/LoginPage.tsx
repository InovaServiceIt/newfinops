import React from 'react';
import { LogIn, Shield, Zap, Globe } from 'lucide-react';
import './LoginPage.css';

const LoginPage: React.FC = () => {
    const handleLogin = () => {
        // Redireciona para o fluxo oficial do Azure AD configurado no backend
        window.location.href = '/MicrosoftIdentity/Account/SignIn';
    };

    return (
        <div className="login-provider-root">
            <div className="login-background-geometry">
                <div className="shape shape-1"></div>
                <div className="shape shape-2"></div>
            </div>

            <div className="login-card-container">
                <div className="login-glass-card">
                    <div className="login-header">
                        <div className="brand-logo">
                            <Zap size={32} color="#ce1212" fill="#ce1212" />
                            <span className="brand-name">PREDICT</span>
                        </div>
                        <h1>Bem-vindo ao Predict!</h1>
                        <p>Faça login com sua conta corporativa para acessar a plataforma de inteligência Service IT.</p>
                    </div>

                    <div className="login-actions">
                        <button onClick={handleLogin} className="btn-login-azure">
                            <div className="microsoft-icon">
                                <span className="ms-square ms-red"></span>
                                <span className="ms-square ms-green"></span>
                                <span className="ms-square ms-yellow"></span>
                                <span className="ms-square ms-blue"></span>
                            </div>
                            Entrar com Microsoft Service IT
                        </button>
                    </div>

                    <div className="login-footer">
                        <div className="service-it-logo">
                             <div className="logo-spark"></div>
                             <span>SERVICE IT</span>
                        </div>
                        <p>© 2026 Predict Platform • Advanced Analytics</p>
                    </div>
                </div>
            </div>

            <div className="login-features-strip">
                <div className="feature-item">
                    <Shield size={20} />
                    <span>Acesso Seguro</span>
                </div>
                <div className="feature-item">
                    <Zap size={20} />
                    <span>IA Avançada</span>
                </div>
                <div className="feature-item">
                    <Globe size={20} />
                    <span>Global Ops</span>
                </div>
            </div>
        </div>
    );
};

export default LoginPage;
