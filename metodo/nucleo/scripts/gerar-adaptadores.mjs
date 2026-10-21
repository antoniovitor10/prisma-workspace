#!/usr/bin/env node
// Gera os adaptadores do método para as pastas que cada ferramenta procura.
// O conteúdo real continua em metodo/; os adaptadores são ponteiros curtos.
// Nunca edite os arquivos gerados: rode o script de novo.
//
//   node metodo/nucleo/scripts/gerar-adaptadores.mjs
//
// Gera os ponteiros nas pastas listadas em `adaptadores:` do config.yaml
// (padrão: [agents, claude]; ver metodo/nucleo/referencias/agent-skills.md):
//   agents   → .agents/skills   (Codex, Copilot, Cursor, Gemini CLI, OpenCode)
//   claude   → .claude/skills e .claude/agents (Claude Code: skills e subagentes)
//   github | cursor | gemini | opencode | kiro → .<nome>/skills
// Também confere cada skill contra o padrão Agent Skills e avisa o que estiver fora.

import fs from 'node:fs';
import path from 'node:path';

const RAIZ = process.cwd();
const MARCA = '<!-- gerado por metodo/nucleo/scripts/gerar-adaptadores.mjs — não edite -->';

function frontmatter(arquivo) {
  const t = fs.readFileSync(arquivo, 'utf8').replace(/\r\n/g, '\n');
  const m = t.match(/^---\n([\s\S]*?)\n---/);
  if (!m) return {};
  const d = {};
  for (const linha of m[1].split('\n')) {
    const mm = linha.match(/^([a-zA-Z_-]+):\s*(.*)$/);
    if (mm) d[mm[1]] = mm[2].trim().replace(/^["']|["']$/g, '');
  }
  return d;
}

const yamlStr = (s) => JSON.stringify(String(s));

const PASTAS = { agents: '.agents/skills', claude: '.claude/skills', github: '.github/skills', cursor: '.cursor/skills', gemini: '.gemini/skills', opencode: '.opencode/skills', kiro: '.kiro/skills' };
function lerAdaptadores() {
  const cfg = path.join(RAIZ, 'metodo/projeto/config.yaml');
  if (!fs.existsSync(cfg)) return ['agents', 'claude'];
  const m = fs.readFileSync(cfg, 'utf8').match(/^adaptadores:\s*\[([^\]]*)\]/m);
  if (!m) return ['agents', 'claude'];
  const lista = m[1].split(',').map((x) => x.trim()).filter(Boolean);
  for (const a of lista) if (!PASTAS[a]) console.warn(`! adaptador "${a}" desconhecido (válidos: ${Object.keys(PASTAS).join(', ')}); ignorado.`);
  return lista.filter((a) => PASTAS[a]);
}

// Conformidade com https://agentskills.io/specification
const CAMPOS = new Set(['name', 'description', 'license', 'compatibility', 'metadata', 'allowed-tools']);
let avisos = 0;
function conferir(arq, pasta) {
  const txt = fs.readFileSync(path.join(RAIZ, arq), 'utf8').replace(/\r\n/g, '\n');
  const m = txt.match(/^---\n([\s\S]*?)\n---/);
  const aviso = (msg) => { console.warn(`! ${arq}: ${msg}`); avisos++; };
  if (!m) return aviso('sem frontmatter');
  const fm = frontmatter(path.join(RAIZ, arq));
  for (const linha of m[1].split('\n')) {
    const k = (linha.match(/^([a-zA-Z_-]+):/) || [])[1];
    if (k && !CAMPOS.has(k)) aviso(`campo "${k}" fora do padrão (mova para metadata)`);
  }
  if (!fm.name) aviso('falta name');
  else if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(fm.name) || fm.name.length > 64) aviso(`name "${fm.name}" inválido (a-z, 0-9, hífens simples, até 64)`);
  else if (fm.name !== pasta) aviso(`name "${fm.name}" diferente da pasta "${pasta}"`);
  if (!fm.description) aviso('falta description');
  else if (fm.description.length > 1024) aviso(`description com ${fm.description.length} caracteres (máximo 1024)`);
  const linhas = txt.split('\n').length;
  if (linhas > 500) aviso(`${linhas} linhas (recomendado < 500; mova detalhe para references/)`);
  // Skill de fora precisa dizer de onde veio (metodo/nucleo/skills/criar-skill-projeto, passo 3b).
  const dirSkill = path.dirname(path.join(RAIZ, arq));
  const externa = /^\s+origem:\s*["']?externa/m.test(m[1]) || fs.existsSync(path.join(dirSkill, 'LICENSE')) || fs.existsSync(path.join(dirSkill, 'LICENSE.md'));
  if (externa && !fs.existsSync(path.join(dirSkill, 'SOURCE.md'))) aviso('skill de fora sem SOURCE.md (origem, revisão fixada, data, licença, o que foi adaptado)');
}

// Links quebrados: links Markdown relativos e caminhos `metodo/nucleo/...` citados nos arquivos do método e das skills.
function conferirLinks() {
  const bases = ['metodo/nucleo', 'metodo/projeto/skills', 'metodo/projeto'].map((d) => path.join(RAIZ, d));
  const arquivos = [];
  const andar = (d, raso) => { if (!fs.existsSync(d)) return; for (const n of fs.readdirSync(d)) { const p = path.join(d, n); const st = fs.statSync(p); if (st.isDirectory()) { if (!raso && n !== 'node_modules') andar(p, false); } else if (n.endsWith('.md')) arquivos.push(p); } };
  andar(bases[0], false); andar(bases[1], false); andar(bases[2], true);
  const ehModelo = (alvo) => /[<>*{}…]|XXXX|AAAA|\.\.\.|^\s*$/.test(alvo);
  let n = 0;
  for (const arq of [...new Set(arquivos)]) {
    const rel = path.relative(RAIZ, arq).split(path.sep).join('/');
    const txt = fs.readFileSync(arq, 'utf8').replace(/```[\s\S]*?```/g, '');
    for (const m of txt.matchAll(/\]\(([^)\s]+)\)/g)) {
      const alvo = m[1].split('#')[0];
      if (!alvo || /^[a-z]+:/i.test(alvo) || ehModelo(alvo)) continue;
      if (!fs.existsSync(path.resolve(path.dirname(arq), decodeURI(alvo)))) { console.warn(`! ${rel}: link quebrado para ${m[1]}`); n++; }
    }
    for (const m of txt.matchAll(/`(metodo\/nucleo\/[^`\s]+)`/g)) {
      const alvo = m[1].replace(/[.,;:]$/, '');
      if (ehModelo(alvo)) continue;
      if (!fs.existsSync(path.join(RAIZ, alvo))) { console.warn(`! ${rel}: cita \`${alvo}\`, que não existe`); n++; }
    }
  }
  return n;
}

