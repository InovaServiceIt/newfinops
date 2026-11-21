import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, ArrowDown, ArrowUp, Download } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import { useCurrentUser } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import { listarContas, type ContaCloud } from '../../services/contas';
import {
  buscarRecursos, buscarResumo, listarTags, ROTULO_CATEGORIA, rotuloTipo, SINAIS, urlExportar,
  type FiltroInventario, type Ordem, type PaginaRecursos, type RecursoItem, type ResumoInventario, type Sinal,
} from '../../services/inventario';
import PainelRecurso from './PainelRecurso';
import './InventarioPage.css';

const numero = new Intl.NumberFormat('pt-BR');
const dataCurta = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short' });
const dataHora = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
const fmtData = (iso: string | null) => (iso ? dataCurta.format(new Date(iso)) : '—');

const COLUNAS: { ordem: Ordem; rotulo: string }[] = [
  { ordem: 'nome', rotulo: 'Nome' }, { ordem: 'tipo', rotulo: 'Tipo' }, { ordem: 'regiao', rotulo: 'Região' },
  { ordem: 'compartimento', rotulo: 'Compartimento' }, { ordem: 'estado', rotulo: 'Estado' },
  { ordem: 'criado', rotulo: 'Criado em' },
];

/** Atualiza o valor aplicado só depois de uma pausa na digitação. */
function useAtrasado(valor: string, ms = 400): string {
  const [v, setV] = useState(valor);
  useEffect(() => {
    const t = window.setTimeout(() => setV(valor), ms);
    return () => window.clearTimeout(t);
  }, [valor, ms]);
  return v;
}

