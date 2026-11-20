// Cliente das contas de cloud (/api/contas e /api/admin/contas).
import { apiGet, apiSend } from './api';

export type Provedor = 'OCI' | 'AWS' | 'AZURE' | 'GCP';
export type Escopo = 'TENANCY' | 'CONTA' | 'ORGANIZACAO' | 'TENANT' | 'SUBSCRICAO' | 'PROJETO';

export interface ContaCloud {
  id: number;
  clienteId: number;
  clienteNome: string;
  provedor: Provedor;
  nome: string;
  identificador: string;
  escopo: Escopo;
  regiaoPadrao: string | null;
  ativo: boolean;
  criadoEm: string;
  /** Placeholder do MD 0.3 (cofre de credenciais). */
  credencialStatus: string | null;
}

export interface FiltroContas {
  clienteId?: number | null;
  provedor?: Provedor | null;
  incluirInativas?: boolean;
}

export interface ContaCriacao {
  clienteId: number;
  provedor: Provedor;
  nome: string;
  identificador: string;
  escopo: Escopo;
  regiaoPadrao: string | null;
}

export interface ContaAlteracao {
  nome: string;
  escopo: Escopo;
  regiaoPadrao: string | null;
}

export function listarContas(f: FiltroContas = {}): Promise<ContaCloud[]> {
  const qs = new URLSearchParams();
  if (f.clienteId) qs.set('clienteId', String(f.clienteId));
  if (f.provedor) qs.set('provedor', f.provedor);
  qs.set('incluirInativas', String(!!f.incluirInativas));
  return apiGet<ContaCloud[]>(`/api/contas?${qs.toString()}`);
}

export const criarConta = (c: ContaCriacao) => apiSend<ContaCloud>('POST', '/api/admin/contas', c);
export const atualizarConta = (id: number, c: ContaAlteracao) => apiSend<ContaCloud>('PUT', `/api/admin/contas/${id}`, c);
export const alterarAtivoConta = (id: number, ativo: boolean) =>
  apiSend<ContaCloud>('PATCH', `/api/admin/contas/${id}/ativo`, { ativo });
