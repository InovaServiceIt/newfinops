import { useEffect, useState } from 'react';

function readTokens<T extends string>(names: readonly T[]): Record<T, string> {
  const style = getComputedStyle(document.documentElement);
  return Object.fromEntries(names.map((n) => [n, style.getPropertyValue(n).trim()])) as Record<T, string>;
}

/**
 * Valores atuais de tokens CSS (ex.: cores para bibliotecas de gráfico, que não aceitam var()).
 * Relê quando o atributo data-theme do <html> muda, ou seja, ao trocar o skin.
 */
export function useTokenValues<T extends string>(names: readonly T[]): Record<T, string> {
  const key = names.join('|');
  const [values, setValues] = useState<Record<T, string>>(() => readTokens(names));

  useEffect(() => {
    setValues(readTokens(names));
    const observer = new MutationObserver(() => setValues(readTokens(names)));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return values;
}
