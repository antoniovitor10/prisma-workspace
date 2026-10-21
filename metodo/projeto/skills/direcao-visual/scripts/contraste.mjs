#!/usr/bin/env node
// Contraste WCAG 2.x entre as cores de um tokens.css (ou entre dois hex).
// Sem dependências. Só entende hex (#rgb, #rrggbb) e var(--outro-token).
//
//   node contraste.mjs caminho/tokens.css      → confere os pares de papel do método
//   node contraste.mjs "#1B1C1E" "#FFFFFF"     → contraste de um par
//
// Sai com código 1 se algum par obrigatório falhar.

import fs from 'node:fs';

const lum = (hex) => {
  const h = hex.replace('#', '');
  const full = h.length === 3 ? h.split('').map((c) => c + c).join('') : h.slice(0, 6);
  const [r, g, b] = [0, 2, 4].map((i) => {
    const v = parseInt(full.slice(i, i + 2), 16) / 255;
    return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
export const contraste = (a, b) => {
  const [x, y] = [lum(a), lum(b)].sort((m, n) => n - m);
  return (x + 0.05) / (y + 0.05);
};

const args = process.argv.slice(2);
if (args.length === 2 && args.every((a) => /^#[0-9a-f]{3,8}$/i.test(a))) {
  const c = contraste(args[0], args[1]);
  console.log(`${args[0]} × ${args[1]} = ${c.toFixed(2)}:1  ${c >= 4.5 ? 'AA texto' : c >= 3 ? 'AA só texto grande / componente' : 'FALHA'}`);
  process.exit(0);
}
if (args.length !== 1 || !fs.existsSync(args[0])) {
  console.error('Uso: node contraste.mjs tokens.css   |   node contraste.mjs "#hex1" "#hex2"');
  process.exit(2);
}

const css = fs.readFileSync(args[0], 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
const raiz = css.match(/:root\s*\{([\s\S]*?)\}/);
const tokens = {};
for (const m of (raiz ? raiz[1] : css).matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) tokens[m[1]] = m[2].trim();
const resolver = (nome, n = 0) => {
  const v = tokens[nome];
  if (!v || n > 5) return null;
  const ref = v.match(/^var\((--[\w-]+)\)$/);
  if (ref) return resolver(ref[1], n + 1);
  return /^#[0-9a-f]{3,8}$/i.test(v) ? v : null;
};

// [frente, fundo, mínimo, obrigatório]
const PARES = [
  ['--c-texto', '--c-fundo', 4.5, true],
  ['--c-texto', '--c-superficie', 4.5, true],
  ['--c-texto-2', '--c-fundo', 4.5, true],
  ['--c-texto-2', '--c-superficie', 4.5, false],
  ['--c-acao-texto', '--c-acao', 4.5, true],
  ['--c-acao', '--c-fundo', 3, true],
  ['--c-acao', '--c-superficie', 3, false],
  ['--c-foco', '--c-fundo', 3, true],
  ['--c-acento', '--c-fundo', 3, false],
];

let falhas = 0;
console.log(`Contraste WCAG · ${args[0]}\n`);
for (const [a, b, min, obrig] of PARES) {
  const ha = resolver(a), hb = resolver(b);
  if (!ha || !hb) { console.log(`  ?  ${a} sobre ${b}: não é hex (use um verificador no navegador; o style-tile.html calcula sozinho)`); continue; }
  const c = contraste(ha, hb);
  const ok = c >= min;
  if (!ok && obrig) falhas++;
  console.log(`  ${ok ? '✓' : obrig ? '✗' : '!'}  ${a} ${ha} sobre ${b} ${hb}: ${c.toFixed(2)}:1 (mínimo ${min}:1${obrig ? '' : ', recomendado'})`);
}
console.log(falhas ? `\n✗ ${falhas} par(es) obrigatório(s) abaixo do mínimo.` : '\n✓ Pares obrigatórios dentro do mínimo AA.');
process.exit(falhas ? 1 : 0);
