#!/usr/bin/env node
// Varredura de "cara de IA" no código e no texto de um site. Sem dependências.
//
//   node varredura.mjs <pasta-ou-arquivo> [--permitir Inter,bento] [--json]
//
// Procura defaults de cor, fonte, componente e movimento, imagem de banco e frases
// genéricas em português. Os números (#n) são os de references/sintomas.md.
// Exceções aprovadas: DESIGN.md → seção "Exceções à varredura", linhas "- `padrão`: motivo".
// Sai com código 1 se houver achado crítico sem exceção.

import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const alvo = args.find((a) => !a.startsWith('--'));
const json = args.includes('--json');
const iPerm = args.indexOf('--permitir');
const permitidosCli = iPerm >= 0 && args[iPerm + 1] ? args[iPerm + 1].split(',').map((s) => s.trim()).filter(Boolean) : [];
if (!alvo || !fs.existsSync(alvo)) {
  console.error('Uso: node varredura.mjs <pasta-ou-arquivo> [--permitir padrao1,padrao2] [--json]');
  process.exit(2);
}

// ---------- exceções do DESIGN.md ----------
function excecoesDesign() {
  const cands = [path.join(process.cwd(), 'DESIGN.md')];
  let d = path.resolve(alvo);
  for (let i = 0; i < 6; i++) { cands.push(path.join(d, 'DESIGN.md')); d = path.dirname(d); }
  const arq = cands.find((c) => fs.existsSync(c));
  if (!arq) return { arq: null, lista: [] };
  const t = fs.readFileSync(arq, 'utf8').replace(/\r\n/g, '\n');
  const sec = t.split(/\n## /).find((s) => /^Exce[çc][õo]es à varredura/i.test(s));
  const lista = sec ? [...sec.matchAll(/^- `([^`]+)`/gm)].map((m) => m[1]) : [];
  return { arq, lista };
}
const exc = excecoesDesign();
const permitidos = [...permitidosCli, ...exc.lista].map((s) => s.toLowerCase());

// ---------- regras ----------
// tipo: 'codigo' (CSS/HTML/JS/config) ou 'texto' (conteúdo visível). critico: conta para o veredito.
const R = (n, critico, tipo, rx, msg) => ({ n, critico, tipo, rx, msg });
const REGRAS = [
  R(6, true, 'codigo', /(?:font-family[^;\n]*["'\s,:]Inter\b|family=Inter\b|\bfont-inter\b|["']Inter["'])/, 'Inter: escolha um par deliberado no DESIGN.md'),
  R(6, true, 'codigo', /\bGeist(?: Sans| Mono)?\b/, 'Geist: escolha um par deliberado no DESIGN.md'),
  R(6, false, 'codigo', /font-family:\s*["']?Arial\b/i, 'Arial como fonte principal'),
  R(1, true, 'codigo', /\b(?:bg|text|from|via|to|border|ring|shadow|fill|stroke)-(?:indigo|violet|purple)-\d{2,3}\b/, 'indigo/violet/purple do Tailwind: cor sem motivo'),
  R(1, false, 'codigo', /\b(?:bg-clip-text|background-clip:\s*text)\b/, 'texto com gradiente'),
  R(1, false, 'codigo', /\bbg-gradient-to-[a-z]+\b[^"'\n]*\b(?:from|to)-(?:blue|indigo|violet|purple|fuchsia|pink)-/, 'gradiente azul/roxo/rosa'),
  R(4, false, 'codigo', /\bslate-900\b|#0f172a\b/i, 'slate-900 como texto padrão (default do Tailwind)'),
  R(11, false, 'codigo', /\brounded-2xl\b[^"'\n]*\bshadow-(?:lg|xl)\b|\bshadow-(?:lg|xl)\b[^"'\n]*\brounded-2xl\b/, 'possível card padrão (rounded-2xl + shadow-lg)'),
  R(12, false, 'codigo', /\bbackdrop-blur(?:-\w+)?\b|backdrop-filter\s*:/, 'glassmorphism / backdrop-blur'),
  R(7, false, 'codigo', /\buppercase\b[^"'\n]*\btracking-(?:wider|widest|\[0?\.[2-9])/, 'eyebrow em caixa-alta espaçada'),
  R(8, false, 'codigo', /letter-spacing:\s*(?:0?\.(?:1[5-9]|[2-9])\d*|[1-9]\d*(?:\.\d+)?)em/, 'espaçamento entre letras extremo'),
  R(13, false, 'codigo', />\s*0[1-9]\s*</, 'numeração 01/02/03: só se for uma sequência real'),
  R(14, false, 'codigo', /\bbento\b/i, 'bento grid'),
  R(29, false, 'codigo', /\bdata-aos=["']fade|\banimate-fade-?(?:in-)?up\b|\bfadeInUp\b|\bfade-up\b/i, 'fade-up em seção'),
  R(28, false, 'codigo', /whatsapp[-_]?(?:float|flutuante|fixo|sticky)|wa[-_]float|floating[-_]?whatsapp|joinchat/i, 'botão flutuante de WhatsApp de plugin'),
  R(21, true, 'codigo', /images\.unsplash\.com|pexels\.com|shutterstock|freepik|istockphoto|gettyimages|depositphotos|adobestock/i, 'imagem de banco'),
  R(22, false, 'codigo', /midjourney|dall-?e|stable-?diffusion|firefly|ai[-_]generated|gerad[ao][-_]por[-_]ia/i, 'imagem gerada por IA'),
  R(20, true, 'codigo', /randomuser\.me|i\.pravatar\.cc|ui-avatars\.com|\bavatar-?\d+\.(?:png|jpe?g|webp)/i, 'avatar genérico (depoimento inventado?)'),
  R(17, true, 'texto', /\btransform(?:e|a|ar|amos|ando|ação)\s+(?:seu|sua|o|a|vida|sorriso|autoestima)/i, '"transforme…": troque por um fato concreto'),
  R(17, true, 'texto', /\belev(?:e|ar|amos)\s+(?:seu|sua|o|a)\b/i, '"eleve…": troque por um fato concreto'),
  R(18, true, 'texto', /solu[çc](?:ão|ões)\s+complet[ao]s?|solu[çc](?:ão|ões)\s+integrad[ao]s?/i, '"solução completa": diga o que é feito'),
  R(18, true, 'texto', /excel[êe]ncia/i, '"excelência": troque por prova'),
  R(18, true, 'texto', /humanizad[oa]s?/i, '"humanizado": diga o que muda na prática'),
  R(18, true, 'texto', /\bde ponta\b/i, '"de ponta": o equipamento pelo nome'),
  R(19, false, 'texto', /\bsua jornada\b|jornada d[oa] (?:paciente|cliente)/i, '"sua jornada": tom de agência'),
  R(19, false, 'texto', /mais (?:do )?que um sorriso|muito al[ée]m d[eoa]/i, 'fórmula de agência'),
  R(19, false, 'texto', /venha nos conhecer|fa[çc]a-nos uma visita/i, 'convite genérico: diga onde e como chegar'),
  R(19, false, 'texto', /\b(?:desbloqueie|potencialize|revolucion[áa]ri[oa]|disruptiv[oa]|inovador[a]?)\b/i, 'verbo/adjetivo de tradução de marketing'),
  R(19, false, 'texto', /\b(?:refer[êe]ncia|l[íi]der)\s+em\b/i, 'autoelogio sem prova'),
  R(16, false, 'texto', />\s*(?:Saiba mais|Clique aqui)\s*</i, 'botão sem verbo exato'),
  R(0, false, 'texto', /lorem ipsum/i, 'texto de preenchimento'),
];

// cores próximas dos defaults (distância RGB)
const CORES_DEFAULT = [
  { hex: 'f4f1ea', n: 2, quente: true, msg: 'creme perto de #F4F1EA (default "anti-IA")' },
  { hex: 'd97757', n: 2, msg: 'terracota perto de #D97757 (default "anti-IA")' },
  { hex: '6366f1', n: 1, msg: 'indigo-500 em hex' },
  { hex: '8b5cf6', n: 1, msg: 'violet-500 em hex' },
  { hex: 'c6ff00', n: 3, msg: 'verde-ácido sobre escuro (default "anti-IA")' },
  { hex: 'd4ff3f', n: 3, msg: 'verde-ácido sobre escuro (default "anti-IA")' },
];
const rgb = (h) => { const x = h.length === 3 ? h.split('').map((c) => c + c).join('') : h.slice(0, 6); return [0, 2, 4].map((i) => parseInt(x.slice(i, i + 2), 16)); };
const dist = (a, b) => Math.hypot(...a.map((v, i) => v - b[i]));

// ---------- arquivos ----------
const EXT_CODIGO = new Set(['.html', '.htm', '.css', '.scss', '.sass', '.less', '.js', '.mjs', '.cjs', '.jsx', '.ts', '.tsx', '.vue', '.svelte', '.astro', '.php', '.twig', '.liquid', '.njk', '.hbs', '.ejs']);
const EXT_TEXTO = new Set([...EXT_CODIGO, '.md', '.mdx', '.json', '.yml', '.yaml', '.txt']);
const PASTAS_FORA = new Set(['node_modules', '.git', 'dist', 'build', 'out', '.next', '.nuxt', '.astro', '.svelte-kit', 'vendor', 'coverage', 'qa', 'metodo', '.agents', '.claude', '.kiro', 'insumos', 'decisoes', 'trabalho', 'wp-admin', 'wp-includes']);
const ARQ_FORA = new Set(['DESIGN.md', 'README.md', 'AGENTS.md', 'CLAUDE.md', 'CHANGELOG.md', 'package-lock.json', 'pnpm-lock.yaml', 'yarn.lock']);

function listar(p, raiz = true) {
  const st = fs.statSync(p);
  if (st.isFile()) return [p];
  const out = [];
  for (const n of fs.readdirSync(p)) {
    const f = path.join(p, n);
    let s; try { s = fs.statSync(f); } catch { continue; }
    if (s.isDirectory()) { if (!PASTAS_FORA.has(n)) out.push(...listar(f, false)); }
    else if (!ARQ_FORA.has(n) && !/\.min\.|\.map$|\.template\./.test(n) && EXT_TEXTO.has(path.extname(n).toLowerCase()) && s.size < 2_000_000) out.push(f);
  }
  return out;
}

const achados = [];
let temMovimento = false, temReduced = false;
const arquivos = listar(alvo);
for (const arq of arquivos) {
  const ext = path.extname(arq).toLowerCase();
  const ehCodigo = EXT_CODIGO.has(ext);
  const linhas = fs.readFileSync(arq, 'utf8').split(/\r?\n/);
  linhas.forEach((linha, i) => {
    if (/prefers-reduced-motion/.test(linha)) temReduced = true;
    if (ehCodigo && /\b(?:animation|transition|@keyframes|animate-|data-aos)\b/.test(linha)) temMovimento = true;
    for (const r of REGRAS) {
      if (r.tipo === 'codigo' && !ehCodigo) continue;
      const m = linha.match(r.rx);
      if (m) achados.push({ n: r.n, critico: r.critico, arq, linha: i + 1, trecho: m[0].trim().slice(0, 80), msg: r.msg });
    }
    if (ehCodigo) for (const m of linha.matchAll(/#([0-9a-f]{6}|[0-9a-f]{3})\b/gi)) {
      for (const c of CORES_DEFAULT) {
        const cor = rgb(m[1]);
        // creme: além de perto, precisa ser quente (vermelho acima do azul); cinza-claro neutro não conta
        if (dist(cor, rgb(c.hex)) <= 14 && (!c.quente || cor[0] - cor[2] >= 6)) achados.push({ n: c.n, critico: true, arq, linha: i + 1, trecho: m[0], msg: c.msg });
      }
    }
  });
}
if (temMovimento && !temReduced) achados.push({ n: 29, critico: false, arq: alvo, linha: 0, trecho: '—', msg: 'há animação/transição e nenhum @media (prefers-reduced-motion)' });

// setas em excesso
const setas = arquivos.filter((a) => EXT_CODIGO.has(path.extname(a).toLowerCase())).reduce((n, a) => n + (fs.readFileSync(a, 'utf8').match(/→|&rarr;/g) || []).length, 0);
if (setas > 3) achados.push({ n: 16, critico: false, arq: alvo, linha: 0, trecho: `${setas}×`, msg: 'seta → em muitos botões/links' });

// exceções
for (const a of achados) a.permitido = permitidos.some((p) => a.trecho.toLowerCase().includes(p) || a.msg.toLowerCase().includes(p) || `#${a.n}` === p);

const criticos = achados.filter((a) => a.critico && !a.permitido);
const avisos = achados.filter((a) => !a.critico && !a.permitido);
const liberados = achados.filter((a) => a.permitido);

if (json) {
  console.log(JSON.stringify({ alvo, arquivos: arquivos.length, excecoes: exc.lista, criticos, avisos, liberados }, null, 2));
} else {
  const rel = (f) => path.relative(process.cwd(), f) || f;
  console.log(`Varredura anti-cara-de-IA · ${alvo} · ${arquivos.length} arquivo(s)`);
  if (exc.arq) console.log(`Exceções de ${rel(exc.arq)}: ${exc.lista.length ? exc.lista.join(', ') : '(nenhuma)'}`);
  const imprimir = (lista, marca) => {
    for (const a of lista) console.log(`  ${marca} #${a.n || '-'}  ${rel(a.arq)}${a.linha ? ':' + a.linha : ''}  «${a.trecho}»  ${a.msg}`);
  };
  if (criticos.length) { console.log(`\nCríticos (${criticos.length}):`); imprimir(criticos, '✗'); }
  if (avisos.length) { console.log(`\nAvisos (${avisos.length}):`); imprimir(avisos, '!'); }
  if (liberados.length) { console.log(`\nLiberados por exceção (${liberados.length}):`); imprimir(liberados, '·'); }
  console.log(`\n${criticos.length ? '✗' : '✓'} ${criticos.length} crítico(s), ${avisos.length} aviso(s). A varredura não vê composição: olhe as capturas.`);
}
process.exit(criticos.length ? 1 : 0);