function skillsDe(dir, origem) {
  const abs = path.join(RAIZ, dir);
  if (!fs.existsSync(abs)) return [];
  return fs.readdirSync(abs)
    .filter((n) => fs.existsSync(path.join(abs, n, 'SKILL.md')))
    .map((n) => {
      const arq = `${dir}/${n}/SKILL.md`;
      const fm = frontmatter(path.join(RAIZ, arq));
      conferir(arq, n);
      return { nome: n, descricao: fm.description || '', arq, origem };
    });
}

function limparGerados(dir, ehPasta) {
  const abs = path.join(RAIZ, dir);
  if (!fs.existsSync(abs)) return;
  for (const n of fs.readdirSync(abs)) {
    const alvo = ehPasta ? path.join(abs, n, 'SKILL.md') : path.join(abs, n);
    if (fs.existsSync(alvo) && fs.statSync(alvo).isFile() && fs.readFileSync(alvo, 'utf8').includes(MARCA)) {
      fs.rmSync(ehPasta ? path.join(abs, n) : alvo, { recursive: true, force: true });
    }
  }
}

function escreverSkills(destinoDir, skills) {
  limparGerados(destinoDir, true);
  let n = 0;
  for (const s of skills) {
    const destino = path.join(RAIZ, destinoDir, s.nome);
    if (fs.existsSync(path.join(destino, 'SKILL.md'))) {
      console.warn(`! ${destinoDir}/${s.nome}/SKILL.md já existe e não foi gerado pelo método; mantido.`);
      continue;
    }
    fs.mkdirSync(destino, { recursive: true });
    fs.writeFileSync(path.join(destino, 'SKILL.md'), [
      '---',
      `name: ${s.nome}`,
      `description: ${yamlStr(s.descricao)}`,
      '---',
      MARCA,
      '',
      `Leia e siga integralmente \`${s.arq}\` (skill do ${s.origem}). Antes, leia \`AGENTS.md\` se ainda não leu nesta sessão.`,
      '',
    ].join('\n'));
    n++;
  }
  return n;
}

