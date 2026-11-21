import { apiGet } from './api';

// Cliente de /api/inventario (tipos espelham os DTOs do backend).

export interface ContagemItem { chave: string; total: number }
export interface CompartimentoOpcao { ocid: string; caminho: string }
export interface ProvenienciaConta { contaId: number; contaNome: string | null; vistoEm: string | null; job: number | null }

export type Sinal = 'volumes_soltos' | 'ips_sem_uso' | 'instancias_paradas' | 'backups_antigos' | 'certificados_vencendo' | 'sem_tag';

export interface ResumoInventario {
  totalAtivos: number;
  novos7d: number;
  removidos7d: number;
  porCategoria: ContagemItem[];
  sinais: Record<Sinal, number>;
  regioes: string[];
  tipos: ContagemItem[];
  estados: string[];
  compartimentos: CompartimentoOpcao[];
  proveniencia: ProvenienciaConta[];
}

export type Tags = { freeform?: Record<string, string>; defined?: Record<string, Record<string, unknown>> };

export interface RecursoItem {
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
  removidoEm: string | null;
}

export interface PaginaRecursos { itens: RecursoItem[]; total: number; pagina: number; tamanho: number }

export interface RecursoDetalhe extends Omit<RecursoItem, 'compartimento'> {
  contaNome: string | null;
  compartimentoOcid: string | null;
  compartimento: string | null;
  atributos: Record<string, string | number | boolean>;
  primeiroVistoEm: string;
  job: number | null;
}

export type Ordem = 'nome' | 'tipo' | 'regiao' | 'compartimento' | 'estado' | 'criado' | 'visto';

export interface FiltroInventario {
  clienteId: number;
  contaId: number | null;
  regiao: string;
  tipo: string;
  compartimento: string;
  estado: string;
  busca: string;
  tag: string;
  sinal: Sinal | '';
  removidos: boolean;
}

function query(f: FiltroInventario, extra: Record<string, string> = {}): string {
  const qs = new URLSearchParams({ clienteId: String(f.clienteId), ...extra });
  if (f.contaId) qs.set('contaId', String(f.contaId));
  for (const k of ['regiao', 'tipo', 'compartimento', 'estado', 'busca', 'tag', 'sinal'] as const)
    if (f[k].trim()) qs.set(k, f[k].trim());
  if (f.removidos) qs.set('removidos', 'true');
  return qs.toString();
}

export function buscarResumo(clienteId: number, contaId: number | null, signal?: AbortSignal): Promise<ResumoInventario> {
  const qs = new URLSearchParams({ clienteId: String(clienteId) });
  if (contaId) qs.set('contaId', String(contaId));
  return apiGet<ResumoInventario>(`/api/inventario/resumo?${qs.toString()}`, signal);
}

export const buscarRecursos = (f: FiltroInventario, pagina: number, ordem: Ordem, direcao: 'asc' | 'desc', signal?: AbortSignal) =>
  apiGet<PaginaRecursos>(`/api/inventario/recursos?${query(f, { pagina: String(pagina), ordem, direcao })}`, signal);

export const buscarRecurso = (contaId: number, chave: string) =>
  apiGet<RecursoDetalhe>(`/api/inventario/recurso?${new URLSearchParams({ contaId: String(contaId), chave }).toString()}`);

/** URL do CSV (mesma origem: o cookie de sessão vai junto). */
export const urlExportar = (f: FiltroInventario) => `/api/inventario/exportar?${query(f)}`;

// ── Apresentação ──

