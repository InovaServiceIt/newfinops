const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 2, maximumFractionDigits: 2 });
const brlCompacto = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', notation: 'compact', maximumFractionDigits: 1 });
const pct = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** R$ 1.234,56 (ou "—" quando não há valor). */
export function formatBRL(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? '—' : brl.format(valor);
}

/** R$ 1,2 mil / R$ 3,4 mi — para eixos de gráfico. */
export function formatBRLCompacto(valor: number): string {
  return brlCompacto.format(valor);
}

/** 87,5% */
export function formatPercent(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? '—' : `${pct.format(valor)}%`;
}

/** 2026-11-09 → 09/11/2026 (sem conversão de fuso). */
export function formatData(iso: string): string {
  const [ano, mes, dia] = iso.split('-');
  return `${dia}/${mes}/${ano}`;
}
