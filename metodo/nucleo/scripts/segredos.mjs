#!/usr/bin/env node
// Procura segredos no que a task mudou: linhas adicionadas no diff (staged e não staged)
// e arquivos novos não rastreados. Nunca imprime o valor encontrado: só arquivo, linha e tipo.
//
// Uso:
//   node metodo/nucleo/scripts/segredos.mjs            # diff contra HEAD + arquivos novos
//   node metodo/nucleo/scripts/segredos.mjs --base main # diff contra outra referência
//
// Saída 0 = nada encontrado; 1 = achados (a task não conclui até resolver ou o desenvolvedor aceitar).
// Se o gitleaks estiver instalado, rode-o também; este script é o mínimo que funciona sem instalar nada.

import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const iBase = args.indexOf('--base');
const base = iBase >= 0 && args[iBase + 1] ? args[iBase + 1] : 'HEAD';

const sh = (cmd) => { try { return execSync(cmd, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], maxBuffer: 64 * 1024 * 1024 }); } catch { return null; } };
const raiz = (sh('git rev-parse --show-toplevel') || '').trim();
if (!raiz) { console.error('Não é um repositório git: rode na pasta do projeto.'); process.exit(2); }

const PADROES = [
  ['chave privada', /-----BEGIN [A-Z ]*PRIVATE KEY-----/],
  ['chave AWS', /\bAKIA[0-9A-Z]{16}\b/],
  ['token GitHub', /\bgh[pousr]_[A-Za-z0-9]{30,}\b/],
  ['token GitLab', /\bglpat-[A-Za-z0-9_-]{20,}\b/],
  ['token Slack', /\bxox[abprs]-[A-Za-z0-9-]{10,}\b/],
  ['chave Google API', /\bAIza[0-9A-Za-z_-]{35}\b/],
  ['chave Stripe', /\b[sr]k_(live|test)_[A-Za-z0-9]{16,}\b/],
  ['JWT', /\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/],
  ['string de conexão com senha', /\b[a-z][a-z0-9+.-]*:\/\/[^\s:/@]+:[^\s@/]{4,}@/i],
  // texto literal entre aspas atribuído a um nome de segredo (código, JSON, YAML)
  ['senha ou segredo atribuído', /(?:^|[^A-Za-z0-9])[A-Za-z0-9_]*?(pass(word|wd)?|senha|secret|segredo|token|api[_-]?key)[A-Za-z0-9_]*["']?\s*[:=]\s*["'`][^"'`\s]{6,}["'`]/i],
  // linha inteira NOME=valor (.env, .properties, gradle.properties)
  ['senha ou segredo atribuído', /^\s*[A-Za-z0-9_.]*(pass(word|wd)?|senha|secret|segredo|token|api[_-]?key)[A-Za-z0-9_.]*\s*=\s*[^\s#()'";]{6,}\s*$/i],
];
// Valores que são claramente placeholders não contam.
const PLACEHOLDER = /(\$\{|process\.env|import\.meta\.env|<[^>]+>|xxx|\*\*\*|changeme|example|exemplo|placeholder|your[_-]|seu[_-]|dummy|fake|teste123)/i;
const NOMES_PROIBIDOS = [/(^|\/)\.env(\..*)?$/, /\.(pem|key|p12|pfx|jks|keystore)$/i, /(^|\/)id_(rsa|ed25519|ecdsa)$/];
const EXEMPLO_ENV = /(^|\/)\.env\.(example|exemplo|sample|template)$/i;

const achados = [];
const mascarar = (s) => (s.length <= 4 ? '****' : `${s.slice(0, 2)}…(${s.length})`);

function examinarLinha(arquivo, nLinha, linha) {
  for (const [tipo, re] of PADROES) {
    const m = linha.match(re);
    if (!m) continue;
    if (tipo === 'senha ou segredo atribuído' && PLACEHOLDER.test(linha)) continue;
    achados.push({ arquivo, linha: nLinha, tipo, trecho: mascarar(m[0]) });
    break;
  }
}

// 1. Linhas adicionadas no diff
const diff = sh(`git -C "${raiz}" diff ${base} --unified=0 --no-color`) ?? sh(`git -C "${raiz}" diff --unified=0 --no-color`) ?? '';
let atual = null; let n = 0;
for (const l of diff.split('\n')) {
  if (l.startsWith('+++ ')) { atual = l.slice(4).replace(/^b\//, ''); continue; }
  const h = l.match(/^@@ -\d+(?:,\d+)? \+(\d+)/);
  if (h) { n = Number(h[1]); continue; }
  if (!atual || atual === '/dev/null') continue;
  if (l.startsWith('+') && !l.startsWith('+++')) { examinarLinha(atual, n, l.slice(1)); n++; }
  else if (!l.startsWith('-')) n++;
}
const tocados = new Set([...diff.matchAll(/^\+\+\+ b\/(.+)$/gm)].map((m) => m[1]));

// 2. Arquivos novos não rastreados
const novos = (sh(`git -C "${raiz}" ls-files --others --exclude-standard`) || '').split('\n').filter(Boolean);
for (const rel of novos) {
  tocados.add(rel);
  const abs = path.join(raiz, rel);
  let st; try { st = fs.statSync(abs); } catch { continue; }
  if (!st.isFile() || st.size > 2 * 1024 * 1024) continue;
  const buf = fs.readFileSync(abs);
  if (buf.includes(0)) continue; // binário
  buf.toString('utf8').split('\n').forEach((linha, i) => examinarLinha(rel, i + 1, linha));
}

// 3. Arquivo de segredo entrando no repositório
for (const rel of tocados) {
  if (EXEMPLO_ENV.test(rel)) continue;
  if (NOMES_PROIBIDOS.some((re) => re.test(rel))) achados.push({ arquivo: rel, linha: '-', tipo: 'arquivo de segredo no diff', trecho: '' });
}

if (!achados.length) {
  console.log(`✓ Nenhum segredo encontrado (diff contra ${base} + ${novos.length} arquivo(s) novo(s)).`);
} else {
  console.log(`✗ ${achados.length} possível(is) segredo(s). O valor não é mostrado:`);
  for (const a of achados) console.log(`  - ${a.arquivo}:${a.linha} · ${a.tipo}${a.trecho ? ` · ${a.trecho}` : ''}`);
  console.log('Tire o valor do código (variável de ambiente ou cofre) ou, se for falso positivo, explique no relatório.');
  process.exitCode = 1;
}