const skills = [
  ...skillsDe('metodo/nucleo/skills', 'método'),
  ...skillsDe('metodo/projeto/skills', 'projeto'),
];
const nomes = new Map();
for (const s of skills) {
  if (nomes.has(s.nome)) {
    console.error(`✗ Skill "${s.nome}" existe em ${nomes.get(s.nome)} e em ${s.arq}. Renomeie a do projeto.`);
    process.exit(1);
  }
  nomes.set(s.nome, s.arq);
}

const adaptadores = lerAdaptadores();
const gerados = [];
for (const a of Object.keys(PASTAS)) {
  if (adaptadores.includes(a)) gerados.push(`${escreverSkills(PASTAS[a], skills)} em ${PASTAS[a]}`);
  else limparGerados(PASTAS[a], true); // adaptador removido do config: apaga só os ponteiros gerados
}

// Claude Code: subagentes (papéis)
limparGerados('.claude/agents', false);
const comPapeis = adaptadores.includes('claude');
const papeisDir = path.join(RAIZ, 'metodo/nucleo/papeis');
const papeis = comPapeis && fs.existsSync(papeisDir) ? fs.readdirSync(papeisDir).filter((f) => f.endsWith('.md')) : [];
if (papeis.length) fs.mkdirSync(path.join(RAIZ, '.claude', 'agents'), { recursive: true });
let nPapeis = 0;
for (const f of papeis) {
  const arq = `metodo/nucleo/papeis/${f}`;
  const fm = frontmatter(path.join(RAIZ, arq));
  const nome = fm.name || f.replace(/\.md$/, '');
  const destino = path.join(RAIZ, '.claude', 'agents', `${nome}.md`);
  if (fs.existsSync(destino)) {
    console.warn(`! .claude/agents/${nome}.md já existe e não foi gerado pelo método; mantido.`);
    continue;
  }
  fs.writeFileSync(destino, [
    '---',
    `name: ${nome}`,
    `description: ${yamlStr(fm.description || '')}`,
    '---',
    MARCA,
    '',
    `Você atua no papel **${nome}** do método deste projeto.`,
    '',
    `1. Leia \`${arq}\`: sua responsabilidade, o que faz e o que não faz.`,
    '2. Leia `metodo/nucleo/fluxo.md`, `metodo/projeto/config.yaml` e `metodo/projeto/principios.md`.',
    '3. Leia a task (ou HU/PRD) indicada por quem te chamou e as skills listadas nela.',
    '4. Devolva o resultado no formato de entrega descrito no seu papel.',
    '',
  ].join('\n'));
  nPapeis++;
}

