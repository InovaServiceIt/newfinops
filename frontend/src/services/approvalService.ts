import axios from 'axios';

export interface Suggestion {
  id: number;
  idContrato: string;
  nmHost: string;
  qtAlertasCluster: number;
  descricaoRepresentativa: string;
  tpAlvo: string;
  tpOperador: string;
  txPadrao: string;
  rotuloAcao: string;
  cdPrioridade: string;
  txAcaoInicialSuger: string;
  justificativaIa: string;
  status: string;
  dtSugestao: string;
}

const API_URL = '/api/analise-padroes';

export const approvalService = {
  getPendingSuggestions: async (): Promise<Suggestion[]> => {
    const response = await axios.get(`${API_URL}/suggestions`);
    return response.data;
  },

  approve: async (id: number, observation?: string): Promise<void> => {
    await axios.post(`${API_URL}/suggestions/${id}/approve`, { observation });
  },

  reject: async (id: number, observation?: string): Promise<void> => {
    await axios.post(`${API_URL}/suggestions/${id}/reject`, { observation });
  }
};
