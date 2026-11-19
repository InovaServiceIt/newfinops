import axios from 'axios';
import { PredictDiscoveryOpportunity, PredictDiscoveryProcess } from '../types/discovery';

const API_URL = '/api/opportunities';

export const discoveryService = {
    async getOpportunities(params?: { clientCode?: string; status?: string; search?: string }): Promise<PredictDiscoveryOpportunity[]> {
        const response = await axios.get(API_URL, { params });
        return response.data;
    },

    async getOpportunity(id: number): Promise<PredictDiscoveryOpportunity> {
        const response = await axios.get(`${API_URL}/${id}`);
        return response.data;
    },

    async createOpportunity(opportunity: Partial<PredictDiscoveryOpportunity>): Promise<PredictDiscoveryOpportunity> {
        const response = await axios.post(API_URL, opportunity);
        return response.data;
    },

    async updateOpportunity(id: number, opportunity: Partial<PredictDiscoveryOpportunity>): Promise<void> {
        await axios.put(`${API_URL}/${id}`, opportunity);
    },

    async inactivateOpportunity(id: number): Promise<void> {
        await axios.patch(`${API_URL}/${id}/inactivate`);
    },

    async getProcesses(): Promise<PredictDiscoveryProcess[]> {
        const response = await axios.get(`${API_URL}/processes`);
        return response.data;
    }
};
