import React from 'react';
import './ui.css';

interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
  isDestructive?: boolean;
}

export function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmLabel = 'Sim, confirmar',
  cancelLabel = 'Cancelar',
  onConfirm,
  onCancel,
  isDestructive = false
}: ConfirmDialogProps) {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-content">
        <h3 className="modal-title">{title}</h3>
        <p className="modal-body">{message}</p>
        <div className="modal-actions">
          <button className="btn-secondary" onClick={onCancel}>
            {cancelLabel}
          </button>
          <button 
            className="btn-primary" 
            style={isDestructive ? { backgroundColor: 'var(--status-error)' } : {}}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
