import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Pencil, Plus, Search } from 'lucide-react';
import Modal, { Confirmar } from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import {
  alterarAtivoCliente, atualizarCliente, criarCliente, impactoDesativacaoCliente, listarClientes,
  type Cliente, type ClienteEntrada, type UsuarioAfetado,
} from '../../services/admin';

const VAZIO: ClienteEntrada = { nome: '', apelido: '', documento: null };

/** Sugestão de apelido a partir do nome: minúsculas, sem acento, hífen no lugar de espaços. */
function sugerirApelido(nome: string): string {
  return nome
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .toLowerCase().trim()
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')
    .slice(0, 60);
}

const ClientesAba: React.FC = () => {
  const [clientes, setClientes] = useState<Cliente[] | null>(null);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [busca, setBusca] = useState('');
  const [mostrarInativos, setMostrarInativos] = useState(true);

  const [editando, setEditando] = useState<Cliente | 'novo' | null>(null);
  const [form, setForm] = useState<ClienteEntrada>(VAZIO);
  const [apelidoTocado, setApelidoTocado] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const [confirmando, setConfirmando] = useState<Cliente | null>(null);
  const [erroConfirmar, setErroConfirmar] = useState<string | null>(null);
  /** Usuários CLIENTE que ficariam sem acesso (null = carregando ou não se aplica). */
  const [afetados, setAfetados] = useState<UsuarioAfetado[] | null>(null);
  const [carregandoImpacto, setCarregandoImpacto] = useState(false);

  const carregar = useCallback(() => {
    setErroLista(null);
    listarClientes(true)
      .then(setClientes)
      .catch((e) => setErroLista(mensagemDeErro(e, 'Não foi possível carregar os clientes.')));
  }, []);

  useEffect(carregar, [carregar]);

  const visiveis = useMemo(() => {
    const termo = busca.trim().toLocaleLowerCase('pt-BR');
    return (clientes ?? [])
      .filter((c) => mostrarInativos || c.ativo)
      .filter((c) => !termo || [c.nome, c.apelido, c.documento ?? ''].some((v) => v.toLocaleLowerCase('pt-BR').includes(termo)));
  }, [clientes, busca, mostrarInativos]);

  const abrir = (c: Cliente | 'novo') => {
    setEditando(c);
    setForm(c === 'novo' ? VAZIO : { nome: c.nome, apelido: c.apelido, documento: c.documento });
    setApelidoTocado(c !== 'novo');
    setErroForm(null);
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    setSalvando(true);
    setErroForm(null);
    const dados = { ...form, documento: form.documento?.trim() ? form.documento.trim() : null };
    try {
      if (editando === 'novo') await criarCliente(dados);
      else if (editando) await atualizarCliente(editando.id, dados);
      setEditando(null);
      carregar();
    } catch (err) {
      setErroForm(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  const pedirConfirmacao = async (c: Cliente) => {
    setErroConfirmar(null);
    setAfetados(null);
    setConfirmando(c);
    if (!c.ativo) return;
    setCarregandoImpacto(true);
    try {
      const r = await impactoDesativacaoCliente(c.id);
      setAfetados(r.usuariosAfetados);
    } catch (err) {
      setErroConfirmar(mensagemDeErro(err, 'Não foi possível verificar os usuários afetados.'));
    } finally {
      setCarregandoImpacto(false);
    }
  };

  const confirmarAtivo = async () => {
    if (!confirmando) return;
    setSalvando(true);
    setErroConfirmar(null);
    try {
      await alterarAtivoCliente(confirmando.id, !confirmando.ativo);
      setConfirmando(null);
      carregar();
    } catch (err) {
      setErroConfirmar(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  return (
    <section className="ui-card cfg-painel">
      <div className="cfg-barra">
        <div className="cfg-busca">
          <Search size={16} />
          <input className="ui-input" placeholder="Buscar por nome, apelido ou documento" value={busca}
            onChange={(e) => setBusca(e.target.value)} aria-label="Buscar clientes" />
        </div>
        <label className="ui-switch">
          <input type="checkbox" checked={mostrarInativos} onChange={(e) => setMostrarInativos(e.target.checked)} />
          <span className="ui-switch-track" />
          Mostrar inativos
        </label>
        <button type="button" className="ui-btn ui-btn-primary" onClick={() => abrir('novo')}>
          <Plus size={16} /> Novo cliente
        </button>
      </div>

      {erroLista && <div className="ui-alert ui-alert-danger" role="alert">{erroLista}</div>}
      {!clientes && !erroLista && <div className="ui-skeleton cfg-esqueleto" />}
      {clientes && visiveis.length === 0 && (
        <div className="ui-empty">{clientes.length === 0 ? 'Nenhum cliente cadastrado.' : 'Nenhum cliente encontrado.'}</div>
      )}
      {clientes && visiveis.length > 0 && (
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr><th>Nome</th><th>Apelido</th><th>Documento</th><th>Situação</th><th className="cfg-col-acoes">Ações</th></tr>
            </thead>
            <tbody>
              {visiveis.map((c) => (
                <tr key={c.id}>
                  <td>{c.nome}</td>
                  <td><code>{c.apelido}</code></td>
                  <td>{c.documento ?? '—'}</td>
                  <td><span className={`ui-badge ${c.ativo ? 'ui-badge-success' : 'ui-badge-warning'}`}>{c.ativo ? 'Ativo' : 'Inativo'}</span></td>
                  <td className="cfg-col-acoes">
                    <button type="button" className="ui-btn ui-btn-ghost" onClick={() => abrir(c)} aria-label={`Editar ${c.nome}`}>
                      <Pencil size={14} /> Editar
                    </button>
                    <button type="button" className="ui-btn ui-btn-ghost" onClick={() => pedirConfirmacao(c)}>
                      {c.ativo ? 'Desativar' : 'Ativar'}
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
        titulo={editando === 'novo' ? 'Novo cliente' : 'Editar cliente'}
        onFechar={() => setEditando(null)}
        rodape={
          <>
            <button type="button" className="ui-btn ui-btn-secondary" onClick={() => setEditando(null)} disabled={salvando}>Cancelar</button>
            <button type="submit" form="cfg-form-cliente" className="ui-btn ui-btn-primary" disabled={salvando}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </>
        }
      >
        <form id="cfg-form-cliente" className="cfg-form" onSubmit={salvar}>
          <div className="ui-field">
            <label className="ui-label" htmlFor="cli-nome">Nome</label>
            <input id="cli-nome" className="ui-input" maxLength={150} required value={form.nome} autoFocus
              onChange={(e) => setForm((f) => ({ ...f, nome: e.target.value, apelido: apelidoTocado ? f.apelido : sugerirApelido(e.target.value) }))} />
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="cli-apelido">Apelido</label>
            <input id="cli-apelido" className="ui-input" maxLength={60} required pattern="[a-z0-9-]+" value={form.apelido}
              onChange={(e) => { setApelidoTocado(true); setForm((f) => ({ ...f, apelido: e.target.value })); }} />
            <span className="cfg-ajuda">Minúsculas sem acento, números e hífen. Ex.: ultragaz</span>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="cli-doc">Documento (opcional)</label>
            <input id="cli-doc" className="ui-input" maxLength={20} inputMode="numeric" pattern="[0-9]*" value={form.documento ?? ''}
              onChange={(e) => setForm((f) => ({ ...f, documento: e.target.value }))} />
            <span className="cfg-ajuda">Somente dígitos (CNPJ/CPF sem pontuação).</span>
          </div>
          {erroForm && <div className="ui-alert ui-alert-danger" role="alert">{erroForm}</div>}
        </form>
      </Modal>

      <Confirmar
        aberto={confirmando !== null}
        titulo={confirmando?.ativo ? 'Desativar cliente' : 'Ativar cliente'}
        mensagem={confirmando?.ativo ? (
          <>
            Desativar <strong>{confirmando?.nome}</strong>? Ele deixa de aparecer para os usuários vinculados.
            {carregandoImpacto && <span className="cfg-impacto-carregando">Verificando usuários afetados...</span>}
            {afetados && afetados.length > 0 && (
              <span className="ui-alert ui-alert-warning cfg-impacto" role="alert">
                <span>
                  <strong>Atenção:</strong> {afetados.length} usuário(s) do perfil Cliente ficarão sem acesso a dados
                  (este é o único cliente deles): {afetados.map((u) => `${u.nome} (${u.email})`).join(', ')}.
                </span>
              </span>
            )}
          </>
        ) : <>Reativar <strong>{confirmando?.nome}</strong>?</>}
        textoConfirmar={confirmando?.ativo ? 'Desativar' : 'Ativar'}
        perigo={confirmando?.ativo}
        ocupado={salvando || carregandoImpacto}
        erro={erroConfirmar}
        onConfirmar={confirmarAtivo}
        onCancelar={() => setConfirmando(null)}
      />
    </section>
  );
};

export default ClientesAba;
