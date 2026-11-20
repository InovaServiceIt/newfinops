import React, { useEffect, useState } from 'react';
import { Pencil, Plus } from 'lucide-react';
import Modal from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import { listarClientes, type Cliente } from '../../services/admin';
import { listarContas, type ContaCloud } from '../../services/contas';
import { atualizarFator, criarFator, listarFatores, type FatorCliente } from '../../services/visaoGeral';
import { formatData } from '../../utils/format';

interface Formulario {
  clienteId: string;
  contaId: string;
  fator: string;
  vigenteDesde: string;
  vigenteAte: string;
}

const NOVO: Formulario = { clienteId: '', contaId: '', fator: '', vigenteDesde: '', vigenteAte: '' };

/** Valida como o backend: > 0, até 9999,999999, no máximo 6 casas, datas coerentes. */
export function validarFator(f: Formulario): string | null {
  if (!f.clienteId) return 'Cliente: selecione o cliente.';
  const texto = f.fator.trim().replace(',', '.');
  if (!/^\d{1,4}(\.\d{1,6})?$/.test(texto) || Number(texto) <= 0)
    return 'Fator: número maior que zero, até 9999,999999 (no máximo 6 casas).';
  if (!f.vigenteDesde) return 'Vigência: informe a data de início.';
  if (f.vigenteAte && f.vigenteAte < f.vigenteDesde) return 'Vigência: a data final deve ser igual ou posterior à inicial.';
  return null;
}

/** Fator de cobrança por cliente (e opcionalmente por conta). Só ADMINISTRADOR. */
const FatorAba: React.FC = () => {
  const [fatores, setFatores] = useState<FatorCliente[] | null>(null);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [filtroCliente, setFiltroCliente] = useState('');

  const [editando, setEditando] = useState<FatorCliente | 'novo' | null>(null);
  const [form, setForm] = useState<Formulario>(NOVO);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const carregar = () => {
    setErroLista(null);
    listarFatores(filtroCliente ? Number(filtroCliente) : null)
      .then(setFatores)
      .catch((e) => { setFatores([]); setErroLista(mensagemDeErro(e)); });
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(carregar, [filtroCliente]);
  useEffect(() => { listarClientes(false).then(setClientes).catch(() => setClientes([])); }, []);
  useEffect(() => {
    if (!form.clienteId) { setContas([]); return; }
    listarContas({ clienteId: Number(form.clienteId) }).then(setContas).catch(() => setContas([]));
  }, [form.clienteId]);

  const abrir = (f: FatorCliente | 'novo') => {
    setEditando(f);
    setErroForm(null);
    setForm(f === 'novo' ? { ...NOVO, clienteId: filtroCliente } : {
      clienteId: String(f.clienteId), contaId: f.cloudContaId ? String(f.cloudContaId) : '',
      fator: String(f.fator).replace('.', ','), vigenteDesde: f.vigenteDesde, vigenteAte: f.vigenteAte ?? '',
    });
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    const erro = validarFator(form);
    if (erro) { setErroForm(erro); return; }
    setSalvando(true);
    setErroForm(null);
    const entrada = {
      clienteId: Number(form.clienteId),
      cloudContaId: form.contaId ? Number(form.contaId) : null,
      fator: Number(form.fator.trim().replace(',', '.')),
      vigenteDesde: form.vigenteDesde,
      vigenteAte: form.vigenteAte || null,
    };
    try {
      if (editando === 'novo') await criarFator(entrada);
      else if (editando) await atualizarFator(editando.id, entrada);
      setEditando(null);
      carregar();
    } catch (err) {
      setErroForm(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  return (
    <section className="ui-card cfg-painel">
      <div className="cfg-barra">
        <select className="ui-select" aria-label="Filtrar por cliente" value={filtroCliente} onChange={(e) => setFiltroCliente(e.target.value)}>
          <option value="">Todos os clientes</option>
          {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
        </select>
        <button type="button" className="ui-btn ui-btn-primary" onClick={() => abrir('novo')}>
          <Plus size={16} /> Novo fator
        </button>
      </div>
      <p className="cfg-ajuda">
        O fator multiplica o custo na Visão Geral ("Ver com fator do cliente"). Sem conta, vale para todas as contas do cliente;
        o fator de uma conta tem precedência sobre o do cliente. Vigências do mesmo cliente/conta não podem se sobrepor.
      </p>

      {erroLista && <div className="ui-alert ui-alert-danger" role="alert">{erroLista}</div>}
      {!fatores && !erroLista && <div className="ui-skeleton cfg-esqueleto" />}
      {fatores && fatores.length === 0 && !erroLista && <div className="ui-empty">Nenhum fator cadastrado.</div>}
      {fatores && fatores.length > 0 && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr><th>Cliente</th><th>Conta</th><th className="num">Fator</th><th>Vigência</th><th>Criado por</th><th className="cfg-col-acoes">Ações</th></tr>
            </thead>
            <tbody>
              {fatores.map((f) => (
                <tr key={f.id}>
                  <td>{f.clienteNome}</td>
                  <td>{f.contaNome ?? <span className="cfg-ajuda">Todas as contas</span>}</td>
                  <td className="num">{f.fator.toLocaleString('pt-BR', { maximumFractionDigits: 6 })}</td>
                  <td>{formatData(f.vigenteDesde)} {f.vigenteAte ? `a ${formatData(f.vigenteAte)}` : 'em diante'}</td>
                  <td>{f.criadoPor ?? '—'}</td>
                  <td className="cfg-col-acoes">
                    <button type="button" className="ui-btn ui-btn-ghost" onClick={() => abrir(f)} aria-label={`Editar fator ${f.id}`}>
                      <Pencil size={14} /> Editar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        aberto={editando !== null}
        titulo={editando === 'novo' ? 'Novo fator do cliente' : 'Editar fator do cliente'}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="cfg-form-fator" className="ui-btn ui-btn-primary" disabled={salvando}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        <form id="cfg-form-fator" className="cfg-form" onSubmit={salvar}>
          <div className="ui-field">
            <label className="ui-label" htmlFor="fator-cliente">Cliente</label>
            <select id="fator-cliente" className="ui-select" required value={form.clienteId} disabled={editando !== 'novo'}
              onChange={(e) => setForm((f) => ({ ...f, clienteId: e.target.value, contaId: '' }))}>
              <option value="">Selecione...</option>
              {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="fator-conta">Conta (opcional)</label>
            <select id="fator-conta" className="ui-select" value={form.contaId}
              onChange={(e) => setForm((f) => ({ ...f, contaId: e.target.value }))}>
              <option value="">Todas as contas do cliente</option>
              {contas.map((c) => <option key={c.id} value={c.id}>{c.provedor} · {c.nome}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="fator-valor">Fator</label>
            <input id="fator-valor" className="ui-input" required inputMode="decimal" placeholder="Ex.: 1,35"
              value={form.fator} onChange={(e) => setForm((f) => ({ ...f, fator: e.target.value }))} />
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="fator-desde">Vigente desde</label>
            <input id="fator-desde" type="date" className="ui-input" required value={form.vigenteDesde}
              onChange={(e) => setForm((f) => ({ ...f, vigenteDesde: e.target.value }))} />
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="fator-ate">Vigente até (opcional)</label>
            <input id="fator-ate" type="date" className="ui-input" value={form.vigenteAte}
              onChange={(e) => setForm((f) => ({ ...f, vigenteAte: e.target.value }))} />
          </div>
          {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
        </form>
      </Modal>
    </section>
  );
};

export default FatorAba;
