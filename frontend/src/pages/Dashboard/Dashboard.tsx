import React, { useState } from 'react';
import { 
  Radio, Share, Settings, ShieldCheck, PlayCircle, ShieldAlert,
  Gauge, Zap, Flame, TrendingUp, Monitor,
  PlusCircle, GitBranch, BarChart2, ListOrdered, Database, LayoutDashboard,
  CheckSquare, Lock, MonitorPlay, FileText,
  UserCog, GitMerge, MessageSquare, Library, FlaskConical, GitCommit,
  Users, FileSignature, ListChecks, FileSearch, Server,
  Play, Plug, Ticket, Activity, Save, PieChart
} from 'lucide-react';
import { PatternAnalysisTrigger } from '../Discovery/components/PatternAnalysisTrigger';
import './Dashboard.css';

const modules = [
  {
    id: 'radar',
    title: 'Predict Radar',
    description: 'Alertar o especialista no momento certo',
    icon: <Radio size={32} strokeWidth={1.5} />,
    color: '#E63946'
  },
  {
    id: 'discovery',
    title: 'Predict Discovery',
    description: 'Especialista mapeia oportunidades de automação',
    icon: <Share size={32} strokeWidth={1.5} />,
    color: '#E63946'
  },
  {
    id: 'gate',
    title: 'Predict Gate',
    description: 'Especialista valida riscos antes de agir',
    icon: <ShieldAlert size={32} strokeWidth={1.5} />,
    color: '#E63946'
  },
  {
    id: 'builder',
    title: 'Predict Builder',
    description: 'Especialista codifica seu conhecimento em agentes',
    icon: <Settings size={32} strokeWidth={1.5} />,
    color: '#E63946'
  },
  {
    id: 'approval',
    title: 'Predict Approval',
    description: 'Especialista autoriza a entrada em produção',
    icon: <ShieldCheck size={32} strokeWidth={1.5} />,
    color: '#E63946'
  },
  {
    id: 'action',
    title: 'Predict Action',
    description: 'Especialista executa com velocidade e governança',
    icon: <PlayCircle size={32} strokeWidth={1.5} />,
    color: '#E63946'
  }
];

const subFeaturesRadar = [
  { icon: <Gauge size={24} />, title: 'Painel executivo', desc: 'Visão consolidada de saúde operacional em tempo real', path: '/radar' },
  { icon: <Zap size={24} />, title: 'Eventos em tempo real', desc: 'Monitoramento de fluxo contínuo de eventos', path: '#' },
  { icon: <Flame size={24} />, title: 'Top ofensores', desc: 'Ranking de hosts por criticidade e volume', path: '#' },
  { icon: <TrendingUp size={24} />, title: 'Tendências e recorrências', desc: 'Identificação de padrões sazonais e recorrentes', path: '#' },
  { icon: <Monitor size={24} />, title: 'Perturbações por hostname', desc: 'Status detalhado de alertas por máquina', path: '#' },
];

const subFeaturesDiscovery = [
  { icon: <PlusCircle size={24} />, title: 'Oportunidades de automação', desc: 'Registro e mapeamento de processos candidatos à automação', path: '/discovery/opportunities' },
  { icon: <BarChart2 size={24} />, title: 'Matriz de priorização', desc: 'Avaliação consolidada por ganho, esforço e risco', path: '/discovery/matrix' },
  { icon: <ListOrdered size={24} />, title: 'Priorização de oportunidades', desc: 'Fila consolidada de prioridades para execução', path: '#' },
  { icon: <Database size={24} />, title: 'Estimativa de volumetria', desc: 'Análise estrutural de frequência e impacto esperado', path: '#' },
  { icon: <LayoutDashboard size={24} />, title: 'Painel de oportunidades', desc: 'Visão consolidada do Discovery com filtros avançados', path: '/discovery/panel' },
  { icon: <Library size={24} />, title: 'Catálogo de serviços', desc: 'Repositório de atividades e instruções vetorizadas para o Agente', path: '/discovery/catalog' },
];

const subFeaturesGate = [
  { icon: <ShieldAlert size={24} />, title: 'Avaliação de risco e impacto', desc: 'Análise consolidada de risco operacional e impacto por ativo', path: '#' },
  { icon: <CheckSquare size={24} />, title: 'Checagem de pré-condições', desc: 'Validação de dependências e pré-requisitos antes da execução', path: '#' },
  { icon: <Lock size={24} />, title: 'Políticas e restrições', desc: 'Regras de bloqueio, janelas permitidas e baseline de segurança', path: '#' },
  { icon: <MonitorPlay size={24} />, title: 'Simulação de execução', desc: 'Dry-run e validação antes de agir em produção', path: '#' },
  { icon: <FileText size={24} />, title: 'Parecer e auditoria', desc: 'Registro de justificativas e decisão de prosseguir ou reter', path: '#' },
];

const subFeaturesBuilder = [
  { icon: <UserCog size={24} />, title: 'Gestão de agentes', desc: 'Criação e manutenção de agentes de automação', path: '#' },
  { icon: <Settings size={24} />, title: 'Modelagem de regras', desc: 'Definição de regras operacionais do agente', path: '/builder' },
  { icon: <GitMerge size={24} />, title: 'Modelagem de fluxos', desc: 'Construção de árvores de decisão com entradas e saídas', path: '/decision-rules' },
  { icon: <MessageSquare size={24} />, title: 'Instruções e prompts', desc: 'Parametrização de IA e LLMs para comportamento do agente', path: '#' },
  { icon: <Library size={24} />, title: 'Biblioteca de componentes', desc: 'Componentes reutilizáveis entre agentes', path: '#' },
  { icon: <FlaskConical size={24} />, title: 'Testes de comportamento', desc: 'Simulação e validação do comportamento do agente', path: '#' },
  { icon: <GitCommit size={24} />, title: 'Versionamento e publicação', desc: 'Controle de versões, histórico e disponibilização para homologação', path: '#' },
];

