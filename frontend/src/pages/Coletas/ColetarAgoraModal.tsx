import React, { useEffect, useState } from 'react';
import { Info } from 'lucide-react';
import Modal from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import { coletarAgora, ROTULO_TIPO, type TipoColeta } from '../../services/coletas';
import type { ContaCloud } from '../../services/contas';

export const AVISO_DADO =
  'Coletar agora traz o dado mais recente que a cloud já publicou. Custos têm atraso da própria fonte ' +
  '(OCI ~24 h; relatórios FOCUS podem atrasar dias). Inventário é praticamente imediato.';

const TIPOS: TipoColeta[] = ['COLETA_COMPLETA', 'COLETA_CUSTO', 'COLETA_INVENTARIO'];

interface Props {
  aberto: boolean;
  /** Contas que podem ser coletadas (credencial ATIVA). Com uma só, ela vem pré-selecionada. */
  contas: ContaCloud[];
  contaInicial?: number | null;
  onFechar: () => void;
  onEnfileirado: (jobId: number) => void;
}

const ColetarAgoraModal: React.FC<Props> = ({ aberto, contas, contaInicial, onFechar, onEnfileirado }) => {
  const [contaId, setContaId] = useState<string>('');
  const [tipo, setTipo] = useState<TipoColeta>('COLETA_COMPLETA');
  const [dias, setDias] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    if (!aberto) return;
    setContaId(contaInicial ? String(contaInicial) : contas.length === 1 ? String(contas[0].id) : '');
    setTipo('COLETA_COMPLETA');
    setDias('');
    setErro(null);
  }, [aberto, contaInicial, contas]);

  const comCusto = tipo !== 'COLETA_INVENTARIO';

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!contaId) { setErro('Selecione a conta.'); return; }
    const costDays = comCusto && dias.trim() ? Number(dias) : null;
    if (costDays !== null && (!Number.isInteger(costDays) || costDays < 30 || costDays > 92)) {
      setErro('Dias de custo: entre 30 e 92.');
      return;
    }
    setOcupado(true);
    setErro(null);
    try {
      const r = await coletarAgora(Number(contaId), tipo, costDays);
      onEnfileirado(r.jobId);
    } catch (err) {
      setErro(mensagemDeErro(err));   // 400/403/409/429 com a mensagem da API
    } finally {
      setOcupado(false);
    }
  };

  return (
    <Modal
      aberto={aberto}
      titulo="Coletar agora"
      onFechar={onFechar}
      rodape={
        <>
          <button type="button" className="ui-btn ui-btn-secondary" onClick={onFechar} disabled={ocupado}>Cancelar</button>
          <button type="submit" form="form-coletar" className="ui-btn ui-btn-primary" disabled={ocupado || contas.length === 0}>
            {ocupado ? 'Enfileirando...' : 'Coletar agora'}
          </button>
        </>
      }
    >
      <form id="form-coletar" className="contas-form" onSubmit={enviar}>
        <div className="ui-alert ui-alert-info"><Info size={16} /><span>{AVISO_DADO}</span></div>

        <div className="ui-field">
          <label className="ui-label" htmlFor="coletar-conta">Conta</label>
          <select id="coletar-conta" className="ui-select" value={contaId} onChange={(e) => setContaId(e.target.value)} required>
            <option value="">Selecione...</option>
            {contas.map((c) => <option key={c.id} value={c.id}>{c.clienteNome} · {c.nome} ({c.provedor})</option>)}
          </select>
          {contas.length === 0 && <span className="contas-ajuda">Nenhuma conta com credencial ativa. Teste a credencial antes de coletar.</span>}
        </div>

        <div className="contas-form-linha">
          <div className="ui-field">
            <label className="ui-label" htmlFor="coletar-tipo">Tipo</label>
            <select id="coletar-tipo" className="ui-select" value={tipo} onChange={(e) => setTipo(e.target.value as TipoColeta)}>
              {TIPOS.map((t) => <option key={t} value={t}>{ROTULO_TIPO[t]}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="coletar-dias">Dias de custo (opcional)</label>
            <input id="coletar-dias" className="ui-input" type="number" min={30} max={92} placeholder="90" disabled={!comCusto}
              value={dias} onChange={(e) => setDias(e.target.value)} />
          </div>
        </div>

        {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      </form>
    </Modal>
  );
};

export default ColetarAgoraModal;