// AGENTS.md: lista "Skills deste projeto" entre o comentário-marcador e o próximo título
const agentsPath = path.join(RAIZ, 'AGENTS.md');
if (fs.existsSync(agentsPath)) {
  const txt = fs.readFileSync(agentsPath, 'utf8');
  const marca = '<!-- Lista gerada por metodo/nucleo/scripts/gerar-adaptadores.mjs a partir da description de cada skill. Não edite à mão. -->';
  const velha = '<!-- Lista mantida pelas skills aplicar-metodo e criar-skill-projeto. Formato: - `nome`: quando usar -->'; // até a v0.2.1
  let base = txt.includes(velha) ? txt.replace(velha, marca) : txt;
  const i = base.indexOf(marca);
  if (i >= 0) {
    const fim = base.indexOf('\n## ', i);
    const doProjeto = skills.filter((s) => s.origem === 'projeto');
    const lista = doProjeto.length
      ? doProjeto.map((s) => `- \`${s.nome}\`: ${s.descricao || '(sem description)'}`).join('\n')
      : '- (nenhuma ainda)';
    const novo = base.slice(0, i + marca.length) + '\n' + lista + '\n' + (fim >= 0 ? base.slice(fim) : '');
    if (novo !== txt) { fs.writeFileSync(agentsPath, novo); console.log(`✓ AGENTS.md: lista de skills do projeto atualizada (${doProjeto.length}).`); }
  }
  // O roteador não pode apontar para skill que não existe neste projeto.
  const atual = fs.readFileSync(agentsPath, 'utf8');
  for (const m of new Set([...atual.matchAll(/metodo\/(?:projeto|nucleo)\/skills\/([a-z0-9-]+)\//g)].map((x) => x[0]))) {
    if (!fs.existsSync(path.join(RAIZ, m, 'SKILL.md'))) { console.warn(`! AGENTS.md cita ${m}SKILL.md, que não existe neste projeto: remova ou ajuste a linha.`); avisos++; }
  }
}

// Claude Code: trava de leitura dos arquivos de segredo (config.yaml → seguranca.arquivos_proibidos).
// A regra de texto do AGENTS.md vale para todas as ferramentas; esta trava só o Claude Code aplica.
// Nunca remove regras que já estão no settings.json: só acrescenta.
let nTravas = 0;
if (adaptadores.includes('claude')) {
  const cfgTxt = fs.existsSync(path.join(RAIZ, 'metodo/projeto/config.yaml')) ? fs.readFileSync(path.join(RAIZ, 'metodo/projeto/config.yaml'), 'utf8') : '';
  const m = cfgTxt.match(/^\s+arquivos_proibidos:\s*\[([^\]]*)\]/m);
  const PADRAO = ['.env', '.env.*', '*.pem', '*.key', '*.p12', '*.pfx', '*.jks', '*.keystore', 'id_rsa', 'id_ed25519'];
  const lista = m ? m[1].split(',').map((x) => x.trim().replace(/^["']|["']$/g, '')).filter(Boolean) : PADRAO;
  const regras = new Set();
  for (const p of lista) {
    if (p.includes('/')) regras.add(`Read(./${p.replace(/^\.?\//, '')})`);
    else { regras.add(`Read(./${p})`); regras.add(`Read(./**/${p})`); }
  }
  const settingsPath = path.join(RAIZ, '.claude', 'settings.json');
  let settings = {};
  let ok = true;
  if (fs.existsSync(settingsPath)) {
    try { settings = JSON.parse(fs.readFileSync(settingsPath, 'utf8')); }
    catch { ok = false; console.warn('! .claude/settings.json não é JSON válido; a trava de segredos não foi aplicada.'); avisos++; }
  }
  if (ok) {
    settings.permissions = settings.permissions || {};
    const deny = new Set(settings.permissions.deny || []);
    const antes = deny.size;
    for (const r of regras) deny.add(r);
    nTravas = regras.size;
    if (deny.size !== antes || !fs.existsSync(settingsPath)) {
      settings.permissions.deny = [...deny];
      fs.mkdirSync(path.join(RAIZ, '.claude'), { recursive: true });
      fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2) + '\n');
      console.log(`✓ .claude/settings.json: ${deny.size - antes} trava(s) de leitura nova(s) para arquivos de segredo.`);
    }
  }
}

avisos += conferirLinks();

console.log(`✓ Adaptadores (${adaptadores.join(', ')}): ${skills.length} skill(s) → ${gerados.join(' · ')}${comPapeis ? ` · ${nPapeis} papel(éis) em .claude/agents` : ''}${nTravas ? ` · ${nTravas} trava(s) de segredo` : ''}`);
console.log(avisos ? `! ${avisos} aviso(s): veja acima (padrão Agent Skills, origem de skills de fora, links quebrados).` : '✓ Todas as skills conformes ao padrão Agent Skills e sem links quebrados.');