/** Rótulos em português por tipo técnico (o tipo aparece no tooltip). */
export const ROTULO_TIPO: Record<string, string> = {
  'compute.instances': 'Instâncias',
  'block_storage.volumes': 'Volumes',
  'block_storage.boot_volumes': 'Boot volumes',
  'block_storage.volume_backups': 'Backups de volume',
  'block_storage.boot_volume_backups': 'Backups de boot volume',
  'object_storage.buckets': 'Buckets',
  'database.db_systems': 'DB Systems',
  'database.autonomous_databases': 'Autonomous DB',
  'database.cloud_vm_clusters': 'Cloud VM Clusters',
  'database.vm_clusters': 'VM Clusters',
  'database.databases': 'Bancos de dados',
  'database.db_homes': 'DB Homes',
  'oke.clusters': 'Clusters OKE',
  'oke.node_pools': 'Node pools',
  'oke.virtual_node_pools': 'Virtual node pools',
  'load_balancers.load_balancers': 'Load balancers',
  'load_balancers.network_load_balancers': 'Network load balancers',
  'file_storage.file_systems': 'File systems',
  'file_storage.mount_targets': 'Mount targets',
  'network.vcns': 'VCNs',
  'network.subnets': 'Sub-redes',
  'network.internet_gateways': 'Internet gateways',
  'network.nat_gateways': 'NAT gateways',
  'network.service_gateways': 'Service gateways',
  'network.drgs': 'DRGs',
  'network.ip_sec_connections': 'Conexões IPSec',
  'network.virtual_circuits': 'FastConnect',
  'network.public_ips': 'IPs públicos',
  'security.vaults': 'Vaults',
  'security.keys': 'Chaves (KMS)',
  'security.bastions': 'Bastions',
  'security.certificates': 'Certificados',
  'serverless.applications': 'Aplicações (Functions)',
  'serverless.functions': 'Functions',
  'serverless.gateways': 'API gateways',
  'serverless.api_deployments': 'API deployments',
  'serverless.container_instances': 'Container instances',
  'scheduler.schedules': 'Agendamentos',
};

export const ROTULO_CATEGORIA: Record<string, string> = {
  compute: 'Computação', block_storage: 'Block storage', object_storage: 'Object storage', database: 'Banco de dados',
  oke: 'Kubernetes', load_balancers: 'Load balancers', file_storage: 'File storage', network: 'Rede',
  security: 'Segurança', serverless: 'Serverless', scheduler: 'Agendador',
};

export const rotuloTipo = (tipo: string) => ROTULO_TIPO[tipo] ?? tipo;

export const SINAIS: { chave: Sinal; rotulo: string; ajuda: string }[] = [
  { chave: 'volumes_soltos', rotulo: 'Volumes não anexados', ajuda: 'Volumes e boot volumes sem anexo ATTACHED' },
  { chave: 'ips_sem_uso', rotulo: 'IPs reservados sem uso', ajuda: 'IPs públicos RESERVED sem atribuição' },
  { chave: 'instancias_paradas', rotulo: 'Instâncias paradas', ajuda: 'Instâncias em STOPPED (o boot volume continua cobrando)' },
  { chave: 'backups_antigos', rotulo: 'Backups > 90 dias', ajuda: 'Backups de volume criados há mais de 90 dias' },
  { chave: 'certificados_vencendo', rotulo: 'Certificados vencendo', ajuda: 'Validade em até 30 dias (ou já vencidos)' },
  { chave: 'sem_tag', rotulo: 'Sem tag (exceto automáticas)', ajuda: 'Sem nenhuma tag defined ou freeform, desconsiderando Oracle-Tags CreatedBy/CreatedOn (mesma regra da Governança)' },
];

/** Tags achatadas: freeform "chave=valor" e defined "ns.chave=valor". */
export function listarTags(tags: Tags | null | undefined): string[] {
  const lista: string[] = [];
  for (const [k, v] of Object.entries(tags?.freeform ?? {})) lista.push(`${k}=${String(v)}`);
  for (const [ns, chaves] of Object.entries(tags?.defined ?? {}))
    for (const [k, v] of Object.entries(chaves ?? {})) lista.push(`${ns}.${k}=${typeof v === 'object' ? JSON.stringify(v) : String(v)}`);
  return lista;
}
