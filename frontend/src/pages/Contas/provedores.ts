import type { Escopo, Provedor } from '../../services/contas';

/** Metadados dos provedores. A validação espelha o backend (que é a autoridade). */
export interface InfoEscopo {
  escopo: Escopo;
  rotulo: string;
  padrao: RegExp;
  placeholder: string;
  ajuda: string;
}

export interface InfoProvedor {
  rotulo: string;
  escopos: InfoEscopo[];
  regiaoExemplo: string;
}

const GUID = /^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$/;

export const PROVEDORES: Record<Provedor, InfoProvedor> = {
  OCI: {
    rotulo: 'OCI',
    regiaoExemplo: 'sa-saopaulo-1',
    escopos: [
      {
        escopo: 'TENANCY', rotulo: 'Tenancy',
        padrao: /^ocid1\.tenancy\.oc1\.\.[a-z0-9]+$/,
        placeholder: 'ocid1.tenancy.oc1..aaaa…',
        ajuda: 'OCID da tenancy (começa com ocid1.tenancy.oc1..).',
      },
    ],
  },
  AWS: {
    rotulo: 'AWS',
    regiaoExemplo: 'us-east-1',
    escopos: [
      { escopo: 'CONTA', rotulo: 'Conta', padrao: /^[0-9]{12}$/, placeholder: '123456789012', ajuda: 'Account id com 12 dígitos.' },
      {
        escopo: 'ORGANIZACAO', rotulo: 'Organização', padrao: /^[0-9]{12}$/, placeholder: '123456789012',
        ajuda: 'Account id (12 dígitos) da conta payer/management da organização.',
      },
    ],
  },
  AZURE: {
    rotulo: 'Azure',
    regiaoExemplo: 'brazilsouth',
    escopos: [
      { escopo: 'TENANT', rotulo: 'Tenant', padrao: GUID, placeholder: '00000000-0000-0000-0000-000000000000', ajuda: 'Tenant id (GUID).' },
      { escopo: 'SUBSCRICAO', rotulo: 'Subscrição', padrao: GUID, placeholder: '00000000-0000-0000-0000-000000000000', ajuda: 'Subscription id (GUID).' },
    ],
  },
  GCP: {
    rotulo: 'GCP',
    regiaoExemplo: 'southamerica-east1',
    escopos: [
      { escopo: 'ORGANIZACAO', rotulo: 'Organização', padrao: /^[0-9]+$/, placeholder: '123456789012', ajuda: 'Id numérico da organização.' },
      {
        escopo: 'PROJETO', rotulo: 'Projeto', padrao: /^[a-z][a-z0-9-]{4,28}[a-z0-9]$/, placeholder: 'meu-projeto-prod',
        ajuda: 'Project id: 6 a 30 caracteres, minúsculas, dígitos e hífen, começando por letra.',
      },
    ],
  },
};

export const LISTA_PROVEDORES = Object.keys(PROVEDORES) as Provedor[];

export const ROTULO_ESCOPO: Record<Escopo, string> = {
  TENANCY: 'Tenancy',
  CONTA: 'Conta',
  ORGANIZACAO: 'Organização',
  TENANT: 'Tenant',
  SUBSCRICAO: 'Subscrição',
  PROJETO: 'Projeto',
};

export function infoEscopo(provedor: Provedor, escopo: Escopo): InfoEscopo | undefined {
  return PROVEDORES[provedor].escopos.find((e) => e.escopo === escopo);
}

/** Mensagem de erro do identificador (ou null se válido) — espelho da validação do backend. */
export function validarIdentificador(provedor: Provedor, escopo: Escopo, identificador: string): string | null {
  const id = identificador.trim();
  if (!id) return 'Informe o identificador.';
  if (id.length > 200) return 'No máximo 200 caracteres.';
  const info = infoEscopo(provedor, escopo);
  if (!info) return 'Escopo inválido para o provedor.';
  return info.padrao.test(id) ? null : `Formato inválido. ${info.ajuda}`;
}
