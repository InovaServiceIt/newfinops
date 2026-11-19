import React from 'react';
import { 
  Activity, ArrowUpRight, ArrowDownRight, Clock, ShieldCheck, 
  AlertTriangle, Server, Database, Globe, Zap
} from 'lucide-react';
import './RadarPage.css';

const StatCard = ({ title, value, change, isPositive, icon: Icon, color }: any) => (
  <div className="radar-stat-card">
    <div className="stat-header">
      <div className={`stat-icon-wrapper ${color}`}>
        <Icon size={20} />
      </div>
      <div className={`stat-change ${isPositive ? 'positive' : 'negative'}`}>
        {isPositive ? <ArrowUpRight size={16} /> : <ArrowDownRight size={16} />}
        {change}%
      </div>
    </div>
    <div className="stat-body">
      <span className="stat-value">{value}</span>
      <h3 className="stat-title">{title}</h3>
    </div>
  </div>
);

export default function RadarPage() {
  return (
    <div className="radar-page animate-fade-in">
      <header className="radar-header">
        <div>
          <h1 className="radar-page-title">Predict Radar</h1>
          <p className="radar-page-subtitle">Monitoramento em tempo real e observabilidade preditiva.</p>
        </div>
        <div className="radar-live-indicator">
          <div className="pulse-circle"></div>
          RESPOSTA EM TEMPO REAL
        </div>
      </header>

      {/* Stats Row */}
      <div className="radar-stats-grid">
        <StatCard 
          title="Eventos Totais (24h)" 
          value="1.248.302" 
          change="12.5" 
          isPositive={true} 
          icon={Activity}
          color="blue"
        />
        <StatCard 
          title="Incidentes Evitados" 
          value="42" 
          change="8.2" 
          isPositive={true} 
          icon={ShieldCheck}
          color="green"
        />
        <StatCard 
          title="Anomalias Detectadas" 
          value="128" 
          change="4.1" 
          isPositive={false} 
          icon={AlertTriangle}
          color="orange"
        />
        <StatCard 
          title="MTTR Médio" 
          value="14m 22s" 
          change="18.4" 
          isPositive={true} 
          icon={Clock}
          color="purple"
        />
      </div>

      <div className="radar-main-grid">
        {/* Health Chart Column */}
        <div className="radar-panel health-panel">
          <h3 className="panel-title">Saúde do Sistema</h3>
          <div className="health-score-container">
            <svg viewBox="0 0 100 100" className="health-gauge">
              <circle className="gauge-bg" cx="50" cy="50" r="45" />
              <circle className="gauge-fill" cx="50" cy="50" r="45" style={{ strokeDasharray: '283', strokeDashoffset: '20' }} />
            </svg>
            <div className="health-score-value">92%</div>
            <div className="health-status-tag">ESTÁVEL</div>
          </div>
          
          <div className="health-details">
            <div className="health-item">
              <span>Infraestrutura</span>
              <div className="progress-bar"><div className="progress-fill" style={{width: '94%'}}></div></div>
            </div>
            <div className="health-item">
              <span>Sistemas Críticos</span>
              <div className="progress-bar"><div className="progress-fill" style={{width: '88%'}}></div></div>
            </div>
            <div className="health-item">
              <span>Banco de Dados</span>
              <div className="progress-bar"><div className="progress-fill" style={{width: '91%'}}></div></div>
            </div>
          </div>
        </div>

        {/* Live Feed Column */}
        <div className="radar-panel feed-panel">
          <div className="panel-header">
            <h3 className="panel-title">Eventos Recentes</h3>
            <span className="live-badge">LIVE</span>
          </div>
          <div className="events-list">
            {[1, 2, 3, 4, 5].map((i) => (
              <div key={i} className="event-item">
                <div className="event-time">20:38:1{i}</div>
                <div className="event-content">
                  <div className="event-subject">Sinal de latência em <strong>BR-SAO-01</strong></div>
                  <div className="event-meta">Rede • Normalizado • Prioridade 2</div>
                </div>
                <Zap size={16} className="event-action-icon" />
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Infrastructure Overview */}
      <div className="radar-infra-row">
        <div className="infra-card">
          <Server size={24} />
          <div>
            <h4>156 Servidores</h4>
            <p>152 Online • 4 Alertas</p>
          </div>
        </div>
        <div className="infra-card">
          <Database size={24} />
          <div>
            <h4>12 Clusters</h4>
            <p>Em conformidade</p>
          </div>
        </div>
        <div className="infra-card">
          <Globe size={24} />
          <div>
            <h4>5 Regiões</h4>
            <p>Latência global: 45ms</p>
          </div>
        </div>
      </div>
    </div>
  );
}
