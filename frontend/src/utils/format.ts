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

const cacheMoeda = new Map<string, Intl.NumberFormat>();
function formatador(moeda: string, compacto: boolean): Intl.NumberFormat {
  const chave = `${moeda}|${compacto}`;
  let f = cacheMoeda.get(chave);
  if (!f) {
    try {
      f = new Intl.NumberFormat('pt-BR', compacto
        ? { style: 'currency', currency: moeda, notation: 'compact', maximumFractionDigits: 1 }
        : { style: 'currency', currency: moeda, minimumFractionDigits: 2, maximumFractionDigits: 2 });
    } catch {
      // Código fora do ISO 4217: número com o código ao lado.
      f = new Intl.NumberFormat('pt-BR', compacto
        ? { notation: 'compact', maximumFractionDigits: 1 }
        : { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    cacheMoeda.set(chave, f);
  }
  return f;
}

/** Valor na moeda recebida (sem conversão): R$ 1.234,56 · US$ 12,00. */
export function formatMoeda(valor: number | null | undefined, moeda: string): string {
  if (valor === null || valor === undefined) return '—';
  const f = formatador(moeda, false);
  const s = f.format(valor);
  return f.resolvedOptions().style === 'currency' ? s : `${moeda} ${s}`;
}

export function formatMoedaCompacto(valor: number, moeda: string): string {
  const f = formatador(moeda, true);
  const s = f.format(valor);
  return f.resolvedOptions().style === 'currency' ? s : `${moeda} ${s}`;
}
