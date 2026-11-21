import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useCurrentUser, useEhAdministrador } from '../../components/auth/AuthGuard';
import { Confirmar } from '../../components/common/Modal';
import { HOME_PATH } from '../../navigation/menu';
import { Play, RefreshCw, RotateCcw } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import { mensagemDeErro } from '../../services/api';
import {
  listarColetas, permissaoColeta, reimportarColeta, ROTULO_ORIGEM, ROTULO_TIPO, STATUS_JOB,
  type Coleta, type PaginaColetas, type StatusJob, type TipoJob,
} from '../../services/coletas';
import { listarContas, type ContaCloud } from '../../services/contas';
import ColetarAgoraModal, { AVISO_DADO } from './ColetarAgoraModal';
import DetalhesColeta, { BadgeCusto, temDetalhes } from './DetalhesColeta';
import '../Contas/Contas.css';
import './Coletas.css';

const TAMANHO = 50;
const ATUALIZAR_MS = 10_000;
const dataHora = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
const numero = new Intl.NumberFormat('pt-BR');

const hojeIso = (deslocDias = 0) => {
  const d = new Date();
  d.setDate(d.getDate() + deslocDias);
  return d.toISOString().slice(0, 10);
};

function duracao(seg: number | null): string {
  if (seg === null) return '—';
  const s = Math.round(seg);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}min ${s % 60}s`;
  return `${Math.floor(m / 60)}h ${m % 60}min`;
}

/** "1.248 recursos · 4 regiões · 12 meses de custo · 2 avisos" ou o resultado do teste. */
function resumoLegivel(c: Coleta): string {
  const r = c.resumo ?? {};
  if (typeof r.resultado === 'string') return r.resultado;
  const partes: string[] = [];
  if (typeof r.recursos === 'number') partes.push(`${numero.format(r.recursos)} recursos`);
  if (typeof r.regioes === 'number') partes.push(`${r.regioes} ${r.regioes === 1 ? 'região' : 'regiões'}`);
  if (typeof r.meses_custo === 'number' && r.meses_custo > 0) partes.push(`${r.meses_custo} meses de custo`);
  if (typeof r.erros_nao_fatais === 'number' && r.erros_nao_fatais > 0) partes.push(`${r.erros_nao_fatais} ${r.erros_nao_fatais === 1 ? 'aviso' : 'avisos'}`);
  return partes.join(' · ') || '—';
}

/** Guarda de perfil: Cliente não vê Coletas nesta fase (o backend também exige a política Analista). */
const ColetasPage: React.FC = () => {
  const perfil = useCurrentUser()?.perfil;
  if (perfil !== 'ADMINISTRADOR' && perfil !== 'ANALISTA_FINOPS') return <Navigate to={HOME_PATH} replace />;
  return <ColetasConteudo />;
};

const ColetasConteudo: React.FC = () => {
  const [pagina, setPagina] = useState<PaginaColetas | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [podeColetar, setPodeColetar] = useState(false);
  const [modal, setModal] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const [expandido, setExpandido] = useState<number | null>(null);
  const ehAdmin = useEhAdministrador();
  const [reimportando, setReimportando] = useState<Coleta | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [erroReimportar, setErroReimportar] = useState<string | null>(null);

  const [cliente, setCliente] = useState('');
  const [conta, setConta] = useState('');
  const [status, setStatus] = useState<StatusJob | ''>('');
  const [tipo, setTipo] = useState<TipoJob | ''>('');
  const [de, setDe] = useState(hojeIso(-7));
  const [ate, setAte] = useState(hojeIso(0));
  const [numPagina, setNumPagina] = useState(1);

  useEffect(() => {
    listarContas({ incluirInativas: true }).then(setContas).catch(() => setContas([]));
    permissaoColeta().then((r) => setPodeColetar(r.podeColetar)).catch(() => setPodeColetar(false));
  }, []);

  const carregar = useCallback(() => {
    setErro(null);
    const ateExclusivo = new Date(`${ate}T00:00:00`);
    ateExclusivo.setDate(ateExclusivo.getDate() + 1);
    listarColetas({
      clienteId: cliente ? Number(cliente) : undefined,
      contaId: conta ? Number(conta) : undefined,
      status: status || undefined,
      tipoJob: tipo || undefined,
      de: new Date(`${de}T00:00:00`).toISOString(),
      ate: ateExclusivo.toISOString(),
      pagina: numPagina,
      tamanho: TAMANHO,
    })
      .then(setPagina)
      .catch((e) => setErro(mensagemDeErro(e, 'Não foi possível carregar as coletas.')));
  }, [cliente, conta, status, tipo, de, ate, numPagina]);

  useEffect(carregar, [carregar]);

  const confirmarReimportar = async () => {
    if (!reimportando) return;
    setOcupado(true);
    setErroReimportar(null);
    try {
      await reimportarColeta(reimportando.id);
      setAviso(`Coleta #${reimportando.id} enviada para reimportação. O importador processa em instantes.`);
      setReimportando(null);
      carregar();
    } catch (e) {
      setErroReimportar(mensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  };

  // Atualiza a cada 10 s só enquanto houver job pendente/executando na página.
  const emAndamento = useMemo(() => (pagina?.itens ?? []).some((c) => c.status === 'PENDENTE' || c.status === 'EXECUTANDO'), [pagina]);
  useEffect(() => {
    if (!emAndamento) return;
    const t = window.setInterval(carregar, ATUALIZAR_MS);
    return () => window.clearInterval(t);
  }, [emAndamento, carregar]);

  const clientes = useMemo(() => {
    const m = new Map<number, string>();
    contas.forEach((c) => m.set(c.clienteId, c.clienteNome));
    return Array.from(m, ([id, nome]) => ({ id, nome })).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  }, [contas]);
  const contasDoFiltro = contas.filter((c) => !cliente || String(c.clienteId) === cliente);
  const coletaveis = contas.filter((c) => c.ativo && c.credencialStatus === 'ATIVA');
  const totalPaginas = pagina ? Math.max(1, Math.ceil(pagina.total / TAMANHO)) : 1;

  return (
    <div className="page-content" style={{ maxWidth: 'none' }}>
      <PageHeader
        title="Coletas"
        actions={podeColetar && (
          <button type="button" className="ui-btn ui-btn-primary" onClick={() => setModal(true)}>
            <Play size={16} /> Coletar agora
          </button>
        )}
      />

      <p className="coletas-aviso">{AVISO_DADO}</p>
      {aviso && <div className="ui-alert ui-alert-success">{aviso}</div>}

      <section className="ui-card contas-painel">
        <div className="contas-barra">
          <div className="ui-field">
            <label className="ui-label" htmlFor="col-cliente">Cliente</label>
            <select id="col-cliente" className="ui-select" value={cliente} onChange={(e) => { setCliente(e.target.value); setConta(''); setNumPagina(1); }}>
              <option value="">Todos</option>
              {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="col-conta">Conta</label>
            <select id="col-conta" className="ui-select" value={conta} onChange={(e) => { setConta(e.target.value); setNumPagina(1); }}>
              <option value="">Todas</option>
              {contasDoFiltro.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="col-status">Status</label>
            <select id="col-status" className="ui-select" value={status} onChange={(e) => { setStatus(e.target.value as StatusJob | ''); setNumPagina(1); }}>
              <option value="">Todos</option>
              {(Object.keys(STATUS_JOB) as StatusJob[]).map((s) => <option key={s} value={s}>{STATUS_JOB[s].rotulo}</option>)}
            </select>
          </div>
          <div className="ui-field">
            <label className="ui-label" htmlFor="col-tipo">Tipo</label>
            <select id="col-tipo" className="ui-select" value={tipo} onChange={(e) => { setTipo(e.target.value as TipoJob | ''); setNumPagina(1); }}>
              <option value="">Todos</option>
              {(Object.keys(ROTULO_TIPO) as TipoJob[]).map((t) => <option key={t} value={t}>{ROTULO_TIPO[t]}</option>)}
            </select>
          </div>
          <div className="ui-field coletas-data">
            <label className="ui-label" htmlFor="col-de">De</label>
            <input id="col-de" type="date" className="ui-input" value={de} max={ate} onChange={(e) => { setDe(e.target.value); setNumPagina(1); }} />
          </div>
          <div className="ui-field coletas-data">
            <label className="ui-label" htmlFor="col-ate">Até</label>
            <input id="col-ate" type="date" className="ui-input" value={ate} min={de} onChange={(e) => { setAte(e.target.value); setNumPagina(1); }} />
          </div>
          <div className="contas-acoes">
            <button type="button" className="ui-btn ui-btn-ghost contas-icone" onClick={carregar} title="Atualizar" aria-label="Atualizar">
              <RefreshCw size={16} />
            </button>
          </div>
        </div>

        {emAndamento && <span className="coletas-auto">Atualizando automaticamente a cada 10 s enquanto houver coleta em andamento.</span>}
        {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
        {!pagina && !erro && <div className="ui-skeleton contas-esqueleto" />}
        {pagina && pagina.itens.length === 0 && <div className="ui-empty">Nenhuma coleta no período.</div>}

        {pagina && pagina.itens.length > 0 && (
          <div className="ui-table-wrap">
            <table className="ui-table">
              <thead>
                <tr>
                  <th>Criado em</th><th>Tipo</th><th>Cliente · conta</th><th>Origem</th><th>Status</th>
                  <th className="num">Duração</th><th>Resumo</th>
                </tr>
              </thead>
              <tbody>
                {pagina.itens.map((c) => (
                  <React.Fragment key={c.id}>
                    <tr>
                      <td title={`Job #${c.id}`}>{dataHora.format(new Date(c.criadoEm))}</td>
                      <td>{ROTULO_TIPO[c.tipoJob] ?? c.tipoJob}</td>
                      <td>
                        <span className="coletas-conta">{c.contaNome ?? '—'}</span>
                        <span className="coletas-sub">{c.clienteNome ?? '—'}{c.provedor ? ` · ${c.provedor}` : ''}</span>
                      </td>
                      <td>
                        {ROTULO_ORIGEM[c.origem] ?? c.origem}
                        {c.solicitadoPorNome && <span className="coletas-sub">{c.solicitadoPorNome}</span>}
                      </td>
                      <td>
                        <span className={`ui-badge ${STATUS_JOB[c.status]?.classe ?? 'ui-badge-info'}`}>{STATUS_JOB[c.status]?.rotulo ?? c.status}</span>
                        {c.tentativas > 1 && <span className="coletas-sub">{c.tentativas}ª tentativa</span>}
                      </td>
                      <td className="num">{duracao(c.duracaoSeg)}</td>
                      <td>
                        <span>{resumoLegivel(c)}</span>
                        <BadgeCusto c={c} />
                        {temDetalhes(c) && (
                          <button type="button" className="coletas-erro-botao" onClick={() => setExpandido(expandido === c.id ? null : c.id)}
                            aria-expanded={expandido === c.id}>
                            {expandido === c.id ? 'Ocultar detalhes' : 'Ver detalhes'}
                          </button>
                        )}
                        {ehAdmin && c.podeReimportar && (
                          <button type="button" className="coletas-erro-botao" onClick={() => { setErroReimportar(null); setReimportando(c); }}>
                            <RotateCcw size={12} /> Reimportar
                          </button>
                        )}
                      </td>
                    </tr>
                    {expandido === c.id && temDetalhes(c) && (
                      <tr className="coletas-erro-linha">
                        <td colSpan={7}><DetalhesColeta c={c} /></td>
                      </tr>
                    )}
                  </React.Fragment>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {pagina && pagina.total > TAMANHO && (
          <div className="coletas-paginacao">
            <button type="button" className="ui-btn ui-btn-secondary" disabled={numPagina <= 1} onClick={() => setNumPagina((p) => p - 1)}>Anterior</button>
            <span>Página {numPagina} de {totalPaginas} · {numero.format(pagina.total)} coletas</span>
            <button type="button" className="ui-btn ui-btn-secondary" disabled={numPagina >= totalPaginas} onClick={() => setNumPagina((p) => p + 1)}>Próxima</button>
          </div>
        )}
      </section>

      <Confirmar
        aberto={reimportando !== null}
        titulo="Reimportar coleta"
        mensagem={<>Reimportar a coleta <strong>#{reimportando?.id}</strong> ({reimportando?.contaNome ?? '—'})? O custo e o inventário
          são regravados a partir do JSON guardado, sem nova chamada à cloud. Reimportar de novo não duplica nada.</>}
        textoConfirmar="Reimportar"
        ocupado={ocupado}
        erro={erroReimportar}
        onConfirmar={confirmarReimportar}
        onCancelar={() => setReimportando(null)}
      />

      <ColetarAgoraModal
        aberto={modal}
        contas={coletaveis}
        onFechar={() => setModal(false)}
        onEnfileirado={(jobId) => { setModal(false); setAviso(`Coleta enfileirada (job #${jobId}).`); setNumPagina(1); carregar(); }}
      />
    </div>
  );
};

export default ColetasPage;
