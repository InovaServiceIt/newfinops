import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Settings } from 'lucide-react';
import './GlobalHeader.css';

const GlobalHeader: React.FC = () => {
  const navigate = useNavigate();

  return (
    <header className="global-header">
      <div className="header-left" onClick={() => navigate('/')}>
        <div className="header-logo-icon">
          <Settings size={20} />
        </div>
        <div className="header-title-group">
          <span className="header-brand">PREDICT</span>
          <span className="header-tagline">— Xurupita. Operações mais inteligentes.</span>
        </div>
      </div>
      
      <div className="header-right">
        <div className="service-it-group">
          <img 
            src="/service-it-logo.png" 
            alt="Service IT Logo" 
            className="service-it-logo-img" 
          />
        </div>
        <div className="header-help-icon">?</div>
      </div>
    </header>
  );
};

export default GlobalHeader;
