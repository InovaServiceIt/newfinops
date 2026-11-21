import { apiGet } from './api';
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

export const buscarAvaliacao = (f: FiltroGovernanca, chaves: string[], signal?: AbortSignal) =>
  apiGet<AvaliacaoGovernanca>(`/api/governanca-tags/avaliacao?${query(f, { chaves: chaves.join(',') })}`, signal);

export const buscarVariacoes = (f: FiltroGovernanca, chave: string, signal?: AbortSignal) =>
  apiGet<GrupoVariacao[]>(`/api/governanca-tags/variacoes?${query(f, { chave })}`, signal);

export const buscarNaoConformes = (f: FiltroGovernanca, chaves: string[], faltando: string, pagina: number, signal?: AbortSignal) =>
  apiGet<PaginaNaoConformes>(`/api/governanca-tags/nao-conformes?${query(f, { chaves: chaves.join(','), faltando, pagina: String(pagina) })}`, signal);

export const urlExportarNaoConformes = (f: FiltroGovernanca, chaves: string[], faltando: string) =>
  `/api/governanca-tags/nao-conformes/exportar?${query(f, { chaves: chaves.join(','), faltando })}`;
