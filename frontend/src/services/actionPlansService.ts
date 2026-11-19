import { ActionPlan, ActionPlansResponse } from '../types/actionPlan';

const API_BASE = '/api';

export interface ActionPlanFilters {
  nomeEntidade?: string;
  prioridade?: string;
  rotuloAcao?: string;
  cdCategoria?: string;
  statusChamado?: string;
}

export const actionPlansService = {
  async getAll(filters: ActionPlanFilters = {}): Promise<ActionPlansResponse> {
    const params = new URLSearchParams();
    if (filters.nomeEntidade) params.append('nomeEntidade', filters.nomeEntidade);
    if (filters.prioridade) params.append('prioridade', filters.prioridade);
    if (filters.rotuloAcao) params.append('rotuloAcao', filters.rotuloAcao);
    if (filters.cdCategoria) params.append('cdCategoria', filters.cdCategoria);
    if (filters.statusChamado) params.append('statusChamado', filters.statusChamado);

    const url = `${API_BASE}/actionplans${params.toString() ? '?' + params.toString() : ''}`;
    const response = await fetch(url);
    if (!response.ok) throw new Error('Erro ao carregar planos de ação');
    return response.json();
  },

  async getById(id: number): Promise<{ plan: ActionPlan; narrative?: any }> {
    const response = await fetch(`${API_BASE}/actionplans/${id}`);
    if (!response.ok) throw new Error('Plano não encontrado');
    return response.json();
  },

  async generateNarrative(id: number): Promise<any> {
    const response = await fetch(`${API_BASE}/actionplans/${id}/narrative`, {
      method: 'POST'
    });
    if (!response.ok) throw new Error('Erro ao gerar narrativa');
    const result = await response.json();
    return result.narrative;
  }
};
