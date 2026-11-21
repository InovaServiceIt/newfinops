import React from 'react';
import type { ItemValor } from '../../services/visaoGeral';
import { formatMoeda } from '../../utils/format';

// ── Card de indicador ───────────────────────────────────────────────────────
interface CardIndicadorProps {
  titulo: string;
  /** null = em apuração (sem número). */
  valor: number | null;
  moeda: string;
  referencia?: string;
  destaque?: boolean;
  parcial?: boolean;
}

export const CardIndicador: React.FC<CardIndicadorProps> = ({ titulo, valor, moeda, referencia, destaque, parcial }) => (
  <div className={`ui-card vg-kpi${destaque ? ' vg-kpi-destaque' : ''}`}>
    <div className="vg-kpi-titulo">
      <span>{titulo}</span>
      {parcial && <span className="ui-badge ui-badge-warning">parcial</span>}
    </div>
    <div className="vg-kpi-valor">{valor === null ? '—' : formatMoeda(valor, moeda)}</div>
    {valor === null
      ? <div className="vg-kpi-ref">Valores em apuração</div>
      : referencia && <div className="vg-kpi-ref">{referencia}</div>}
  </div>
);

// ── Ranking (barras horizontais) ─────────────────────────────────────────────
export const Ranking: React.FC<{ titulo: string; itens: ItemValor[]; moeda: string }> = ({ titulo, itens, moeda }) => {
  const max = Math.max(...itens.map((i) => i.valor), 0);
  return (
    <div className="ui-card vg-ranking">
      <h3 className="vg-bloco-subtitulo">{titulo}</h3>
      {itens.length === 0 ? (
        <div className="ui-empty">Sem dados no mês</div>
      ) : (
        <ol className="vg-ranking-lista">
          {itens.map((i) => (
            <li key={i.chave ?? `${i.nome}|${i.detalhe ?? ''}`} className="vg-ranking-item">
              <div className="vg-ranking-linha">
                <span className="vg-ranking-nome" title={[i.nome, i.detalhe, i.chave && i.chave !== i.nome ? i.chave : null].filter(Boolean).join(' · ')}>
                  {i.nome}
                  {i.detalhe && <span className="vg-ranking-detalhe"> · {i.detalhe}</span>}
                </span>
                <span className="vg-ranking-valor">{formatMoeda(i.valor, moeda)}</span>
              </div>
              <div className="vg-ranking-trilho">
                <div className="vg-ranking-barra" style={{ width: `${max > 0 ? Math.max(0, (i.valor / max) * 100) : 0}%` }} />
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
};