const InventarioPage: React.FC = () => {
  const usuario = useCurrentUser();
  const clientes = useMemo(() => [...(usuario?.clientes ?? [])].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')), [usuario]);

  const [clienteId, setClienteId] = useState<number | null>(clientes[0]?.id ?? null);
  const [contaId, setContaId] = useState<number | null>(null);
  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [regiao, setRegiao] = useState('');
  const [tipo, setTipo] = useState('');
  const [compartimento, setCompartimento] = useState('');
  const [estado, setEstado] = useState('');
  const [buscaDigitada, setBusca] = useState('');
  const [tagDigitada, setTag] = useState('');
  const [sinal, setSinal] = useState<Sinal | ''>('');
  const [removidos, setRemovidos] = useState(false);
  const [ordem, setOrdem] = useState<Ordem>('nome');
  const [direcao, setDirecao] = useState<'asc' | 'desc'>('asc');
  const [pagina, setPagina] = useState(1);
  const busca = useAtrasado(buscaDigitada);
  const tag = useAtrasado(tagDigitada);

  const [resumo, setResumo] = useState<ResumoInventario | null>(null);
  const [erroResumo, setErroResumo] = useState<string | null>(null);
  const [lista, setLista] = useState<PaginaRecursos | null>(null);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [aberto, setAberto] = useState<RecursoItem | null>(null);

  // Troca de cliente/conta zera os filtros que dependem das facetas.
  const limparFacetas = () => { setRegiao(''); setTipo(''); setCompartimento(''); setEstado(''); setSinal(''); };

  useEffect(() => {
    if (!clienteId) return;
    let vivo = true;
    listarContas({ clienteId }).then((c) => vivo && setContas(c)).catch(() => vivo && setContas([]));
    return () => { vivo = false; };
  }, [clienteId]);

  useEffect(() => {
    if (!clienteId) return;
    const ctrl = new AbortController();
    setResumo(null);
    setErroResumo(null);
    buscarResumo(clienteId, contaId, ctrl.signal)
      .then(setResumo)
      .catch((e) => { if (!ctrl.signal.aborted) setErroResumo(mensagemDeErro(e, 'Não foi possível carregar o inventário.')); });
    return () => ctrl.abort();
  }, [clienteId, contaId]);

  const filtro: FiltroInventario | null = useMemo(() => clienteId
    ? { clienteId, contaId, regiao, tipo, compartimento, estado, busca, tag, sinal, removidos }
    : null, [clienteId, contaId, regiao, tipo, compartimento, estado, busca, tag, sinal, removidos]);

  useEffect(() => { setPagina(1); }, [filtro, ordem, direcao]);

  useEffect(() => {
    if (!filtro) return;
    const ctrl = new AbortController();
    setErroLista(null);
    buscarRecursos(filtro, pagina, ordem, direcao, ctrl.signal)
      .then(setLista)
      .catch((e) => { if (!ctrl.signal.aborted) setErroLista(mensagemDeErro(e, 'Não foi possível carregar os recursos.')); });
    return () => ctrl.abort();
  }, [filtro, pagina, ordem, direcao]);

  const ordenar = (o: Ordem) => {
    if (o === ordem) setDirecao((d) => (d === 'asc' ? 'desc' : 'asc'));
    else { setOrdem(o); setDirecao('asc'); }
  };

  if (!clienteId || !filtro) {
    return (
      <div className="page-content inv-pagina">
        <PageHeader title="Inventário multicloud" />
        <div className="ui-card ui-empty">Nenhum cliente vinculado ao seu usuário.</div>
      </div>
    );
  }

  const vazio = resumo !== null && resumo.totalAtivos === 0 && resumo.removidos7d === 0 && resumo.proveniencia.length === 0;
  const totalPaginas = lista ? Math.max(1, Math.ceil(lista.total / lista.tamanho)) : 1;

  return (
    <div className="page-content inv-pagina">
      <PageHeader
        title="Inventário multicloud"
        actions={!vazio && (
          <a className="ui-btn ui-btn-secondary" href={urlExportar(filtro)} download>
            <Download size={16} /> Exportar CSV
          </a>
        )}
      />

      <div className="ui-card inv-filtros">
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-cliente">Cliente</label>
          <select id="inv-cliente" className="ui-select" value={clienteId}
            onChange={(e) => { setClienteId(Number(e.target.value)); setContaId(null); limparFacetas(); }}>
            {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-conta">Conta</label>
          <select id="inv-conta" className="ui-select" value={contaId ?? ''}
            onChange={(e) => { setContaId(e.target.value ? Number(e.target.value) : null); limparFacetas(); }}>
            <option value="">Todas as contas</option>
            {contas.map((c) => <option key={c.id} value={c.id}>{c.provedor} · {c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-regiao">Região</label>
          <select id="inv-regiao" className="ui-select" value={regiao} onChange={(e) => setRegiao(e.target.value)}>
            <option value="">Todas</option>
            {(resumo?.regioes ?? []).map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-tipo">Tipo</label>
          <select id="inv-tipo" className="ui-select" value={tipo} onChange={(e) => setTipo(e.target.value)}>
            <option value="">Todos</option>
            {(resumo?.tipos ?? []).map((t) => <option key={t.chave} value={t.chave} title={t.chave}>{rotuloTipo(t.chave)} ({numero.format(t.total)})</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-comp">Compartimento</label>
          <select id="inv-comp" className="ui-select" value={compartimento} onChange={(e) => setCompartimento(e.target.value)}>
            <option value="">Todos</option>
            {(resumo?.compartimentos ?? []).map((c) => <option key={c.ocid} value={c.caminho}>{c.caminho}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="inv-estado">Estado</label>
          <select id="inv-estado" className="ui-select" value={estado} onChange={(e) => setEstado(e.target.value)}>
            <option value="">Todos</option>
            {(resumo?.estados ?? []).map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        <div className="ui-field inv-campo-largo">
          <label className="ui-label" htmlFor="inv-busca">Nome ou OCID</label>
          <input id="inv-busca" className="ui-input" placeholder="Buscar..." value={buscaDigitada} maxLength={300}
            onChange={(e) => setBusca(e.target.value)} />
        </div>
        <div className="ui-field inv-campo-largo">
          <label className="ui-label" htmlFor="inv-tag">Tag</label>
          <input id="inv-tag" className="ui-input" placeholder="namespace.chave=valor ou valor" value={tagDigitada} maxLength={300}
            onChange={(e) => setTag(e.target.value)} />
        </div>
        <label className="ui-switch inv-removidos">
          <input type="checkbox" checked={removidos} onChange={(e) => setRemovidos(e.target.checked)} />
          <span className="ui-switch-track" />
          Mostrar removidos
        </label>
      </div>

      {erroResumo && <div className="ui-alert ui-alert-danger" role="alert"><AlertTriangle size={16} /> {erroResumo}</div>}
      {!resumo && !erroResumo && <div className="ui-skeleton inv-esqueleto" />}

      {vazio && (
        <div className="ui-card ui-empty">
          <span>Nenhum inventário importado para este filtro. Verifique em <Link to="/coletas">Coletas</Link>.</span>
        </div>
      )}

      {resumo && !vazio && (
        <>
          <section className="inv-kpis" aria-label="Indicadores">
            <div className="ui-card inv-kpi inv-kpi-destaque">
              <span className="inv-kpi-titulo">Recursos ativos</span>
              <span className="inv-kpi-valor">{numero.format(resumo.totalAtivos)}</span>
            </div>
            <div className="ui-card inv-kpi">
              <span className="inv-kpi-titulo">Novos · 7 dias</span>
              <span className="inv-kpi-valor">{numero.format(resumo.novos7d)}</span>
            </div>
            <div className="ui-card inv-kpi">
              <span className="inv-kpi-titulo">Removidos · 7 dias</span>
              <span className="inv-kpi-valor">{numero.format(resumo.removidos7d)}</span>
            </div>
            <div className="ui-card inv-kpi inv-categorias">
              <span className="inv-kpi-titulo">Por categoria</span>
              <ul>
                {resumo.porCategoria.map((c) => (
                  <li key={c.chave}><span>{ROTULO_CATEGORIA[c.chave] ?? c.chave}</span><strong>{numero.format(c.total)}</strong></li>
                ))}
              </ul>
            </div>
          </section>

          <section className="inv-sinais" aria-label="Sinais rápidos">
            {SINAIS.map((s) => {
              const n = resumo.sinais[s.chave] ?? 0;
              const ativo = sinal === s.chave;
              return (
                <button key={s.chave} type="button" title={s.ajuda} aria-pressed={ativo}
                  className={`ui-card inv-sinal${ativo ? ' inv-sinal-ativo' : ''}${n > 0 ? ' inv-sinal-com' : ''}`}
                  onClick={() => setSinal(ativo ? '' : s.chave)}>
                  <span className="inv-sinal-n">{numero.format(n)}</span>
                  <span className="inv-sinal-rotulo">{s.rotulo}</span>
                </button>
              );
            })}
          </section>
          <p className="inv-nota">Sinais sem custo: apontam onde olhar. Recomendação com valor em R$ vem quando houver custo importado.</p>

          <section className="ui-card inv-tabela">
            {erroLista && <div className="ui-alert ui-alert-danger" role="alert">{erroLista}</div>}
            {lista && (
              <div className="inv-tabela-topo">
                <span>{numero.format(lista.total)} recurso(s){sinal && ` · ${SINAIS.find((s) => s.chave === sinal)?.rotulo}`}</span>
                {sinal && <button type="button" className="ui-btn ui-btn-ghost" onClick={() => setSinal('')}>Limpar sinal</button>}
              </div>
            )}
            {!lista && !erroLista && <div className="ui-skeleton inv-esqueleto" />}
            {lista && lista.itens.length === 0 && <div className="ui-empty">Nenhum recurso para este filtro.</div>}
            {lista && lista.itens.length > 0 && (
              <div className="ui-table-wrap">
                <table className="ui-table">
                  <thead>
                    <tr>
                      {COLUNAS.map((c) => (
                        <th key={c.ordem} aria-sort={ordem === c.ordem ? (direcao === 'asc' ? 'ascending' : 'descending') : 'none'}>
                          <button type="button" className="inv-ordenar" onClick={() => ordenar(c.ordem)}>
                            {c.rotulo}
                            {ordem === c.ordem && (direcao === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
                          </button>
                        </th>
                      ))}
                      <th>
                        <button type="button" className="inv-ordenar" onClick={() => ordenar('visto')}>
                          Visto em {ordem === 'visto' && (direcao === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
                        </button>
                      </th>
                      <th>Tags</th>
                    </tr>
                  </thead>
                  <tbody>
                    {lista.itens.map((r) => {
                      const tags = listarTags(r.tags);
                      return (
                        <tr key={`${r.contaId}|${r.chave}`} className="inv-linha" tabIndex={0}
                          onClick={() => setAberto(r)} onKeyDown={(e) => e.key === 'Enter' && setAberto(r)}>
                          <td className="inv-nome" title={r.chave}>
                            {r.nome}
                            {r.removidoEm && <span className="ui-badge ui-badge-warning inv-badge">removido</span>}
                          </td>
                          <td title={r.tipo}>{rotuloTipo(r.tipo)}</td>
                          <td>{r.regiao}</td>
                          <td className="inv-comp" title={r.compartimento ?? ''}>{r.compartimento ?? '—'}</td>
                          <td>{r.estado ?? '—'}</td>
                          <td>{fmtData(r.criadoEmCloud)}</td>
                          <td>{fmtData(r.ultimoVistoEm)}</td>
                          <td>
                            <span className="inv-chips">
                              {tags.slice(0, 3).map((t) => <span key={t} className="inv-chip" title={t}>{t}</span>)}
                              {tags.length > 3 && <span className="inv-chip inv-chip-mais" title={tags.slice(3).join('\n')}>+{tags.length - 3}</span>}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
            {lista && lista.total > lista.tamanho && (
              <div className="inv-paginacao">
                <button type="button" className="ui-btn ui-btn-secondary" disabled={pagina <= 1} onClick={() => setPagina((p) => p - 1)}>Anterior</button>
                <span>Página {pagina} de {totalPaginas}</span>
                <button type="button" className="ui-btn ui-btn-secondary" disabled={pagina >= totalPaginas} onClick={() => setPagina((p) => p + 1)}>Próxima</button>
              </div>
            )}
          </section>

          <p className="inv-proveniencia">
            {resumo.proveniencia.map((p) => (
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

export default InventarioPage;
