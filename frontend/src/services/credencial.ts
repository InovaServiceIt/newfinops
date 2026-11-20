// Cliente do cofre de credenciais. O segredo só VAI para a API; nada aqui o devolve.
import { apiGet, apiSend } from './api';

export type TipoCredencial =
  | 'OCI_API_KEY' | 'AWS_ACCESS_KEY' | 'AWS_ASSUME_ROLE' | 'AZURE_SERVICE_PRINCIPAL' | 'GCP_SERVICE_ACCOUNT';

export type StatusCredencial = 'PENDENTE_TESTE' | 'ATIVA' | 'FALHA_TESTE' | 'DESATIVADA';

export interface CredencialMetadados {
  existe: boolean;
  tipo: TipoCredencial | null;
  dica: string | null;
  status: StatusCredencial | null;
  testadaEm: string | null;
  testeResultado: string | null;
  cadastradaPorNome: string | null;
  cadastradaEm: string | null;
}

export interface AuditoriaItem {
  acao: string;
  detalhe: string | null;
  usuario: string;
  origem: string;
  ocorridoEm: string;
}

const base = (contaId: number) => `/api/contas/${contaId}/credencial`;

export const obterCredencial = (contaId: number) => apiGet<CredencialMetadados>(base(contaId));

export const salvarCredencial = (contaId: number, tipo: TipoCredencial, campos: Record<string, string>) =>
  apiSend<CredencialMetadados>('PUT', base(contaId), { tipo, campos, aceiteSomenteLeitura: true });

export const alterarAtivoCredencial = (contaId: number, ativo: boolean) =>
  apiSend<CredencialMetadados>('PATCH', `${base(contaId)}/ativo`, { ativo });

export const testarCredencial = (contaId: number) =>
  apiSend<{ jobId: number; mensagem: string }>('POST', `${base(contaId)}/testar`, {});

export type StatusJob = 'PENDENTE' | 'EXECUTANDO' | 'COLETADO' | 'IMPORTANDO' | 'IMPORTADO' | 'CONCLUIDO' | 'ERRO' | 'CANCELADO';

export interface JobSituacao {
  id: number;
  tipoJob: string;
  status: StatusJob;
  criadoEm: string;
  iniciadoEm: string | null;
  finalizadoEm: string | null;
  erro: string | null;
}

export const obterJob = (id: number) => apiGet<JobSituacao>(`/api/jobs/${id}`);

export const auditoriaCredencial = (contaId: number) => apiGet<AuditoriaItem[]>(`${base(contaId)}/auditoria`);
