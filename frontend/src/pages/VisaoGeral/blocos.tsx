import React from 'react';
import type { BlocoPeriodo, Indicador, ItemRanking } from '../../services/visaoGeral';
import { formatBRL, formatData, formatPercent } from '../../utils/format';

// ── Card de indicador ───────────────────────────────────────────────────────
interface CardIndicadorProps {
  titulo: string;
  indicador: Indicador;
  /** Texto do estado neutro quando não há valor (ex.: sem orçamento cadastrado). */
  vazio?: string;
  /** Rótulo do percentual (ex.: "do orçamento mensal"). */
  rotuloPercentual?: string;
  destaque?: boolean;
}

export const CardIndicador: React.FC<CardIndicadorProps> = ({ titulo, indicador, vazio, rotuloPercentual, destaque }) => {
  const semValor = indicador.valor === null;
  const pct = indicador.percentualOrcamento;
  const classePct = pct === null ? '' : pct > 100 ? 'ui-badge-danger' : pct > 90 ? 'ui-badge-warning' : 'ui-badge-success';

  return (
    <div className={`ui-card vg-kpi${destaque ? ' vg-kpi-destaque' : ''}`}>
      <div className="vg-kpi-titulo">
        <span>{titulo}</span>
        {indicador.referencia && <span className="vg-kpi-ref">{indicador.referencia}</span>}
      </div>
      {semValor && vazio ? (
        <div className="vg-kpi-vazio">{vazio}</div>
      ) : (
        <div className="vg-kpi-valor">{formatBRL(indicador.valor)}</div>
      )}
      {pct !== null && rotuloPercentual && (
        <div className="vg-kpi-pct">
          <span className={`ui-badge ${classePct}`}>{formatPercent(pct)}</span>
          <span>{rotuloPercentual}</span>
        </div>
      )}
    </div>
  );
};

// ── Ranking (barras horizontais) ─────────────────────────────────────────────
const Ranking: React.FC<{ titulo: string; itens: ItemRanking[] }> = ({ titulo, itens }) => {
  const max = Math.max(...itens.map((i) => i.valor), 0);
  return (
    <div className="ui-card vg-ranking">
      <h3 className="vg-bloco-subtitulo">{titulo}</h3>
      {itens.length === 0 ? (
        <div className="ui-empty">Sem dados para o filtro</div>
      ) : (
        <ol className="vg-ranking-lista">
          {itens.map((i) => (
            <li key={i.nome} className="vg-ranking-item">
              <div className="vg-ranking-linha">
                <span className="vg-ranking-nome" title={i.nome}>{i.nome}</span>
                <span className="vg-ranking-valor">{formatBRL(i.valor)}</span>
              </div>
              <div className="vg-ranking-trilho">
                <div className="vg-ranking-barra" style={{ width: `${max > 0 ? (i.valor / max) * 100 : 0}%` }} />
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
};

// ── Bloco de período (semana anterior / dia anterior) ───────────────────────
export const BlocoPeriodoView: React.FC<{ titulo: string; bloco: BlocoPeriodo }> = ({ titulo, bloco }) => {
  const periodo = bloco.inicio === bloco.fim
    ? formatData(bloco.inicio)
    : `${formatData(bloco.inicio)} a ${formatData(bloco.fim)}`;

  return (
    <section className="vg-periodo">
      <div className="ui-card vg-kpi vg-periodo-total">
        <div className="vg-kpi-titulo"><span>{titulo}</span></div>
        <div className="vg-kpi-valor">{formatBRL(bloco.total)}</div>
        <div className="vg-kpi-ref">{periodo}</div>
      </div>
      <Ranking titulo="Top 5 por Serviço" itens={bloco.topServicos} />
      <Ranking titulo="Top 5 por SKU" itens={bloco.topSkus} />
      <Ranking titulo="Top 5 por Região" itens={bloco.topRegioes} />
    </section>
  );
};
