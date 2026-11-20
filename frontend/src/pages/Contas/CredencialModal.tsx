import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Eye, KeyRound, ShieldCheck, Upload } from 'lucide-react';
import Modal from '../../components/common/Modal';
import { useCurrentUser } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import type { ContaCloud } from '../../services/contas';
import {
  alterarAtivoCredencial, auditoriaCredencial, obterCredencial, obterJob, salvarCredencial, testarCredencial,
  type AuditoriaItem, type CredencialMetadados, type StatusJob, type TipoCredencial,
} from '../../services/credencial';
import { ACOES_AUDITORIA, infoTipo, ONBOARDING, STATUS, tiposDoProvedor } from './credenciais';
import { PROVEDORES } from './provedores';

const TAMANHO_MAXIMO_ARQUIVO = 16 * 1024;
const INTERVALO_POLLING_MS = 3000;
const MAX_CONSULTAS = 40; // ~2 minutos
const STATUS_FINAIS: StatusJob[] = ['CONCLUIDO', 'ERRO', 'CANCELADO', 'IMPORTADO'];
const NOME_STATUS_JOB: Record<StatusJob, string> = {
  PENDENTE: 'na fila', EXECUTANDO: 'em execução', COLETADO: 'coletado', IMPORTANDO: 'importando',
  IMPORTADO: 'importado', CONCLUIDO: 'concluído', ERRO: 'falhou', CANCELADO: 'cancelado',
};
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
const formatar = (iso: string | null) => (iso ? dataHora.format(new Date(iso)) : '—');

interface Props {
  conta: ContaCloud | null;
  onFechar: () => void;
  /** Chamado após gravar/desativar, para a lista atualizar o status. */
  onAlterado: () => void;
}

/**
 * Credencial de uma conta. "Mão única": o segredo é digitado, enviado e descartado; nenhuma tela
 * o exibe de volta. Não vai para console, localStorage, query string nem mensagem de erro.
 */
