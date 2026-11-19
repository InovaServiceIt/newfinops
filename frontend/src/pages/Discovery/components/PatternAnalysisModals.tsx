import React from 'react';
import { AlertTriangle, CheckCircle, XCircle } from 'lucide-react';
import { PatternAnalysisStatus, ProgressJob } from '../../../services/patternAnalysisService';

// shared modal wrapper
const ModalOverlay: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <div className="pattern-modal-overlay">
    <div className="pattern-modal">
      {children}
    </div>
  </div>
);

interface BlockedModalProps {
  status: PatternAnalysisStatus;
  onClose: () => void;
  onGoToApproval: () => void;
  onForceRun: () => void;
  isAdmin: boolean;
}

export const PatternBlockedModal: React.FC<BlockedModalProps> = ({ status, onClose, onGoToApproval, onForceRun, isAdmin }) => {
  return (
    <ModalOverlay>
      <div className="pattern-modal-header warning">
        <AlertTriangle size={24} />
        <h3>Análise já realizada recentemente</h3>
      </div>
      <div className="pattern-modal-body">
        <p>Última execução: {status.ultimaExecucao ? new Date(status.ultimaExecucao).toLocaleDateString() : 'Desconhecida'}</p>
        
        <div className="pattern-stat mt-4">
          <span>Clusters encontrados:</span>
          <strong>{status.qtClusters}</strong>
        </div>
        <div className="pattern-stat">
          <span>Sugestões geradas:</span>
          <strong>{status.qtSugestoes}</strong>
        </div>
        <div className="pattern-stat">
          <span>Aguardando sua aprovação:</span>
          <strong style={{ color: '#fbbf24' }}>{status.qtPendentes}</strong>
        </div>
        
        <p className="mt-4 text-center">Próxima execução disponível em: <strong>{status.diasRestantes} dias</strong></p>
      </div>
      <div className="pattern-modal-footer">
        <button className="btn-secondary" onClick={onClose}>Fechar</button>
        <button className="btn-primary" onClick={onGoToApproval}>Ir para Approval</button>
      </div>
      {isAdmin && (
        <div className="mt-4 text-center">
          <button className="btn-force" onClick={onForceRun}>
            Forçar nova execução mesmo assim &rarr;
          </button>
        </div>
      )}
    </ModalOverlay>
  );
};

interface SuccessModalProps {
  result: ProgressJob;
  discoveryResult?: { inseridas: number; ignoradas: number; totalView: number };
  onClose: () => void;
  onGoToApproval: () => void;
}

export const PatternSuccessModal: React.FC<SuccessModalProps> = ({ result, discoveryResult, onClose, onGoToApproval }) => {
  const hasSuggestions = (result.qtSugestoes || 0) > 0;

  return (
    <ModalOverlay>
      <div className="pattern-modal-header success">
        <CheckCircle size={24} />
        <h3>Análise concluída</h3>
      </div>
      <div className="pattern-modal-body">
        <div className="pattern-stat">
          <span>Alertas analisados:</span>
          <strong>{result.qtAlertas}</strong>
        </div>
        <div className="pattern-stat">
          <span>Clusters identificados:</span>
          <strong>{result.qtClusters}</strong>
        </div>
        <div className="pattern-stat">
          <span>Novas sugestões geradas:</span>
          <strong style={{ color: '#4ade80' }}>{result.qtSugestoes}</strong>
        </div>

        {discoveryResult && (
          <div className="pattern-stat" style={{ borderLeft: '3px solid #3b82f6', background: '#eff6ff' }}>
            <span>Oportunidades Discovery:</span>
            <strong style={{ color: '#3b82f6' }}>+{discoveryResult.inseridas} novas</strong>
          </div>
        )}

        {hasSuggestions ? (
           <p className="mt-4 text-center">
             <strong>{result.qtSugestoes}</strong> sugestões de regras e <strong>{discoveryResult?.inseridas || 0}</strong> oportunidades identificadas.
           </p>
        ) : (
           <p className="mt-4 text-center">
             Análise concluída. {discoveryResult?.inseridas || 0} novas oportunidades de automação encontradas.
           </p>
        )}
      </div>
      <div className="pattern-modal-footer">
        <button className="btn-secondary" onClick={onClose}>Fechar</button>
        {hasSuggestions && (
          <button className="btn-primary" onClick={onGoToApproval}>Ir para Approval</button>
        )}
      </div>
    </ModalOverlay>
  );
};

interface ErrorModalProps {
  errorMsg: string;
  onClose: () => void;
  onRetry: () => void;
}

export const PatternErrorModal: React.FC<ErrorModalProps> = ({ errorMsg, onClose, onRetry }) => {
  return (
    <ModalOverlay>
      <div className="pattern-modal-header error">
        <XCircle size={24} />
        <h3>Erro durante a análise</h3>
      </div>
      <div className="pattern-modal-body">
        <p>Ocorreu um erro ao processar os dados na etapa atual.</p>
        <p className="mt-2 text-xs" style={{ color: '#ef4444' }}>{errorMsg}</p>
        <p className="mt-4 text-center">Você pode tentar novamente.</p>
      </div>
      <div className="pattern-modal-footer">
        <button className="btn-secondary" onClick={onClose}>Fechar</button>
        <button className="btn-primary danger" style={{ background: '#ef4444' }} onClick={onRetry}>Tentar novamente</button>
      </div>
    </ModalOverlay>
  );
};
