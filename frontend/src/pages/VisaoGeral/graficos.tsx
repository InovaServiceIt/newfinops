import React from 'react';
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { useTokenValues } from '../../theme/useTokenValues';
import type { BarraMensal, Faixa, PontoMensal } from '../../services/visaoGeral';
import { formatBRL, formatBRLCompacto } from '../../utils/format';

/** Tokens usados pelos gráficos (recharts precisa de cor resolvida, não var()). */
const TOKENS = [
  '--accent', '--accent-2', '--success', '--warning', '--danger', '--chart-grid',
  '--text', '--text-muted', '--bg', '--border-strong', '--surface-hover',
] as const;

function useChartTheme() {
  const t = useTokenValues(TOKENS);
  return {
    t,
    eixo: { fill: t['--text-muted'], fontSize: 12 },
    tooltip: {
      contentStyle: {
        background: t['--bg'],
        border: `1px solid ${t['--border-strong']}`,
        borderRadius: 8,
        color: t['--text'],
        fontSize: 13,
      },
      labelStyle: { color: t['--text'], fontWeight: 600 },
      itemStyle: { color: t['--text'] },
      cursor: { fill: t['--surface-hover'] },
    },
  };
}

export function corDaFaixa(faixa: Faixa, t: Record<(typeof TOKENS)[number], string>): string {
  switch (faixa) {
    case 'ok': return t['--success'];
    case 'atencao': return t['--warning'];
    case 'acima': return t['--danger'];
    default: return t['--accent'];
  }
}

const ALTURA = 260;

// ── Custo mensal (barras coloridas vs orçamento) ───────────────────────────
export const GraficoCustoMensal: React.FC<{ dados: BarraMensal[] }> = ({ dados }) => {
  const { t, eixo, tooltip } = useChartTheme();
  return (
    <ResponsiveContainer width="100%" height={ALTURA}>
      <BarChart data={dados} margin={{ top: 8, right: 24, bottom: 0, left: 8 }}>
        <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
        <XAxis dataKey="rotulo" tick={eixo} axisLine={false} tickLine={false} interval={0}
          tickFormatter={(r: string) => (r.startsWith('Projeção') ? 'Projeção' : r)} />
        <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={formatBRLCompacto} width={80} />
        <Tooltip {...tooltip} formatter={(v: number) => [formatBRL(v), 'Custo']} />
        <Bar dataKey="valor" radius={[4, 4, 0, 0]} maxBarSize={48} isAnimationActive={false}>
          {dados.map((b) => {
            const cor = corDaFaixa(b.faixa, t);
            return (
              <Cell
                key={`${b.ano}-${b.mes}-${b.projecao}`}
                fill={cor}
                fillOpacity={b.projecao ? 0.35 : 1}
                stroke={b.projecao ? cor : undefined}
                strokeDasharray={b.projecao ? '4 3' : undefined}
              />
            );
          })}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
};

/** Legenda explícita das cores do gráfico mensal. */
export const LegendaFaixas: React.FC<{ temOrcamento: boolean; temProjecao: boolean }> = ({ temOrcamento, temProjecao }) => (
  <div className="vg-legenda">
    {temOrcamento ? (
      <>
        <span><i className="vg-legenda-cor vg-cor-ok" />≤ 90% do orçamento mensal</span>
        <span><i className="vg-legenda-cor vg-cor-atencao" />90% a 100%</span>
        <span><i className="vg-legenda-cor vg-cor-acima" />&gt; 100%</span>
      </>
    ) : (
      <span><i className="vg-legenda-cor vg-cor-sem" />Custo (sem orçamento cadastrado)</span>
    )}
    {temProjecao && <span><i className="vg-legenda-cor vg-legenda-projecao" />Projeção do mês atual</span>}
  </div>
);

// ── Últimos 12 meses (área) ───────────────────────────────────────────────
export const GraficoUltimosMeses: React.FC<{ dados: PontoMensal[] }> = ({ dados }) => {
  const { t, eixo, tooltip } = useChartTheme();
  return (
    <ResponsiveContainer width="100%" height={ALTURA}>
      <AreaChart data={dados} margin={{ top: 8, right: 24, bottom: 0, left: 8 }}>
        <defs>
          <linearGradient id="vg-area" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={t['--accent-2']} stopOpacity={0.35} />
            <stop offset="100%" stopColor={t['--accent-2']} stopOpacity={0.02} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
        <XAxis dataKey="rotulo" tick={eixo} axisLine={false} tickLine={false} interval={0} />
        <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={formatBRLCompacto} width={80} />
        <Tooltip
          {...tooltip}
          formatter={(v: number, _n, item) => [formatBRL(v), item?.payload?.parcial ? 'Custo (mês em curso)' : 'Custo']}
        />
        <Area type="monotone" dataKey="valor" stroke={t['--accent-2']} strokeWidth={2} fill="url(#vg-area)"
          dot={{ r: 3, fill: t['--accent-2'] }} activeDot={{ r: 5 }} isAnimationActive={false} />
      </AreaChart>
    </ResponsiveContainer>
  );
};

// ── Custo diário (barras) ─────────────────────────────────────────────────
export const GraficoCustoDiario: React.FC<{ dados: { dia: number; valor: number }[] }> = ({ dados }) => {
  const { t, eixo, tooltip } = useChartTheme();
  return (
    <ResponsiveContainer width="100%" height={ALTURA}>
      <BarChart data={dados} margin={{ top: 8, right: 24, bottom: 0, left: 8 }}>
        <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
        <XAxis dataKey="dia" tick={eixo} axisLine={false} tickLine={false} />
        <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={formatBRLCompacto} width={80} />
        <Tooltip {...tooltip} labelFormatter={(d) => `Dia ${d}`} formatter={(v: number) => [formatBRL(v), 'Custo']} />
        <Bar dataKey="valor" fill={t['--accent']} radius={[3, 3, 0, 0]} maxBarSize={28} isAnimationActive={false} />
      </BarChart>
    </ResponsiveContainer>
  );
};
