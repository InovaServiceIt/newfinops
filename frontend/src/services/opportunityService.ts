import { PredictDiscoveryOpportunity, PredictDiscoveryProcess } from '../types/opportunity';

const API_BASE_URL = '/api';

export const opportunityService = {
  getAll: async (
    params?: { clientCode?: string; status?: string; search?: string }
  ): Promise<PredictDiscoveryOpportunity[]> => {
    const query = new URLSearchParams();
    if (params?.clientCode) query.append('clientCode', params.clientCode);
    if (params?.status) query.append('status', params.status);
    if (params?.search) query.append('search', params.search);

    const qs = query.toString();
    const url = `${API_BASE_URL}/opportunities${qs ? `?${qs}` : ''}`;

    const response = await fetch(url);
    if (!response.ok) {
      throw new Error('Failed to fetch opportunities');
    }
    return response.json();
  },

  getById: async (id: number): Promise<PredictDiscoveryOpportunity> => {
    const response = await fetch(`${API_BASE_URL}/opportunities/${id}`);
    if (!response.ok) {
      throw new Error(`Failed to fetch opportunity ${id}`);
    }
    return response.json();
  },

  create: async (opportunity: Omit<PredictDiscoveryOpportunity, 'id' | 'createdAt' | 'updatedAt'>): Promise<PredictDiscoveryOpportunity> => {
    const response = await fetch(`${API_BASE_URL}/opportunities`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(opportunity),
    });
    if (!response.ok) {
      throw new Error('Failed to create opportunity');
    }
    return response.json();
  },

  update: async (id: number, opportunity: PredictDiscoveryOpportunity): Promise<void> => {
    const response = await fetch(`${API_BASE_URL}/opportunities/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(opportunity),
    });
    if (!response.ok) {
      throw new Error(`Failed to update opportunity ${id}`);
    }
  },

  inactivate: async (id: number): Promise<void> => {
    const response = await fetch(`${API_BASE_URL}/opportunities/${id}/inactivate`, {
      method: 'PATCH',
    });
    if (!response.ok) {
      throw new Error(`Failed to inactivate opportunity ${id}`);
    }
  },

  getProcesses: async (): Promise<PredictDiscoveryProcess[]> => {
    const response = await fetch(`${API_BASE_URL}/opportunities/processes`);
    if (!response.ok) {
      throw new Error('Failed to fetch processes');
    }
    return response.json();
  }
};
