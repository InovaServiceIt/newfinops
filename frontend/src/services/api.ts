// Cliente HTTP comum da API CloudVista.

/** Erro com a mensagem amigável que a API devolve em { erro }. */
export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

export const CODIGO_NAO_AUTORIZADO = 'USUARIO_NAO_AUTORIZADO';

async function tratarErro(res: Response): Promise<never> {
  let mensagem = 'Não foi possível concluir a operação.';
  let codigo: string | undefined;
  try {
    const body = await res.json();
    if (body && typeof body.erro === 'string') mensagem = body.erro;
    if (body && typeof body.codigo === 'string') codigo = body.codigo;
  } catch {
    /* corpo não é JSON */
  }

  if (res.status === 401) {
    window.location.href = '/login';
    throw new ApiError('Sessão expirada.', 401);
  }
  if (res.status === 403 && codigo === CODIGO_NAO_AUTORIZADO) {
    // O acesso foi revogado durante a sessão: recarrega para o AuthGuard mostrar a tela certa.
    window.location.reload();
    throw new ApiError('Acesso não autorizado.', 403);
  }
  if (res.status === 403) mensagem = 'Você não tem permissão para esta operação.';
  throw new ApiError(mensagem, res.status);
}

export async function apiGet<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal, headers: { Accept: 'application/json' } });
  if (!res.ok) return tratarErro(res);
  return (await res.json()) as T;
}

export async function apiSend<T>(method: 'POST' | 'PUT' | 'PATCH', url: string, body: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) return tratarErro(res);
  return (await res.json()) as T;
}

export function mensagemDeErro(e: unknown, padrao = 'Não foi possível concluir a operação.'): string {
  return e instanceof ApiError ? e.message : padrao;
}
