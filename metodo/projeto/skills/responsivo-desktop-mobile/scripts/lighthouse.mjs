#!/usr/bin/env node
// Lighthouse no mobile e no desktop, com resumo das métricas. Funciona no Windows, Linux e macOS.
//
//   node lighthouse.mjs <url-http> [--saida qa/lighthouse]
//
// Precisa de uma URL http(s): sirva a pasta antes (o comando do projeto em config.yaml, ou
// `npx -y http-server -p 4173 -c-1`). Usa `npx -y lighthouse` (baixa na primeira vez: avise o desenvolvedor)
// e o Chrome instalado; para outro Chromium, defina CHROME_PATH.
// Lighthouse é laboratório. A verdade é o dado de campo (PageSpeed Insights / CrUX), quando o site tiver tráfego.

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const args = process.argv.slice(2);
const url = args.find((a) => /^https?:\/\//i.test(a));
const i = args.indexOf('--saida');
const saida = path.resolve(i >= 0 && args[i + 1] ? args[i + 1] : path.join('qa', 'lighthouse'));
if (!url) { console.error('Uso: node lighthouse.mjs http://localhost:4173/ [--saida qa/lighthouse]\nLighthouse não abre arquivo local: sirva a pasta primeiro.'); process.exit(2); }
fs.mkdirSync(saida, { recursive: true });

// Como root no Linux (container, CI), o Chrome só abre sem sandbox.
const flagsChrome = ['--headless=new', ...(process.platform === 'linux' && process.getuid?.() === 0 ? ['--no-sandbox'] : [])].join(' ');
const METAS = { lcp: 2500, cls: 0.1, tbt: 200 };
const rodar = (nome, extra) => {
  const base = path.join(saida, nome);
  const r = spawnSync('npx', ['-y', 'lighthouse', url, '--quiet', `--chrome-flags=${flagsChrome}`, '--output=json', '--output=html', `--output-path=${base}`, '--only-categories=performance,accessibility,best-practices,seo', ...extra],
    { stdio: 'inherit', shell: process.platform === 'win32' });
  const arq = `${base}.report.json`;
  if (r.status !== 0 || !fs.existsSync(arq)) return { nome, erro: `lighthouse saiu com código ${r.status}` };
  const j = JSON.parse(fs.readFileSync(arq, 'utf8'));
  const a = j.audits || {};
  // O elemento LCP muda de lugar entre versões (LH 12: largest-contentful-paint-element; LH 13: lcp-breakdown-insight).
  const acharNo = (x) => { if (!x || typeof x !== 'object') return null; if (x.type === 'node') return x; for (const v of Object.values(x)) { const r = acharNo(v); if (r) return r; } return null; };
  const no = acharNo(a['largest-contentful-paint-element']?.details) || acharNo(a['lcp-breakdown-insight']?.details);
  const elementoLcp = no ? `${no.selector || ''} ${no.nodeLabel ? '· ' + no.nodeLabel : ''}`.trim() : '';
  const nota = (c) => (j.categories[c] ? Math.round(j.categories[c].score * 100) : null);
  return {
    nome, html: `${base}.report.html`,
    desempenho: nota('performance'), acessibilidade: nota('accessibility'), boasPraticas: nota('best-practices'), seo: nota('seo'),
    lcp: a['largest-contentful-paint']?.numericValue, cls: a['cumulative-layout-shift']?.numericValue, tbt: a['total-blocking-time']?.numericValue,
    elementoLcp,
    contrasteOk: a['color-contrast'] ? a['color-contrast'].score === 1 : null,
    alvoToqueOk: a['target-size'] ? a['target-size'].score === 1 : null,
  };
};

const res = [rodar('mobile', []), rodar('desktop', ['--preset=desktop'])];
const ms = (v) => (v == null ? '—' : `${(v / 1000).toFixed(2)} s`);
const ok = (v, meta) => (v == null ? '' : v <= meta ? ' ✓' : ' ✗');
let md = `# Lighthouse · ${url}\n\n${new Date().toISOString()} · laboratório (mobile emula aparelho médio com rede e CPU lentas)\n\n`;
md += '| | Desempenho | LCP (≤ 2,5 s) | CLS (≤ 0,1) | TBT (≤ 200 ms, aproxima o INP) | Acessibilidade | Contraste | Alvos de toque | Elemento LCP |\n|---|---|---|---|---|---|---|---|---|\n';
for (const r of res) {
  if (r.erro) { md += `| ${r.nome} | erro: ${r.erro} |||||||| \n`; continue; }
  md += `| ${r.nome} | ${r.desempenho} | ${ms(r.lcp)}${ok(r.lcp, METAS.lcp)} | ${r.cls?.toFixed(3) ?? '—'}${ok(r.cls, METAS.cls)} | ${Math.round(r.tbt ?? 0)} ms${ok(r.tbt, METAS.tbt)} | ${r.acessibilidade} | ${r.contrasteOk == null ? '—' : r.contrasteOk ? 'ok' : 'falha'} | ${r.alvoToqueOk == null ? '—' : r.alvoToqueOk ? 'ok' : 'falha'} | \`${(r.elementoLcp || '').replace(/\|/g, '/').slice(0, 80)}\` |\n`;
}
md += `\nRelatórios completos: ${res.filter((r) => r.html).map((r) => r.html).join(' · ')}\n`;
fs.writeFileSync(path.join(saida, 'resumo.md'), md);
console.log('\n' + md);
process.exit(res.some((r) => r.erro) ? 1 : 0);
