#!/usr/bin/env node
// Captura uma página (URL ou arquivo HTML) em várias larguras e confere o básico de desktop e mobile.
//
//   node capturar.mjs <url-ou-arquivo> [--viewports 1440x900,390x844] [--saida qa/capturas/nome] [--dpr 2]
//
// Padrão: 1440x900, 1366x768, 768x1024, 390x844, 360x800.
// Gera em --saida: <largura>-dobra.png, <largura>-inteira.png, relatorio.json, relatorio.md
// e comparar.html (abre direto no navegador, sem servidor).
//
// Precisa do Playwright (npm i -D playwright). Usa o Chromium do Playwright ou, se não houver,
// o Chrome/Edge instalado. Sem Playwright, gera só um comparar.html "ao vivo" (iframes nas larguras)
// e sai com código 3. Não instale nada sem perguntar ao desenvolvedor.

import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf(n); return i >= 0 && args[i + 1] ? args[i + 1] : d; };
const alvoArg = args.find((a, i) => !a.startsWith('--') && !(i > 0 && args[i - 1].startsWith('--')));
if (!alvoArg) { console.error('Uso: node capturar.mjs <url-ou-arquivo> [--viewports 1440x900,390x844] [--saida pasta] [--dpr 2]'); process.exit(2); }

const ehUrl = /^(https?|file):\/\//i.test(alvoArg);
if (!ehUrl && !fs.existsSync(alvoArg)) { console.error(`Não encontrei ${alvoArg}.`); process.exit(2); }
const url = ehUrl ? alvoArg : pathToFileURL(path.resolve(alvoArg)).href;

