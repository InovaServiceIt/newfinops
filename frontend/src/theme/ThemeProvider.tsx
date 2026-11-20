import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { DEFAULT_THEME, THEME_STORAGE_KEY, THEMES, ThemeId, isThemeId } from './themes';

interface ThemeContextValue {
  theme: ThemeId;
  setTheme: (theme: ThemeId) => void;
  /** Alterna para o próximo skin da lista. */
  cycleTheme: () => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function readInitialTheme(): ThemeId {
  // O script inline do index.html já aplicou data-theme antes do React montar.
  const fromDom = document.documentElement.getAttribute('data-theme');
  if (isThemeId(fromDom)) return fromDom;
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (isThemeId(stored)) return stored;
  } catch {
    /* localStorage indisponível: usa o padrão */
  }
  return DEFAULT_THEME;
}

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [theme, setThemeState] = useState<ThemeId>(readInitialTheme);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      /* sem persistência: o skin vale só para esta aba */
    }
  }, [theme]);

  const setTheme = useCallback((next: ThemeId) => setThemeState(next), []);

  const cycleTheme = useCallback(() => {
    setThemeState((current) => {
      const index = THEMES.findIndex((t) => t.id === current);
      return THEMES[(index + 1) % THEMES.length].id;
    });
  }, []);

  return (
    <ThemeContext.Provider value={{ theme, setTheme, cycleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme precisa estar dentro de <ThemeProvider>.');
  return ctx;
}