const CredencialModal: React.FC<Props> = ({ conta, onFechar, onAlterado }) => {
  const usuario = useCurrentUser();
  const podeGravar = usuario?.perfil === 'ADMINISTRADOR' || usuario?.perfil === 'CLIENTE';
  const ehAdmin = usuario?.perfil === 'ADMINISTRADOR';

  const [meta, setMeta] = useState<CredencialMetadados | null>(null);
  const [auditoria, setAuditoria] = useState<AuditoriaItem[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const [editando, setEditando] = useState(false);
  const [tipo, setTipo] = useState<TipoCredencial | null>(null);
  const [valores, setValores] = useState<Record<string, string>>({});
  const [mostrando, setMostrando] = useState<string | null>(null);
  const [aceite, setAceite] = useState(false);
  const polling = useRef<number | null>(null);

  const pararPolling = () => {
    if (polling.current !== null) window.clearTimeout(polling.current);
    polling.current = null;
  };

  useEffect(() => pararPolling, []);

  const limparFormulario = () => {
    setValores({});
    setMostrando(null);
    setAceite(false);
  };

  const carregar = useCallback(async (contaId: number) => {
    setErro(null);
    try {
      const [m, a] = await Promise.all([obterCredencial(contaId), auditoriaCredencial(contaId)]);
      setMeta(m);
      setAuditoria(a);
      return m;
    } catch (e) {
      setErro(mensagemDeErro(e, 'Não foi possível carregar a credencial.'));
      return null;
    }
  }, []);

  useEffect(() => {
    pararPolling();
    limparFormulario();
    setAviso(null);
    setMeta(null);
    setAuditoria([]);
    if (!conta) return;
    const tipos = tiposDoProvedor(conta.provedor);
    setTipo(tipos[0]?.tipo ?? null);
    carregar(conta.id).then((m) => setEditando(!!m && !m.existe && podeGravar));
  }, [conta, carregar, podeGravar]);

  if (!conta) return null;

  const fechar = () => {
    pararPolling();
    limparFormulario();
    setEditando(false);
    onFechar();
  };

  const info = infoTipo(tipo);
  const status = STATUS[meta?.existe && meta.status ? meta.status : 'SEM'];

  const carregarArquivo = (campo: string, arquivo: File | undefined) => {
    if (!arquivo) return;
    if (arquivo.size > TAMANHO_MAXIMO_ARQUIVO) {
      setErro('O arquivo excede 16 KB.');
      return;
    }
    const leitor = new FileReader();
    leitor.onload = () => setValores((v) => ({ ...v, [campo]: String(leitor.result ?? '') }));
    leitor.onerror = () => setErro('Não foi possível ler o arquivo.');
    leitor.readAsText(arquivo);
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!tipo || !info) return;
    if (!aceite) {
      setErro('Confirme que a credencial tem somente permissões de leitura.');
      return;
    }
    setOcupado(true);
    setErro(null);
    try {
      const campos: Record<string, string> = {};
      info.campos.forEach((c) => {
        const v = valores[c.nome];
        if (v !== undefined && v !== '') campos[c.nome] = v;
      });
      await salvarCredencial(conta.id, tipo, campos);
      limparFormulario();          // o segredo sai do estado do React imediatamente
      setEditando(false);
      setAviso('Credencial salva. Ela ficará "Pendente de teste" até o coletor validar a conexão.');
      await carregar(conta.id);
      onAlterado();
    } catch (err) {
      setErro(mensagemDeErro(err));   // mensagens da API nunca ecoam valores
    } finally {
      setOcupado(false);
    }
  };

  const testar = async () => {
    setOcupado(true);
    setErro(null);
    try {
      const r = await testarCredencial(conta.id);
      setAviso(`${r.mensagem} (job #${r.jobId}: na fila)`);
      await carregar(conta.id);
      acompanharJob(conta.id, r.jobId, 0);
    } catch (err) {
      setErro(mensagemDeErro(err));
    } finally {
      setOcupado(false);
    }
  };

  /** Acompanha o job de teste por polling até um estado final (ou ~2 min). */
  const acompanharJob = (contaId: number, jobId: number, consultas: number) => {
    polling.current = window.setTimeout(async () => {
      try {
        const job = await obterJob(jobId);
        const final = STATUS_FINAIS.includes(job.status);
        setAviso(`Teste da conexão (job #${jobId}): ${NOME_STATUS_JOB[job.status] ?? job.status}${job.erro ? ` · ${job.erro}` : ''}.`);
        if (final) {
          await carregar(contaId);
          onAlterado();
          return;
        }
        if (consultas + 1 < MAX_CONSULTAS) acompanharJob(contaId, jobId, consultas + 1);
        else setAviso(`O teste (job #${jobId}) continua na fila. Ele aparecerá no histórico quando o coletor terminar.`);
      } catch {
        /* falha transitória no polling: não interrompe a tela */
      }
    }, INTERVALO_POLLING_MS);
  };

  const alternarAtivo = async () => {
    if (!meta) return;
    setOcupado(true);
    setErro(null);
    try {
      await alterarAtivoCredencial(conta.id, meta.status === 'DESATIVADA');
      await carregar(conta.id);
      onAlterado();
    } catch (err) {
      setErro(mensagemDeErro(err));
    } finally {
      setOcupado(false);
    }
  };

  const rodape = editando ? (
    <>
      <button type="button" className="ui-btn ui-btn-secondary" disabled={ocupado}
        onClick={() => { limparFormulario(); setEditando(false); setErro(null); }}>
        Cancelar
      </button>
      <button type="submit" form="form-credencial" className="ui-btn ui-btn-primary" disabled={ocupado || !aceite}>
        {ocupado ? 'Salvando...' : 'Salvar credencial'}
      </button>
    </>
  ) : (
    <>
      {ehAdmin && meta?.existe && (
        <button type="button" className={`ui-btn ${meta.status === 'DESATIVADA' ? 'ui-btn-secondary' : 'ui-btn-perigo'}`}
          disabled={ocupado} onClick={alternarAtivo}>
          {meta.status === 'DESATIVADA' ? 'Reativar' : 'Desativar'}
        </button>
      )}
      {meta?.existe && (
        <button type="button" className="ui-btn ui-btn-secondary" disabled={ocupado || meta.status === 'DESATIVADA'} onClick={testar}>
          Testar conexão
        </button>
      )}
      {podeGravar && (
        <button type="button" className="ui-btn ui-btn-primary" disabled={ocupado}
          onClick={() => { setEditando(true); setAviso(null); setErro(null); }}>
          {meta?.existe ? 'Substituir credencial' : 'Cadastrar credencial'}
        </button>
      )}
    </>
  );

  return (
    <Modal aberto titulo={`Credencial · ${conta.nome}`} onFechar={fechar} rodape={rodape}>
      <div className="cred-cabecalho">
        <span className="contas-provedor">{PROVEDORES[conta.provedor].rotulo}</span>
        <span className="cred-cliente">{conta.clienteNome}</span>
        <span className={`ui-badge ${status.classe}`}>{status.rotulo}</span>
      </div>

      {aviso && <div className="ui-alert ui-alert-info">{aviso}</div>}
      {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      {!meta && !erro && <div className="ui-skeleton cred-esqueleto" />}

      {meta && !editando && (
        meta.existe ? (
          <>
            <dl className="cred-meta">
              <dt>Tipo</dt><dd>{infoTipo(meta.tipo)?.rotulo ?? meta.tipo}</dd>
              <dt>Dica</dt><dd><code>{meta.dica ?? '—'}</code></dd>
              <dt>Último teste</dt><dd>{meta.testadaEm ? `${formatar(meta.testadaEm)}${meta.testeResultado ? ` · ${meta.testeResultado}` : ''}` : 'Nunca testada'}</dd>
              <dt>Cadastrada por</dt><dd>{meta.cadastradaPorNome ?? '—'} · {formatar(meta.cadastradaEm)}</dd>
            </dl>
            <p className="cred-nota"><KeyRound size={14} /> O segredo não pode ser visualizado. Para trocar, use “Substituir credencial”.</p>
          </>
        ) : (
          <div className="ui-empty">
            {podeGravar ? 'Esta conta ainda não tem credencial.' : 'Esta conta ainda não tem credencial. Peça ao administrador ou ao responsável do cliente.'}
          </div>
        )
      )}

      {meta && editando && info && (
        <form id="form-credencial" className="contas-form" onSubmit={salvar} autoComplete="off">
          <div className="ui-alert ui-alert-warning">
            <ShieldCheck size={16} />
            <span>O segredo não poderá ser visualizado depois de salvo.</span>
          </div>

          <details className="cred-onboarding">
            <summary>Como criar uma credencial somente leitura ({PROVEDORES[conta.provedor].rotulo})</summary>
            <ol>{ONBOARDING[conta.provedor].map((passo) => <li key={passo}>{passo}</li>)}</ol>
          </details>

          <div className="ui-field">
            <label className="ui-label" htmlFor="cred-tipo">Tipo</label>
            <select id="cred-tipo" className="ui-select" value={tipo ?? ''}
              onChange={(e) => { setTipo(e.target.value as TipoCredencial); limparFormulario(); }}>
              {tiposDoProvedor(conta.provedor).map((t) => <option key={t.tipo} value={t.tipo}>{t.rotulo}</option>)}
            </select>
          </div>

          {info.campos.map((c) => {
            const id = `cred-${c.nome}`;
            const visivel = mostrando === c.nome;
            return (
              <div className="ui-field" key={c.nome}>
                <label className="ui-label" htmlFor={id}>{c.rotulo}{c.opcional ? ' (opcional)' : ''}</label>
                {c.multilinha ? (
                  <>
                    <textarea id={id} className={`ui-input cred-textarea${c.secreto && !visivel ? ' cred-oculto' : ''}`}
                      required={!c.opcional} spellCheck={false} autoComplete="off" placeholder={c.placeholder}
                      value={valores[c.nome] ?? ''} onChange={(e) => setValores((v) => ({ ...v, [c.nome]: e.target.value }))} />
                    <div className="cred-acoes-campo">
                      <label className="ui-btn ui-btn-ghost cred-arquivo">
                        <Upload size={14} /> Carregar arquivo
                        <input type="file" accept=".pem,.key,.json,.txt"
                          onChange={(e) => { carregarArquivo(c.nome, e.target.files?.[0]); e.target.value = ''; }} />
                      </label>
                      {c.secreto && <BotaoMostrar campo={c.nome} setMostrando={setMostrando} />}
                    </div>
                  </>
                ) : (
                  <div className="cred-linha">
                    <input id={id} className="ui-input" required={!c.opcional} spellCheck={false}
                      type={c.secreto && !visivel ? 'password' : 'text'} autoComplete={c.secreto ? 'new-password' : 'off'}
                      placeholder={c.placeholder} value={valores[c.nome] ?? ''}
                      onChange={(e) => setValores((v) => ({ ...v, [c.nome]: e.target.value }))} />
                    {c.secreto && <BotaoMostrar campo={c.nome} setMostrando={setMostrando} />}
                  </div>
                )}
              </div>
            );
          })}

          <label className="cred-aceite">
            <input type="checkbox" checked={aceite} onChange={(e) => setAceite(e.target.checked)} />
            <span>Confirmo que esta credencial tem <strong>somente permissões de leitura</strong>.</span>
          </label>
        </form>
      )}

      {meta && auditoria.length > 0 && !editando && (
        <section className="cred-historico">
          <h3>Histórico</h3>
          <ul>
            {auditoria.map((a, i) => (
              <li key={`${a.ocorridoEm}-${i}`}>
                <span className="cred-hist-acao">{ACOES_AUDITORIA[a.acao] ?? a.acao}</span>
                <span className="cred-hist-meta">{a.usuario} · {formatar(a.ocorridoEm)}{a.origem === 'COLETOR' ? ' · coletor' : ''}</span>
                {a.detalhe && <span className="cred-hist-detalhe">{a.detalhe}</span>}
              </li>
            ))}
          </ul>
        </section>
      )}
    </Modal>
  );
};

/** "Mostrar" momentâneo: só enquanto o botão estiver pressionado. */
const BotaoMostrar: React.FC<{ campo: string; setMostrando: (c: string | null) => void }> = ({ campo, setMostrando }) => (
  <button
    type="button"
    className="ui-btn ui-btn-ghost cred-mostrar"
    title="Mostrar enquanto pressionado"
    aria-label="Mostrar enquanto pressionado"
    onPointerDown={() => setMostrando(campo)}
    onPointerUp={() => setMostrando(null)}
    onPointerLeave={() => setMostrando(null)}
    onBlur={() => setMostrando(null)}
  >
    <Eye size={16} />
  </button>
);

export default CredencialModal;
