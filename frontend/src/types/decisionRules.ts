export type DecisionStatus = 
  | 'ignored' 
  | 'monitored' 
  | 'alert' 
  | 'incident_candidate' 
  | 'incident_opened' 
  | 'closed';

export interface DecisionRule {
  id?: string;
  rule_code: string;
  rule_name: string;
  rule_description?: string;
  
  event_origin?: string;
  event_category?: string;
  event_subcategory?: string;
  environment?: string;
  service_name?: string;
  
  severity_min?: number;
  severity_max?: number;
  event_count_min?: number;
  event_count_window_minutes?: number;
  
  text_contains?: string;
  text_not_contains?: string;
  
  decision_status: DecisionStatus;
  open_incident: boolean;
  close_event: boolean;
  priority_order: number;
  
  is_active: boolean;
  valid_from?: string | null;
  valid_to?: string | null;
  notes?: string;
  
  created_at?: string;
  created_by?: string;
  updated_at?: string;
  updated_by?: string;
}
