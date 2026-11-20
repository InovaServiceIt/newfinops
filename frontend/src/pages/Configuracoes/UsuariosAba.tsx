import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Pencil, Plus, Search } from 'lucide-react';
import Modal, { Confirmar } from '../../components/common/Modal';
import MultiSelect from '../../components/forms/MultiSelect';
import { NOME_PERFIL, useCurrentUser, type Perfil } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import {
  alterarAtivoUsuario, atualizarUsuario, criarUsuario, listarClientes, listarUsuarios,
  type Cliente, type Usuario, type UsuarioEntrada,
} from '../../services/admin';

const PERFIS: Perfil[] = ['ADMINISTRADOR', 'ANALISTA_FINOPS', 'CLIENTE'];
const VAZIO: UsuarioEntrada = { email: '', nome: '', perfil: 'ANALISTA_FINOPS', clienteIds: [] };

const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
const formatarLogin = (iso: string | null) => (iso ? dataHora.format(new Date(iso)) : 'Nunca');

const classePerfil: Record<Perfil, string> = {
  ADMINISTRADOR: 'ui-badge-accent',
  ANALISTA_FINOPS: 'ui-badge-info',
  CLIENTE: 'ui-badge-success',
};

const UsuariosAba: React.FC = () => {
  const eu = useCurrentUser();
  const [usuarios, setUsuarios] = useState<Usuario[] | null>(null);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [busca, setBusca] = useState('');

  const [editando, setEditando] = useState<Usuario | 'novo' | null>(null);
  const [form, setForm] = useState<UsuarioEntrada>(VAZIO);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const [confirmando, setConfirmando] = useState<Usuario | null>(null);
  const [erroConfirmar, setErroConfirmar] = useState<string | null>(null);

  const carregar = useCallback(() => {
    setErroLista(null);
    Promise.all([listarUsuarios(), listarClientes(false)])
      .then(([u, c]) => { setUsuarios(u); setClientes(c); })
      .catch((e) => setErroLista(mensagemDeErro(e, 'Não foi possível carregar os usuários.')));
  }, []);

  useEffect(carregar, [carregar]);

  const visiveis = useMemo(() => {
    const termo = busca.trim().toLocaleLowerCase('pt-BR');
    return (usuarios ?? []).filter((u) => !termo
      || [u.nome, u.email, ...u.clientes.map((c) => c.nome)].some((v) => v.toLocaleLowerCase('pt-BR').includes(termo)));
  }, [usuarios, busca]);

  // Clientes ativos + os já vinculados (mesmo se inativos) para não sumirem da edição.
  const opcoesClientes = useMemo(() => {
    const mapa = new Map(clientes.map((c) => [c.id, c.nome]));
    if (editando && editando !== 'novo') editando.clientes.forEach((c) => mapa.has(c.id) || mapa.set(c.id, `${c.nome} (inativo)`));
    return Array.from(mapa, ([id, nome]) => ({ value: String(id), label: nome }));
  }, [clientes, editando]);

  const abrir = (u: Usuario | 'novo') => {
    setEditando(u);
    setForm(u === 'novo' ? VAZIO : { email: u.email, nome: u.nome, perfil: u.perfil, clienteIds: u.clientes.map((c) => c.id) });
    setErroForm(null);
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    setSalvando(true);
    setErroForm(null);
    const dados = { ...form, clienteIds: form.perfil === 'ADMINISTRADOR' ? [] : form.clienteIds };
    try {
      if (editando === 'novo') await criarUsuario(dados);
      else if (editando) await atualizarUsuario(editando.id, dados);
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
      await alterarAtivoUsuario(confirmando.id, !confirmando.ativo);
      setConfirmando(null);
      carregar();
    } catch (err) {
      setErroConfirmar(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  const ehEu = (u: Usuario) => eu?.usuarioId === u.id;
  const rotuloClientes = form.perfil === 'CLIENTE' ? 'Cliente(s)' : 'Carteira';

  return (
    <section className="ui-card cfg-painel">
      <div className="cfg-barra">
        <div className="cfg-busca">
          <Search size={16} />
          <input className="ui-input" placeholder="Buscar por nome, e-mail ou cliente" value={busca}
            onChange={(e) => setBusca(e.target.value)} aria-label="Buscar usuários" />
        </div>
        <button type="button" className="ui-btn ui-btn-primary" onClick={() => abrir('novo')}>
          <Plus size={16} /> Novo usuário
        </button>
      </div>

      {erroLista && <div className="ui-alert ui-alert-danger" role="alert">{erroLista}</div>}
      {!usuarios && !erroLista && <div className="ui-skeleton cfg-esqueleto" />}
      {usuarios && visiveis.length === 0 && <div className="ui-empty">Nenhum usuário encontrado.</div>}
      {usuarios && visiveis.length > 0 && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr><th>Nome</th><th>E-mail</th><th>Perfil</th><th>Clientes</th><th>Situação</th><th>Último login</th><th className="cfg-col-acoes">Ações</th></tr>
            </thead>
            <tbody>
              {visiveis.map((u) => (
                <tr key={u.id}>
                  <td>{u.nome}{ehEu(u) && <span className="cfg-voce"> (você)</span>}</td>
                  <td className="cfg-email">{u.email}</td>
                  <td><span className={`ui-badge ${classePerfil[u.perfil]}`}>{NOME_PERFIL[u.perfil]}</span></td>
                  <td className="cfg-clientes">
                    {u.perfil === 'ADMINISTRADOR' ? <span className="cfg-muted">Todos</span>
                      : u.clientes.length === 0 ? <span className="cfg-muted">—</span>
                        : u.clientes.map((c) => c.nome).join(', ')}
                  </td>
                  <td><span className={`ui-badge ${u.ativo ? 'ui-badge-success' : 'ui-badge-warning'}`}>{u.ativo ? 'Ativo' : 'Inativo'}</span></td>
                  <td className="cfg-muted">{formatarLogin(u.ultimoLoginEm)}</td>
                  <td className="cfg-col-acoes">
                    <button type="button" className="ui-btn ui-btn-ghost" onClick={() => abrir(u)} aria-label={`Editar ${u.nome}`}>
                      <Pencil size={14} /> Editar
                    </button>
                    {!ehEu(u) && (
                      <button type="button" className="ui-btn ui-btn-ghost" onClick={() => { setErroConfirmar(null); setConfirmando(u); }}>
                        {u.ativo ? 'Desativar' : 'Ativar'}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <Modal
        aberto={editando !== null}
        titulo={editando === 'novo' ? 'Novo usuário' : 'Editar usuário'}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="cfg-form-usuario" className="ui-btn ui-btn-primary" disabled={salvando}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        <form id="cfg-form-usuario" className="cfg-form" onSubmit={salvar}>
          <div className="ui-field">
            <label className="ui-label" htmlFor="usu-email">E-mail</label>
            <input id="usu-email" className="ui-input" type="email" maxLength={254} required autoFocus={editando === 'novo'}
              value={form.email ?? ''} disabled={editando !== 'novo'}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} />
            {editando !== 'novo' && <span className="cfg-ajuda">O e-mail não pode ser alterado depois de criado.</span>}
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="usu-nome">Nome</label>
            <input id="usu-nome" className="ui-input" maxLength={150} required value={form.nome}
              onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value }))} />
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="usu-perfil">Perfil</label>
            <select id="usu-perfil" className="ui-select" value={form.perfil}
              disabled={editando !== 'novo' && editando !== null && ehEu(editando)}
              onChange={(e) => setForm((f) => ({ ...f, perfil: e.target.value as Perfil }))}>
              {PERFIS.map((p) => <option key={p} value={p}>{NOME_PERFIL[p]}</option>)}
            </select>
            {editando !== 'novo' && editando !== null && ehEu(editando) && (
              <span className="cfg-ajuda">Você não pode alterar o seu próprio perfil.</span>
            )}
          </div>
          {form.perfil !== 'ADMINISTRADOR' && (
            <div>
              <MultiSelect
                label={rotuloClientes}
                options={opcoesClientes}
                value={form.clienteIds.map(String)}
                onChange={(v) => setForm((f) => ({ ...f, clienteIds: v.map(Number) }))}
                searchable
                textoVazio="Nenhum"
              />
              <span className="cfg-ajuda">
                {form.perfil === 'CLIENTE'
                  ? 'Obrigatório: a quais clientes este usuário pertence.'
                  : 'Clientes que este analista acompanha (opcional).'}
              </span>
            </div>
          )}
          {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
        </form>
      </Modal>

      <Confirmar
        aberto={confirmando !== null}
        titulo={confirmando?.ativo ? 'Desativar usuário' : 'Ativar usuário'}
        mensagem={confirmando?.ativo
          ? <>Desativar <strong>{confirmando?.nome}</strong> ({confirmando?.email})? Ele perde o acesso ao CloudVista.</>
          : <>Reativar <strong>{confirmando?.nome}</strong> ({confirmando?.email})?</>}
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

export default UsuariosAba;
