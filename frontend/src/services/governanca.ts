import { apiGet, apiSend } from './api';
import type { ProvenienciaConta, Tags } from './inventario';

// Cliente de /api/governanca-tags (leitura sobre o inventário; nada é persistido).

export interface ValorContagem { valor: string; n: number }
export interface ChaveTag {
  chave: string;
  origem: 'DEFINED' | 'FREEFORM';
  recursos: number;
  coberturaPct: number;
  valoresDistintos: number;
  topValores: ValorContagem[];
}
export interface CoberturaTipo { tipo: string; total: number; comAlgumaTag: number }
export interface ResumoGovernanca {
  totalRecursos: number;
  comAlgumaTag: number;
  semTag: number;
  chaves: ChaveTag[];
  truncado: boolean;
  coberturaPorTipo: CoberturaTipo[];
  proveniencia: ProvenienciaConta[];
}

export interface PresencaChave { chave: string; presentes: number; ausentes: number }
export interface GrupoNaoConforme { nome: string; total: number; naoConformes: number }
export interface AvaliacaoGovernanca {
  total: number;
  conformes: number;
  naoConformes: number;
  coberturaPct: number;
  porChave: PresencaChave[];
  porCompartimento: GrupoNaoConforme[];
  porTipo: GrupoNaoConforme[];
}

export interface Grafia { valor: string; n: number }
export interface GrupoVariacao { normalizado: string; total: number; grafias: Grafia[] }

export interface NaoConformeItem {
  contaId: number;
  chave: string;
  nome: string;
  tipo: string;
  regiao: string;
  compartimento: string | null;
  estado: string | null;
  criadoEmCloud: string | null;
  tags: Tags;
  ultimoVistoEm: string;
  faltando: string[];
}
export interface PaginaNaoConformes { itens: NaoConformeItem[]; total: number; pagina: number; tamanho: number }

export interface FiltroGovernanca {
  clienteId: number;
  contaId: number | null;
  regiao: string;
  tipo: string;
  compartimento: string;
  automaticas: boolean;
}

export const MAX_CHAVES = 10;

function query(f: FiltroGovernanca, extra: Record<string, string> = {}): string {
  const qs = new URLSearchParams({ clienteId: String(f.clienteId) });
  if (f.contaId) qs.set('contaId', String(f.contaId));
  if (f.regiao) qs.set('regiao', f.regiao);
  if (f.tipo) qs.set('tipo', f.tipo);
  if (f.compartimento) qs.set('compartimento', f.compartimento);
  if (f.automaticas) qs.set('automaticas', 'true');
  for (const [k, v] of Object.entries(extra)) if (v) qs.set(k, v);
  return qs.toString();
}

export const buscarResumoGovernanca = (f: FiltroGovernanca, signal?: AbortSignal) =>
  apiGet<ResumoGovernanca>(`/api/governanca-tags/resumo?${query(f)}`, signal);

/** politica=true: avalia as chaves ativas da política do cliente (com valores permitidos); chaves é ignorado. */
export const buscarAvaliacao = (f: FiltroGovernanca, chaves: string[], signal?: AbortSignal, politica = false) =>
  apiGet<AvaliacaoGovernanca>(`/api/governanca-tags/avaliacao?${query(f, politica ? { politica: 'true' } : { chaves: chaves.join(',') })}`, signal);

export const buscarVariacoes = (f: FiltroGovernanca, chave: string, signal?: AbortSignal) =>
  apiGet<GrupoVariacao[]>(`/api/governanca-tags/variacoes?${query(f, { chave })}`, signal);

const chavesOuPolitica = (chaves: string[], politica: boolean): Record<string, string> =>
  politica ? { politica: 'true' } : { chaves: chaves.join(',') };

export const buscarNaoConformes = (f: FiltroGovernanca, chaves: string[], faltando: string, pagina: number, signal?: AbortSignal, politica = false) =>
  apiGet<PaginaNaoConformes>(`/api/governanca-tags/nao-conformes?${query(f, { ...chavesOuPolitica(chaves, politica), faltando, pagina: String(pagina) })}`, signal);

export const urlExportarNaoConformes = (f: FiltroGovernanca, chaves: string[], faltando: string, politica = false) =>
  `/api/governanca-tags/nao-conformes/exportar?${query(f, { ...chavesOuPolitica(chaves, politica), faltando })}`;

// ── Política de tags por cliente ──
export interface PoliticaChave {
  id: number;
  chave: string;
  valoresPermitidos: string[] | null;
  descricao: string | null;
  ativa: boolean;
  presentes: number | null;
  ausentes: number | null;
  foraDaLista: number | null;
  presentesPct: number | null;
  atualizadoEm: string | null;
  atualizadoPor: string | null;
}
export interface PoliticaResumo {
  clienteId: number;
  total: number;
  conformes: number;
  conformidadePct: number;
  comAlgumaTag: number;
  chaves: PoliticaChave[];
}
export interface PoliticaEntrada {
  clienteId?: number;
  chave?: string;
  valoresPermitidos: string[] | null;
  descricao: string | null;
  ativa?: boolean;
}
export interface PontoEvolucao { dia: string; total: number; conformes: number; comAlgumaTag: number; conformidadePct: number; chaves: string[] }

export const buscarPolitica = (clienteId: number, signal?: AbortSignal) =>
  apiGet<PoliticaResumo>(`/api/politica-tags?clienteId=${clienteId}`, signal);
export const criarChavePolitica = (e: PoliticaEntrada) => apiSend<PoliticaResumo>('POST', '/api/politica-tags', e);
export const atualizarChavePolitica = (id: number, e: PoliticaEntrada) => apiSend<PoliticaResumo>('PUT', `/api/politica-tags/${id}`, e);
export const buscarEvolucao = (clienteId: number, signal?: AbortSignal) =>
  apiGet<{ meta: number; pontos: PontoEvolucao[] }>(`/api/politica-tags/evolucao?clienteId=${clienteId}`, signal);