const carimbo = new Date().toISOString().slice(0, 16).replace(/[-:T]/g, '');
const slug = (ehUrl ? alvoArg.replace(/^\w+:\/\//, '') : path.basename(path.dirname(path.resolve(alvoArg))) + '-' + path.basename(alvoArg, path.extname(alvoArg)))
  .toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 50) || 'pagina';
const saida = path.resolve(opt('--saida', path.join('qa', 'capturas', `${slug}-${carimbo}`)));
fs.mkdirSync(saida, { recursive: true });

const dpr = Number(opt('--dpr', '2'));
const VIEWPORTS = opt('--viewports', '1440x900,1366x768,768x1024,390x844,360x800').split(',').map((v) => {
  const [w, h] = v.trim().toLowerCase().split('x').map(Number);
  const mobile = w < 1024;
  return { nome: `${w}x${h}`, width: w, height: h, isMobile: mobile, hasTouch: mobile, deviceScaleFactor: mobile ? dpr : 1, alvoCasa: w < 900 ? 48 : 44 };
});

async function carregarPlaywright() {
  const nomes = ['playwright', '@playwright/test', 'playwright-core'];
  const bases = [process.cwd()];
  try { bases.push(execSync('npm root -g', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim()); } catch {}
  for (const n of nomes) { try { const m = await import(n); return m.chromium ? m : m.default; } catch {} }
  for (const b of bases) for (const n of nomes) {
    try { const r = createRequire(path.join(b, 'noop.js')).resolve(n); const m = await import(pathToFileURL(r).href); return m.chromium ? m : m.default; } catch {}
  }
  return null;
}

function comparar(linhas, titulo) {
  return `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>${titulo}</title>
<style>body{margin:0;font:14px/1.4 system-ui,sans-serif;background:#eceeef;color:#16181b}header{padding:16px 24px;background:#fff;border-bottom:1px solid #d6d9dc}
h1{font-size:18px;margin:0 0 4px}.faixa{display:flex;gap:24px;align-items:flex-start;padding:24px;overflow-x:auto}figure{margin:0;flex:none}
figcaption{margin-bottom:6px;font-weight:600}img,iframe{display:block;background:#fff;border:1px solid #c9cdd1}.avisos{color:#8a1c12}</style></head>
<body><header><h1>${titulo}</h1><div>${url}</div></header>${linhas}</body></html>`;
}

const pw = await carregarPlaywright();
if (!pw) {
  // Sem Playwright: comparação ao vivo com iframes (funciona para arquivo local e para URL que permite iframe).
  const src = ehUrl ? url : path.relative(saida, path.resolve(alvoArg)).split(path.sep).join('/');
  const escala = (v) => Math.min(1, 520 / v.width);
  const fig = VIEWPORTS.map((v) => `<figure><figcaption>${v.nome}</figcaption><div style="width:${v.width * escala(v)}px;height:${v.height * escala(v)}px;overflow:hidden"><iframe src="${src}" width="${v.width}" height="${v.height}" style="transform:scale(${escala(v)});transform-origin:0 0"></iframe></div></figure>`).join('');
  fs.writeFileSync(path.join(saida, 'comparar.html'), comparar(`<div class="faixa">${fig}</div>`, 'Comparação ao vivo (sem Playwright)'));
  console.log(`! Playwright não encontrado. Gerei ${path.join(saida, 'comparar.html')}: abra no navegador para ver as larguras lado a lado.`);
  console.log('  Para capturas e checagens automáticas: npm i -D playwright  (e, se não houver Chrome instalado, npx playwright install chromium). Pergunte ao desenvolvedor antes.');
  process.exit(3);
}

let navegador;
for (const o of [{}, { channel: 'chrome' }, { channel: 'msedge' }]) {
  try { navegador = await pw.chromium.launch({ headless: true, ...o }); break; } catch (e) { if (!Object.keys(o).length) console.warn('! Chromium do Playwright indisponível; tentando Chrome/Edge instalado…'); }
}
if (!navegador) { console.error('✗ Nenhum navegador. Rode `npx playwright install chromium` (pergunte antes) ou instale o Chrome.'); process.exit(4); }

// Checagens dentro da página
function checar(alvoCasa) {
  const vis = (el) => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && Number(s.opacity) > 0; };
  const desc = (el) => (el.getAttribute('aria-label') || el.textContent || el.getAttribute('name') || el.tagName).trim().replace(/\s+/g, ' ').slice(0, 40);
  const iw = window.innerWidth, ih = window.innerHeight;
  const sw = document.documentElement.scrollWidth;
  const largos = sw > iw + 1 ? [...document.querySelectorAll('body *')].filter((e) => { const r = e.getBoundingClientRect(); return r.right > iw + 1 && r.width > 0; })
    .sort((a, b) => b.getBoundingClientRect().right - a.getBoundingClientRect().right).slice(0, 5).map((e) => `${e.tagName.toLowerCase()}${e.className && typeof e.className === 'string' ? '.' + e.className.trim().split(/\s+/)[0] : ''} (até ${Math.round(e.getBoundingClientRect().right)}px)`) : [];
  const interativos = [...document.querySelectorAll('a[href], button, input:not([type=hidden]), select, textarea, summary, [role=button], [role=link], [tabindex]:not([tabindex="-1"])')].filter(vis);
  const emTexto = (el) => el.tagName === 'A' && getComputedStyle(el).display === 'inline' && el.parentElement && /^(P|LI|TD|DD|SPAN|EM|STRONG)$/.test(el.parentElement.tagName) && (el.parentElement.textContent || '').trim().length > (el.textContent || '').trim().length + 10;
  const alvos = interativos.filter((e) => !emTexto(e)).map((e) => { const r = e.getBoundingClientRect(); return { t: desc(e), w: Math.round(r.width), h: Math.round(r.height) }; });
  const abaixo24 = alvos.filter((a) => a.w < 24 || a.h < 24);
  const abaixoCasa = alvos.filter((a) => (a.w < alvoCasa || a.h < alvoCasa) && !(a.w < 24 || a.h < 24));
  const campos = [...document.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([type=radio]), select, textarea')].filter(vis);
  const camposPequenos = campos.filter((c) => parseFloat(getComputedStyle(c).fontSize) < 16).map(desc);
  const h1s = [...document.querySelectorAll('h1')].filter(vis);
  const h1 = h1s[0];
  let h1Linhas = null, h1NaDobra = false;
  if (h1) { const r = h1.getBoundingClientRect(); const lh = parseFloat(getComputedStyle(h1).lineHeight) || parseFloat(getComputedStyle(h1).fontSize) * 1.2; h1Linhas = Math.round(r.height / lh); h1NaDobra = r.top < ih && r.bottom > 0; }
  const ctas = [...document.querySelectorAll('[data-cta], a[href*="wa.me"], a[href*="api.whatsapp.com"], a[href^="tel:"]')].filter(vis);
  const ctaNaDobra = ctas.some((c) => { const r = c.getBoundingClientRect(); return r.top < ih && r.bottom > 0; });
  const fixosBaixo = [...document.querySelectorAll('body *')].filter((e) => { const s = getComputedStyle(e); return (s.position === 'fixed' || s.position === 'sticky') && vis(e) && e.getBoundingClientRect().bottom >= ih - 2 && e.getBoundingClientRect().top > ih / 2; }).length;
  const imgs = [...document.images];
  return {
    viewportMeta: !!document.querySelector('meta[name=viewport]'),
    rolagemHorizontal: sw > iw + 1, larguraDoc: sw, largos,
    alvos: alvos.length, alvosAbaixo24: abaixo24.length, exemplosAbaixo24: abaixo24.slice(0, 6),
    alvosAbaixoCasa: abaixoCasa.length, exemplosAbaixoCasa: abaixoCasa.slice(0, 6),
    camposFonteMenor16: camposPequenos,
    h1Quantidade: h1s.length, h1Linhas, h1NaDobra,
    ctas: ctas.length, ctaNaDobra, barraFixaInferior: fixosBaixo > 0,
    imgsSemDimensao: imgs.filter((i) => !i.getAttribute('width') || !i.getAttribute('height')).length,
    imgsLazyNaDobra: imgs.filter((i) => i.loading === 'lazy' && i.getBoundingClientRect().top < ih).length,
    reducedMotionNoCss: (() => { let ilegivel = false; const achou = [...document.styleSheets].some((s) => { try { return [...s.cssRules].some((r) => r.media && /prefers-reduced-motion/.test(r.media.mediaText)); } catch { ilegivel = true; return false; } }); return achou ? true : ilegivel ? null : false; })(),
  };
}

const relatorio = { alvo: url, data: new Date().toISOString(), viewports: [] };
for (const v of VIEWPORTS) {
  const ctx = await navegador.newContext({ viewport: { width: v.width, height: v.height }, isMobile: v.isMobile, hasTouch: v.hasTouch, deviceScaleFactor: v.deviceScaleFactor, reducedMotion: 'reduce', locale: 'pt-BR' });
  const page = await ctx.newPage();
  const r = { ...v, erro: null };
  try {
    await page.goto(url, { waitUntil: 'load', timeout: 30000 });
    await page.waitForLoadState('networkidle', { timeout: 5000 }).catch(() => {});
    await page.evaluate(() => document.fonts && document.fonts.ready);
    await page.screenshot({ path: path.join(saida, `${v.width}-dobra.png`), animations: 'disabled' });
    Object.assign(r, await page.evaluate(checar, v.alvoCasa));
    // rola até o fim para carregar imagens preguiçosas, volta ao topo e captura a página inteira
    await page.evaluate(async () => { for (let y = 0; y < document.body.scrollHeight; y += innerHeight * 0.8) { scrollTo(0, y); await new Promise((ok) => setTimeout(ok, 120)); } scrollTo(0, 0); });
    await page.waitForTimeout(300);
    await page.screenshot({ path: path.join(saida, `${v.width}-inteira.png`), fullPage: true, animations: 'disabled' });
  } catch (e) { r.erro = String(e.message || e).split('\n')[0]; }
  relatorio.viewports.push(r);
  await ctx.close();
  console.log(`${r.erro ? '✗' : '✓'} ${v.nome}${r.erro ? ': ' + r.erro : ''}`);
}
await navegador.close();

// Problemas por viewport
const problemas = (r) => {
  if (r.erro) return [`erro: ${r.erro}`];
  const p = [];
  if (!r.viewportMeta) p.push('sem <meta name="viewport">');
  if (r.rolagemHorizontal) p.push(`rolagem horizontal (documento com ${r.larguraDoc}px): ${r.largos.join(', ')}`);
  if (r.alvosAbaixo24) p.push(`${r.alvosAbaixo24} alvo(s) de toque < 24px (falha WCAG 2.5.8): ${r.exemplosAbaixo24.map((a) => `"${a.t}" ${a.w}×${a.h}`).join('; ')}`);
  if (r.alvosAbaixoCasa) p.push(`${r.alvosAbaixoCasa} alvo(s) abaixo do padrão da casa (${r.alvoCasa}px): ${r.exemplosAbaixoCasa.map((a) => `"${a.t}" ${a.w}×${a.h}`).join('; ')}`);
  if (r.isMobile && r.camposFonteMenor16.length) p.push(`campo(s) com fonte < 16px (o iOS dá zoom): ${r.camposFonteMenor16.join(', ')}`);
  if (r.h1Quantidade !== 1) p.push(`${r.h1Quantidade} h1 visíveis (esperado 1)`);
  if (r.h1Quantidade && !r.h1NaDobra) p.push('h1 fora da primeira tela');
  if (r.isMobile && r.width < 500 && r.h1Linhas > 3) p.push(`h1 com ${r.h1Linhas} linhas (máximo 3 no mobile)`);
  if (r.ctas && !r.ctaNaDobra) p.push('nenhum CTA (WhatsApp, tel: ou [data-cta]) visível na primeira tela');
  if (!r.ctas) p.push('nenhum CTA reconhecido (marque o principal com data-cta)');
  if (r.isMobile && r.width < 500 && !r.barraFixaInferior) p.push('sem barra de ação fixa no rodapé (se o DESIGN.md pedir)');
  if (r.imgsSemDimensao) p.push(`${r.imgsSemDimensao} imagem(ns) sem width/height (risco de CLS)`);
  if (r.imgsLazyNaDobra) p.push(`${r.imgsLazyNaDobra} imagem(ns) com loading="lazy" na primeira tela (piora o LCP)`);
  return p;
};
let md = `# Capturas · ${url}\n\n${relatorio.data}\n\nAs checagens são automáticas e parciais: olhe as imagens.\n\n| Viewport | Problemas |\n|---|---|\n`;
for (const r of relatorio.viewports) { const p = problemas(r); r.problemas = p; md += `| ${r.nome} | ${p.length ? p.join('<br>') : 'nenhum encontrado'} |\n`; }
const rm = relatorio.viewports.map((r) => r.reducedMotionNoCss);
if (rm.includes(true)) md += '\n`@media (prefers-reduced-motion)` encontrado no CSS.\n';
else if (rm.includes(null)) md += '\nNão deu para ler todo o CSS (arquivo local ou outro domínio): confira `prefers-reduced-motion` com a varredura da anti-cara-de-ia.\n';
else md += '\nNenhuma regra `@media (prefers-reduced-motion)` encontrada no CSS carregado.\n';
fs.writeFileSync(path.join(saida, 'relatorio.json'), JSON.stringify(relatorio, null, 2));
fs.writeFileSync(path.join(saida, 'relatorio.md'), md);

const linha = (tipo, alt) => `<div class="faixa">${relatorio.viewports.filter((r) => !r.erro).map((r) => `<figure><figcaption>${r.nome}${r.problemas.length ? ` <span class="avisos">· ${r.problemas.length} problema(s)</span>` : ''}</figcaption><img src="${r.width}-${tipo}.png" width="${Math.min(r.width, alt)}" alt="${r.nome} ${tipo}"></figure>`).join('')}</div>`;
fs.writeFileSync(path.join(saida, 'comparar.html'), comparar(`<h2 style="padding:0 24px">Primeira tela</h2>${linha('dobra', 480)}<h2 style="padding:0 24px">Página inteira</h2>${linha('inteira', 360)}`, 'Capturas por largura'));

const total = relatorio.viewports.reduce((n, r) => n + r.problemas.length, 0);
console.log(`\n${total ? '!' : '✓'} ${total} problema(s). Relatório: ${path.join(saida, 'relatorio.md')} · Lado a lado: ${path.join(saida, 'comparar.html')}`);
process.exit(relatorio.viewports.some((r) => r.erro) ? 1 : 0);
