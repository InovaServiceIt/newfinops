import React, { ReactNode } from 'react';
import { X } from 'lucide-react';
import './SideFormDrawer.css';

interface SideFormDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
}

export function SideFormDrawer({ isOpen, onClose, title, children, footer }: SideFormDrawerProps) {
  if (!isOpen) return null;

  return (
    <>
      <div className="drawer-overlay" onClick={onClose} />
      <div className={`drawer-content ${isOpen ? 'open' : ''}`}>
        <div className="drawer-header">
          <h2>{title}</h2>
          <button className="drawer-close" onClick={onClose}>
            <X size={24} />
          </button>
        </div>
        <div className="drawer-body">
          {children}
        </div>
        {footer && (
          <div className="drawer-footer">
            {footer}
          </div>
        )}
      </div>
    </>
  );
}
