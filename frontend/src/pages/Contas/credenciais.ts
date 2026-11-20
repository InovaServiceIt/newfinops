import type { Provedor } from '../../services/contas';
import type { StatusCredencial, TipoCredencial } from '../../services/credencial';

export interface CampoCredencial {
  nome: string;
  rotulo: string;
  /** Campo secreto: input password com "mostrar" momentâneo. */
  secreto?: boolean;
  /** Texto longo (PEM/JSON): textarea + "carregar arquivo" (lido no navegador). */
  multilinha?: boolean;
  opcional?: boolean;
  placeholder?: string;
}

export interface InfoTipo {
  tipo: TipoCredencial;
  rotulo: string;
  provedor: Provedor;
  campos: CampoCredencial[];
}

export const TIPOS: InfoTipo[] = [
  {
    tipo: 'OCI_API_KEY', rotulo: 'OCI · API Key', provedor: 'OCI',
    campos: [
      { nome: 'user_ocid', rotulo: 'User OCID', placeholder: 'ocid1.user.oc1..aaaa…' },
      { nome: 'tenancy_ocid', rotulo: 'Tenancy OCID', placeholder: 'ocid1.tenancy.oc1..aaaa… (igual ao da conta)' },
      { nome: 'fingerprint', rotulo: 'Fingerprint', placeholder: 'aa:bb:cc:…' },
      { nome: 'region', rotulo: 'Região', placeholder: 'sa-saopaulo-1' },
      { nome: 'private_key_pem', rotulo: 'Chave privada (PEM)', secreto: true, multilinha: true, placeholder: '-----BEGIN PRIVATE KEY-----' },
      { nome: 'passphrase', rotulo: 'Passphrase da chave', secreto: true, opcional: true },
    ],
  },
  {
    tipo: 'AWS_ACCESS_KEY', rotulo: 'AWS · Access key', provedor: 'AWS',
    campos: [
      { nome: 'access_key_id', rotulo: 'Access key ID', placeholder: 'AKIA…' },
      { nome: 'secret_access_key', rotulo: 'Secret access key', secreto: true },
    ],
  },
  {
    tipo: 'AWS_ASSUME_ROLE', rotulo: 'AWS · Assume role', provedor: 'AWS',
    campos: [
      { nome: 'access_key_id', rotulo: 'Access key ID', placeholder: 'AKIA…' },
      { nome: 'secret_access_key', rotulo: 'Secret access key', secreto: true },
      { nome: 'role_arn', rotulo: 'Role ARN', placeholder: 'arn:aws:iam::123456789012:role/CloudVistaLeitura' },
      { nome: 'external_id', rotulo: 'External ID', opcional: true },
    ],
  },
  {
    tipo: 'AZURE_SERVICE_PRINCIPAL', rotulo: 'Azure · Service principal', provedor: 'AZURE',
    campos: [
      { nome: 'tenant_id', rotulo: 'Tenant ID', placeholder: '00000000-0000-0000-0000-000000000000' },
      { nome: 'client_id', rotulo: 'Client ID (app id)', placeholder: '00000000-0000-0000-0000-000000000000' },
      { nome: 'client_secret', rotulo: 'Client secret', secreto: true },
    ],
  },
  {
    tipo: 'GCP_SERVICE_ACCOUNT', rotulo: 'GCP · Service account', provedor: 'GCP',
    campos: [
      { nome: 'service_account_json', rotulo: 'JSON da service account', secreto: true, multilinha: true, placeholder: '{ "type": "service_account", … }' },
    ],
  },
];

/** Passo a passo de onboarding: permissões mínimas, somente leitura. Texto estático nesta fase. */
export const ONBOARDING: Record<Provedor, string[]> = {
  OCI: [
    'Crie um usuário de serviço e um grupo dedicados ao CloudVista.',
    'Políticas do grupo: "allow group <grupo> to read all-resources in tenancy" e "allow group <grupo> to read usage-report in tenancy".',
    'Gere uma API key para o usuário e informe aqui o fingerprint e a chave privada.',
  ],
  AWS: [
    'Use um usuário/role IAM dedicado, preferencialmente na conta payer/management.',
    'Anexe a policy ReadOnlyAccess (ou equivalente restrita) e leitura (s3:GetObject/ListBucket) do bucket do export de billing (CUR).',
    'Com Assume role, informe o ARN do role e, se houver, o External ID.',
  ],
  AZURE: [
    'Registre um aplicativo (service principal) no Entra ID do cliente e crie um client secret.',
    'Atribua os papéis "Reader" e "Cost Management Reader" no escopo da subscrição ou do management group.',
  ],
  GCP: [
    'Crie uma service account dedicada e gere uma chave JSON.',
    'Conceda "Viewer" nos projetos/organização e "Billing Account Viewer" na conta de faturamento (e leitura do dataset do export de billing no BigQuery).',
  ],
};

export const STATUS: Record<StatusCredencial | 'SEM', { rotulo: string; classe: string }> = {
  SEM: { rotulo: 'Sem credencial', classe: 'ui-badge-warning' },
  PENDENTE_TESTE: { rotulo: 'Pendente de teste', classe: 'ui-badge-info' },
  ATIVA: { rotulo: 'Ativa', classe: 'ui-badge-success' },
  FALHA_TESTE: { rotulo: 'Falha no teste', classe: 'ui-badge-danger' },
  DESATIVADA: { rotulo: 'Desativada', classe: 'ui-badge-warning' },
};

export const ACOES_AUDITORIA: Record<string, string> = {
  CADASTRADA: 'Cadastrada',
  SUBSTITUIDA: 'Substituída',
  TESTE_SOLICITADO: 'Teste solicitado',
  TESTADA_OK: 'Teste OK',
  TESTADA_FALHA: 'Teste falhou',
  DESATIVADA: 'Desativada',
  REATIVADA: 'Reativada',
};

export const tiposDoProvedor = (p: Provedor) => TIPOS.filter((t) => t.provedor === p);
export const infoTipo = (t: TipoCredencial | null) => TIPOS.find((x) => x.tipo === t);
