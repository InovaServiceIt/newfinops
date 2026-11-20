import React, { useEffect } from 'react';
import { X } from 'lucide-react';
import './Modal.css';

interface ModalProps {
  titulo: string;
  aberto: boolean;
  onFechar: () => void;
  children: React.ReactNode;
  rodape?: React.ReactNode;
  largura?: 'sm' | 'md';
}

/** Diálogo modal simples (tokens + ui-*). Fecha com Esc e clique no fundo. */
const Modal: React.FC<ModalProps> = ({ titulo, aberto, onFechar, children, rodape, largura = 'md' }) => {
  useEffect(() => {
    if (!aberto) return;
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && onFechar();
    document.addEventListener('keydown', esc);
    return () => document.removeEventListener('keydown', esc);
  }, [aberto, onFechar]);

  if (!aberto) return null;

  return (
    <div className="cv-modal-fundo" onMouseDown={(e) => e.target === e.currentTarget && onFechar()}>
      <div className={`cv-modal cv-modal-${largura}`} role="dialog" aria-modal="true" aria-label={titulo}>
        <div className="cv-modal-cabecalho">
          <h2>{titulo}</h2>
          <button type="button" className="ui-btn ui-btn-ghost cv-modal-fechar" onClick={onFechar} aria-label="Fechar">
            <X size={18} />
          </button>
        </div>
        <div className="cv-modal-corpo">{children}</div>
        {rodape && <div className="cv-modal-rodape">{rodape}</div>}
      </div>
    </div>
  );
};

export default Modal;

interface ConfirmarProps {
  aberto: boolean;
  titulo: string;
  mensagem: React.ReactNode;
  textoConfirmar: string;
  perigo?: boolean;
  ocupado?: boolean;
  erro?: string | null;
  onConfirmar: () => void;
  onCancelar: () => void;
}

/** Diálogo de confirmação (ex.: ativar/desativar). */
export const Confirmar: React.FC<ConfirmarProps> = ({
  aberto, titulo, mensagem, textoConfirmar, perigo, ocupado, erro, onConfirmar, onCancelar,
}) => (
  <Modal
    aberto={aberto}
    titulo={titulo}
    onFechar={onCancelar}
    largura="sm"
    rodape={
      <>
        <button type="button" className="ui-btn ui-btn-secondary" onClick={onCancelar} disabled={ocupado}>Cancelar</button>
        <button
          type="button"
          className={`ui-btn ${perigo ? 'ui-btn-perigo' : 'ui-btn-primary'}`}
          onClick={onConfirmar}
          disabled={ocupado}
        >
          {ocupado ? 'Salvando...' : textoConfirmar}
        </button>
      </>
    }
  >
    <p className="cv-modal-texto">{mensagem}</p>
    {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
  </Modal>
);
