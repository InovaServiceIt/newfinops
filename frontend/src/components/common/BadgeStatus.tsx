import React from 'react';
import './ui.css';
import { DecisionStatus } from '../../types/decisionRules';
import { ShieldAlert, Info, AlertTriangle, Play, CircleSlash, CheckCircle2 } from 'lucide-react';

interface BadgeStatusProps {
  status: DecisionStatus;
}

export function BadgeStatus({ status }: BadgeStatusProps) {
  const getIcon = () => {
    switch(status) {
      case 'ignored': return <CircleSlash size={14} />;
      case 'monitored': return <Play size={14} />;
      case 'alert': return <AlertTriangle size={14} />;
      case 'incident_candidate': return <Info size={14} />;
      case 'incident_opened': return <ShieldAlert size={14} />;
      case 'closed': return <CheckCircle2 size={14} />;
      default: return null;
    }
  };

  const getLabel = () => {
    switch(status) {
      case 'ignored': return 'Ignored';
      case 'monitored': return 'Monitored';
      case 'alert': return 'Alert';
      case 'incident_candidate': return 'Candidate';
      case 'incident_opened': return 'Incident';
      case 'closed': return 'Closed';
      default: return status;
    }
  };

  return (
    <div className={`badge status-${status}`}>
      {getIcon()}
      <span>{getLabel()}</span>
    </div>
  );
}
