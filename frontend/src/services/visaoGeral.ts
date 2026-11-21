import { apiGet, apiSend } from './api';

// Cliente de /api/visao-geral e /api/fator-cliente (tipos espelham os DTOs do backend).

/** null = em apuração (perfil CLIENTE sem fator vigente no período). */
export interface Kpis {
  mesCorrente: number | null;
  mesAnterior: number | null;
  ytd: number | null;
  previsaoMes: number | null;
  creditosMes: number | null;
}

export interface PontoMensal {
  mes: string;
  rotulo: string;
  parcial: boolean;
  valores: Record<string, number>;
  total: number;
  emApuracao?: boolean;
}

export interface PontoDiario {
  dia: string;
  valores: Record<string, number>;
  emApuracao?: boolean;
}

export interface ItemValor {
  nome: string;
  valor: number;
  detalhe: string | null;
  /** OCID quando o nome veio do inventário. */
  chave?: string | null;
}

export interface VisaoMoeda {
  moeda: string;
  kpis: Kpis;
  servicosMensal: string[];
  mensal: PontoMensal[];
  donut: ItemValor[];
  servicosDiario: string[];
  diario: PontoDiario[];
  topCompartimentos: ItemValor[];
  topRecursos: ItemValor[];
  mesEmApuracao?: boolean;
}

export interface VisaoGeral {
  clienteId: number;
  contaId: number | null;
  hoje: string;
  dadosAte: string | null;
  ultimoJob: number | null;
  ultimaImportacao: string | null;
  comFator: boolean;
  /** Ausentes para o perfil CLIENTE (o fator nunca é exposto a ele). */
  fatorDisponivel?: boolean;
  fatorAtual?: number | null;
  vazio: boolean;
  moedas: VisaoMoeda[];
  emApuracao?: boolean;
}

export interface FiltrosVisaoGeral {
  clienteId: number;
  contaId: number | null;
  comFator: boolean;
}

export function buscarVisaoGeral(f: FiltrosVisaoGeral, signal?: AbortSignal): Promise<VisaoGeral> {
  const qs = new URLSearchParams({ clienteId: String(f.clienteId), comFator: String(f.comFator) });
  if (f.contaId) qs.set('contaId', String(f.contaId));
  return apiGet<VisaoGeral>(`/api/visao-geral?${qs.toString()}`, signal);
}

// ── Fator do cliente ──
export interface FatorCliente {
  id: number;
  clienteId: number;
  clienteNome: string | null;
  cloudContaId: number | null;
  contaNome: string | null;
  fator: number;
  vigenteDesde: string;
  vigenteAte: string | null;
  criadoEm: string;
  criadoPor: string | null;
}

export interface FatorEntrada {
  clienteId: number | null;
  cloudContaId: number | null;
  fator: number | null;
  vigenteDesde: string | null;
  vigenteAte: string | null;
}

export const listarFatores = (clienteId?: number | null) =>
  apiGet<FatorCliente[]>(`/api/fator-cliente${clienteId ? `?clienteId=${clienteId}` : ''}`);
export const criarFator = (f: FatorEntrada) => apiSend<FatorCliente>('POST', '/api/fator-cliente', f);
export const atualizarFator = (id: number, f: FatorEntrada) => apiSend<FatorCliente>('PUT', `/api/fator-cliente/${id}`, f);
