#!/usr/bin/env node
/**
 * lint:colors — falha se houver cor literal fora de src/theme/.
 *
 * Detecta, em .css/.ts/.tsx:
 *   - hexadecimais (#abc, #abcd, #aabbcc, #aabbccdd)
 *   - funções de cor: rgb(), rgba(), hsl(), hsla(), hwb(), lab(), lch(), oklab(), oklch(), color()
 *   - nomes de cor CSS (red, white, black, ...) usados como valor de cor
 * Permitidos: transparent, currentColor, inherit, initial, unset, none e var(--token).
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('..', import.meta.url));
const SRC = join(ROOT, 'src');
const THEME_DIR = join(SRC, 'theme') + sep;
const EXTENSIONS = ['.css', '.ts', '.tsx'];

const NAMED_COLORS = [
  'aliceblue', 'antiquewhite', 'aqua', 'aquamarine', 'azure', 'beige', 'bisque', 'black', 'blanchedalmond',
  'blue', 'blueviolet', 'brown', 'burlywood', 'cadetblue', 'chartreuse', 'chocolate', 'coral', 'cornflowerblue',
  'cornsilk', 'crimson', 'cyan', 'darkblue', 'darkcyan', 'darkgoldenrod', 'darkgray', 'darkgreen', 'darkgrey',
  'darkkhaki', 'darkmagenta', 'darkolivegreen', 'darkorange', 'darkorchid', 'darkred', 'darksalmon',
  'darkseagreen', 'darkslateblue', 'darkslategray', 'darkslategrey', 'darkturquoise', 'darkviolet', 'deeppink',
  'deepskyblue', 'dimgray', 'dimgrey', 'dodgerblue', 'firebrick', 'floralwhite', 'forestgreen', 'fuchsia',
  'gainsboro', 'ghostwhite', 'gold', 'goldenrod', 'gray', 'green', 'greenyellow', 'grey', 'honeydew', 'hotpink',
  'indianred', 'indigo', 'ivory', 'khaki', 'lavender', 'lavenderblush', 'lawngreen', 'lemonchiffon', 'lightblue',
  'lightcoral', 'lightcyan', 'lightgoldenrodyellow', 'lightgray', 'lightgreen', 'lightgrey', 'lightpink',
  'lightsalmon', 'lightseagreen', 'lightskyblue', 'lightslategray', 'lightslategrey', 'lightsteelblue',
  'lightyellow', 'lime', 'limegreen', 'linen', 'magenta', 'maroon', 'mediumaquamarine', 'mediumblue',
  'mediumorchid', 'mediumpurple', 'mediumseagreen', 'mediumslateblue', 'mediumspringgreen', 'mediumturquoise',
  'mediumvioletred', 'midnightblue', 'mintcream', 'mistyrose', 'moccasin', 'navajowhite', 'navy', 'oldlace',
  'olive', 'olivedrab', 'orange', 'orangered', 'orchid', 'palegoldenrod', 'palegreen', 'paleturquoise',
  'palevioletred', 'papayawhip', 'peachpuff', 'peru', 'pink', 'plum', 'powderblue', 'purple', 'rebeccapurple',
  'red', 'rosybrown', 'royalblue', 'saddlebrown', 'salmon', 'sandybrown', 'seagreen', 'seashell', 'sienna',
  'silver', 'skyblue', 'slateblue', 'slategray', 'slategrey', 'snow', 'springgreen', 'steelblue', 'tan', 'teal',
  'thistle', 'tomato', 'turquoise', 'violet', 'wheat', 'white', 'whitesmoke', 'yellow', 'yellowgreen',
];

const HEX = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/g;
const COLOR_FN = /\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\s*\(/gi;

// Propriedades CSS (ou chaves de style={{}} em camelCase) que recebem cor.
const COLOR_PROPS =
  '(?:color|background(?:-color)?|backgroundColor|border(?:-(?:top|right|bottom|left))?(?:-color)?|' +
  'border(?:Top|Right|Bottom|Left)?Color|outline(?:-color)?|outlineColor|fill|stroke|box-shadow|boxShadow|' +
  'text-shadow|textShadow|caret-color|caretColor|accent-color|accentColor|text-decoration-color|column-rule-color)';
const NAMED_IN_CSS = new RegExp(
  `${COLOR_PROPS}\\s*:\\s*[^;{}]*?\\b(${NAMED_COLORS.join('|')})\\b`,
  'gi',
);
// Atributos JSX de cor (ex.: <Icon color="red" fill="white" />).
const NAMED_IN_JSX = new RegExp(
  `\\b(?:color|fill|stroke|stopColor|floodColor|lightingColor)\\s*=\\s*["'{]\\s*["']?(${NAMED_COLORS.join('|')})\\b`,
  'gi',
);

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    return statSync(full).isDirectory() ? walk(full) : [full];
  });
}

function stripComments(text) {
  // Remove /* ... */ e // ... (mantendo as quebras de linha para o número da linha bater).
  return text
    .replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '))
    .replace(/(^|[^:"'`])\/\/[^\n]*/g, (m, p) => p + ' '.repeat(m.length - p.length));
}

const files = walk(SRC).filter((f) => EXTENSIONS.some((e) => f.endsWith(e)) && !f.startsWith(THEME_DIR));
const problems = [];

for (const file of files) {
  const lines = stripComments(readFileSync(file, 'utf8')).split('\n');
  const isCss = file.endsWith('.css');
  lines.forEach((line, i) => {
    const hits = [];
    for (const m of line.matchAll(HEX)) hits.push(m[0]);
    for (const m of line.matchAll(COLOR_FN)) hits.push(m[0].replace(/\s*\($/, '()'));
    // Nomes de cor: ignora o que estiver dentro de var(--token) (ex.: --brand-ms-red).
    const withoutVars = line.replace(/var\(\s*--[\w-]+\s*\)/g, 'var()');
    for (const m of withoutVars.matchAll(NAMED_IN_CSS)) hits.push(m[1]);
    if (!isCss) for (const m of withoutVars.matchAll(NAMED_IN_JSX)) hits.push(m[1]);
    for (const hit of hits) {
      problems.push({ file: relative(ROOT, file), line: i + 1, hit, text: line.trim() });
    }
  });
}

if (problems.length > 0) {
  console.error(`✖ ${problems.length} cor(es) literal(is) fora de src/theme/:\n`);
  for (const p of problems) console.error(`  ${p.file}:${p.line}  ${p.hit}\n      ${p.text}`);
  console.error('\nUse um token (var(--...)) definido em src/theme/.');
  process.exit(1);
}

console.log(`✔ lint:colors: nenhuma cor literal fora de src/theme/ (${files.length} arquivos verificados).`);
