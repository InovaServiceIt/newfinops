// Coletas, "Coletar agora", agenda e parâmetros.
import { apiGet, apiSend, apiSendVazio } from './api';
import type { Provedor } from './contas';

export type TipoColeta = 'COLETA_COMPLETA' | 'COLETA_CUSTO' | 'COLETA_INVENTARIO';
export type TipoJob = TipoColeta | 'TESTE_CREDENCIAL';
export type StatusJob = 'PENDENTE' | 'EXECUTANDO' | 'COLETADO' | 'IMPORTANDO' | 'IMPORTADO' | 'CONCLUIDO' | 'ERRO' | 'CANCELADO';

export interface Coleta {
  id: number;
  tipoJob: TipoJob;
  clienteNome: string | null;
  contaNome: string | null;
  provedor: Provedor | null;
  origem: 'AGENDADA' | 'MANUAL' | 'SISTEMA';
  solicitadoPorNome: string | null;
  status: StatusJob;
  tentativas: number;
  criadoEm: string;
  iniciadoEm: string | null;
  finalizadoEm: string | null;
  duracaoSeg: number | null;
  erro: string | null;
  resumo: Record<string, unknown> | null;
  coletorVersao: string | null;
  /** IMPORTADO/ERRO com JSON bruto guardado (o banco confirma ao reimportar). */
  podeReimportar: boolean;
}

/** Volta a coleta para COLETADO: o importador refaz custo e inventário no próximo ciclo (só ADMINISTRADOR). */
export const reimportarColeta = (id: number) =>
  apiSend<{ jobId: number; statusAnterior: string }>('POST', `/api/coletas/${id}/reimportar`, {});

export interface PaginaColetas {
  itens: Coleta[];
  total: number;
  pagina: number;
  tamanho: number;
}

export interface FiltroColetas {
  clienteId?: number;
  contaId?: number;
  status?: StatusJob;
  tipoJob?: TipoJob;
  de?: string;
  ate?: string;
  pagina?: number;
  tamanho?: number;
}

export function listarColetas(f: FiltroColetas): Promise<PaginaColetas> {
  const qs = new URLSearchParams();
  Object.entries(f).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '') qs.set(k, String(v));
  });
  return apiGet<PaginaColetas>(`/api/coletas?${qs.toString()}`);
}

export const permissaoColeta = () => apiGet<{ podeColetar: boolean }>('/api/coletas/permissao');

export const coletarAgora = (contaId: number, tipoJob: TipoColeta, costDays: number | null) =>
  apiSend<{ jobId: number }>('POST', `/api/contas/${contaId}/coletar`, { tipoJob, costDays });

// ── Agenda ───────────────────────────────────────────────────────────────────
export interface Agenda {
  id: number;
  nome: string;
  tipoJob: TipoColeta;
  clienteId: number | null;
  clienteNome: string | null;
  cloudContaId: number | null;
  contaNome: string | null;
  cron: string;
  fuso: string;
  costDays: number | null;
  ativo: boolean;
  atualizadoEm: string | null;
  atualizadoPor: string | null;
}

export interface AgendaEntrada {
  nome: string;
  tipoJob: TipoColeta;
  clienteId: number | null;
  cloudContaId: number | null;
  cron: string;
  fuso: string;
  costDays: number | null;
}

export interface PreviaCron {
  valida: boolean;
  motivo: string | null;
  proximas: string[];
}

export const listarAgenda = () =>
  apiGet<{ itens: Agenda[]; atalhos: { rotulo: string; cron: string }[] }>('/api/admin/agenda');
export const previaCron = (cron: string, fuso: string) => apiSend<PreviaCron>('POST', '/api/admin/agenda/previa', { cron, fuso });
export const criarAgenda = (a: AgendaEntrada) => apiSend<{ id: number }>('POST', '/api/admin/agenda', a);
export const atualizarAgenda = (id: number, a: AgendaEntrada) => apiSend<{ id: number }>('PUT', `/api/admin/agenda/${id}`, a);
export const alterarAtivoAgenda = (id: number, ativo: boolean) =>
  apiSendVazio('PATCH', `/api/admin/agenda/${id}/ativo`, { ativo });

// ── Parâmetros ───────────────────────────────────────────────────────────────
export interface Parametro {
  chave: string;
  valor: string | null;
  tipo: 'TEXTO' | 'INTEIRO' | 'DECIMAL' | 'BOOLEANO' | 'JSON';
  descricao: string | null;
  atualizadoEm: string | null;
  atualizadoPor: string | null;
}

export const listarParametros = () => apiGet<Parametro[]>('/api/admin/parametros');
export const atualizarParametro = (chave: string, valor: string) =>
  apiSendVazio('PUT', `/api/admin/parametros/${encodeURIComponent(chave)}`, { valor });

// ── Rótulos ──────────────────────────────────────────────────────────────────
export const ROTULO_TIPO: Record<TipoJob, string> = {
  COLETA_COMPLETA: 'Coleta completa',
  COLETA_CUSTO: 'Coleta de custo',
  COLETA_INVENTARIO: 'Coleta de inventário',
  TESTE_CREDENCIAL: 'Teste de credencial',
};

export const STATUS_JOB: Record<StatusJob, { rotulo: string; classe: string }> = {
  PENDENTE: { rotulo: 'Pendente', classe: 'ui-badge-info' },
  EXECUTANDO: { rotulo: 'Executando', classe: 'ui-badge-accent' },
  COLETADO: { rotulo: 'Coletado', classe: 'ui-badge-success' },
  IMPORTANDO: { rotulo: 'Importando', classe: 'ui-badge-accent' },
  IMPORTADO: { rotulo: 'Importado', classe: 'ui-badge-success' },
  CONCLUIDO: { rotulo: 'Concluído', classe: 'ui-badge-success' },
  ERRO: { rotulo: 'Erro', classe: 'ui-badge-danger' },
  CANCELADO: { rotulo: 'Cancelado', classe: 'ui-badge-warning' },
};

export const ROTULO_ORIGEM: Record<Coleta['origem'], string> = { AGENDADA: 'Agendada', MANUAL: 'Manual', SISTEMA: 'Sistema' };
