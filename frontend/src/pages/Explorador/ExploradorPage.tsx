import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertTriangle, ArrowDown, ArrowUp, Download, X } from 'lucide-react';
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import PageHeader from '../../components/layout/PageHeader';
import { useCurrentUser } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import { listarContas, type ContaCloud } from '../../services/contas';
import {
  buscarValores, consultarExplorador, periodoDoAtalho, ROTULO_DIMENSAO, urlExportarExplorador,
  type Atalho, type ConsultaExplorador, type Dimensao, type Granularidade, type LinhaTabela, type RespostaExplorador,
} from '../../services/explorador';
import { useTokenValues } from '../../theme/useTokenValues';
import { formatMoeda, formatMoedaCompacto } from '../../utils/format';
import PainelRecurso from '../Inventario/PainelRecurso';
import '../Inventario/InventarioPage.css';
import '../VisaoGeral/VisaoGeralPage.css';
import './ExploradorPage.css';

const OUTROS = 'Outros';
const TOKENS = [
  '--chart-s1', '--chart-s2', '--chart-s3', '--chart-s4', '--chart-s5', '--chart-s6', '--chart-s7', '--chart-s8',
  '--chart-outros', '--chart-grid', '--text', '--text-muted', '--bg', '--surface', '--border-strong', '--surface-hover',
] as const;
const SLOTS = TOKENS.slice(0, 8);
const ATALHOS: { id: Atalho; rotulo: string }[] = [
  { id: 'mes', rotulo: 'Mês atual' }, { id: '30d', rotulo: 'Últimos 30 dias' }, { id: '3m', rotulo: '3 meses' },
  { id: '6m', rotulo: '6 meses' }, { id: '12m', rotulo: '12 meses' }, { id: 'custom', rotulo: 'Personalizado' },
];
const DIMENSOES: Dimensao[] = ['SERVICO', 'COMPARTIMENTO', 'RECURSO', 'TAG'];
const dataHora = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
const rotuloPeriodo = (p: string) => (p.length === 7 ? `${MESES[Number(p.slice(5, 7)) - 1]}/${p.slice(2, 4)}` : `${p.slice(8, 10)}/${p.slice(5, 7)}`);
const pct = (v: number) => `${v.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;

type Estado<T> = { status: 'carregando' } | { status: 'ok'; dados: T } | { status: 'erro'; mensagem: string };
type OrdemTabela = 'rotulo' | 'total' | 'pct' | 'variacaoPct';

/** Explorador de custos. A consulta inteira fica na URL (compartilhável). */
const ExploradorPage: React.FC = () => {
  const usuario = useCurrentUser();
  const clientes = useMemo(() => [...(usuario?.clientes ?? [])].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')), [usuario]);
  const podeFator = usuario?.perfil === 'ADMINISTRADOR' || usuario?.perfil === 'ANALISTA_FINOPS';
  const [params, setParams] = useSearchParams();

  const atalho = (params.get('periodo') as Atalho | null) ?? '3m';
  const padrao = periodoDoAtalho(atalho === 'custom' ? '3m' : atalho);
  const consulta: ConsultaExplorador | null = useMemo(() => {
    const clienteId = Number(params.get('cliente')) || clientes[0]?.id;
    if (!clienteId) return null;
    const custom = atalho === 'custom';
    return {
      clienteId,
      contaId: Number(params.get('conta')) || null,
      granularidade: (custom ? (params.get('gran') as Granularidade) : padrao.granularidade) ?? 'MES',
      de: (custom ? params.get('de') : null) ?? padrao.de,
      ate: (custom ? params.get('ate') : null) ?? padrao.ate,
      dimensao: (params.get('dim') as Dimensao) ?? 'SERVICO',
      filtro: (params.get('filtro') ?? '').split('|').filter(Boolean),
      tagChave: params.get('tag') ?? '',
      moeda: params.get('moeda') ?? '',
      comFator: podeFator && params.get('fator') === '1',
      top: Number(params.get('top')) || 10,
    };
  }, [params, clientes, podeFator, atalho, padrao.granularidade, padrao.de, padrao.ate]);
  const modo = params.get('modo') === 'linhas' ? 'linhas' : 'empilhado';

  const atualizar = (m: Record<string, string | null>) => {
    const p = new URLSearchParams(params);
    for (const [k, v] of Object.entries(m)) { if (v === null || v === '') p.delete(k); else p.set(k, v); }
    setParams(p, { replace: true });
  };

  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [tagsDisponiveis, setTagsDisponiveis] = useState<string[]>([]);
  const [resultado, setResultado] = useState<Estado<RespostaExplorador>>({ status: 'carregando' });
  const [ordem, setOrdem] = useState<{ col: OrdemTabela; desc: boolean }>({ col: 'total', desc: true });
  const [aberto, setAberto] = useState<{ contaId: number; chave: string } | null>(null);

  const clienteId = consulta?.clienteId ?? null;
  const contaId = consulta?.contaId ?? null;
  useEffect(() => {
    if (!clienteId) return;
    let vivo = true;
    listarContas({ clienteId }).then((c) => vivo && setContas(c)).catch(() => vivo && setContas([]));
    return () => { vivo = false; };
  }, [clienteId]);

  useEffect(() => {
    if (!clienteId || consulta?.dimensao !== 'TAG') return;
    const ctrl = new AbortController();
    buscarValores(clienteId, contaId, 'TAG', '', '', ctrl.signal).then(setTagsDisponiveis).catch(() => setTagsDisponiveis([]));
    return () => ctrl.abort();
  }, [clienteId, contaId, consulta?.dimensao]);

  const chaveConsulta = consulta ? JSON.stringify(consulta) : '';
  useEffect(() => {
    if (!consulta) return;
    if (consulta.dimensao === 'TAG' && !consulta.tagChave) { setResultado({ status: 'erro', mensagem: 'Escolha a chave da tag para agrupar.' }); return; }
    const ctrl = new AbortController();
    setResultado({ status: 'carregando' });
    consultarExplorador(consulta, ctrl.signal)
      .then((d) => setResultado({ status: 'ok', dados: d }))
      .catch((e) => { if (!ctrl.signal.aborted) setResultado({ status: 'erro', mensagem: mensagemDeErro(e, 'Não foi possível carregar os custos.') }); });
    return () => ctrl.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chaveConsulta]);

  if (!consulta) {
    return (
      <div className="page-content exp-pagina">
        <PageHeader title="Explorador de custos" />
        <div className="ui-card ui-empty">Nenhum cliente vinculado ao seu usuário.</div>
      </div>
    );
  }

  const r = resultado.status === 'ok' ? resultado.dados : null;
  const custom = atalho === 'custom';
  const mudarDimensao = (d: Dimensao) => atualizar({
    dim: d, filtro: null, tag: null,
    ...(d === 'TAG' && consulta.granularidade === 'DIA' ? { periodo: '3m', gran: null, de: null, ate: null } : {}),
  });

  return (
    <div className="page-content exp-pagina">
      <PageHeader
        title="Explorador de custos"
        actions={r && !r.vazio && (
          <a className="ui-btn ui-btn-secondary" href={urlExportarExplorador(consulta)} download>
            <Download size={16} /> Exportar CSV
          </a>
        )}
      />

      <div className="ui-card exp-barra">
        <div className="ui-field">
          <label className="ui-label" htmlFor="exp-cliente">Cliente</label>
          <select id="exp-cliente" className="ui-select" value={consulta.clienteId}
            onChange={(e) => atualizar({ cliente: e.target.value, conta: null, filtro: null, tag: null, moeda: null })}>
            {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="exp-conta">Conta</label>
          <select id="exp-conta" className="ui-select" value={consulta.contaId ?? ''} onChange={(e) => atualizar({ conta: e.target.value || null })}>
            <option value="">Todas as contas</option>
            {contas.map((c) => <option key={c.id} value={c.id}>{c.provedor} · {c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="exp-periodo">Período</label>
          <select id="exp-periodo" className="ui-select" value={atalho}
            onChange={(e) => {
              const a = e.target.value as Atalho;
              if (a === 'custom') atualizar({ periodo: a, gran: consulta.granularidade, de: consulta.de, ate: consulta.ate });
              else atualizar({ periodo: a, gran: null, de: null, ate: null });
            }}>
            {ATALHOS.map((a) => <option key={a.id} value={a.id} disabled={consulta.dimensao === 'TAG' && (a.id === 'mes' || a.id === '30d')}>{a.rotulo}</option>)}
          </select>
        </div>
        {custom && (
          <>
            <div className="ui-field">
              <label className="ui-label" htmlFor="exp-gran">Granularidade</label>
              <select id="exp-gran" className="ui-select" value={consulta.granularidade} onChange={(e) => atualizar({ gran: e.target.value })}>
                <option value="MES">Mensal</option>
                <option value="DIA" disabled={consulta.dimensao === 'TAG'}>Diária</option>
              </select>
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="exp-de">De</label>
              <input id="exp-de" className="ui-input" type={consulta.granularidade === 'MES' ? 'month' : 'date'}
                value={consulta.granularidade === 'MES' ? consulta.de.slice(0, 7) : consulta.de}
                onChange={(e) => atualizar({ de: consulta.granularidade === 'MES' ? `${e.target.value}-01` : e.target.value })} />
            </div>
            <div className="ui-field">
              <label className="ui-label" htmlFor="exp-ate">Até</label>
              <input id="exp-ate" className="ui-input" type={consulta.granularidade === 'MES' ? 'month' : 'date'}
                value={consulta.granularidade === 'MES' ? consulta.ate.slice(0, 7) : consulta.ate}
                onChange={(e) => atualizar({ ate: consulta.granularidade === 'MES' ? `${e.target.value}-01` : e.target.value })} />
            </div>
          </>
        )}
        <div className="ui-field">
          <label className="ui-label" htmlFor="exp-dim">Agrupar por</label>
          <select id="exp-dim" className="ui-select" value={consulta.dimensao} onChange={(e) => mudarDimensao(e.target.value as Dimensao)}>
            {DIMENSOES.map((d) => <option key={d} value={d}>{ROTULO_DIMENSAO[d]}</option>)}
          </select>
        </div>
        {consulta.dimensao === 'TAG' && (
          <div className="ui-field">
            <label className="ui-label" htmlFor="exp-tag">Chave da tag</label>
            <select id="exp-tag" className="ui-select" value={consulta.tagChave} onChange={(e) => atualizar({ tag: e.target.value, filtro: null })}>
              <option value="">Selecione...</option>
              {tagsDisponiveis.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
        )}
        <div className="ui-field">
          <label className="ui-label" htmlFor="exp-top">Top</label>
          <select id="exp-top" className="ui-select" value={consulta.top} onChange={(e) => atualizar({ top: e.target.value })}>
            {[5, 10, 15, 20, 30, 50].map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
        </div>
        {r && r.moedas.length > 1 && (
          <div className="ui-field">
            <label className="ui-label" htmlFor="exp-moeda">Moeda</label>
            <select id="exp-moeda" className="ui-select" value={r.moeda ?? ''} onChange={(e) => atualizar({ moeda: e.target.value })}>
              {r.moedas.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
          </div>
        )}
        {podeFator && (
          <label className="ui-switch exp-fator" aria-disabled={!!r && !r.fatorDisponivel}
            title={r && !r.fatorDisponivel ? 'Sem fator cadastrado para este cliente/conta' : ''}>
            <input type="checkbox" checked={consulta.comFator && (!r || r.fatorDisponivel)} disabled={!!r && !r.fatorDisponivel}
              onChange={(e) => atualizar({ fator: e.target.checked ? '1' : null })} />
            <span className="ui-switch-track" />
            {consulta.comFator ? 'Valor de faturamento (com fator)' : 'Custo de nuvem (sem fator)'}
          </label>
        )}
        <FiltroValores consulta={consulta} onChange={(f) => atualizar({ filtro: f.join('|') || null })} />
      </div>

      {r && r.moedas.length > 1 && (
        <div className="ui-alert ui-alert-warning" role="status">
          <AlertTriangle size={16} /> Este filtro tem custos em {r.moedas.join(', ')}. Mostrando {r.moeda}; valores em outras moedas não somados.
        </div>
      )}
      {resultado.status === 'erro' && <div className="ui-alert ui-alert-danger" role="alert"><AlertTriangle size={16} /> {resultado.mensagem}</div>}
      {resultado.status === 'carregando' && <div className="ui-skeleton inv-esqueleto" />}

      {r && r.vazio && (
        <div className="ui-card ui-empty">
          <span>Ainda não há custo importado para este filtro. Se a coleta de custo falhou por permissão, veja o diagnóstico em <Link to="/coletas">Coletas</Link>.</span>
        </div>
      )}

      {r && !r.vazio && r.moeda && (
        <Resultado r={r} moeda={r.moeda} modo={modo} ordem={ordem} setOrdem={setOrdem}
          onModo={(m) => atualizar({ modo: m === 'linhas' ? 'linhas' : null })}
          onSerie={(k) => k !== OUTROS && atualizar({ filtro: k })}
          onRecurso={(l) => l.contaId && setAberto({ contaId: l.contaId, chave: l.chave })} />
      )}

      {r && r.proveniencia.length > 0 && (
        <p className="inv-proveniencia">
          {r.proveniencia.map((p) => (
            <span key={p.contaId}>
              {p.contaNome ?? `Conta ${p.contaId}`}: custo importado em {p.atualizadoEm ? dataHora.format(new Date(p.atualizadoEm)) : '—'}
              {p.job !== null && <> (job #{p.job})</>}
            </span>
          ))}
        </p>
      )}

      {aberto && <PainelRecurso contaId={aberto.contaId} chave={aberto.chave} onFechar={() => setAberto(null)} />}
    </div>
  );
};

// ── Filtro de valores (autocomplete multi) ──────────────────────────────────
const FiltroValores: React.FC<{ consulta: ConsultaExplorador; onChange: (f: string[]) => void }> = ({ consulta, onChange }) => {
  const [texto, setTexto] = useState('');
  const [sugestoes, setSugestoes] = useState<string[]>([]);
  const [aberta, setAberta] = useState(false);
  const caixa = useRef<HTMLDivElement>(null);
  const desabilitado = consulta.dimensao === 'TAG' && !consulta.tagChave;

  useEffect(() => {
    if (!aberta || desabilitado) return;
    const ctrl = new AbortController();
    const t = window.setTimeout(() => {
      buscarValores(consulta.clienteId, consulta.contaId, consulta.dimensao, consulta.tagChave, texto, ctrl.signal)
        .then(setSugestoes).catch(() => setSugestoes([]));
    }, 250);
    return () => { window.clearTimeout(t); ctrl.abort(); };
  }, [aberta, texto, consulta.clienteId, consulta.contaId, consulta.dimensao, consulta.tagChave, desabilitado]);

  useEffect(() => {
    const fora = (e: MouseEvent) => { if (caixa.current && !caixa.current.contains(e.target as Node)) setAberta(false); };
    document.addEventListener('mousedown', fora);
    return () => document.removeEventListener('mousedown', fora);
  }, []);

  const adicionar = (v: string) => {
    if (consulta.filtro.includes(v) || consulta.filtro.length >= 20) return;
    onChange([...consulta.filtro, v]);
    setTexto('');
  };

  return (
    <div className="ui-field exp-filtro" ref={caixa}>
      <label className="ui-label" htmlFor="exp-filtro">Filtrar {ROTULO_DIMENSAO[consulta.dimensao].toLowerCase()}</label>
      <div className="exp-filtro-caixa">
        {consulta.filtro.map((f) => (
          <span key={f} className="inv-chip exp-chip" title={f}>
            {f}
            <button type="button" aria-label={`Remover ${f}`} onClick={() => onChange(consulta.filtro.filter((x) => x !== f))}><X size={12} /></button>
          </span>
        ))}
        <input id="exp-filtro" className="exp-filtro-input" placeholder={desabilitado ? 'Escolha a chave da tag' : consulta.filtro.length ? '' : 'Todos'}
          value={texto} disabled={desabilitado} maxLength={200} autoComplete="off"
          onFocus={() => setAberta(true)} onChange={(e) => { setTexto(e.target.value); setAberta(true); }}
          onKeyDown={(e) => { if (e.key === 'Enter' && texto.trim()) { e.preventDefault(); adicionar(texto.trim()); } }} />
      </div>
      {aberta && sugestoes.length > 0 && (
        <ul className="exp-sugestoes" role="listbox">
          {sugestoes.filter((s) => !consulta.filtro.includes(s)).map((s) => (
            <li key={s}><button type="button" onClick={() => adicionar(s)} title={s}>{s}</button></li>
          ))}
        </ul>
      )}
    </div>
  );
};

// ── Resultado: KPIs, gráfico e tabela ───────────────────────────────────────
const Resultado: React.FC<{
  r: RespostaExplorador; moeda: string; modo: 'empilhado' | 'linhas';
  ordem: { col: OrdemTabela; desc: boolean }; setOrdem: (o: { col: OrdemTabela; desc: boolean }) => void;
  onModo: (m: 'empilhado' | 'linhas') => void; onSerie: (chave: string) => void; onRecurso: (l: LinhaTabela) => void;
}> = ({ r, moeda, modo, ordem, setOrdem, onModo, onSerie, onRecurso }) => {
  const t = useTokenValues(TOKENS);
  const cores = useMemo(() => new Map(r.grupos.map((g, i) => [g, t[SLOTS[i % SLOTS.length]]])), [r.grupos, t]);
  const cor = (k: string) => (k === OUTROS ? t['--chart-outros'] : r.grupos.indexOf(k) < SLOTS.length ? cores.get(k)! : t['--chart-outros']);
  const rotulos = useMemo(() => new Map(r.tabela.map((l) => [l.chave, l.rotulo])), [r.tabela]);
  const rotulo = (k: string) => rotulos.get(k) ?? k;
  const temOutros = r.serie.some((p) => p.outros !== 0);
  const series = [...r.grupos, ...(temOutros ? [OUTROS] : [])];
  const dados = r.serie.map((p) => ({
    periodo: rotuloPeriodo(p.periodo), total: p.grupos.reduce((s, g) => s + g.valor, 0) + p.outros,
    ...Object.fromEntries(p.grupos.map((g) => [g.chave, g.valor])), ...(temOutros ? { [OUTROS]: p.outros } : {}),
  }));
  const variacao = r.totalPeriodoAnterior ? ((r.total - r.totalPeriodoAnterior) * 100) / Math.abs(r.totalPeriodoAnterior) : null;
  const maior = r.tabela[0];

  const eixo = { fill: t['--text-muted'], fontSize: 12 };
  const tooltip = {
    contentStyle: { background: t['--bg'], border: `1px solid ${t['--border-strong']}`, borderRadius: 8, color: t['--text'], fontSize: 13 },
    labelStyle: { color: t['--text'], fontWeight: 600 }, itemStyle: { color: t['--text'] },
    formatter: (v: number, n: string, item: { payload?: { total?: number } }) => {
      const tot = item?.payload?.total ?? 0;
      return [`${formatMoeda(v, moeda)}${tot ? ` · ${pct((v * 100) / tot)}` : ''}`, rotulo(n)];
    },
  };
  const legenda = { wrapperStyle: { fontSize: 12, paddingTop: 8 }, formatter: (v: string) => <span style={{ color: t['--text-muted'] }}>{rotulo(v)}</span> };

  const linhas = [...r.tabela].sort((a, b) => {
    const va = a[ordem.col] ?? -Infinity;
    const vb = b[ordem.col] ?? -Infinity;
    const c = typeof va === 'string' ? va.localeCompare(String(vb), 'pt-BR') : (va as number) - (vb as number);
    return ordem.desc ? -c : c;
  });
  const ordenar = (col: OrdemTabela) => setOrdem({ col, desc: ordem.col === col ? !ordem.desc : col !== 'rotulo' });
  const Seta = ({ col }: { col: OrdemTabela }) => (ordem.col === col ? (ordem.desc ? <ArrowDown size={12} /> : <ArrowUp size={12} />) : null);

  return (
    <>
      <section className="exp-kpis" aria-label="Indicadores">
        <div className="ui-card vg-kpi vg-kpi-destaque">
          <div className="vg-kpi-titulo"><span>Total do período</span>{r.comFator && <span className="ui-badge ui-badge-info">com fator</span>}</div>
          <div className="vg-kpi-valor">{formatMoeda(r.total, moeda)}</div>
          <div className="vg-kpi-ref">{rotuloPeriodo(r.de)} a {rotuloPeriodo(r.ate)}</div>
        </div>
        <div className="ui-card vg-kpi">
          <div className="vg-kpi-titulo"><span>Vs. período anterior</span></div>
          <div className={`vg-kpi-valor exp-variacao${variacao === null ? '' : variacao > 0 ? ' exp-sobe' : ' exp-desce'}`}>
            {variacao === null ? '—' : <>{variacao > 0 ? <ArrowUp size={18} /> : <ArrowDown size={18} />} {pct(Math.abs(variacao))}</>}
          </div>
          <div className="vg-kpi-ref">{r.totalPeriodoAnterior === null ? 'Sem dado no período anterior' : `antes: ${formatMoeda(r.totalPeriodoAnterior, moeda)}`}</div>
        </div>
        {r.creditos !== null && (
          <div className="ui-card vg-kpi">
            <div className="vg-kpi-titulo"><span>Créditos</span></div>
            <div className="vg-kpi-valor">{formatMoeda(r.creditos, moeda)}</div>
          </div>
        )}
        {maior && (
          <div className="ui-card vg-kpi">
            <div className="vg-kpi-titulo"><span>Maior grupo</span></div>
            <div className="exp-maior" title={maior.chave}>{maior.rotulo}</div>
            <div className="vg-kpi-ref">{formatMoeda(maior.total, moeda)} · {pct(maior.pct)}</div>
          </div>
        )}
      </section>

      <section className="ui-card vg-grafico">
        <div className="vg-grafico-cabecalho">
          <h2 className="ui-card-title">Custo por {ROTULO_DIMENSAO[r.dimensao].toLowerCase()} · {r.granularidade === 'MES' ? 'mensal' : 'diário'}</h2>
          <div className="ui-tabs exp-modo" role="tablist">
            <button type="button" role="tab" className="ui-tab" aria-selected={modo === 'empilhado'} onClick={() => onModo('empilhado')}>Empilhado</button>
            <button type="button" role="tab" className="ui-tab" aria-selected={modo === 'linhas'} onClick={() => onModo('linhas')}>Linhas</button>
          </div>
        </div>
        <ResponsiveContainer width="100%" height={320}>
          {modo === 'empilhado' ? (
            <BarChart data={dados} margin={{ top: 8, right: 16, bottom: 0, left: 8 }}>
              <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
              <XAxis dataKey="periodo" tick={eixo} axisLine={false} tickLine={false} minTickGap={8} />
              <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={(v: number) => formatMoedaCompacto(v, moeda)} width={84} />
              <Tooltip {...tooltip} cursor={{ fill: t['--surface-hover'] }} />
              <Legend {...legenda} />
              {series.map((s, i) => (
                <Bar key={s} dataKey={s} name={s} stackId="e" fill={cor(s)} stroke={t['--surface']} strokeWidth={1} maxBarSize={48}
                  isAnimationActive={false} radius={i === series.length - 1 ? [4, 4, 0, 0] : undefined}
                  cursor={s === OUTROS ? undefined : 'pointer'} onClick={() => onSerie(s)} />
              ))}
            </BarChart>
          ) : (
            <LineChart data={dados} margin={{ top: 8, right: 16, bottom: 0, left: 8 }}>
              <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
              <XAxis dataKey="periodo" tick={eixo} axisLine={false} tickLine={false} minTickGap={8} />
              <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={(v: number) => formatMoedaCompacto(v, moeda)} width={84} />
              <Tooltip {...tooltip} cursor={{ stroke: t['--border-strong'] }} />
              <Legend {...legenda} />
              {series.map((s) => (
                <Line key={s} type="monotone" dataKey={s} name={s} stroke={cor(s)} strokeWidth={2} dot={false}
                  activeDot={{ r: 4, onClick: () => onSerie(s) }} isAnimationActive={false} />
              ))}
            </LineChart>
          )}
        </ResponsiveContainer>
        <p className="vg-nota">Clique numa série para filtrar por ela. "Outros" soma os grupos fora do top {r.grupos.length}.</p>
      </section>

      <section className="ui-card inv-tabela" aria-label="Tabela">
        {r.tabelaTruncada && <div className="ui-alert ui-alert-info">Mostrando os 200 maiores grupos.</div>}
        <div className="ui-table-wrap">
          <table className="ui-table">
            <thead>
              <tr>
                <th><button type="button" className="inv-ordenar" onClick={() => ordenar('rotulo')}>{ROTULO_DIMENSAO[r.dimensao]} <Seta col="rotulo" /></button></th>
                <th className="num"><button type="button" className="inv-ordenar" onClick={() => ordenar('total')}>Total <Seta col="total" /></button></th>
                <th className="num"><button type="button" className="inv-ordenar" onClick={() => ordenar('pct')}>% do total <Seta col="pct" /></button></th>
                <th className="num"><button type="button" className="inv-ordenar" onClick={() => ordenar('variacaoPct')}>Variação <Seta col="variacaoPct" /></button></th>
                <th>Tendência</th>
              </tr>
            </thead>
            <tbody>
              {linhas.map((l) => {
                const clicavel = r.dimensao === 'RECURSO' && l.contaId !== null;
                return (
                  <tr key={l.chave} className={clicavel ? 'inv-linha' : undefined} tabIndex={clicavel ? 0 : undefined}
                    onClick={clicavel ? () => onRecurso(l) : undefined}
                    onKeyDown={clicavel ? (e) => e.key === 'Enter' && onRecurso(l) : undefined}>
                    <td className="exp-grupo" title={l.chave}>
                      <i className="vg-legenda-cor" style={{ background: r.grupos.includes(l.chave) ? cor(l.chave) : t['--chart-outros'] }} />
                      {l.rotulo}
                      {l.rotulo !== l.chave && <span className="exp-chave">{l.chave}</span>}
                    </td>
                    <td className="num">{formatMoeda(l.total, moeda)}</td>
                    <td className="num">{pct(l.pct)}</td>
                    <td className={`num${l.variacaoPct === null ? '' : l.variacaoPct > 0 ? ' exp-sobe' : ' exp-desce'}`}>
                      {l.variacaoPct === null ? '—' : `${l.variacaoPct > 0 ? '+' : ''}${pct(l.variacaoPct)}`}
                    </td>
                    <td><Sparkline valores={l.serie} cor={t['--chart-s1']} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
};

const Sparkline: React.FC<{ valores: number[]; cor: string }> = ({ valores, cor }) => {
  if (valores.length < 2) return <span className="inv-nota">—</span>;
  const max = Math.max(...valores, 0);
  const min = Math.min(...valores, 0);
  const faixa = max - min || 1;
  const pontos = valores.map((v, i) => `${(i / (valores.length - 1)) * 96 + 2},${22 - ((v - min) / faixa) * 20}`).join(' ');
  return (
    <svg className="exp-spark" viewBox="0 0 100 24" width="100" height="24" aria-hidden="true">
      <polyline points={pontos} fill="none" stroke={cor} strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
  );
};

export default ExploradorPage;
