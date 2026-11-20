import React, { useEffect, useMemo, useState } from 'react';
import { Plus } from 'lucide-react';
import Modal, { Confirmar } from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import { listarClientes, type Cliente } from '../../services/admin';
import {
  alterarAtivoConta, atualizarConta, criarConta,
  type ContaCloud, type Escopo, type Provedor,
} from '../../services/contas';
import ContasLista from '../Contas/ContasLista';
import { infoEscopo, LISTA_PROVEDORES, PROVEDORES, validarIdentificador } from '../Contas/provedores';
import '../Contas/Contas.css';

interface Formulario {
  clienteId: string;
  provedor: Provedor;
  nome: string;
  identificador: string;
  escopo: Escopo;
  regiaoPadrao: string;
}

const NOVO: Formulario = { clienteId: '', provedor: 'OCI', nome: '', identificador: '', escopo: 'TENANCY', regiaoPadrao: '' };

/** Cadastro de contas de cloud (só ADMINISTRADOR). */
const ContasAba: React.FC = () => {
  const [versao, setVersao] = useState(0);
  const [clientes, setClientes] = useState<Cliente[]>([]);

  const [editando, setEditando] = useState<ContaCloud | 'nova' | null>(null);
  const [form, setForm] = useState<Formulario>(NOVO);
  const [identificadorTocado, setIdentificadorTocado] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const [confirmando, setConfirmando] = useState<ContaCloud | null>(null);
  const [erroConfirmar, setErroConfirmar] = useState<string | null>(null);

  useEffect(() => {
    listarClientes(false).then(setClientes).catch(() => setClientes([]));
  }, [versao]);

  const nova = editando === 'nova';
  const contaEditada: ContaCloud | null = editando === null || editando === 'nova' ? null : editando;
  const escopos = PROVEDORES[form.provedor].escopos;
  const info = infoEscopo(form.provedor, form.escopo);
  const erroIdentificador = useMemo(
    () => (nova && identificadorTocado ? validarIdentificador(form.provedor, form.escopo, form.identificador) : null),
    [nova, identificadorTocado, form.provedor, form.escopo, form.identificador],
  );

  const abrir = (c: ContaCloud | 'nova') => {
    setEditando(c);
    setForm(c === 'nova' ? NOVO : {
      clienteId: String(c.clienteId), provedor: c.provedor, nome: c.nome,
      identificador: c.identificador, escopo: c.escopo, regiaoPadrao: c.regiaoPadrao ?? '',
    });
    setIdentificadorTocado(false);
    setErroForm(null);
  };

  const trocarProvedor = (p: Provedor) =>
    setForm((f) => ({ ...f, provedor: p, escopo: PROVEDORES[p].escopos[0].escopo }));

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    setErroForm(null);
    if (nova) {
      setIdentificadorTocado(true);
      const erroId = validarIdentificador(form.provedor, form.escopo, form.identificador);
      if (erroId) { setErroForm(`Identificador: ${erroId}`); return; }
      if (!form.clienteId) { setErroForm('Cliente: selecione o cliente da conta.'); return; }
    }
    setSalvando(true);
    const regiao = form.regiaoPadrao.trim() || null;
    try {
      if (nova) {
        await criarConta({
          clienteId: Number(form.clienteId), provedor: form.provedor, nome: form.nome,
          identificador: form.identificador.trim(), escopo: form.escopo, regiaoPadrao: regiao,
        });
      } else if (editando) {
        await atualizarConta(editando.id, { nome: form.nome, escopo: form.escopo, regiaoPadrao: regiao });
      }
      setEditando(null);
      setVersao((v) => v + 1);
    } catch (err) {
      setErroForm(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  const confirmarAtivo = async () => {
    if (!confirmando) return;
    setSalvando(true);
    setErroConfirmar(null);
    try {
      await alterarAtivoConta(confirmando.id, !confirmando.ativo);
      setConfirmando(null);
      setVersao((v) => v + 1);
    } catch (err) {
      setErroConfirmar(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  return (
    <>
      <ContasLista
        gerenciavel
        versao={versao}
        onEditar={abrir}
        onAlternarAtivo={(c) => { setErroConfirmar(null); setConfirmando(c); }}
        acoes={
          <button type="button" className="ui-btn ui-btn-primary" onClick={() => abrir('nova')}>
            <Plus size={16} /> Nova conta
          </button>
        }
      />

      <Modal
        aberto={editando !== null}
        titulo={nova ? 'Nova conta de cloud' : 'Editar conta de cloud'}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="form-conta" className="ui-btn ui-btn-primary" disabled={salvando}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        <form id="form-conta" className="contas-form" onSubmit={salvar}>
          <div className="ui-field">
            <label className="ui-label" htmlFor="conta-cliente">Cliente</label>
            {nova ? (
              <select id="conta-cliente" className="ui-select" required value={form.clienteId}
                onChange={(e) => setForm((f) => ({ ...f, clienteId: e.target.value }))}>
                <option value="">Selecione...</option>
                {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
              </select>
            ) : (
              <input id="conta-cliente" className="ui-input" disabled
                value={contaEditada?.clienteNome ?? ''} />
            )}
            {nova && clientes.length === 0 && <span className="contas-ajuda">Cadastre um cliente ativo antes.</span>}
          </div>

          <div className="contas-form-linha">
            <div className="ui-field">
              <label className="ui-label" htmlFor="conta-provedor">Provedor</label>
              <select id="conta-provedor" className="ui-select" value={form.provedor} disabled={!nova}
                onChange={(e) => trocarProvedor(e.target.value as Provedor)}>
                {LISTA_PROVEDORES.map((p) => <option key={p} value={p}>{PROVEDORES[p].rotulo}</option>)}
              </select>
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="conta-escopo">Escopo</label>
              <select id="conta-escopo" className="ui-select" value={form.escopo}
                onChange={(e) => setForm((f) => ({ ...f, escopo: e.target.value as Escopo }))}>
                {escopos.map((s) => <option key={s.escopo} value={s.escopo}>{s.rotulo}</option>)}
              </select>
            </div>
          </div>

          <div className="ui-field">
            <label className="ui-label" htmlFor="conta-identificador">Identificador</label>
            <input id="conta-identificador" className="ui-input contas-id-input" required maxLength={200}
              placeholder={info?.placeholder} value={form.identificador} disabled={!nova}
              aria-invalid={!!erroIdentificador}
              onChange={(e) => { setIdentificadorTocado(true); setForm((f) => ({ ...f, identificador: e.target.value })); }} />
            <span className={`contas-ajuda${erroIdentificador ? ' contas-ajuda-erro' : ''}`}>
              {erroIdentificador ?? (nova ? info?.ajuda : 'Provedor e identificador não podem ser alterados depois de criados.')}
            </span>
          </div>

          <div className="ui-field">
            <label className="ui-label" htmlFor="conta-nome">Nome</label>
            <input id="conta-nome" className="ui-input" required maxLength={150} placeholder="Ex.: OCI Ultragaz principal"
              value={form.nome} onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))} />
          </div>

          <div className="ui-field">
            <label className="ui-label" htmlFor="conta-regiao">Região padrão (opcional)</label>
            <input id="conta-regiao" className="ui-input" maxLength={50} placeholder={`Ex.: ${PROVEDORES[form.provedor].regiaoExemplo}`}
              value={form.regiaoPadrao} onChange={(e) => setForm((f) => ({ ...f, regiaoPadrao: e.target.value }))} />
          </div>

          {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
        </form>
      </Modal>

      <Confirmar
        aberto={confirmando !== null}
        titulo={confirmando?.ativo ? 'Desativar conta' : 'Ativar conta'}
        mensagem={confirmando?.ativo
          ? <>Desativar a conta <strong>{confirmando?.nome}</strong> ({confirmando?.clienteNome})? Ela deixa de ser coletada.</>
          : <>Reativar a conta <strong>{confirmando?.nome}</strong> ({confirmando?.clienteNome})?</>}
        textoConfirmar={confirmando?.ativo ? 'Desativar' : 'Ativar'}
        perigo={confirmando?.ativo}
        ocupado={salvando}
        erro={erroConfirmar}
        onConfirmar={confirmarAtivo}
        onCancelar={() => setConfirmando(null)}
      />
    </>
  );
};

export default ContasAba;
