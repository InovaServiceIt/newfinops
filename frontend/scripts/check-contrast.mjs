#!/usr/bin/env node
/**
 * check:contrast — calcula o contraste WCAG 2.x dos pares texto/fundo de cada skin
 * (arquivos src/theme/*.css com [data-theme="..."]) e falha se algum ficar abaixo de 4.5:1.
 *
 * Cores com alpha (glass) são compostas sobre o fundo em que aparecem na tela
 * (ver BACKDROPS). Quando um token pode aparecer sobre mais de um fundo, vale o pior caso.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const THEME_DIR = fileURLToPath(new URL('../src/theme/', import.meta.url));
const MIN_RATIO = 4.5;

/** Pares [texto, fundo]. Os 9 primeiros grupos são os exigidos; os demais cobrem usos reais da casca. */
const PAIRS = [
  ['--text', '--bg'],
  ['--text', '--surface'],
  ['--text-muted', '--bg'],
  ['--text-muted', '--surface'],
  ['--text-on-accent', '--accent'],
  ['--header-text', '--header-bg'],
  ['--sidebar-item', '--sidebar-bg'],
  ['--sidebar-item-active-text', '--sidebar-item-active-bg'],
  ['--success', '--success-soft'],
  ['--warning', '--warning-soft'],
  ['--danger', '--danger-soft'],
  ['--info', '--info-soft'],
  // extras
  ['--text', '--surface-2'],
  ['--text-muted', '--surface-2'],
  ['--text-on-accent', '--accent-hover'],
  ['--accent', '--surface'],
  ['--header-text-muted', '--header-bg'],
  ['--header-text', '--header-brand-icon-bg'],
  ['--sidebar-section', '--sidebar-bg'],
  ['--sidebar-item-active-text', '--accent-soft'],
];

/** Onde cada fundo translúcido aparece (para compor o alpha). Sem entrada = sobre --bg. */
const BACKDROPS = {
  '--surface': ['--bg'],
  '--surface-2': ['--bg', '--surface'],
  '--surface-hover': ['--bg', '--surface'],
  '--sidebar-bg': ['--bg'],
  '--sidebar-item-active-bg': ['--sidebar-bg'],
  '--accent-soft': ['--bg', '--surface'],
  '--success-soft': ['--bg', '--surface'],
  '--warning-soft': ['--bg', '--surface'],
  '--danger-soft': ['--bg', '--surface'],
  '--info-soft': ['--bg', '--surface'],
};

// ── Parsing ──────────────────────────────────────────────────────────────────
function parseThemes() {
  const themes = [];
  for (const file of readdirSync(THEME_DIR).filter((f) => f.endsWith('.css'))) {
    const css = readFileSync(join(THEME_DIR, file), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    const id = css.match(/\[data-theme="([^"]+)"\]/)?.[1];
    if (!id) continue;
    const vars = {};
    for (const m of css.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) vars[m[1]] = m[2].trim();
    themes.push({ id, file, vars });
  }
  return themes;
}

function parseColor(value, vars, seen = new Set()) {
  const v = value.trim();
  const ref = v.match(/^var\((--[\w-]+)\)$/);
  if (ref) {
    if (seen.has(ref[1]) || !(ref[1] in vars)) throw new Error(`referência inválida: ${v}`);
    seen.add(ref[1]);
    return parseColor(vars[ref[1]], vars, seen);
  }
  let m = v.match(/^#([0-9a-f]{3,8})$/i);
  if (m) {
    let h = m[1];
    if (h.length <= 4) h = [...h].map((c) => c + c).join('');
    const n = (i) => parseInt(h.slice(i, i + 2), 16);
    return { r: n(0), g: n(2), b: n(4), a: h.length === 8 ? n(6) / 255 : 1 };
  }
  m = v.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)$/i);
  if (m) return { r: +m[1], g: +m[2], b: +m[3], a: m[4] === undefined ? 1 : +m[4] };
  throw new Error(`formato de cor não suportado: ${v}`);
}

// ── Cor e contraste ──────────────────────────────────────────────────────────
const over = (top, base) => ({
  r: top.r * top.a + base.r * (1 - top.a),
  g: top.g * top.a + base.g * (1 - top.a),
  b: top.b * top.a + base.b * (1 - top.a),
  a: 1,
});

function luminance({ r, g, b }) {
  const ch = (c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b);
}

function ratio(a, b) {
  const [l1, l2] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (l1 + 0.05) / (l2 + 0.05);
}

const hex = ({ r, g, b }) => '#' + [r, g, b].map((c) => Math.round(c).toString(16).padStart(2, '0')).join('');

/** Todas as cores opacas possíveis de um token na tela (uma por fundo em que aparece). */
function effectiveBackgrounds(token, theme, depth = 0) {
  const color = parseColor(theme.vars[token], theme.vars);
  if (color.a >= 1) return [color];
  if (depth > 4) throw new Error(`cadeia de fundos muito longa em ${token}`);
  const bases = BACKDROPS[token] ?? ['--bg'];
  return bases.flatMap((b) => effectiveBackgrounds(b, theme, depth + 1)).map((base) => over(color, base));
}

// ── Execução ─────────────────────────────────────────────────────────────────
const themes = parseThemes();
if (themes.length === 0) {
  console.error('✖ Nenhum skin encontrado em src/theme/.');
  process.exit(1);
}

let failures = 0;
for (const theme of themes) {
  console.log(`\n${theme.id}  (${theme.file})`);
  console.log('| Texto | Fundo | Fundo efetivo | Contraste | Resultado |');
  console.log('|---|---|---|---:|---|');
  for (const [fgToken, bgToken] of PAIRS) {
    try {
      const results = effectiveBackgrounds(bgToken, theme).map((bg) => {
        const fgRaw = parseColor(theme.vars[fgToken], theme.vars);
        const fg = fgRaw.a < 1 ? over(fgRaw, bg) : fgRaw;
        return { bg, value: ratio(fg, bg) };
      });
      const worst = results.reduce((a, b) => (b.value < a.value ? b : a));
      const ok = worst.value >= MIN_RATIO;
      if (!ok) failures++;
      console.log(
        `| \`${fgToken}\` | \`${bgToken}\` | ${hex(worst.bg)} | ${worst.value.toFixed(2)}:1 | ${ok ? '✅' : '❌ < 4.5'} |`,
      );
    } catch (err) {
      failures++;
      console.log(`| \`${fgToken}\` | \`${bgToken}\` | — | — | ❌ ${err.message} |`);
    }
  }
}

if (failures > 0) {
  console.error(`\n✖ check:contrast: ${failures} par(es) abaixo de ${MIN_RATIO}:1.`);
  process.exit(1);
}
console.log(`\n✔ check:contrast: todos os pares ≥ ${MIN_RATIO}:1 em ${themes.length} skins.`);
