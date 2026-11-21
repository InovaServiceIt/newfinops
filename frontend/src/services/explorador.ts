import { apiGet } from './api';

// Cliente de /api/explorador (tipos espelham os DTOs do backend).

export type Granularidade = 'DIA' | 'MES';
export type Dimensao = 'SERVICO' | 'COMPARTIMENTO' | 'RECURSO' | 'TAG';

export interface GrupoValor { chave: string; valor: number }
export interface PontoSerie { periodo: string; grupos: GrupoValor[]; outros: number }
export interface LinhaTabela {
  chave: string;
  rotulo: string;
  total: number;
  pct: number;
  variacaoPct: number | null;
  serie: number[];
  contaId: number | null;
}
export interface ProvenienciaCusto { contaId: number; contaNome: string | null; atualizadoEm: string | null; job: number | null }

export interface RespostaExplorador {
  granularidade: Granularidade;
  dimensao: Dimensao;
  de: string;
  ate: string;
  moeda: string | null;
  moedas: string[];
  comFator: boolean;
  fatorDisponivel: boolean;
  grupos: string[];
  serie: PontoSerie[];
  tabela: LinhaTabela[];
  tabelaTruncada: boolean;
  total: number;
  totalPeriodoAnterior: number | null;
  creditos: number | null;
  proveniencia: ProvenienciaCusto[];
  vazio: boolean;
}

export interface ConsultaExplorador {
  clienteId: number;
  contaId: number | null;
  granularidade: Granularidade;
  de: string;
  ate: string;
  dimensao: Dimensao;
  filtro: string[];
  tagChave: string;
  moeda: string;
  comFator: boolean;
  top: number;
}

function query(c: ConsultaExplorador, incluirTop = true): string {
  const qs = new URLSearchParams({
    clienteId: String(c.clienteId), granularidade: c.granularidade, de: c.de, ate: c.ate, dimensao: c.dimensao,
  });
  if (c.contaId) qs.set('contaId', String(c.contaId));
  if (c.filtro.length) qs.set('filtroChave', c.filtro.join('|'));
  if (c.dimensao === 'TAG' && c.tagChave) qs.set('tagChave', c.tagChave);
  if (c.moeda) qs.set('moeda', c.moeda);
  if (c.comFator) qs.set('comFator', 'true');
  if (incluirTop) qs.set('top', String(c.top));
  return qs.toString();
}

export const consultarExplorador = (c: ConsultaExplorador, signal?: AbortSignal) =>
  apiGet<RespostaExplorador>(`/api/explorador/consulta?${query(c)}`, signal);

export const urlExportarExplorador = (c: ConsultaExplorador) => `/api/explorador/exportar?${query(c, false)}`;

/** Autocomplete (até 50). Com dimensao=TAG e sem tagChave devolve as chaves de tag. */
export function buscarValores(clienteId: number, contaId: number | null, dimensao: Dimensao, tagChave: string, busca: string, signal?: AbortSignal) {
  const qs = new URLSearchParams({ clienteId: String(clienteId), dimensao });
  if (contaId) qs.set('contaId', String(contaId));
  if (tagChave) qs.set('tagChave', tagChave);
  if (busca.trim()) qs.set('busca', busca.trim());
  return apiGet<string[]>(`/api/explorador/valores?${qs.toString()}`, signal);
}

export const ROTULO_DIMENSAO: Record<Dimensao, string> = {
  SERVICO: 'Serviço', COMPARTIMENTO: 'Compartimento', RECURSO: 'Recurso', TAG: 'Tag',
};

// ── Períodos ──
export type Atalho = 'mes' | '3m' | '6m' | '12m' | '30d' | 'custom';

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** Atalho → granularidade + intervalo (datas locais). */
export function periodoDoAtalho(a: Exclude<Atalho, 'custom'>, hoje = new Date()): { granularidade: Granularidade; de: string; ate: string } {
  const primeiro = new Date(hoje.getFullYear(), hoje.getMonth(), 1);
  switch (a) {
    case 'mes': return { granularidade: 'DIA', de: iso(primeiro), ate: iso(hoje) };
    case '30d': return { granularidade: 'DIA', de: iso(new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() - 29)), ate: iso(hoje) };
    default: {
      const n = a === '3m' ? 3 : a === '6m' ? 6 : 12;
      return { granularidade: 'MES', de: iso(new Date(hoje.getFullYear(), hoje.getMonth() - (n - 1), 1)), ate: iso(primeiro) };
    }
  }
}
