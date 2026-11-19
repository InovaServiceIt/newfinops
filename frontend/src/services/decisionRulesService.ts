import axios from 'axios';
import { DecisionRule } from '../types/decisionRules';

const API_URL = '/api/decision-rules';

// Fallback mock data
const mockData: DecisionRule[] = [
  {
    id: '1',
    rule_code: 'R1_IGNORE_DEPLOYMENT',
    rule_name: 'Ignorar eventos de deployment',
    event_origin: 'Datadog',
    environment: 'prod',
    decision_status: 'ignored',
    open_incident: false,
    close_event: true,
    priority_order: 1,
    is_active: true,
    text_contains: 'deployment started',
    created_at: new Date().toISOString(),
    created_by: 'admin',
  },
  {
    id: '2',
    rule_code: 'R2_HIGH_CPU_INCIDENT',
    rule_name: 'Abertura de Incidente para Alta CPU contínua',
    event_category: 'infrastructure',
    event_subcategory: 'cpu',
    severity_min: 3,
    severity_max: 5,
    event_count_min: 3,
    event_count_window_minutes: 5,
    decision_status: 'incident_candidate',
    open_incident: true,
    close_event: false,
    priority_order: 2,
    is_active: true,
    created_at: new Date().toISOString(),
    created_by: 'admin',
  }
];

export const decisionRulesService = {
  async getRules(): Promise<DecisionRule[]> {
    try {
      const response = await axios.get(API_URL);
      return response.data;
    } catch (err) {
      // fallback para mock se a API falhar
      console.warn('API indisponível, usando mock local. Erro:', err);
      return mockData;
    }
  },
  
  async createRule(data: Omit<DecisionRule, 'id'>): Promise<DecisionRule> {
    const response = await axios.post(API_URL, data);
    return response.data;
  },
  
  async updateRule(id: string, updates: Partial<DecisionRule>): Promise<DecisionRule> {
    const response = await axios.put(`${API_URL}/${id}`, updates);
    return response.data;
  },
  
  async toggleActiveStatus(id: string, isActive: boolean): Promise<void> {
    await axios.patch(`${API_URL}/${id}/status`, { is_active: isActive });
  },

  async deleteRule(id: string): Promise<void> {
    await axios.delete(`${API_URL}/${id}`);
  }
};
