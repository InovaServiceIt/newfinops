import React, { ReactNode } from 'react';

interface FormSectionProps {
  title: string;
  children: ReactNode;
}

export function FormSection({ title, children }: FormSectionProps) {
  return (
    <div className="form-section">
      <h3 className="form-section-title">{title}</h3>
      {children}
    </div>
  );
}
