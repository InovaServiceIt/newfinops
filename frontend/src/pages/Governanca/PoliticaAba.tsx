import React, { useEffect, useState } from 'react';
import { Pencil, Plus } from 'lucide-react';
import { CartesianGrid, Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import Modal from '../../components/common/Modal';
import { mensagemDeErro } from '../../services/api';
import {
  atualizarChavePolitica, buscarEvolucao, criarChavePolitica,
  type PoliticaChave, type PoliticaResumo, type PontoEvolucao,
} from '../../services/governanca';
import { useTokenValues } from '../../theme/useTokenValues';

const numero = new Intl.NumberFormat('pt-BR');
const pct = (v: number | null) => (v === null ? '—' : `${v.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`);
const REGEX_CHAVE = /^[A-Za-z0-9_.\-:/ ]{1,200}$/;

// ── Modal criar/editar chave ────────────────────────────────────────────────
export interface ChaveEmEdicao {
  id?: number;
  chave: string;
  valores: string[];
  descricao: string;
  ativa: boolean;
}

export const ModalChavePolitica: React.FC<{
  clienteId: number;
  edicao: ChaveEmEdicao | null;
  sugestoes?: string[];
  onSalvo: (r: PoliticaResumo) => void;
  onFechar: () => void;
}> = ({ clienteId, edicao, sugestoes = [], onSalvo, onFechar }) => {
  const [form, setForm] = useState<ChaveEmEdicao | null>(edicao);
  const [novoValor, setNovoValor] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => { setForm(edicao); setErro(null); setNovoValor(''); }, [edicao]);

  if (!form) return null;
  const nova = form.id === undefined;
  const adicionar = (v: string) => {
    const t = v.trim();
    if (t && !form.valores.includes(t)) setForm({ ...form, valores: [...form.valores, t] });
    setNovoValor('');
  };

  const salvar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (nova && !REGEX_CHAVE.test(form.chave.trim())) { setErro('Chave: use letras sem acento, números e _ . - : / (até 200).'); return; }
    setSalvando(true);
    setErro(null);
    try {
      const entrada = { valoresPermitidos: form.valores.length ? form.valores : null, descricao: form.descricao.trim() || null };
      const r = nova
        ? await criarChavePolitica({ ...entrada, clienteId, chave: form.chave.trim() })
        : await atualizarChavePolitica(form.id!, { ...entrada, ativa: form.ativa });
      onSalvo(r);
    } catch (err) {
      setErro(mensagemDeErro(err));
    } finally {
      setSalvando(false);
    }
  };

  const restantes = sugestoes.filter((s) => s && !form.valores.includes(s));
  return (
    <Modal
      aberto
      titulo={nova ? 'Adicionar chave à política' : 'Editar chave da política'}
      onFechar={onFechar}
      rodape={
        <>
          <button type="button" className="ui-btn ui-btn-secondary" onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button type="submit" form="form-politica" className="ui-btn ui-btn-primary" disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
        </>
      }
    >
      <form id="form-politica" className="cfg-form" onSubmit={salvar}>
        <div className="ui-field">
          <label className="ui-label" htmlFor="pol-chave">Chave (namespace.chave ou freeform)</label>
          <input id="pol-chave" className="ui-input" required maxLength={200} disabled={!nova} value={form.chave}
            placeholder="Ex.: CostCenter.area" onChange={(e) => setForm({ ...form, chave: e.target.value })} />
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="pol-valor">Valores permitidos (vazio = qualquer valor não vazio)</label>
          <span className="inv-chips inv-chips-quebra">
            {form.valores.map((v) => (
              <span key={v} className="inv-chip pol-chip">
                {v}
                <button type="button" aria-label={`Remover ${v}`} onClick={() => setForm({ ...form, valores: form.valores.filter((x) => x !== v) })}>×</button>
              </span>
            ))}
          </span>
          <input id="pol-valor" className="ui-input" maxLength={200} value={novoValor} placeholder="Digite e tecle Enter"
            onChange={(e) => setNovoValor(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); adicionar(novoValor); } }} />
          {restantes.length > 0 && (
            <span className="pol-sugestoes">
              Mais usados:{' '}
              {restantes.slice(0, 8).map((s) => (
                <button key={s} type="button" className="inv-chip gov-chip-remover" onClick={() => adicionar(s)}>+ {s}</button>
              ))}
            </span>
          )}
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="pol-desc">Descrição (opcional)</label>
          <input id="pol-desc" className="ui-input" maxLength={500} value={form.descricao}
            onChange={(e) => setForm({ ...form, descricao: e.target.value })} />
        </div>
        {!nova && (
          <label className="ui-switch">
            <input type="checkbox" checked={form.ativa} onChange={(e) => setForm({ ...form, ativa: e.target.checked })} />
            <span className="ui-switch-track" />
            Ativa (inativa não entra na conformidade)
          </label>
        )}
        {erro && <div className="ui-alert ui-alert-danger" role="alert">{erro}</div>}
      </form>
    </Modal>
  );
};

