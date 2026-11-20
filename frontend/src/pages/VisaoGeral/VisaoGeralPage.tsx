import React, { useEffect, useMemo, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import MultiSelect from '../../components/forms/MultiSelect';
import {
  ApiError, buscarOpcoes, buscarVisaoGeral,
  type FiltrosVisaoGeral, type OpcoesVisaoGeral, type VisaoGeral,
} from '../../services/visaoGeral';
import { BlocoPeriodoView, CardIndicador } from './blocos';
import { GraficoCustoDiario, GraficoCustoMensal, GraficoUltimosMeses, LegendaFaixas } from './graficos';
import './VisaoGeralPage.css';

const MESES = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
const OPCOES_MESES = MESES.map((m, i) => ({ value: String(i + 1), label: m }));

const paraOpcoes = (valores: string[]) => valores.map((v) => ({ value: v, label: v }));

function mensagem(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar os dados.';
}

type Estado<T> = { status: 'carregando' } | { status: 'ok'; dados: T } | { status: 'erro'; mensagem: string };

const VisaoGeralPage: React.FC = () => {
  const [provedor, setProvedor] = useState<number | null>(null);
  const [opcoes, setOpcoes] = useState<Estado<OpcoesVisaoGeral>>({ status: 'carregando' });
  const [filtros, setFiltros] = useState<FiltrosVisaoGeral | null>(null);
  const [visao, setVisao] = useState<Estado<VisaoGeral>>({ status: 'carregando' });

  // Opções do provedor (na primeira vez, o backend escolhe o primeiro provedor).
  useEffect(() => {
    const ctrl = new AbortController();
    setOpcoes({ status: 'carregando' });
    buscarOpcoes(provedor, ctrl.signal)
      .then((o) => {
        setOpcoes({ status: 'ok', dados: o });
        if (o.provedorSelecionado === null) {
          setFiltros(null);
          return;
        }
        const anoAtual = new Date().getFullYear();
        const ano = o.anos.includes(anoAtual) ? anoAtual : o.anos[0] ?? anoAtual;
        setFiltros({
          provedor: o.provedorSelecionado,
          ano,
          meses: [],
          servicos: [],
          skus: [],
          regioes: [],
          comFator: false,
        });
      })
      .catch((e) => {
        if (!ctrl.signal.aborted) setOpcoes({ status: 'erro', mensagem: mensagem(e) });
      });
    return () => ctrl.abort();
  }, [provedor]);

  // Payload da Visão Geral a cada mudança de filtro.
  useEffect(() => {
    if (!filtros) return;
    const ctrl = new AbortController();
    setVisao({ status: 'carregando' });
    buscarVisaoGeral(filtros, ctrl.signal)
      .then((v) => setVisao({ status: 'ok', dados: v }))
      .catch((e) => {
        if (!ctrl.signal.aborted) setVisao({ status: 'erro', mensagem: mensagem(e) });
      });
    return () => ctrl.abort();
  }, [filtros]);

  const op = opcoes.status === 'ok' ? opcoes.dados : null;
  const atualizar = (parcial: Partial<FiltrosVisaoGeral>) => setFiltros((f) => (f ? { ...f, ...parcial } : f));

  const acoes = op && filtros && (
    <div className="vg-acoes">
      <select
        className="ui-select vg-provedor"
        aria-label="Provedor"
        value={filtros.provedor}
        onChange={(e) => setProvedor(Number(e.target.value))}
      >
        {op.provedores.map((p) => <option key={p.id} value={p.id}>{p.rotulo}</option>)}
      </select>
      <label
        className="ui-switch"
        aria-disabled={op.fator === null}
        title={op.fator === null ? 'Sem fator cadastrado para este provedor' : `Fator ${op.fator.toLocaleString('pt-BR')}`}
      >
        <input
          type="checkbox"
          checked={filtros.comFator}
          disabled={op.fator === null}
          onChange={(e) => atualizar({ comFator: e.target.checked })}
        />
        <span className="ui-switch-track" />
        Com fator
      </label>
    </div>
  );

  return (
    <div className="page-content vg-pagina">
      <PageHeader title="Visão Geral" actions={acoes} />

      {opcoes.status === 'erro' && <Alerta texto={opcoes.mensagem} />}
      {op && op.provedores.length === 0 && <div className="ui-card ui-empty">Nenhum provedor cadastrado.</div>}

      {op && filtros && (
        <div className="ui-card vg-filtros">
          <div className="ui-field">
            <label className="ui-label" htmlFor="vg-ano">Ano</label>
            <select id="vg-ano" className="ui-select" value={filtros.ano} onChange={(e) => atualizar({ ano: Number(e.target.value) })}>
              {(op.anos.length ? op.anos : [filtros.ano]).map((a) => <option key={a} value={a}>{a}</option>)}
            </select>
          </div>
          <MultiSelect label="Mês" options={OPCOES_MESES} value={filtros.meses.map(String)}
            onChange={(v) => atualizar({ meses: v.map(Number).sort((a, b) => a - b) })} />
          <MultiSelect label="Serviço" options={paraOpcoes(op.servicos)} value={filtros.servicos}
            onChange={(v) => atualizar({ servicos: v })} searchable />
          <MultiSelect label="SKU" options={paraOpcoes(op.skus)} value={filtros.skus}
            onChange={(v) => atualizar({ skus: v })} searchable />
          <MultiSelect label="Região" options={paraOpcoes(op.regioes)} value={filtros.regioes}
            onChange={(v) => atualizar({ regioes: v })} searchable />
        </div>
      )}

      {(opcoes.status === 'carregando' || (filtros && visao.status === 'carregando')) && <Esqueleto />}
      {visao.status === 'erro' && filtros && <Alerta texto={visao.mensagem} />}
      {visao.status === 'ok' && filtros && <Conteudo v={visao.dados} />}
    </div>
  );
};

// ── Conteúdo ────────────────────────────────────────────────────────────────
const Conteudo: React.FC<{ v: VisaoGeral }> = ({ v }) => {
  const i = v.indicadores;
  const temOrcamento = i.orcamentoAnual.valor !== null;
  const temProjecao = v.custoMensal.some((b) => b.projecao);
  const semDados12 = useMemo(() => v.ultimosDozeMeses.every((p) => p.valor === 0), [v.ultimosDozeMeses]);

  return (
    <>
      {v.comFator && v.fator !== null && (
        <div className="ui-alert ui-alert-info vg-aviso-fator">
          Valores com fator de faturamento ({v.fator.toLocaleString('pt-BR')}). O orçamento não é multiplicado.
        </div>
      )}

      <section className="vg-kpis" aria-label="Indicadores">
        <CardIndicador titulo="Orçamento anual" indicador={i.orcamentoAnual} destaque
          vazio={`Sem orçamento cadastrado para ${v.ano}`} />
        <CardIndicador titulo="Acumulado do ano" indicador={i.acumuladoAno} rotuloPercentual="do orçamento anual" />
        <CardIndicador titulo="Custo do último mês" indicador={i.ultimoMes} rotuloPercentual="do orçamento mensal" />
        <CardIndicador titulo="Custo do mês atual" indicador={i.mesAtual} rotuloPercentual="do orçamento mensal" />
        <CardIndicador titulo="Projeção do mês" indicador={i.projecaoMes} rotuloPercentual="do orçamento mensal"
          vazio="Sem histórico de 3 meses" />
        <CardIndicador titulo="Projeção anual" indicador={i.projecaoAnual} rotuloPercentual="do orçamento anual"
          vazio="Sem histórico para projetar" />
      </section>

      <section className="ui-card vg-grafico">
        <div className="vg-grafico-cabecalho">
          <h2 className="ui-card-title">Custo mensal · {v.ano}</h2>
          <LegendaFaixas temOrcamento={v.orcamentoMensal !== null} temProjecao={temProjecao} />
        </div>
        {v.custoMensal.length === 0 ? <div className="ui-empty">Sem dados para o filtro</div> : <GraficoCustoMensal dados={v.custoMensal} />}
        {temOrcamento && v.orcamentoMensal !== null && (
          <p className="vg-nota">Orçamento mensal de referência: anual ÷ 12.</p>
        )}
      </section>

      <section className="ui-card vg-grafico">
        <h2 className="ui-card-title">Últimos 12 meses</h2>
        {semDados12 ? <div className="ui-empty">Sem dados para o filtro</div> : <GraficoUltimosMeses dados={v.ultimosDozeMeses} />}
        {!semDados12 && (
          <p className="vg-nota">
            {v.ultimosDozeMeses[v.ultimosDozeMeses.length - 1]?.rotulo} é o mês em curso (valor parcial até ontem).
          </p>
        )}
      </section>

      <BlocoPeriodoView titulo="Semana anterior" bloco={v.semanaAnterior} />
      <BlocoPeriodoView titulo="Dia anterior" bloco={v.diaAnterior} />

      <section className="ui-card vg-grafico">
        <h2 className="ui-card-title">Custo diário · mês atual</h2>
        {v.custoDiario.length === 0 ? <div className="ui-empty">Sem dados para o filtro</div> : <GraficoCustoDiario dados={v.custoDiario} />}
      </section>
    </>
  );
};

const Alerta: React.FC<{ texto: string }> = ({ texto }) => (
  <div className="ui-alert ui-alert-danger" role="alert"><AlertTriangle size={16} /> {texto}</div>
);

const Esqueleto: React.FC = () => (
  <div className="vg-esqueleto" aria-busy="true" aria-label="Carregando">
    <div className="vg-kpis">{Array.from({ length: 6 }, (_, k) => <div key={k} className="ui-skeleton vg-esqueleto-kpi" />)}</div>
    <div className="ui-skeleton vg-esqueleto-grafico" />
    <div className="ui-skeleton vg-esqueleto-grafico" />
  </div>
);

export default VisaoGeralPage;
