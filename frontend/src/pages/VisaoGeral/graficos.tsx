import React from 'react';
import {
  Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { useTokenValues } from '../../theme/useTokenValues';
import type { ItemValor, PontoDiario, PontoMensal } from '../../services/visaoGeral';
import { formatMoeda, formatMoedaCompacto } from '../../utils/format';

export const OUTROS = 'Outros';

/** Tokens usados pelos gráficos (recharts precisa de cor resolvida, não var()). */
const TOKENS = [
  '--chart-s1', '--chart-s2', '--chart-s3', '--chart-s4', '--chart-s5', '--chart-s6', '--chart-s7', '--chart-s8',
  '--chart-outros', '--chart-grid', '--text', '--text-muted', '--bg', '--surface', '--border-strong', '--surface-hover',
] as const;
const SLOTS = TOKENS.slice(0, 8);

type Tema = Record<(typeof TOKENS)[number], string>;

function useChartTheme() {
  const t = useTokenValues(TOKENS) as Tema;
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
    legenda: {
      wrapperStyle: { fontSize: 12, paddingTop: 8 },
      // Texto da legenda em tinta de texto; a cor do serviço fica só no marcador.
      formatter: (valor: string) => <span style={{ color: t['--text-muted'] }}>{valor}</span>,
    },
  };
}

/**
 * Cor segue o serviço (não a posição): a ordem vem do ranking de 12 meses e os
 * demais serviços vistos nos outros gráficos pegam os slots livres. "Outros" é cinza.
 */
export function mapaDeCores(ordem: string[][]): Map<string, number> {
  const mapa = new Map<string, number>();
  for (const lista of ordem)
    for (const nome of lista)
      if (nome !== OUTROS && !mapa.has(nome) && mapa.size < SLOTS.length) mapa.set(nome, mapa.size);
  return mapa;
}

function cor(nome: string, mapa: Map<string, number>, t: Tema): string {
  const i = mapa.get(nome);
  return i === undefined ? t['--chart-outros'] : t[SLOTS[i]];
}

const ALTURA = 280;

// ── Mensal empilhado por serviço (12 meses) ─────────────────────────────────
export const GraficoMensal: React.FC<{ dados: PontoMensal[]; servicos: string[]; moeda: string; cores: Map<string, number> }> = ({
  dados, servicos, moeda, cores,
}) => {
  const { t, eixo, tooltip, legenda } = useChartTheme();
  const linhas = dados.map((p) => ({ rotulo: p.parcial ? `${p.rotulo}*` : p.rotulo, total: p.total, ...p.valores }));
  return (
    <ResponsiveContainer width="100%" height={ALTURA + 40}>
      <BarChart data={linhas} margin={{ top: 8, right: 16, bottom: 0, left: 8 }}>
        <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
        <XAxis dataKey="rotulo" tick={eixo} axisLine={false} tickLine={false} interval={0} />
        <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={(v: number) => formatMoedaCompacto(v, moeda)} width={84} />
        <Tooltip {...tooltip} formatter={(v: number, n: string) => [formatMoeda(v, moeda), n]} />
        <Legend {...legenda} />
        {servicos.map((s, i) => (
          <Bar key={s} dataKey={s} name={s} stackId="m" fill={cor(s, cores, t)} stroke={t['--surface']} strokeWidth={1}
            maxBarSize={44} isAnimationActive={false} radius={i === servicos.length - 1 ? [4, 4, 0, 0] : undefined} />
        ))}
      </BarChart>
    </ResponsiveContainer>
  );
};

// ── Donut por serviço (mês corrente) ────────────────────────────────────────
export const GraficoDonut: React.FC<{ itens: ItemValor[]; moeda: string; cores: Map<string, number> }> = ({ itens, moeda, cores }) => {
  const { t, tooltip } = useChartTheme();
  const total = itens.reduce((s, i) => s + i.valor, 0);
  const positivos = itens.filter((i) => i.valor > 0);
  return (
    <div className="vg-donut">
      <ResponsiveContainer width="100%" height={ALTURA}>
        <PieChart>
          <Tooltip {...tooltip} formatter={(v: number, n: string) => [formatMoeda(v, moeda), n]} />
          <Pie data={positivos} dataKey="valor" nameKey="nome" innerRadius="58%" outerRadius="88%" paddingAngle={1}
            stroke={t['--surface']} strokeWidth={2} isAnimationActive={false}>
            {positivos.map((i) => <Cell key={i.nome} fill={cor(i.nome, cores, t)} />)}
          </Pie>
        </PieChart>
      </ResponsiveContainer>
      <ul className="vg-donut-legenda">
        {itens.map((i) => (
          <li key={i.nome}>
            <i className="vg-legenda-cor" style={{ background: cor(i.nome, cores, t) }} />
            <span className="vg-donut-nome" title={i.nome}>{i.nome}</span>
            <span className="vg-donut-valor">{formatMoeda(i.valor, moeda)}</span>
            <span className="vg-donut-pct">{total > 0 ? `${((i.valor / total) * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%` : '—'}</span>
          </li>
        ))}
      </ul>
    </div>
  );
};

// ── Linha diária (30 dias, top 5 + Outros) ──────────────────────────────────
export const GraficoDiario: React.FC<{ dados: PontoDiario[]; servicos: string[]; moeda: string; cores: Map<string, number> }> = ({
  dados, servicos, moeda, cores,
}) => {
  const { t, eixo, tooltip, legenda } = useChartTheme();
  const linhas = dados.map((p) => ({ dia: `${p.dia.slice(8, 10)}/${p.dia.slice(5, 7)}`, ...p.valores }));
  return (
    <ResponsiveContainer width="100%" height={ALTURA + 40}>
      <LineChart data={linhas} margin={{ top: 8, right: 16, bottom: 0, left: 8 }}>
        <CartesianGrid vertical={false} stroke={t['--chart-grid']} />
        <XAxis dataKey="dia" tick={eixo} axisLine={false} tickLine={false} minTickGap={16} />
        <YAxis tick={eixo} axisLine={false} tickLine={false} tickFormatter={(v: number) => formatMoedaCompacto(v, moeda)} width={84} />
        <Tooltip {...tooltip} cursor={{ stroke: t['--border-strong'] }} formatter={(v: number, n: string) => [formatMoeda(v, moeda), n]} />
        <Legend {...legenda} />
        {servicos.map((s) => (
          <Line key={s} type="monotone" dataKey={s} name={s} stroke={cor(s, cores, t)} strokeWidth={2}
            dot={false} activeDot={{ r: 4 }} isAnimationActive={false} />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );
};
