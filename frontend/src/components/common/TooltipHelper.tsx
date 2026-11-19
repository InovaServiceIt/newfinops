import React, { ReactNode } from 'react';
import { HelpCircle } from 'lucide-react';
import './TooltipHelper.css';

interface TooltipHelperProps {
  content: string;
}

export function TooltipHelper({ content }: TooltipHelperProps) {
  return (
    <div className="tooltip-container">
      <HelpCircle size={14} className="tooltip-icon" />
      <div className="tooltip-content">{content}</div>
    </div>
  );
}
