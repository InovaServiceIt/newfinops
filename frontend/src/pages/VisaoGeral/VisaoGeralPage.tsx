import React, { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import { useCurrentUser } from '../../components/auth/AuthGuard';
import { mensagemDeErro } from '../../services/api';
import { listarContas, type ContaCloud } from '../../services/contas';
import { buscarVisaoGeral, type FiltrosVisaoGeral, type VisaoGeral, type VisaoMoeda } from '../../services/visaoGeral';
import { CardIndicador, Ranking } from './blocos';
import { GraficoDiario, GraficoDonut, GraficoMensal, mapaDeCores } from './graficos';
import './VisaoGeralPage.css';

type Estado<T> = { status: 'carregando' } | { status: 'ok'; dados: T } | { status: 'erro'; mensagem: string };

const ddmm = (iso: string) => `${iso.slice(8, 10)}/${iso.slice(5, 7)}`;

function dataHora(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: 'America/Sao_Paulo' })
    .replace(',', '');
}

const VisaoGeralPage: React.FC = () => {
  const usuario = useCurrentUser();
  const clientes = useMemo(() => [...(usuario?.clientes ?? [])].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')), [usuario]);
  const podeFator = usuario?.perfil === 'ADMINISTRADOR' || usuario?.perfil === 'ANALISTA_FINOPS';

  const [filtros, setFiltros] = useState<FiltrosVisaoGeral | null>(() =>
    clientes.length ? { clienteId: clientes[0].id, contaId: null, comFator: false } : null);
  const [contas, setContas] = useState<ContaCloud[]>([]);
  const [visao, setVisao] = useState<Estado<VisaoGeral>>({ status: 'carregando' });

  const clienteId = filtros?.clienteId ?? null;

  // Contas do cliente (filtro opcional).
  useEffect(() => {
    if (!clienteId) return;
    let vivo = true;
    setContas([]);
    listarContas({ clienteId })
      .then((c) => { if (vivo) setContas(c); })
      .catch(() => { if (vivo) setContas([]); });
    return () => { vivo = false; };
  }, [clienteId]);

  useEffect(() => {
    if (!filtros) return;
    const ctrl = new AbortController();
    setVisao({ status: 'carregando' });
    buscarVisaoGeral(filtros, ctrl.signal)
      .then((v) => setVisao({ status: 'ok', dados: v }))
      .catch((e) => {
        if (!ctrl.signal.aborted) setVisao({ status: 'erro', mensagem: mensagemDeErro(e, 'Não foi possível carregar os dados.') });
      });
    return () => ctrl.abort();
  }, [filtros]);

  const atualizar = (parcial: Partial<FiltrosVisaoGeral>) => setFiltros((f) => (f ? { ...f, ...parcial } : f));
  const v = visao.status === 'ok' ? visao.dados : null;

  const acoes = podeFator && filtros && (
    <div className="vg-acoes">
      <label
        className="ui-switch"
        aria-disabled={!v?.fatorDisponivel}
        title={!v ? '' : v.fatorDisponivel
          ? (v.fatorAtual !== null ? `Fator vigente: ${v.fatorAtual.toLocaleString('pt-BR', { maximumFractionDigits: 6 })}` : 'Fator vigente em parte do período')
          : 'Sem fator cadastrado para este cliente/conta'}
      >
        <input
          type="checkbox"
          checked={filtros.comFator && !!v?.fatorDisponivel}
          disabled={!v?.fatorDisponivel}
          onChange={(e) => atualizar({ comFator: e.target.checked })}
        />
        <span className="ui-switch-track" />
        Ver com fator do cliente
      </label>
    </div>
  );

  if (!filtros) {
    return (
      <div className="page-content vg-pagina">
        <PageHeader title="Visão Geral" />
        <div className="ui-card ui-empty">Nenhum cliente vinculado ao seu usuário.</div>
      </div>
    );
  }

  return (
    <div className="page-content vg-pagina">
      <PageHeader title="Visão Geral" actions={acoes} />

      <div className="ui-card vg-filtros">
        <div className="ui-field">
          <label className="ui-label" htmlFor="vg-cliente">Cliente</label>
          <select id="vg-cliente" className="ui-select" value={filtros.clienteId}
            onChange={(e) => atualizar({ clienteId: Number(e.target.value), contaId: null })}>
            {clientes.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        <div className="ui-field">
          <label className="ui-label" htmlFor="vg-conta">Conta</label>
          <select id="vg-conta" className="ui-select" value={filtros.contaId ?? ''}
            onChange={(e) => atualizar({ contaId: e.target.value ? Number(e.target.value) : null })}>
            <option value="">Todas as contas</option>
            {contas.map((c) => <option key={c.id} value={c.id}>{c.provedor} · {c.nome}</option>)}
          </select>
        </div>
      </div>

      {visao.status === 'carregando' && <Esqueleto />}
      {visao.status === 'erro' && <Alerta texto={visao.mensagem} />}
      {v && v.vazio && (
        <div className="ui-card ui-empty">
          <span>Nenhum custo importado para este filtro. Verifique em <Link to="/coletas">Coletas</Link>.</span>
        </div>
      )}
      {v && !v.vazio && <Conteudo v={v} />}
    </div>
  );
};

// ── Conteúdo ────────────────────────────────────────────────────────────────
const Conteudo: React.FC<{ v: VisaoGeral }> = ({ v }) => (
  <>
    {v.moedas.length > 1 && (
      <div className="ui-alert ui-alert-warning" role="status">
        <AlertTriangle size={16} /> Este filtro tem custos em mais de uma moeda ({v.moedas.map((m) => m.moeda).join(', ')}).
        Os valores aparecem separados por moeda, sem conversão.
      </div>
    )}
    {v.comFator && (
      <div className="ui-alert ui-alert-info vg-aviso-fator">
        Valores multiplicados pelo fator do cliente vigente em cada período
        {v.fatorAtual !== null && <> (atual: {v.fatorAtual.toLocaleString('pt-BR', { maximumFractionDigits: 6 })})</>}.
      </div>
    )}

    {v.moedas.map((m) => <BlocoMoeda key={m.moeda} m={m} multi={v.moedas.length > 1} />)}

    <p className="vg-proveniencia">
      Dados de custo até {v.dadosAte ? ddmm(v.dadosAte) : '—'} (fonte OCI Usage API; atraso típico ~24 h)
      {' · '}última importação {v.ultimaImportacao ? dataHora(v.ultimaImportacao) : '—'}
      {v.ultimoJob !== null && <> (job #{v.ultimoJob})</>}
    </p>
  </>
);

const BlocoMoeda: React.FC<{ m: VisaoMoeda; multi: boolean }> = ({ m, multi }) => {
  const cores = useMemo(
    () => mapaDeCores([m.servicosMensal, m.donut.map((i) => i.nome), m.servicosDiario]),
    [m.servicosMensal, m.donut, m.servicosDiario],
  );
  const sufixo = multi ? ` · ${m.moeda}` : '';

  return (
    <>
      {multi && <h2 className="vg-moeda-titulo">Moeda {m.moeda}</h2>}
      <section className="vg-kpis" aria-label={`Indicadores${sufixo}`}>
        <CardIndicador titulo="Mês corrente" valor={m.kpis.mesCorrente} moeda={m.moeda} destaque parcial />
        <CardIndicador titulo="Mês anterior" valor={m.kpis.mesAnterior} moeda={m.moeda} />
        <CardIndicador titulo="Acumulado do ano" valor={m.kpis.ytd} moeda={m.moeda} />
        <CardIndicador titulo="Previsão do mês" valor={m.kpis.previsaoMes} moeda={m.moeda} referencia="realizado + previsão OCI" />
        <CardIndicador titulo="Créditos do mês" valor={m.kpis.creditosMes} moeda={m.moeda} />
      </section>

      <section className="ui-card vg-grafico">
        <h2 className="ui-card-title">Custo mensal por serviço · 12 meses{sufixo}</h2>
        <GraficoMensal dados={m.mensal} servicos={m.servicosMensal} moeda={m.moeda} cores={cores} />
        <p className="vg-nota">* {m.mensal[m.mensal.length - 1]?.rotulo} é o mês em curso (parcial).</p>
      </section>

      <div className="vg-duas-colunas">
        <section className="ui-card vg-grafico">
          <h2 className="ui-card-title">Por serviço · mês corrente{sufixo}</h2>
          {m.donut.length === 0 ? <div className="ui-empty">Sem custo no mês</div> : <GraficoDonut itens={m.donut} moeda={m.moeda} cores={cores} />}
        </section>
        <section className="ui-card vg-grafico">
          <h2 className="ui-card-title">Custo diário · últimos 30 dias{sufixo}</h2>
          {m.diario.length === 0 ? <div className="ui-empty">Sem custo diário</div>
            : <GraficoDiario dados={m.diario} servicos={m.servicosDiario} moeda={m.moeda} cores={cores} />}
        </section>
      </div>

      <div className="vg-duas-colunas">
        <Ranking titulo={`Top 10 compartments · mês${sufixo}`} itens={m.topCompartimentos} moeda={m.moeda} />
        <Ranking titulo={`Top 10 recursos · mês${sufixo}`} itens={m.topRecursos} moeda={m.moeda} />
      </div>
    </>
  );
};

const Alerta: React.FC<{ texto: string }> = ({ texto }) => (
  <div className="ui-alert ui-alert-danger" role="alert"><AlertTriangle size={16} /> {texto}</div>
);

const Esqueleto: React.FC = () => (
  <div className="vg-esqueleto" aria-busy="true" aria-label="Carregando">
    <div className="vg-kpis">{Array.from({ length: 5 }, (_, k) => <div key={k} className="ui-skeleton vg-esqueleto-kpi" />)}</div>
    <div className="ui-skeleton vg-esqueleto-grafico" />
    <div className="ui-skeleton vg-esqueleto-grafico" />
  </div>
);

export default VisaoGeralPage;
