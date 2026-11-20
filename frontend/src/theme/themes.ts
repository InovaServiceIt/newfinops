/** Skins disponíveis. O id é o valor de data-theme no <html> e o que fica salvo no localStorage. */
export const THEMES = [
  { id: 'serviceit-light', name: 'Service IT Light', mode: 'light' },
  { id: 'highlander-dark', name: 'Highlander Dark', mode: 'dark' },
] as const;

export type ThemeId = (typeof THEMES)[number]['id'];

export const DEFAULT_THEME: ThemeId = 'serviceit-light';

/** Chave do localStorage (o script inline do index.html usa a mesma chave). */
export const THEME_STORAGE_KEY = 'finops.theme';

export function isThemeId(value: unknown): value is ThemeId {
  return THEMES.some((t) => t.id === value);
}
