import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { AlertTriangle, Download } from 'lucide-react';
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import PageHeader from '../../components/layout/PageHeader';
import { useCurrentUser } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import { listarContas, type ContaCloud } from '../../services/contas';
import { buscarResumo as buscarResumoInventario, rotuloTipo, type ResumoInventario } from '../../services/inventario';
import {
  buscarAvaliacao, buscarNaoConformes, buscarResumoGovernanca, buscarVariacoes, MAX_CHAVES, urlExportarNaoConformes,
  type AvaliacaoGovernanca, type FiltroGovernanca, type GrupoVariacao, type PaginaNaoConformes, type ResumoGovernanca,
} from '../../services/governanca';
import { useTokenValues } from '../../theme/useTokenValues';
import PainelRecurso from '../Inventario/PainelRecurso';
import '../Inventario/InventarioPage.css';
import './GovernancaPage.css';

const numero = new Intl.NumberFormat('pt-BR');
const pct = (v: number) => `${v.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
const dataHora = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const fmtData = (iso: string | null) => (iso ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short' }).format(new Date(iso)) : '—');

type Estado<T> = { status: 'carregando' } | { status: 'ok'; dados: T } | { status: 'erro'; mensagem: string };

/**
 * Governança de tags. Todo o estado da análise fica na URL (link compartilhável):
 * cliente, conta, regiao, tipo, comp, auto, chaves (até 10), faltando, var (chave de variações).
 */
const GovernancaPage: React.FC = () => {
  const usuario = useCurrentUser();
  const clientes = useMemo(() => [...(usuario?.clientes ?? [])].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')), [usuario]);
  const [params, setParams] = useSearchParams();

  const clienteId = Number(params.get('cliente')) || clientes[0]?.id || null;
  const contaId = Number(params.get('conta')) || null;
  const regiao = params.get('regiao') ?? '';
  const tipo = params.get('tipo') ?? '';
  const compartimento = params.get('comp') ?? '';
  const automaticas = params.get('auto') === '1';
  const chaves = useMemo(() => (params.get('chaves') ?? '').split(',').filter(Boolean).slice(0, MAX_CHAVES), [params]);
  const faltando = params.get('faltando') ?? '';
  const chaveVariacao = params.get('var') ?? '';

  const atualizar = (mudancas: Record<string, string | null>) => {
    const p = new URLSearchParams(params);
    for (const [k, v] of Object.entries(mudancas)) {
      if (v === null || v === '') p.delete(k);
      else p.set(k, v);
    }
    setParams(p, { replace: true });
  };

  const filtro: FiltroGovernanca | null = useMemo(
    () => (clienteId ? { clienteId, contaId, regiao, tipo, compartimento, automaticas } : null),
    [clienteId, contaId, regiao, tipo, compartimento, automaticas],
  );

  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [facetas, setFacetas] = useState<ResumoInventario | null>(null);
  const [resumo, setResumo] = useState<Estado<ResumoGovernanca>>({ status: 'carregando' });
  const [avaliacao, setAvaliacao] = useState<Estado<AvaliacaoGovernanca> | null>(null);
  const [variacoes, setVariacoes] = useState<Estado<GrupoVariacao[]> | null>(null);
  const [lista, setLista] = useState<Estado<PaginaNaoConformes> | null>(null);
  const [pagina, setPagina] = useState(1);
  const [aberto, setAberto] = useState<{ contaId: number; chave: string } | null>(null);
  const tabelaRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (!clienteId) return;
    let vivo = true;
    listarContas({ clienteId }).then((c) => vivo && setContas(c)).catch(() => vivo && setContas([]));
    return () => { vivo = false; };
  }, [clienteId]);

  // Facetas (regiões, tipos, compartimentos) vêm do resumo do Inventário.
  useEffect(() => {
    if (!clienteId) return;
    const ctrl = new AbortController();
    buscarResumoInventario(clienteId, contaId, ctrl.signal).then(setFacetas).catch(() => setFacetas(null));
    return () => ctrl.abort();
  }, [clienteId, contaId]);

  useEffect(() => {
    if (!filtro) return;
    const ctrl = new AbortController();
    setResumo({ status: 'carregando' });
    buscarResumoGovernanca(filtro, ctrl.signal)
      .then((d) => setResumo({ status: 'ok', dados: d }))
      .catch((e) => { if (!ctrl.signal.aborted) setResumo({ status: 'erro', mensagem: mensagemDeErro(e, 'Não foi possível carregar a governança de tags.') }); });
    return () => ctrl.abort();
  }, [filtro]);

  useEffect(() => {
    if (!filtro || chaves.length === 0) { setAvaliacao(null); return; }
    const ctrl = new AbortController();
    setAvaliacao({ status: 'carregando' });
    buscarAvaliacao(filtro, chaves, ctrl.signal)
      .then((d) => setAvaliacao({ status: 'ok', dados: d }))
      .catch((e) => { if (!ctrl.signal.aborted) setAvaliacao({ status: 'erro', mensagem: mensagemDeErro(e) }); });
    return () => ctrl.abort();
  }, [filtro, chaves]);

  useEffect(() => { setPagina(1); }, [filtro, chaves, faltando]);

  useEffect(() => {
    if (!filtro || chaves.length === 0) { setLista(null); return; }
    const ctrl = new AbortController();
    setLista((l) => l ?? { status: 'carregando' });
    buscarNaoConformes(filtro, chaves, faltando, pagina, ctrl.signal)
      .then((d) => setLista({ status: 'ok', dados: d }))
      .catch((e) => { if (!ctrl.signal.aborted) setLista({ status: 'erro', mensagem: mensagemDeErro(e) }); });
    return () => ctrl.abort();
  }, [filtro, chaves, faltando, pagina]);

  useEffect(() => {
    if (!filtro || !chaveVariacao) { setVariacoes(null); return; }
    const ctrl = new AbortController();
    setVariacoes({ status: 'carregando' });
    buscarVariacoes(filtro, chaveVariacao, ctrl.signal)
      .then((d) => setVariacoes({ status: 'ok', dados: d }))
      .catch((e) => { if (!ctrl.signal.aborted) setVariacoes({ status: 'erro', mensagem: mensagemDeErro(e) }); });
    return () => ctrl.abort();
  }, [filtro, chaveVariacao]);

  const alternarChave = (chave: string) => {
    const nova = chaves.includes(chave) ? chaves.filter((c) => c !== chave) : [...chaves, chave];
    if (nova.length > MAX_CHAVES) return;
    atualizar({ chaves: nova.join(','), faltando: nova.includes(faltando) ? faltando : null });
  };

  const irParaTabela = (mudancas: Record<string, string | null>) => {
    atualizar(mudancas);
    window.setTimeout(() => tabelaRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 50);
  };

  if (!clienteId || !filtro) {
    return (
      <div className="page-content gov-pagina">
        <PageHeader title="Governança de tags" />
        <div className="ui-card ui-empty">Nenhum cliente vinculado ao seu usuário.</div>
      </div>
    );
  }

  const r = resumo.status === 'ok' ? resumo.dados : null;
  const vazio = r !== null && r.totalRecursos === 0;
  const av = avaliacao?.status === 'ok' ? avaliacao.dados : null;

  return (
    <div className="page-content gov-pagina">
      <PageHeader title="Governança de tags" />

      <div className="ui-card inv-filtros">
        <div className="ui-field">
          <label className="ui-label" htmlFor="gov-cliente">Cliente</label>
          <select id="gov-cliente" className="ui-select" value={clienteId}
            onChange={(e) => atualizar({ cliente: e.target.value, conta: null, regiao: null, tipo: null, comp: null, chaves: null, faltando: null, var: null })}>
            {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="gov-conta">Conta</label>
          <select id="gov-conta" className="ui-select" value={contaId ?? ''} onChange={(e) => atualizar({ conta: e.target.value || null })}>
            <option value="">Todas as contas</option>
            {contas.map((c) => <option key={c.id} value={c.id}>{c.provedor} · {c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="gov-regiao">Região</label>
          <select id="gov-regiao" className="ui-select" value={regiao} onChange={(e) => atualizar({ regiao: e.target.value })}>
            <option value="">Todas</option>
            {(facetas?.regioes ?? []).map((x) => <option key={x} value={x}>{x}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="gov-tipo">Tipo</label>
          <select id="gov-tipo" className="ui-select" value={tipo} onChange={(e) => atualizar({ tipo: e.target.value })}>
            <option value="">Todos</option>
            {(facetas?.tipos ?? []).map((t) => <option key={t.chave} value={t.chave} title={t.chave}>{rotuloTipo(t.chave)}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="gov-comp">Compartimento</label>
          <select id="gov-comp" className="ui-select" value={compartimento} onChange={(e) => atualizar({ comp: e.target.value })}>
            <option value="">Todos</option>
            {(facetas?.compartimentos ?? []).map((c) => <option key={c.ocid} value={c.caminho}>{c.caminho}</option>)}
          </select>
        </div>
        <label className="ui-switch inv-removidos" title="Oracle-Tags / *-Oracle-Tags (CreatedBy, CreatedOn)">
          <input type="checkbox" checked={automaticas} onChange={(e) => atualizar({ auto: e.target.checked ? '1' : null })} />
          <span className="ui-switch-track" />
          Mostrar tags automáticas
        </label>
      </div>

      {resumo.status === 'erro' && <div className="ui-alert ui-alert-danger" role="alert"><AlertTriangle size={16} /> {resumo.mensagem}</div>}
      {resumo.status === 'carregando' && <div className="ui-skeleton inv-esqueleto" />}

      {vazio && (
        <div className="ui-card ui-empty">
          Nenhum inventário importado para este filtro. Verifique em <Link to="/coletas">Coletas</Link>.
        </div>
      )}

      {r && !vazio && (
        <>
          <section className="gov-kpis" aria-label="Indicadores">
            <div className="ui-card inv-kpi inv-kpi-destaque">
              <span className="inv-kpi-titulo">Recursos ativos</span>
              <span className="inv-kpi-valor">{numero.format(r.totalRecursos)}</span>
            </div>
            <div className="ui-card inv-kpi">
              <span className="inv-kpi-titulo">Com alguma tag</span>
              <span className="inv-kpi-valor">{pct(r.totalRecursos ? (r.comAlgumaTag * 100) / r.totalRecursos : 0)}</span>
              <span className="inv-nota">{numero.format(r.semTag)} sem nenhuma tag{automaticas ? '' : ' (desconsiderando automáticas)'}</span>
            </div>
            <div className="ui-card inv-kpi">
              <span className="inv-kpi-titulo">Chaves distintas</span>
              <span className="inv-kpi-valor">{numero.format(r.chaves.length)}{r.truncado ? '+' : ''}</span>
            </div>
            <div className="ui-card inv-kpi">
              <span className="inv-kpi-titulo">Conformidade</span>
              <span className="inv-kpi-valor">{av ? pct(av.coberturaPct) : '—'}</span>
              <span className="inv-nota">{chaves.length ? `${chaves.length} chave(s) obrigatória(s)` : 'Selecione chaves abaixo'}</span>
            </div>
          </section>

          <section className="ui-card gov-bloco">
            <div className="gov-bloco-topo">
              <h2 className="ui-card-title">Chaves encontradas</h2>
              <span className="inv-nota">Marque até {MAX_CHAVES} chaves para avaliar como obrigatórias ({chaves.length}/{MAX_CHAVES}).</span>
            </div>
            {r.truncado && <div className="ui-alert ui-alert-info">Mostrando as 300 chaves mais usadas.</div>}
            {r.chaves.length === 0 ? <div className="ui-empty">Nenhuma tag nos recursos deste filtro.</div> : (
              <div className="ui-table-wrap">
                <table className="ui-table">
                  <thead>
                    <tr><th>Avaliar</th><th>Chave</th><th>Cobertura</th><th className="num">Recursos</th><th className="num">Valores</th><th>Mais usados</th><th /></tr>
                  </thead>
                  <tbody>
                    {r.chaves.map((c) => {
                      const marcada = chaves.includes(c.chave);
                      return (
                        <tr key={`${c.origem}|${c.chave}`}>
                          <td>
                            <input type="checkbox" checked={marcada} aria-label={`Avaliar ${c.chave} como obrigatória`}
                              disabled={!marcada && chaves.length >= MAX_CHAVES} onChange={() => alternarChave(c.chave)} />
                          </td>
                          <td>
                            <span className="gov-chave">
                              <code>{c.chave}</code>
                              {c.origem === 'FREEFORM' && <span className="ui-badge ui-badge-info">freeform</span>}
                            </span>
                          </td>
                          <td>
                            <span className="gov-cobertura">
                              <span className="gov-trilho"><span className="gov-barra" style={{ width: `${Math.min(100, c.coberturaPct)}%` }} /></span>
                              <span className="gov-pct">{pct(c.coberturaPct)}</span>
                            </span>
                          </td>
                          <td className="num">{numero.format(c.recursos)}</td>
                          <td className="num">{numero.format(c.valoresDistintos)}</td>
                          <td>
                            <span className="inv-chips">
                              {c.topValores.map((v) => <span key={v.valor} className="inv-chip" title={`${v.valor} (${v.n})`}>{v.valor || '(vazio)'} · {numero.format(v.n)}</span>)}
                            </span>
                          </td>
                          <td>
                            <button type="button" className="ui-btn ui-btn-ghost" onClick={() => atualizar({ var: c.chave })}>Variações</button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {chaves.length > 0 && (
            <section className="ui-card gov-bloco" aria-label="Conformidade">
              <div className="gov-bloco-topo">
                <h2 className="ui-card-title">Conformidade</h2>
                <span className="gov-selecionadas">
                  {chaves.map((c) => (
                    <button key={c} type="button" className="inv-chip gov-chip-remover" onClick={() => alternarChave(c)} title="Remover da avaliação">{c} ×</button>
                  ))}
                </span>
              </div>
              {avaliacao?.status === 'carregando' && <div className="ui-skeleton inv-esqueleto" />}
              {avaliacao?.status === 'erro' && <div className="ui-alert ui-alert-danger">{avaliacao.mensagem}</div>}
              {av && <Conformidade av={av} onChave={(c) => irParaTabela({ faltando: c })}
                onCompartimento={(c) => irParaTabela({ comp: c || null })} onTipo={(t) => irParaTabela({ tipo: t })} />}
            </section>
          )}

          {chaveVariacao && (
            <section className="ui-card gov-bloco" aria-label="Valores inconsistentes">
              <div className="gov-bloco-topo">
                <h2 className="ui-card-title">Valores inconsistentes · <code>{chaveVariacao}</code></h2>
                <button type="button" className="ui-btn ui-btn-ghost" onClick={() => atualizar({ var: null })}>Fechar</button>
              </div>
              {variacoes?.status === 'carregando' && <div className="ui-skeleton inv-esqueleto" />}
              {variacoes?.status === 'erro' && <div className="ui-alert ui-alert-danger">{variacoes.mensagem}</div>}
              {variacoes?.status === 'ok' && (variacoes.dados.length === 0
                ? <div className="ui-empty">Nenhuma variação de grafia: os valores desta chave estão padronizados.</div>
                : (
                  <>
                    <p className="inv-nota">Valores que só diferem por maiúsculas, espaços ou acentos. Sugestão: padronizar na grafia mais usada (a primeira de cada grupo).</p>
                    <ul className="gov-variacoes">
                      {variacoes.dados.map((g) => (
                        <li key={g.normalizado}>
                          <strong>{g.normalizado}</strong> <span className="inv-nota">({numero.format(g.total)} recursos)</span>
                          <span className="inv-chips inv-chips-quebra">
                            {g.grafias.map((x) => <span key={x.valor} className="inv-chip">"{x.valor}" · {numero.format(x.n)}</span>)}
                          </span>
                        </li>
                      ))}
                    </ul>
                  </>
                ))}
            </section>
          )}

          {chaves.length > 0 && (
            <section ref={tabelaRef} className="ui-card inv-tabela" aria-label="Não conformes">
              <div className="inv-tabela-topo">
                <span>
                  {lista?.status === 'ok' ? `${numero.format(lista.dados.total)} recurso(s) não conforme(s)` : 'Não conformes'}
                  {faltando && <> · sem <code>{faltando}</code></>}
                </span>
                <span className="gov-acoes">
                  {faltando && <button type="button" className="ui-btn ui-btn-ghost" onClick={() => atualizar({ faltando: null })}>Limpar filtro</button>}
                  <a className="ui-btn ui-btn-secondary" href={urlExportarNaoConformes(filtro, chaves, faltando)} download>
                    <Download size={16} /> Exportar CSV
                  </a>
                </span>
              </div>
              {lista?.status === 'carregando' && <div className="ui-skeleton inv-esqueleto" />}
              {lista?.status === 'erro' && <div className="ui-alert ui-alert-danger">{lista.mensagem}</div>}
              {lista?.status === 'ok' && lista.dados.itens.length === 0 && <div className="ui-empty">Todos os recursos do filtro têm as chaves selecionadas.</div>}
              {lista?.status === 'ok' && lista.dados.itens.length > 0 && (
                <div className="ui-table-wrap">
                  <table className="ui-table">
                    <thead>
                      <tr><th>Nome</th><th>Tipo</th><th>Região</th><th>Compartimento</th><th>Estado</th><th>Faltando</th><th>Visto em</th></tr>
                    </thead>
                    <tbody>
                      {lista.dados.itens.map((x) => (
                        <tr key={`${x.contaId}|${x.chave}`} className="inv-linha" tabIndex={0}
                          onClick={() => setAberto({ contaId: x.contaId, chave: x.chave })}
                          onKeyDown={(e) => e.key === 'Enter' && setAberto({ contaId: x.contaId, chave: x.chave })}>
                          <td className="inv-nome" title={x.chave}>{x.nome}</td>
                          <td title={x.tipo}>{rotuloTipo(x.tipo)}</td>
                          <td>{x.regiao}</td>
                          <td className="inv-comp" title={x.compartimento ?? ''}>{x.compartimento ?? '—'}</td>
                          <td>{x.estado ?? '—'}</td>
                          <td><span className="inv-chips">{x.faltando.map((f) => <span key={f} className="inv-chip gov-chip-falta">{f}</span>)}</span></td>
                          <td>{fmtData(x.ultimoVistoEm)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {lista?.status === 'ok' && lista.dados.total > lista.dados.tamanho && (
                <div className="inv-paginacao">
                  <button type="button" className="ui-btn ui-btn-secondary" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>Anterior</button>
                  <span>Página {pagina} de {Math.ceil(lista.dados.total / lista.dados.tamanho)}</span>
                  <button type="button" className="ui-btn ui-btn-secondary" disabled={pagina * lista.dados.tamanho >= lista.dados.total}
                    onClick={() => setPagina((p) => p + 1)}>Próxima</button>
                </div>
              )}
            </section>
          )}

          <p className="inv-proveniencia">
            {r.proveniencia.map((p) => (
              <span key={p.contaId}>
                {p.contaNome ?? `Conta ${p.contaId}`}: inventário de {p.vistoEm ? dataHora.format(new Date(p.vistoEm)) : '—'}
                {p.job !== null && <> (job #{p.job})</>}
              </span>
            ))}
          </p>
        </>
      )}

      {aberto && <PainelRecurso contaId={aberto.contaId} chave={aberto.chave} onFechar={() => setAberto(null)} />}
    </div>
  );
};

// ── Bloco de conformidade ───────────────────────────────────────────────────
const Conformidade: React.FC<{
  av: AvaliacaoGovernanca;
  onChave: (c: string) => void;
  onCompartimento: (c: string) => void;
  onTipo: (t: string) => void;
}> = ({ av, onChave, onCompartimento, onTipo }) => {
  const t = useTokenValues(['--success', '--danger', '--surface', '--bg', '--border-strong', '--text'] as const);
  const dados = [
    { nome: 'Conformes', valor: av.conformes, cor: t['--success'] },
    { nome: 'Não conformes', valor: av.naoConformes, cor: t['--danger'] },
  ];
  return (
    <div className="gov-conformidade">
      <div className="gov-donut">
        <ResponsiveContainer width="100%" height={200}>
          <PieChart>
            <Tooltip formatter={(v: number, n: string) => [numero.format(v), n]}
              contentStyle={{ background: t['--bg'], border: `1px solid ${t['--border-strong']}`, borderRadius: 8, color: t['--text'], fontSize: 13 }}
              itemStyle={{ color: t['--text'] }} />
            <Pie data={dados.filter((d) => d.valor > 0)} dataKey="valor" nameKey="nome" innerRadius="60%" outerRadius="90%"
              stroke={t['--surface']} strokeWidth={2} isAnimationActive={false}>
              {dados.filter((d) => d.valor > 0).map((d) => <Cell key={d.nome} fill={d.cor} />)}
            </Pie>
          </PieChart>
        </ResponsiveContainer>
        <ul className="gov-legenda">
          <li><i className="gov-cor gov-cor-ok" /> Conformes <strong>{numero.format(av.conformes)}</strong></li>
          <li><i className="gov-cor gov-cor-nok" /> Não conformes <strong>{numero.format(av.naoConformes)}</strong></li>
          <li className="inv-nota">{pct(av.coberturaPct)} de {numero.format(av.total)} recursos têm todas as chaves</li>
        </ul>
      </div>

      <div className="gov-por-chave">
        <h3 className="vg-bloco-subtitulo">Por chave</h3>
        {av.porChave.map((c) => {
          const total = c.presentes + c.ausentes;
          return (
            <button key={c.chave} type="button" className="gov-linha-chave" onClick={() => onChave(c.chave)} title="Ver recursos sem esta chave">
              <span className="gov-linha-rotulo"><code>{c.chave}</code><span>{numero.format(c.ausentes)} sem</span></span>
              <span className="gov-empilhada">
                <span className="gov-seg-ok" style={{ width: `${total ? (c.presentes / total) * 100 : 0}%` }} />
                <span className="gov-seg-nok" style={{ width: `${total ? (c.ausentes / total) * 100 : 0}%` }} />
              </span>
            </button>
          );
        })}
      </div>

      <Piores titulo="Compartimentos com mais não conformes" itens={av.porCompartimento} rotulo={(n) => n || '(sem compartimento)'} onClick={onCompartimento} />
      <Piores titulo="Tipos com mais não conformes" itens={av.porTipo} rotulo={rotuloTipo} onClick={onTipo} />
    </div>
  );
};

const Piores: React.FC<{ titulo: string; itens: AvaliacaoGovernanca['porTipo']; rotulo: (n: string) => string; onClick: (n: string) => void }> = ({
  titulo, itens, rotulo, onClick,
}) => (
  <div className="gov-piores">
    <h3 className="vg-bloco-subtitulo">{titulo}</h3>
    {itens.length === 0 ? <div className="ui-empty">Nenhum não conforme.</div> : (
      <ol className="gov-lista">
        {itens.map((i) => (
          <li key={i.nome}>
            <button type="button" className="gov-item" onClick={() => onClick(i.nome)} title={i.nome}>
              <span className="gov-item-nome">{rotulo(i.nome)}</span>
              <span className="gov-item-n">{numero.format(i.naoConformes)} / {numero.format(i.total)}</span>
            </button>
          </li>
        ))}
      </ol>
    )}
  </div>
);

export default GovernancaPage;
