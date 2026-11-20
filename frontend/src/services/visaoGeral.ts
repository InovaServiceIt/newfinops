// Cliente da API /api/visao-geral (tipos espelham os DTOs do backend).

export interface ProvedorOpcao {
  id: number;
  tipo: string;
  nome: string | null;
  cliente: string | null;
  rotulo: string;
}

export interface OpcoesVisaoGeral {
  provedores: ProvedorOpcao[];
  provedorSelecionado: number | null;
  anos: number[];
  servicos: string[];
  skus: string[];
  regioes: string[];
  fator: number | null;
}

export interface Indicador {
  valor: number | null;
  percentualOrcamento: number | null;
  referencia: string | null;
}

export type Faixa = 'ok' | 'atencao' | 'acima' | 'sem_orcamento';

export interface BarraMensal {
  ano: number;
  mes: number;
  rotulo: string;
  valor: number;
  projecao: boolean;
  faixa: Faixa;
}

export interface PontoMensal {
  ano: number;
  mes: number;
  rotulo: string;
  valor: number;
  parcial: boolean;
}

export interface ItemRanking {
  nome: string;
  valor: number;
}

export interface BlocoPeriodo {
  inicio: string;
  fim: string;
  total: number;
  topServicos: ItemRanking[];
  topSkus: ItemRanking[];
  topRegioes: ItemRanking[];
}

export interface VisaoGeral {
  provedor: number;
  nomeProvedor: string | null;
  ano: number;
  comFator: boolean;
  fator: number | null;
  referencias: {
    hoje: string;
    mesAtual: string;
    ultimoMesFechado: string;
    semanaAnteriorInicio: string;
    semanaAnteriorFim: string;
    ontem: string;
  };
  orcamentoMensal: number | null;
  indicadores: {
    orcamentoAnual: Indicador;
    acumuladoAno: Indicador;
    ultimoMes: Indicador;
    mesAtual: Indicador;
    projecaoMes: Indicador;
    projecaoAnual: Indicador;
  };
  custoMensal: BarraMensal[];
  ultimosDozeMeses: PontoMensal[];
  semanaAnterior: BlocoPeriodo;
  diaAnterior: BlocoPeriodo;
  custoDiario: { dia: number; valor: number }[];
}

export interface FiltrosVisaoGeral {
  provedor: number;
  ano: number;
  meses: number[];
  servicos: string[];
  skus: string[];
  regioes: string[];
  comFator: boolean;
}

/** Erro com a mensagem amigável que a API devolve em { erro }. */
export class ApiError extends Error {}

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal, headers: { Accept: 'application/json' } });
  if (res.status === 401) {
    window.location.href = '/login';
    throw new ApiError('Sessão expirada.');
  }
  if (!res.ok) {
    let mensagem = 'Não foi possível carregar os dados.';
    try {
      const body = await res.json();
      if (body && typeof body.erro === 'string') mensagem = body.erro;
    } catch {
      /* corpo não é JSON */
    }
    throw new ApiError(mensagem);
  }
  return (await res.json()) as T;
}

export function buscarOpcoes(provedor: number | null, signal?: AbortSignal): Promise<OpcoesVisaoGeral> {
  const qs = provedor ? `?provedor=${provedor}` : '';
  return getJson<OpcoesVisaoGeral>(`/api/visao-geral/opcoes${qs}`, signal);
}

export function buscarVisaoGeral(f: FiltrosVisaoGeral, signal?: AbortSignal): Promise<VisaoGeral> {
  const qs = new URLSearchParams();
  qs.set('provedor', String(f.provedor));
  qs.set('ano', String(f.ano));
  qs.set('comFator', String(f.comFator));
  f.meses.forEach((m) => qs.append('meses', String(m)));
  f.servicos.forEach((s) => qs.append('servicos', s));
  f.skus.forEach((s) => qs.append('skus', s));
  f.regioes.forEach((s) => qs.append('regioes', s));
  return getJson<VisaoGeral>(`/api/visao-geral?${qs.toString()}`, signal);
}
