import React, { useCallback, useEffect, useState } from 'react';
import { Pencil, Plus, Power } from 'lucide-react';
import Modal, { Confirmar } from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import {
  alterarAtivoAgenda, atualizarAgenda, criarAgenda, listarAgenda, previaCron, ROTULO_TIPO,
  type Agenda, type AgendaEntrada, type PreviaCron, type TipoColeta,
} from '../../services/coletas';
import { listarContas, type ContaCloud } from '../../services/contas';
import '../Contas/Contas.css';

type Alvo = 'todas' | 'cliente' | 'conta';
interface Formulario { nome: string; tipoJob: TipoColeta; alvo: Alvo; clienteId: string; contaId: string; cron: string; fuso: string; costDays: string }
const NOVO: Formulario = { nome: '', tipoJob: 'COLETA_COMPLETA', alvo: 'todas', clienteId: '', contaId: '', cron: '0 0 3 * * ?', fuso: 'America/Sao_Paulo', costDays: '' };
const TIPOS: TipoColeta[] = ['COLETA_COMPLETA', 'COLETA_CUSTO', 'COLETA_INVENTARIO'];
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'full', timeStyle: 'short' });

/** Agenda de coletas (Configurações → Agenda). O worker relê a cada agenda.sincronizar_seg. */
const AgendaAba: React.FC = () => {
  const [itens, setItens] = useState<Agenda[] | null>(null);
  const [atalhos, setAtalhos] = useState<{ rotulo: string; cron: string }[]>([]);
  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [editando, setEditando] = useState<Agenda | 'nova' | null>(null);
  const [form, setForm] = useState<Formulario>(NOVO);
  const [previa, setPrevia] = useState<PreviaCron | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);
  const [confirmando, setConfirmando] = useState<Agenda | null>(null);
  const [erroConfirmar, setErroConfirmar] = useState<string | null>(null);

  const carregar = useCallback(() => {
    setErro(null);
    listarAgenda().then((r) => { setItens(r.itens); setAtalhos(r.atalhos); })
      .catch((e) => setErro(mensagemDeErro(e, 'Não foi possível carregar a agenda.')));
  }, []);

  useEffect(() => {
    carregar();
    listarContas({ incluirInativas: false }).then(setContas).catch(() => setContas([]));
  }, [carregar]);

  // Prévia das próximas 3 execuções (validada pelo Quartz no backend), com pequeno atraso ao digitar.
  useEffect(() => {
    if (editando === null) return;
    const t = window.setTimeout(() => {
      previaCron(form.cron, form.fuso).then(setPrevia).catch(() => setPrevia(null));
    }, 350);
    return () => window.clearTimeout(t);
  }, [form.cron, form.fuso, editando]);

  const clientes = Array.from(new Map(contas.map((c) => [c.clienteId, c.clienteNome])), ([id, nome]) => ({ id, nome }));

  const abrir = (a: Agenda | 'nova') => {
    setEditando(a);
    setErroForm(null);
    setPrevia(null);
    setForm(a === 'nova' ? NOVO : {
      nome: a.nome, tipoJob: a.tipoJob,
      alvo: a.cloudContaId ? 'conta' : a.clienteId ? 'cliente' : 'todas',
      clienteId: a.clienteId ? String(a.clienteId) : '', contaId: a.cloudContaId ? String(a.cloudContaId) : '',
      cron: a.cron, fuso: a.fuso, costDays: a.costDays ? String(a.costDays) : '',
    });
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    setSalvando(true);
    setErroForm(null);
    const dados: AgendaEntrada = {
      nome: form.nome, tipoJob: form.tipoJob,
      clienteId: form.alvo === 'cliente' && form.clienteId ? Number(form.clienteId) : null,
      cloudContaId: form.alvo === 'conta' && form.contaId ? Number(form.contaId) : null,
      cron: form.cron, fuso: form.fuso,
      costDays: form.tipoJob !== 'COLETA_INVENTARIO' && form.costDays ? Number(form.costDays) : null,
    };
    try {
      if (editando === 'nova') await criarAgenda(dados);
      else if (editando) await atualizarAgenda(editando.id, dados);
      setEditando(null);
      carregar();
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
      await alterarAtivoAgenda(confirmando.id, !confirmando.ativo);
      setConfirmando(null);
      carregar();
    } catch (err) {
      setErroConfirmar(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  const alvoTexto = (a: Agenda) => a.contaNome ? `Conta: ${a.contaNome}` : a.clienteNome ? `Cliente: ${a.clienteNome}` : 'Todas as contas';

  return (
    <section className="ui-card contas-painel">
      <div className="contas-barra">
        <p className="contas-ajuda">O worker relê a agenda a cada minuto (parâmetro <code>agenda.sincronizar_seg</code>): não é preciso reiniciar nada.</p>
        <div className="contas-acoes">
          <button type="button" className="ui-btn ui-btn-primary" onClick={() => abrir('nova')}><Plus size={16} /> Nova agenda</button>
        </div>
      </div>

      {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      {!itens && !erro && <div className="ui-skeleton contas-esqueleto" />}
      {itens && itens.length === 0 && <div className="ui-empty">Nenhuma agenda cadastrada.</div>}
      {itens && itens.length > 0 && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead><tr><th>Nome</th><th>Tipo</th><th>Alvo</th><th>Cron</th><th>Fuso</th><th>Situação</th><th>Atualizado por</th><th className="contas-col-acoes">Ações</th></tr></thead>
            <tbody>
              {itens.map((a) => (
                <tr key={a.id}>
                  <td>{a.nome}</td>
                  <td>{ROTULO_TIPO[a.tipoJob]}{a.costDays ? ` · ${a.costDays} dias` : ''}</td>
                  <td>{alvoTexto(a)}</td>
                  <td><code className="contas-id">{a.cron}</code></td>
                  <td>{a.fuso}</td>
                  <td><span className={`ui-badge ${a.ativo ? 'ui-badge-success' : 'ui-badge-warning'}`}>{a.ativo ? 'Ativa' : 'Inativa'}</span></td>
                  <td className="contas-ajuda">{a.atualizadoPor ?? '—'}</td>
                  <td className="contas-col-acoes">
                    <button type="button" className="ui-btn ui-btn-ghost contas-icone" onClick={() => abrir(a)} title="Editar" aria-label={`Editar ${a.nome}`}><Pencil size={16} /></button>
                    <button type="button" className="ui-btn ui-btn-ghost contas-icone" onClick={() => { setErroConfirmar(null); setConfirmando(a); }}
                      title={a.ativo ? 'Desativar' : 'Ativar'} aria-label={`${a.ativo ? 'Desativar' : 'Ativar'} ${a.nome}`}><Power size={16} /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        aberto={editando !== null}
        titulo={editando === 'nova' ? 'Nova agenda' : 'Editar agenda'}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="form-agenda" className="ui-btn ui-btn-primary" disabled={salvando || previa?.valida === false}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        <form id="form-agenda" className="contas-form" onSubmit={salvar}>
          <div className="ui-field">
            <label className="ui-label" htmlFor="ag-nome">Nome</label>
            <input id="ag-nome" className="ui-input" required maxLength={100} placeholder="Ex.: Coleta diária OCI Ultragaz"
              value={form.nome} onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))} />
          </div>
          <div className="contas-form-linha">
            <div className="ui-field">
              <label className="ui-label" htmlFor="ag-tipo">Tipo</label>
              <select id="ag-tipo" className="ui-select" value={form.tipoJob} onChange={(e) => setForm((f) => ({ ...f, tipoJob: e.target.value as TipoColeta }))}>
                {TIPOS.map((t) => <option key={t} value={t}>{ROTULO_TIPO[t]}</option>)}
              </select>
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="ag-dias">Dias de custo (opcional)</label>
              <input id="ag-dias" className="ui-input" type="number" min={30} max={92} placeholder="90" disabled={form.tipoJob === 'COLETA_INVENTARIO'}
                value={form.costDays} onChange={(e) => setForm((f) => ({ ...f, costDays: e.target.value }))} />
            </div>
          </div>
          <div className="contas-form-linha">
            <div className="ui-field">
              <label className="ui-label" htmlFor="ag-alvo">Alvo</label>
              <select id="ag-alvo" className="ui-select" value={form.alvo} onChange={(e) => setForm((f) => ({ ...f, alvo: e.target.value as Alvo }))}>
                <option value="todas">Todas as contas ativas</option>
                <option value="cliente">Um cliente</option>
                <option value="conta">Uma conta</option>
              </select>
            </div>
            {form.alvo === 'cliente' && (
              <div className="ui-field">
                <label className="ui-label" htmlFor="ag-cliente">Cliente</label>
                <select id="ag-cliente" className="ui-select" required value={form.clienteId} onChange={(e) => setForm((f) => ({ ...f, clienteId: e.target.value }))}>
                  <option value="">Selecione...</option>
                  {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            )}
            {form.alvo === 'conta' && (
              <div className="ui-field">
                <label className="ui-label" htmlFor="ag-conta">Conta</label>
                <select id="ag-conta" className="ui-select" required value={form.contaId} onChange={(e) => setForm((f) => ({ ...f, contaId: e.target.value }))}>
                  <option value="">Selecione...</option>
                  {contas.map((c) => <option key={c.id} value={c.id}>{c.clienteNome} · {c.nome}</option>)}
                </select>
              </div>
            )}
          </div>
          <div className="contas-form-linha">
            <div className="ui-field">
              <label className="ui-label" htmlFor="ag-cron">Cron (Quartz, 6–7 campos)</label>
              <input id="ag-cron" className="ui-input contas-id-input" required value={form.cron} spellCheck={false}
                onChange={(e) => setForm((f) => ({ ...f, cron: e.target.value }))} />
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="ag-fuso">Fuso</label>
              <input id="ag-fuso" className="ui-input" required value={form.fuso} onChange={(e) => setForm((f) => ({ ...f, fuso: e.target.value }))} />
            </div>
          </div>
          <div className="agenda-atalhos">
            {atalhos.map((a) => (
              <button key={a.cron} type="button" className="ui-btn ui-btn-secondary" onClick={() => setForm((f) => ({ ...f, cron: a.cron }))}>{a.rotulo}</button>
            ))}
          </div>
          {previa && (previa.valida ? (
            <div className="ui-alert ui-alert-info">
              <span>Próximas execuções ({form.fuso}):<br />{previa.proximas.map((p) => dataHora.format(new Date(p))).join(' · ')}</span>
            </div>
          ) : <div className="ui-alert ui-alert-warning">{previa.motivo}</div>)}
          {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
        </form>
      </Modal>

      <Confirmar
        aberto={confirmando !== null}
        titulo={confirmando?.ativo ? 'Desativar agenda' : 'Ativar agenda'}
        mensagem={<>{confirmando?.ativo ? 'Desativar' : 'Reativar'} a agenda <strong>{confirmando?.nome}</strong>?</>}
        textoConfirmar={confirmando?.ativo ? 'Desativar' : 'Ativar'}
        perigo={confirmando?.ativo}
        ocupado={salvando}
        erro={erroConfirmar}
        onConfirmar={confirmarAtivo}
        onCancelar={() => setConfirmando(null)}
      />
    </section>
  );
};

export default AgendaAba;
