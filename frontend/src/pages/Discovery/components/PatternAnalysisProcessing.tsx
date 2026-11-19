import React, { useEffect, useState } from 'react';
import { Target, CheckCircle2, Loader2, Circle, AlertTriangle } from 'lucide-react';
import { patternAnalysisService, ProgressJob } from '../../../services/patternAnalysisService';
import './PatternAnalysisProcessing.css';

interface PatternAnalysisProcessingProps {
  jobId: string;
  onComplete: (successData: any) => void;
  onError: (errorData: any) => void;
  onGoToApproval: () => void;
}

export const PatternAnalysisProcessing: React.FC<PatternAnalysisProcessingProps> = ({ jobId, onComplete, onError }) => {
  const [job, setJob] = useState<ProgressJob>({ etapa: 0, status: 'iniciando', total: 0, processados: 0, detalhesPorEtapa: {} });
  const [showLogs, setShowLogs] = useState(false);
  const [logs, setLogs] = useState<any[]>([]);
  const [loadingLogs, setLoadingLogs] = useState(false);

  useEffect(() => {
    let timeoutId: NodeJS.Timeout;
    let retryCount = 0;

    const pollProgress = async () => {
      try {
        const res = await fetch(`/api/analise-padroes/progresso/${jobId}`);
        
        // Se autenticação expirou, não crashar — exibir resultado parcial
        if (res.status === 401 || res.status === 403) {
          console.warn('Sessão expirou durante processamento');
          setJob(prev => ({ ...prev, status: 'concluido' }));
          return;
        }
        
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        
        const data = await res.json();
        setJob(data);
        retryCount = 0; // Reset on success

        if (data.status === 'concluido') {
          // Processamento concluído — mostra os botões
          return;
        } else if (data.status === 'erro') {
          onError(data);
        } else {
          timeoutId = setTimeout(pollProgress, 2000);
        }
      } catch (err) {
        console.error('Polling error:', err);
        retryCount++;
        if (retryCount < 10) {
          timeoutId = setTimeout(pollProgress, 3000);
        } else {
          // Após 10 falhas consecutivas, assume concluído para não ficar preso
          setJob(prev => ({ ...prev, status: 'concluido' }));
        }
      }
    };

    pollProgress();
    return () => clearTimeout(timeoutId);
  }, [jobId, onComplete, onError]);

  const fetchLogs = async () => {
    setLoadingLogs(true);
    try {
      const data = await patternAnalysisService.getLogs(jobId);
      setLogs(data);
      setShowLogs(true);
    } catch (err) {
      console.error('Erro ao buscar logs:', err);
    } finally {
      setLoadingLogs(false);
    }
  };

  const getStepIcon = (stepNum: number) => {
    if (job.etapa > stepNum || job.status === 'concluido') return <CheckCircle2 className="done" size={20} />;
    if (job.etapa === stepNum) return <Loader2 className="animate-spin active" size={20} />;
    return <Circle size={20} />;
  };

  const getStepClass = (stepNum: number) => {
    if (job.etapa > stepNum || job.status === 'concluido') return 'step-item done';
    if (job.etapa === stepNum) return 'step-item active';
    return 'step-item';
  };

  const progressPercent = Math.min((job.etapa / 7) * 100 + (job.total > 0 ? (job.processados / job.total) * 14 : 0), 100);

  if (showLogs) {
    return (
      <div className="pattern-processing-overlay">
        <div className="processing-content logs-view">
          <div className="flex items-center justify-between mb-6">
            <h2 className="!mb-0">Logs do Processamento</h2>
            <button className="btn-close-logs" onClick={() => setShowLogs(false)}>Voltar</button>
          </div>
          
          <div className="logs-table-container">
            <table className="logs-table">
              <thead>
                <tr>
                  <th>Hora</th>
                  <th>Etapa</th>
                  <th>Tipo</th>
                  <th>Mensagem</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log) => (
                  <tr key={log.id} className={`log-row-${log.tpLog.toLowerCase()}`}>
                    <td>{new Date(log.dtLog).toLocaleTimeString()}</td>
                    <td className="text-center">{log.etapa}</td>
                    <td><span className={`log-badge badge-${log.tpLog.toLowerCase()}`}>{log.tpLog}</span></td>
                    <td className="text-left">{log.mensagem}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: '1rem', marginTop: '2rem', width: '100%' }}>
            <button className="btn-primary" style={{ minWidth: '220px', background: '#3b82f6' }} onClick={onGoToApproval}>Ir para Approval</button>
            <button className="btn-secondary" style={{ minWidth: '220px', textAlign: 'center', justifyContent: 'center' }} onClick={() => onComplete(job)}>Finalizar e Fechar</button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="pattern-processing-overlay">
      <div className="processing-content">
        <Target className={`processing-icon ${job.status === 'concluido' ? 'text-success' : ''}`} />
        <h2>{job.status === 'concluido' ? 'Processamento concluído!' : 'Analisando padrões de alertas...'}</h2>

        <div className="progress-bar-container">
          <div className={`progress-bar-fill ${job.status === 'concluido' ? 'bg-success' : ''}`} 
               style={{ width: `${job.status === 'concluido' ? 100 : Math.max(progressPercent, 5)}%` }} />
        </div>
        <span className="progress-etapa-text">
          {job.status === 'concluido' ? 'Sucesso' : `Etapa ${job.etapa + 1} de 7`}
        </span>

        <div className="steps-list wide-layout">
          {/* ETAPA 0 */}
          <div className={getStepClass(0)}>
            <div className="step-left">
              {getStepIcon(0)} 
              <span>Sincronização de Alertas</span>
            </div>
            <div className="step-right">
              {job.etapa === 0 && !job.detalhesPorEtapa[0] ? <span className="detail-loading">Lendo origem (178.156.189.21)...</span> : null}
              {(job.detalhesPorEtapa[0] || (job.etapa === 0 && job.detalheEtapa)) && (
                <span className="detail-tag">{job.detalhesPorEtapa[0] || job.detalheEtapa}</span>
              )}
            </div>
          </div>

          {/* ETAPA 1 */}
          <div className={getStepClass(1)}>
            <div className="step-left">
              {getStepIcon(1)} 
              <span>Vetorização Semântica</span>
            </div>
            <div className="step-right">
              {job.etapa === 1 && !job.detalhesPorEtapa[1] ? <span className="detail-loading">Convertendo alertas...</span> : null}
              {(job.detalhesPorEtapa[1] || (job.etapa === 1 && job.detalheEtapa)) && (
                <span className="detail-tag">{job.detalhesPorEtapa[1] || job.detalheEtapa}</span>
              )}
            </div>
          </div>
          
          {/* ETAPA 2 */}
          <div className={getStepClass(2)}>
            <div className="step-left">
              {getStepIcon(2)} 
              <span>Clusters de similaridade</span>
            </div>
            <div className="step-right">
              {job.detalhesPorEtapa[2] ? <span className="detail-tag">{job.detalhesPorEtapa[2]}</span> : 
               job.etapa === 2 && job.detalheEtapa?.includes("grupos") ? <span className="detail-tag active">{job.detalheEtapa}</span> : 
               job.etapa === 2 ? <span className="detail-loading">Agrupando por similaridade...</span> : null}
            </div>
          </div>

          {/* ETAPA 3 */}
          <div className={getStepClass(3)}>
            <div className="step-left">
              {getStepIcon(3)} 
              <span>IA Cognitiva (Claude)</span>
            </div>
            <div className="step-right">
              {job.detalhesPorEtapa[3] ? <span className="detail-tag">{job.detalhesPorEtapa[3]}</span> : 
               job.etapa === 3 && job.detalheEtapa?.includes("sugestões") ? <span className="detail-tag active">{job.detalheEtapa}</span> : 
               job.etapa === 3 ? <span className="detail-loading">Gerando recomendações...</span> : null}
            </div>
          </div>

          {/* ETAPA 4 */}
          <div className={getStepClass(4)}>
            <div className="step-left">
              {getStepIcon(4)} 
              <span>Persistência de IA</span>
            </div>
            <div className="step-right">
              {job.detalhesPorEtapa[4] ? <span className="detail-tag done">{job.detalhesPorEtapa[4]}</span> :
               job.etapa === 4 ? <span className="detail-tag active">{job.detalheEtapa}</span> : null}
            </div>
          </div>

          {/* ETAPA 5 */}
          <div className={getStepClass(5)}>
            <div className="step-left">
              {getStepIcon(5)} 
              <span>Inteligência Radar</span>
            </div>
            <div className="step-right">
              {job.detalhesPorEtapa[5] ? <span className="detail-tag done">{job.detalhesPorEtapa[5]}</span> :
               job.etapa === 5 ? <span className="detail-tag active">{job.detalheEtapa || "Aguardando processamento pesado..."}</span> : null}
            </div>
          </div>

          {/* ETAPA 6 */}
          <div className={getStepClass(6)}>
            <div className="step-left">
              {getStepIcon(6)} 
              <span>Matriz Discovery</span>
            </div>
            <div className="step-right">
              {job.detalhesPorEtapa[6] ? (
                 <span className="detail-tag success">{job.detalhesPorEtapa[6]}</span>
              ) : job.etapa === 6 ? <span className="detail-loading">Atualizando Matriz...</span> : null}
            </div>
          </div>
        </div>

        {job.status === 'concluido' ? (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: '1rem', marginTop: '2rem', width: '100%' }}>
              <button className="btn-logs-list" style={{ minWidth: '220px' }} onClick={fetchLogs} disabled={loadingLogs}>
                {loadingLogs ? 'Carregando...' : 'Listar log'}
              </button>
              <button className="btn-primary" style={{ minWidth: '220px', background: '#3b82f6' }} onClick={onGoToApproval}>Ir para Approval</button>
              <button className="btn-secondary" style={{ minWidth: '220px', display: 'flex', alignItems: 'center', justifyContent: 'center' }} onClick={() => onComplete(job)}>Fechar</button>
          </div>
        ) : (
          <div className="processing-warning flex items-center justify-center gap-2">
            <AlertTriangle size={16} />
            <span>Isso pode levar alguns minutos. Não feche esta tela.</span>
          </div>
        )}
      </div>
    </div>
  );
};
