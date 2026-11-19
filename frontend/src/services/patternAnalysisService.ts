const API_URL = '/api/analise-padroes';

export interface PatternAnalysisStatus {
  podeExecutar: boolean;
  diasDesde: number;
  diasRestantes: number;
  ultimaExecucao?: string;
  qtClusters: number;
  qtSugestoes: number;
  qtPendentes: number;
  bloqueado?: boolean;
  qtAvaliados: number;
  qtFaltaAvaliar: number;
}

export interface ProgressJob {
  etapa: number;
  status: string; // 'iniciando', 'em_andamento', 'concluido', 'erro'
  total: number;
  processados: number;
  erro?: string;
  qtAlertas?: number;
  qtClusters?: number;
  qtSugestoes?: number;
  discoveryResult?: { inseridas: number; ignoradas: number; totalView: number };
  detalheEtapa?: string;
  detalhesPorEtapa: Record<number, string>;
}

export const patternAnalysisService = {
  getStatus: async (): Promise<PatternAnalysisStatus> => {
    const res = await fetch(`${API_URL}/status`);
    if (!res.ok) throw new Error('Falha ao verificar status');
    return res.json();
  },

  executar: async (forcar = false): Promise<{ jobId: string }> => {
    const res = await fetch(`${API_URL}/executar`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ forcar }),
    });
    
    if (!res.ok) {
      if (res.status === 409) {
         return res.json(); // returns blocked status
      }
      throw new Error('Falha ao iniciar execução');
    }
    return res.json();
  },

  getProgresso: async (jobId: string): Promise<ProgressJob> => {
    const res = await fetch(`${API_URL}/progresso/${jobId}`);
    if (!res.ok) throw new Error('Falha ao consultar progresso');
    return res.json();
  },

  getLogs: async (jobId: string): Promise<ProcessLog[]> => {
    const res = await fetch(`${API_URL}/logs/${jobId}`);
    if (!res.ok) throw new Error('Falha ao buscar logs');
    return res.json();
  },

  analisarPadroesDiscovery: async (): Promise<{ inseridas: number; ignoradas: number; totalView: number }> => {
    const res = await fetch('/api/discovery/analisar-padroes', {
      method: 'POST'
    });
    if (!res.ok) throw new Error('Falha ao popular oportunidades');
    return res.json();
  }
};

export interface ProcessLog {
  id: number;
  jobId: string;
  dtLog: string;
  tpLog: string;
  etapa: number;
  alertaId?: number;
  mensagem: string;
}
