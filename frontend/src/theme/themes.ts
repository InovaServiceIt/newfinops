/** Skins disponíveis. O id é o valor de data-theme no <html> e o que fica salvo no localStorage. */
export const THEMES = [
  { id: 'serviceit-light', name: 'Service IT Light', mode: 'light' },
  { id: 'serviceit-dark', name: 'Service IT Dark', mode: 'dark' },
] as const;

export type ThemeId = (typeof THEMES)[number]['id'];

export const DEFAULT_THEME: ThemeId = 'serviceit-light';

/** Chave do localStorage (o script inline do index.html usa a mesma chave). */
export const THEME_STORAGE_KEY = 'finops.theme';

export function isThemeId(value: unknown): value is ThemeId {
  return THEMES.some((t) => t.id === value);
}

/**
 * Converte o valor salvo no localStorage em um skin válido.
 * Ids que não existem mais (skin renomeado) são migrados pelo modo: "*-dark" → skin escuro,
 * "*-light" → skin claro. Qualquer outro valor → null (usa o padrão).
 * Mantenha a mesma regra no script inline do index.html.
 */
export function resolveStoredTheme(value: unknown): ThemeId | null {
  if (isThemeId(value)) return value;
  if (typeof value !== 'string') return null;
  const mode = value.endsWith('-dark') ? 'dark' : value.endsWith('-light') ? 'light' : null;
  return mode ? (THEMES.find((t) => t.mode === mode)?.id ?? null) : null;
}
