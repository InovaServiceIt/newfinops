// Cliente dos cadastros de administração (/api/admin/*).
import { apiGet, apiSend } from './api';
import type { ClienteResumo, Perfil } from '../components/auth/AuthGuard';

export interface Cliente {
  id: number;
  nome: string;
  apelido: string;
  documento: string | null;
  ativo: boolean;
  criadoEm: string;
  atualizadoEm: string | null;
}

export interface ClienteEntrada {
  nome: string;
  apelido: string;
  documento: string | null;
}

export interface Usuario {
  id: number;
  email: string;
  nome: string;
  perfil: Perfil;
  ativo: boolean;
  ultimoLoginEm: string | null;
  clientes: ClienteResumo[];
}

export interface UsuarioEntrada {
  email?: string;
  nome: string;
  perfil: Perfil;
  clienteIds: number[];
}

export const listarClientes = (incluirInativos: boolean) =>
  apiGet<Cliente[]>(`/api/admin/clientes?incluirInativos=${incluirInativos}`);
export const criarCliente = (c: ClienteEntrada) => apiSend<Cliente>('POST', '/api/admin/clientes', c);
export const atualizarCliente = (id: number, c: ClienteEntrada) => apiSend<Cliente>('PUT', `/api/admin/clientes/${id}`, c);
export const alterarAtivoCliente = (id: number, ativo: boolean) =>
  apiSend<Cliente>('PATCH', `/api/admin/clientes/${id}/ativo`, { ativo });

export interface UsuarioAfetado {
  id: number;
  nome: string;
  email: string;
}

/** Usuários CLIENTE que ficariam sem nenhum cliente ativo se o cliente fosse desativado. */
export const impactoDesativacaoCliente = (id: number) =>
  apiGet<{ usuariosAfetados: UsuarioAfetado[] }>(`/api/admin/clientes/${id}/impacto-desativacao`);

export const listarUsuarios = () => apiGet<Usuario[]>('/api/admin/usuarios');
export const criarUsuario = (u: UsuarioEntrada) => apiSend<Usuario>('POST', '/api/admin/usuarios', u);
export const atualizarUsuario = (id: number, u: UsuarioEntrada) =>
  apiSend<Usuario>('PUT', `/api/admin/usuarios/${id}`, { nome: u.nome, perfil: u.perfil, clienteIds: u.clienteIds });
export const alterarAtivoUsuario = (id: number, ativo: boolean) =>
  apiSend<Usuario>('PATCH', `/api/admin/usuarios/${id}/ativo`, { ativo });
