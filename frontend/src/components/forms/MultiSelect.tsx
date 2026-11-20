import React, { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ChevronDown } from 'lucide-react';
import './MultiSelect.css';

interface Opcao {
  value: string;
  label: string;
}

interface MultiSelectProps {
  label: string;
  options: Opcao[];
  /** Selecionados. Vazio = "Todos". */
  value: string[];
  onChange: (value: string[]) => void;
  /** Mostra campo de busca (listas longas, ex.: SKUs). */
  searchable?: boolean;
}

/** Seleção múltipla com "Todos" quando nada está marcado. Só tokens/classes ui-*. */
const MultiSelect: React.FC<MultiSelectProps> = ({ label, options, value, onChange, searchable }) => {
  const id = useId();
  const [open, setOpen] = useState(false);
  const [busca, setBusca] = useState('');
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const fechar = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false);
    document.addEventListener('mousedown', fechar);
    document.addEventListener('keydown', esc);
    return () => {
      document.removeEventListener('mousedown', fechar);
      document.removeEventListener('keydown', esc);
    };
  }, [open]);

  const visiveis = useMemo(() => {
    const termo = busca.trim().toLocaleLowerCase('pt-BR');
    return termo ? options.filter((o) => o.label.toLocaleLowerCase('pt-BR').includes(termo)) : options;
  }, [options, busca]);

  const selecionados = new Set(value);
  const resumo =
    value.length === 0
      ? 'Todos'
      : value.length === 1
        ? options.find((o) => o.value === value[0])?.label ?? value[0]
        : `${value.length} selecionados`;

  const alternar = (v: string) =>
    onChange(selecionados.has(v) ? value.filter((x) => x !== v) : [...value, v]);

  return (
    <div className="ui-field multiselect" ref={ref}>
      <label className="ui-label" htmlFor={id}>{label}</label>
      <button
        id={id}
        type="button"
        className="ui-select multiselect-trigger"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="multiselect-summary">{resumo}</span>
        <ChevronDown size={14} />
      </button>

      {open && (
        <div className="multiselect-panel" role="listbox" aria-multiselectable="true">
          {searchable && (
            <input
              className="ui-input multiselect-search"
              placeholder="Buscar..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              autoFocus
            />
          )}
          <div className="multiselect-actions">
            <button type="button" className="ui-btn ui-btn-ghost" onClick={() => onChange([])}>Todos</button>
            {visiveis.length > 0 && visiveis.length < options.length && (
              <button
                type="button"
                className="ui-btn ui-btn-ghost"
                onClick={() => onChange(Array.from(new Set([...value, ...visiveis.map((o) => o.value)])))}
              >
                Marcar visíveis
              </button>
            )}
          </div>
          <div className="multiselect-options">
            {visiveis.length === 0 && <div className="multiselect-empty">Nenhuma opção</div>}
            {visiveis.map((o) => (
              <label key={o.value} className="multiselect-option">
                <input type="checkbox" checked={selecionados.has(o.value)} onChange={() => alternar(o.value)} />
                <span>{o.label}</span>
              </label>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default MultiSelect;