const subFeaturesApproval = [
  { icon: <GitMerge size={24} />, title: 'Workflow de aprovação', desc: 'Fila e gestão de aprovações ativas', path: '/approval' },
  { icon: <Users size={24} />, title: 'Aprovação por perfil', desc: 'Controle de alçadas e permissões por papel', path: '#' },
  { icon: <FileSignature size={24} />, title: 'Parecer por alçada', desc: 'Registro de validação técnica e executiva em visão unificada', path: '#' },
  { icon: <ListChecks size={24} />, title: 'Checklists de prontidão', desc: 'Verificações obrigatórias antes do Go-Live', path: '#' },
  { icon: <FileSearch size={24} />, title: 'Evidências de teste', desc: 'Anexação e validação de provas de homologação', path: '#' },
  { icon: <Server size={24} />, title: 'Liberação e rastreabilidade', desc: 'Aprovação segmentada por ambiente com histórico auditável', path: '#' },
];

// ACTION: 10 → 6 cards (2 fusões, 1 eliminação de "Fila de execução")
const subFeaturesAction = [
  { icon: <Play size={24} />, title: 'Modos de execução', desc: 'Execução assistida, automática ou agendada em um único ponto', path: '#' },
  { icon: <Plug size={24} />, title: 'Conectores e integrações', desc: 'Scripts, APIs e webhooks para comunicação com sistemas externos', path: '#' },
  { icon: <Ticket size={24} />, title: 'Chamados', desc: 'Abertura e atualização de tickets integrada ao ITSM', path: '#' },
  { icon: <Activity size={24} />, title: 'Monitoramento da execução', desc: 'Console e feedback em tempo real da execução', path: '#' },
  { icon: <Save size={24} />, title: 'Registro de resultados', desc: 'Coleta de logs, evidências e rastreabilidade das ações', path: '#' },
  { icon: <PieChart size={24} />, title: 'Painel de produtividade', desc: 'Relatório global de efetividade e performance dos agentes', path: '#' },
];

export default function Dashboard() {
  const [activeModule, setActiveModule] = useState('radar');

  const activeModuleData = modules.find(m => m.id === activeModule);

  const getSubFeatures = () => {
    switch (activeModule) {
      case 'radar': return subFeaturesRadar;
      case 'discovery': return subFeaturesDiscovery;
      case 'gate': return subFeaturesGate;
      case 'builder': return subFeaturesBuilder;
      case 'approval': return subFeaturesApproval;
      case 'action': return subFeaturesAction;
      default: return [];
    }
  };

  const currentFeatures = getSubFeatures();

  return (
    <div className="dashboard-wrapper">
      {/* Main Content Area */}
      <div className="dashboard-container">
        
        {/* Module Cards Row */}
        <div className="modules-row">
          {modules.map(mod => {
            const isActive = activeModule === mod.id;
            return (
              <div 
                key={mod.id} 
                className={`module-tab ${isActive ? 'active' : ''}`}
                onClick={() => setActiveModule(mod.id)}
              >
                <div className={`module-tab-icon ${isActive ? 'active-icon' : ''}`}>
                  {mod.icon}
                </div>
                <h3 className={`module-tab-title ${isActive ? 'active-text' : ''}`}>{mod.title}</h3>
                <p className="module-tab-desc">{mod.description}</p>
              </div>
            );
          })}
        </div>

        {/* Selected Module Detail Panel */}
        <div className="module-details-panel">
          <div className="details-header">
            <div className="details-header-left">
              <div className="details-header-icon-box" style={{ background: `rgba(230, 57, 70, 0.08)` }}>
                {activeModuleData?.icon && React.cloneElement(activeModuleData.icon, { color: activeModuleData.color, size: 24 })}
              </div>
              <div>
                <div className="details-header-label">MÓDULO DE OPERAÇÃO - 10.01-22/05/26 22:35</div>
                <h2 className="details-header-title">{activeModuleData?.title}</h2>
              </div>
            </div>
            
            <div className="details-header-right">
              {activeModule === 'discovery' && (
                <PatternAnalysisTrigger />
              )}
              {activeModule === 'action' && (
                <>
                  <div className="avatars-group">
                    <div className="avatar expert-red">Inc</div>
                    <div className="avatar expert-blue">Inc</div>
                    <div className="avatar expert-green">PDCA</div>
                  </div>
                  <span className="expert-status">Agentes por cenário</span>
                </>
              )}
            </div>
          </div>

          <div className="features-grid">
            {currentFeatures.map((feat, idx) => (
              <a href={feat.path} key={idx} className="feature-item">
                <div className="feature-icon">
                  {feat.icon}
                </div>
                <div>
                  <h4 className="feature-title">{feat.title}</h4>
                  <p className="feature-desc">{feat.desc}</p>
                </div>
              </a>
            ))}
          </div>
        </div>

      </div>
    </div>
  );
}
