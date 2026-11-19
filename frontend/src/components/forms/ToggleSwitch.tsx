import React from 'react';
import '../common/ui.css';

interface ToggleSwitchProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label?: string;
  disabled?: boolean;
}

export function ToggleSwitch({ checked, onChange, label, disabled = false }: ToggleSwitchProps) {
  return (
    <div 
      className={`toggle-switch-wrapper ${disabled ? 'disabled' : ''}`}
      onClick={() => !disabled && onChange(!checked)}
    >
      <div className={`toggle-switch ${checked ? 'active' : ''}`}>
        <div className="toggle-slider"></div>
      </div>
      {label && <span className="toggle-label">{label}</span>}
    </div>
  );
}