// ── Card Evolução ───────────────────────────────────────────────────────────
export const EvolucaoCard: React.FC<{ clienteId: number; versao: number }> = ({ clienteId, versao }) => {
  const t = useTokenValues(['--chart-s1', '--chart-grid', '--text', '--text-muted', '--bg', '--border-strong', '--warning'] as const);
  const [dados, setDados] = useState<{ meta: number; pontos: PontoEvolucao[] } | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setErro(null);
    buscarEvolucao(clienteId, ctrl.signal).then(setDados)
      .catch((e) => { if (!ctrl.signal.aborted) setErro(mensagemDeErro(e)); });
    return () => ctrl.abort();
  }, [clienteId, versao]);

  const pontos = (dados?.pontos ?? []).map((p) => ({ dia: `${p.dia.slice(8, 10)}/${p.dia.slice(5, 7)}`, pct: p.conformidadePct, conformes: p.conformes, total: p.total }));
  return (
    <section className="ui-card gov-bloco" aria-label="Evolução da conformidade">
      <div className="gov-bloco-topo">
        <h2 className="ui-card-title">Evolução da conformidade</h2>
        {dados && <span className="inv-nota">Meta: {pct(dados.meta)} (linha tracejada)</span>}
      </div>
      {erro && <div className="ui-alert ui-alert-danger">{erro}</div>}
      {!dados && !erro && <div className="ui-skeleton inv-esqueleto" />}
      {dados && pontos.length < 2 && <div className="ui-empty">A evolução aparece a partir do segundo dia de coleta.</div>}
      {dados && pontos.length >= 2 && (
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={pontos} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
            <XAxis dataKey="dia" tick={{ fill: t['--text-muted'], fontSize: 12 }} axisLine={false} tickLine={false} minTickGap={12} />
            <YAxis domain={[0, 100]} tick={{ fill: t['--text-muted'], fontSize: 12 }} axisLine={false} tickLine={false} tickFormatter={(v: number) => `${v}%`} width={48} />
            <Tooltip
              contentStyle={{ background: t['--bg'], border: `1px solid ${t['--border-strong']}`, borderRadius: 8, color: t['--text'], fontSize: 13 }}
              itemStyle={{ color: t['--text'] }}
              formatter={(v: number, _n, item) => [`${pct(v)} (${numero.format(item?.payload?.conformes ?? 0)} de ${numero.format(item?.payload?.total ?? 0)})`, 'Conformidade']} />
            <ReferenceLine y={dados.meta} stroke={t['--warning']} strokeDasharray="6 4" />
            <Line type="monotone" dataKey="pct" stroke={t['--chart-s1']} strokeWidth={2} dot={{ r: 3 }} isAnimationActive={false} />
          </LineChart>
        </ResponsiveContainer>
      )}
    </section>
  );
};

// ── Aba Política ────────────────────────────────────────────────────────────
const PoliticaAba: React.FC<{
  clienteId: number;
  politica: PoliticaResumo | null;
  erro: string | null;
  podeEditar: boolean;
  versao: number;
  onEditar: (c: ChaveEmEdicao) => void;
}> = ({ clienteId, politica, erro, podeEditar, versao, onEditar }) => {
  const editar = (c: PoliticaChave) => onEditar({ id: c.id, chave: c.chave, valores: c.valoresPermitidos ?? [], descricao: c.descricao ?? '', ativa: c.ativa });
  const ativas = politica?.chaves.filter((c) => c.ativa).length ?? 0;
  return (
    <>
      <section className="ui-card gov-bloco" aria-label="Política de tags">
        <div className="gov-bloco-topo">
          <h2 className="ui-card-title">Política de tags do cliente</h2>
          {podeEditar && (
            <button type="button" className="ui-btn ui-btn-primary" onClick={() => onEditar({ chave: '', valores: [], descricao: '', ativa: true })}>
              <Plus size={16} /> Adicionar chave
            </button>
          )}
        </div>
        {erro && <div className="ui-alert ui-alert-danger">{erro}</div>}
        {!politica && !erro && <div className="ui-skeleton inv-esqueleto" />}
        {politica && (
          <>
            <p className="inv-nota">
              Um recurso ativo é conforme quando tem <strong>todas</strong> as chaves ativas, com valor não vazio e, quando houver lista,
              com um dos valores permitidos (sem diferenciar maiúsculas, espaços e acentos). Tags automáticas da Oracle não contam.
            </p>
            {politica.chaves.length === 0 ? (
              <div className="ui-empty">Nenhuma chave na política.{podeEditar ? ' Use "Adicionar chave" ou a ação na aba Análise.' : ''}</div>
            ) : (
              <>
                {ativas > 0 && (
                  <p className="pol-resumo">
                    Conformidade atual: <strong>{pct(politica.conformidadePct)}</strong> ({numero.format(politica.conformes)} de {numero.format(politica.total)} recursos)
                  </p>
                )}
                <div className="ui-table-wrap">
                  <table className="ui-table">
                    <thead>
                      <tr><th>Chave</th><th>Valores permitidos</th><th>Descrição</th><th>Situação</th><th className="num">Presentes</th><th className="num">Fora da lista</th>{podeEditar && <th />}</tr>
                    </thead>
                    <tbody>
                      {politica.chaves.map((c) => (
                        <tr key={c.id}>
                          <td><code>{c.chave}</code></td>
                          <td>
                            {c.valoresPermitidos?.length
                              ? <span className="inv-chips inv-chips-quebra">{c.valoresPermitidos.map((v) => <span key={v} className="inv-chip">{v}</span>)}</span>
                              : <span className="inv-nota">Qualquer valor</span>}
                          </td>
                          <td>{c.descricao ?? '—'}</td>
                          <td><span className={`ui-badge ${c.ativa ? 'ui-badge-success' : 'ui-badge-warning'}`}>{c.ativa ? 'Ativa' : 'Inativa'}</span></td>
                          <td className="num">{c.presentes === null ? '—' : `${numero.format(c.presentes)} (${pct(c.presentesPct)})`}</td>
                          <td className="num">{c.foraDaLista === null ? '—' : numero.format(c.foraDaLista)}</td>
                          {podeEditar && (
                            <td><button type="button" className="ui-btn ui-btn-ghost" onClick={() => editar(c)}><Pencil size={14} /> Editar</button></td>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </>
        )}
      </section>
      <EvolucaoCard clienteId={clienteId} versao={versao} />
    </>
  );
};

export default PoliticaAba;
