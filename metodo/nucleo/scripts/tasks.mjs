#!/usr/bin/env node
// Ferramenta do método para HUs, tasks e ADRs em docs/ (rastreador local).
// Sem dependências. Rode na raiz do projeto:
//   node metodo/nucleo/scripts/tasks.mjs listar [status]
//   node metodo/nucleo/scripts/tasks.mjs proxima          # autorizadas com dependências concluídas
//   node metodo/nucleo/scripts/tasks.mjs proximo-id hu|task|adr
//   node metodo/nucleo/scripts/tasks.mjs validar
//   node metodo/nucleo/scripts/tasks.mjs indice
//   node metodo/nucleo/scripts/tasks.mjs metricas [HU-XXXX] [--csv]   # soma as métricas das tasks executadas

import fs from 'node:fs';
import path from 'node:path';

const RAIZ = process.cwd();
const TRABALHO = path.join(RAIZ, 'docs', 'trabalho');
const DECISOES = path.join(RAIZ, 'docs', 'decisoes');
const CONFIG = path.join(RAIZ, 'metodo', 'projeto', 'config.yaml');
const SKILLS_PROJETO = path.join(RAIZ, 'metodo', 'projeto', 'skills');

// ---------- leitura ----------

function lerValor(bruto) {
  let v = bruto.trim();
  if (v.startsWith('"') || v.startsWith("'")) {
    const q = v[0];
    const fim = v.indexOf(q, 1);
    if (fim > 0) return v.slice(1, fim);
  }
  const i = v.indexOf(' #');
  if (i >= 0) v = v.slice(0, i).trim();
  if (v.startsWith('[') && v.endsWith(']')) {
    return v.slice(1, -1).split(',').map((s) => s.trim().replace(/^["']|["']$/g, '')).filter(Boolean);
  }
  if (v === 'true') return true;
  if (v === 'false') return false;
  if (/^-?\d+$/.test(v)) return Number(v);
  return v;
}

function lerFrontmatter(arquivo) {
  const texto = fs.readFileSync(arquivo, 'utf8').replace(/\r\n/g, '\n');
  const m = texto.match(/^---\n([\s\S]*?)\n---/);
  if (!m) return null;
  const dados = {};
  for (const linha of m[1].split('\n')) {
    const mm = linha.match(/^([a-zA-Z_]+):\s*(.*)$/);
    if (mm) dados[mm[1]] = lerValor(mm[2]);
  }
  return dados;
}

function lerConfig() {
  const cfg = { statusValidos: ['backlog', 'pronta', 'em-desenvolvimento', 'em-revisao', 'em-teste', 'concluida', 'bloqueada'], podeIniciar: ['em-desenvolvimento'], rastreador: 'local' };
  if (!fs.existsSync(CONFIG)) return cfg;
  const texto = fs.readFileSync(CONFIG, 'utf8');
  const lista = (chave) => {
    const m = texto.match(new RegExp(`^\\s*${chave}:\\s*(\\[[^\\]]*\\])`, 'm'));
    return m ? lerValor(m[1]) : [];
  };
  const todos = [...lista('fluxo'), ...lista('extras')];
  if (todos.length) cfg.statusValidos = todos;
  const pi = lista('agente_pode_iniciar');
  if (pi.length) cfg.podeIniciar = pi;
  const r = texto.match(/^rastreador:\s*(\w+)/m);
  if (r) cfg.rastreador = r[1];
  return cfg;
}

function coletar() {
  const hus = [];
  const tasks = [];
  const adrs = [];
  if (fs.existsSync(TRABALHO)) {
    for (const pasta of fs.readdirSync(TRABALHO).sort()) {
      const dir = path.join(TRABALHO, pasta);
      if (!fs.statSync(dir).isDirectory() || !(/^HU-\d+/.test(pasta) || pasta === 'avulsas')) continue;
      const huArq = path.join(dir, 'hu.md');
      if (fs.existsSync(huArq)) {
        hus.push({ ...(lerFrontmatter(huArq) || {}), _arquivo: path.relative(RAIZ, huArq), _pasta: pasta, _texto: fs.readFileSync(huArq, 'utf8') });
      }
      const tdir = path.join(dir, 'tasks');
      if (fs.existsSync(tdir)) {
        for (const f of fs.readdirSync(tdir).sort()) {
          if (!/^T-\d+.*\.md$/.test(f)) continue;
          const arq = path.join(tdir, f);
          tasks.push({ ...(lerFrontmatter(arq) || {}), _arquivo: path.relative(RAIZ, arq), _pasta: pasta });
        }
      }
    }
  }
  if (fs.existsSync(DECISOES)) {
    for (const f of fs.readdirSync(DECISOES).sort()) {
      if (!/^ADR-\d+.*\.md$/.test(f)) continue;
      adrs.push({ ...(lerFrontmatter(path.join(DECISOES, f)) || {}), _arquivo: path.relative(RAIZ, path.join(DECISOES, f)) });
    }
  }
  return { hus, tasks, adrs };
}

const num = (id) => parseInt(String(id || '').replace(/\D/g, ''), 10) || 0;
const pad = (n) => String(n).padStart(4, '0');
const rel = (p) => p.split(path.sep).join('/');

// ---------- comandos ----------

function linhaTask(t) {
  const dep = Array.isArray(t.depende_de) && t.depende_de.length ? `  (depende de ${t.depende_de.join(', ')})` : '';
  const risco = t.risco === 'alto' ? '  [risco alto]' : '';
  const st = t.status ? String(t.status) : '(status no prisma)';
  console.log(`${t.id}  ${st.padEnd(20)} ${t.titulo}  · ${t.hu}${dep}${risco}`);
  console.log(`        ${t._arquivo}`);
}

function listar(filtro) {
  const { tasks } = coletar();
  const lista = filtro ? tasks.filter((t) => t.status === filtro) : tasks;
  if (!lista.length) {
    console.log(filtro ? `Nenhuma task com status "${filtro}".` : 'Nenhuma task encontrada.');
    return;
  }
  lista.forEach(linhaTask);
}

function proxima() {
  const cfg = lerConfig();
  if (cfg.rastreador !== 'local') {
    console.log(`rastreador: ${cfg.rastreador} — use a operação proximas_tasks do rastreador.`);
    return;
  }
  const { tasks } = coletar();
  const c = ciclos(tasks);
  if (c.length) { console.log(`✗ ciclo de dependências: ${c.join('; ')}. Rode validar e corrija antes.`); process.exitCode = 1; return; }
  const porId = new Map(tasks.map((t) => [t.id, t]));
  const prontas = tasks.filter((t) => cfg.podeIniciar.includes(t.status)).filter((t) => {
    const deps = Array.isArray(t.depende_de) ? t.depende_de : [];
    return deps.every((d) => porId.get(d)?.status === 'concluida');
  });
  if (!prontas.length) {
    console.log(`Nenhuma task autorizada (${cfg.podeIniciar.join(', ')}) com dependências concluídas.`);
    return;
  }
  console.log('Podem começar agora:');
  prontas.forEach(linhaTask);
}

function proximoId(tipo) {
  const { hus, tasks, adrs } = coletar();
  if (tipo === 'hu') {
    const pastas = fs.existsSync(TRABALHO) ? fs.readdirSync(TRABALHO).map((p) => (p.match(/^HU-(\d+)/) || [])[1]).filter(Boolean) : [];
    console.log(`HU-${pad(Math.max(0, ...hus.map((h) => num(h.id)), ...pastas.map(num)) + 1)}`);
  } else if (tipo === 'task') {
    const arquivos = [];
    if (fs.existsSync(TRABALHO)) for (const p of fs.readdirSync(TRABALHO)) {
      const tdir = path.join(TRABALHO, p, 'tasks');
      if (fs.existsSync(tdir)) for (const f of fs.readdirSync(tdir)) { const m = f.match(/^T-(\d+)/); if (m) arquivos.push(m[1]); }
    }
    console.log(`T-${pad(Math.max(0, ...tasks.map((t) => num(t.id)), ...arquivos.map(num)) + 1)}`);
  } else if (tipo === 'adr') {
    const arquivos = fs.existsSync(DECISOES) ? fs.readdirSync(DECISOES).map((f) => (f.match(/^ADR-(\d+)/) || [])[1]).filter(Boolean) : [];
    console.log(`ADR-${pad(Math.max(0, ...adrs.map((a) => num(a.id)), ...arquivos.map(num)) + 1)}`);
  } else {
    erroUso();
  }
}

function casDaHu(h) {
  const ids = new Set();
  for (const m of (h._texto || '').matchAll(/\*\*(CA-\d+):?\*\*/g)) ids.add(m[1]);
  return ids;
}

function ciclos(tasks) {
  const achados = [];
  const grafo = new Map(tasks.map((t) => [t.id, Array.isArray(t.depende_de) ? t.depende_de : []]));
  const estado = new Map();
  const visita = (id, trilha) => {
    if (estado.get(id) === 2) return;
    if (estado.get(id) === 1) { achados.push([...trilha, id].join(' → ')); return; }
    estado.set(id, 1);
    for (const d of grafo.get(id) || []) if (grafo.has(d)) visita(d, [...trilha, id]);
    estado.set(id, 2);
  };
  for (const id of grafo.keys()) visita(id, []);
  return achados;
}

function validar() {
  const { hus, tasks, adrs } = coletar();
  const cfg = lerConfig();
  const problemas = [];
  if (fs.existsSync(CONFIG) && /NOME DO PROJETO/.test(fs.readFileSync(CONFIG, 'utf8'))) problemas.push('metodo/projeto/config.yaml: ainda está no modelo ("NOME DO PROJETO"); rode a skill aplicar-metodo');
  const idsTask = new Map();
  const idsHu = new Set(hus.map((h) => h.id));
  const porHu = new Map(hus.map((h) => [h.id, h]));
  const cobertura = new Map(); // HU -> Set(CA cobertos)

  for (const h of hus) {
    for (const c of ['id', 'titulo', 'status']) if (!h[c]) problemas.push(`${h._arquivo}: falta "${c}" no frontmatter`);
    if (h.id && !h._pasta.startsWith(h.id)) problemas.push(`${h._arquivo}: id ${h.id} não bate com a pasta ${h._pasta}`);
    if (h.prd === 'sim' && !fs.existsSync(path.join(TRABALHO, h._pasta, 'prd.md'))) problemas.push(`${h._arquivo}: prd: sim, mas não existe prd.md`);
    if (/CA\d/.test(h._texto || '')) problemas.push(`${h._arquivo}: critérios devem ser numerados como CA-1, CA-2 (com hífen)`);
    cobertura.set(h.id, new Set());
  }

  for (const t of tasks) {
    for (const c of ['id', 'titulo', 'hu']) if (!t[c]) problemas.push(`${t._arquivo}: falta "${c}" no frontmatter`);
    if (cfg.rastreador === 'local' && !t.status) problemas.push(`${t._arquivo}: falta "status" (rastreador local)`);
    if (cfg.rastreador === 'prisma' && t.status) problemas.push(`${t._arquivo}: "status" não pode existir no frontmatter em modo prisma`);
    if (t.id) {
      if (idsTask.has(t.id)) problemas.push(`${t._arquivo}: id ${t.id} duplicado (também em ${idsTask.get(t.id)})`);
      idsTask.set(t.id, t._arquivo);
      if (!path.basename(t._arquivo).startsWith(t.id)) problemas.push(`${t._arquivo}: nome do arquivo não começa com ${t.id}`);
    }
    if (t.status && !cfg.statusValidos.includes(t.status)) problemas.push(`${t._arquivo}: status "${t.status}" inválido (válidos: ${cfg.statusValidos.join(', ')})`);
    if (t._pasta === 'avulsas') {
      if (t.hu !== 'avulsa') problemas.push(`${t._arquivo}: task em avulsas/ precisa de "hu: avulsa"`);
    } else {
      if (t.hu && !idsHu.has(t.hu)) problemas.push(`${t._arquivo}: hu ${t.hu} não existe`);
      if (t.hu && !t._pasta.startsWith(t.hu)) problemas.push(`${t._arquivo}: hu ${t.hu} não bate com a pasta ${t._pasta}`);
      const cobre = Array.isArray(t.cobre) ? t.cobre : [];
      const cas = porHu.get(t.hu) ? casDaHu(porHu.get(t.hu)) : new Set();
      for (const ca of cobre) {
        if (!cas.has(ca)) problemas.push(`${t._arquivo}: cobre ${ca}, que não existe na ${t.hu}`);
        cobertura.get(t.hu)?.add(ca);
      }
      if (!cobre.length && t.status !== 'concluida') problemas.push(`${t._arquivo}: "cobre" vazio (liste os CA da HU que esta task cumpre, ou diga no objetivo que é infraestrutura)`);
    }
    if (t.risco && !['normal', 'alto'].includes(t.risco)) problemas.push(`${t._arquivo}: risco "${t.risco}" inválido (normal | alto)`);
    if (t.complexidade !== undefined && (typeof t.complexidade !== 'number' || t.complexidade < 1 || t.complexidade > 7)) problemas.push(`${t._arquivo}: complexidade deve ser um número de 1 a 7 (acima disso, quebre a task)`);
    const TRILHAS = ['micro', 'bug', 'funcionalidade', 'epico', 'incidente'];
    if (t.trilha && !TRILHAS.includes(t.trilha)) problemas.push(`${t._arquivo}: trilha "${t.trilha}" inválida (${TRILHAS.join(' | ')})`);
    if (t.hu === 'avulsa' && t.trilha && !['micro', 'bug', 'incidente'].includes(t.trilha)) problemas.push(`${t._arquivo}: task avulsa só pode ter trilha micro, bug ou incidente`);
    if (t.hu !== 'avulsa' && t.trilha && !['funcionalidade', 'epico'].includes(t.trilha)) problemas.push(`${t._arquivo}: task de HU só pode ter trilha funcionalidade ou epico`);
    const textoTask = fs.readFileSync(path.join(RAIZ, t._arquivo), 'utf8');
    if (/\*\*CA\d/.test(textoTask)) problemas.push(`${t._arquivo}: critérios devem ser numerados como CA-1, CA-2 (com hífen)`);
    if (t.trilha === 'bug' && !/## Diagnóstico\s*\n\s*[^\s<#]/.test(textoTask)) problemas.push(`${t._arquivo}: trilha bug exige a seção Diagnóstico preenchida`);
    // Tabela "Testes" (v0.4): exigida em task ainda não iniciada, para não quebrar tasks antigas.
    const naoIniciada = t.status ? ['backlog', 'pronta'].includes(t.status) : !/### Execução ·/.test(textoTask);
    if (naoIniciada && t.trilha !== 'incidente') {
      const secao = (textoTask.split(/\n## Testes\s*\n/)[1] || '').split(/\n## /)[0];
      const linhas = secao.split('\n').filter((l) => /^\|\s*CA-\d+/.test(l));
      if (!linhas.length) problemas.push(`${t._arquivo}: falta a tabela "Testes" com um tipo por CA (metodo/nucleo/referencias/testes.md)`);
      const casTestados = new Set(linhas.map((l) => l.match(/CA-\d+/)[0]));
      for (const ca of Array.isArray(t.cobre) ? t.cobre : []) if (linhas.length && !casTestados.has(ca)) problemas.push(`${t._arquivo}: ${ca} está em "cobre" mas não tem linha na tabela "Testes"`);
      for (const l of linhas) if (/\|\s*manual\s*\|/i.test(l) && !/\(.+\)|motivo|porque|sem /i.test(l)) problemas.push(`${t._arquivo}: teste manual sem motivo: ${l.trim()}`);
    }
    if (t.revisao_humana && !['aprovado', 'ajustes', 'refeito'].includes(t.revisao_humana)) problemas.push(`${t._arquivo}: revisao_humana "${t.revisao_humana}" inválida (aprovado | ajustes | refeito)`);
    if (t.homologacao && !['ok', 'falhou', 'nao-se-aplica'].includes(t.homologacao)) problemas.push(`${t._arquivo}: homologacao "${t.homologacao}" inválida (ok | falhou | nao-se-aplica)`);
    for (const c of ['voltas', 'bloqueios', 'ca_nao_verificados', 'testes_criados', 'testes_pegam']) {
      if (t[c] !== undefined && t[c] !== null && t[c] !== 'null' && t[c] !== '' && (typeof t[c] !== 'number' || t[c] < 0)) problemas.push(`${t._arquivo}: ${c} deve ser um número inteiro >= 0`);
    }
    if (typeof t.testes_pegam === 'number' && typeof t.testes_criados === 'number' && t.testes_pegam > t.testes_criados) problemas.push(`${t._arquivo}: testes_pegam maior que testes_criados`);
    if (['em-revisao', 'em-teste', 'concluida'].includes(t.status) && /### Execução ·/.test(textoTask) && typeof t.voltas !== 'number' && t.ferramenta !== undefined) problemas.push(`${t._arquivo}: task executada sem as métricas preenchidas (voltas, testes_criados…)`);
    for (const sk of Array.isArray(t.skills) ? t.skills : []) {
      if (!fs.existsSync(path.join(SKILLS_PROJETO, sk, 'SKILL.md'))) problemas.push(`${t._arquivo}: skill "${sk}" não existe em metodo/projeto/skills/`);
    }
    if (/<título>|T-XXXX|HU-XXXX/.test(String(t.titulo) + String(t.id) + String(t.hu))) problemas.push(`${t._arquivo}: modelo não preenchido`);
  }
  for (const t of tasks) {
    for (const d of Array.isArray(t.depende_de) ? t.depende_de : []) {
      if (!idsTask.has(d)) problemas.push(`${t._arquivo}: depende de ${d}, que não existe`);
      if (d === t.id) problemas.push(`${t._arquivo}: depende de si mesma`);
    }
  }
  for (const c of ciclos(tasks)) problemas.push(`ciclo de dependências: ${c}`);
  // cobertura de CA
  for (const h of hus) {
    if (h.status !== 'planejada') continue;
    const faltam = [...casDaHu(h)].filter((ca) => !cobertura.get(h.id)?.has(ca));
    if (faltam.length) problemas.push(`${h._arquivo}: CA sem task que o cubra: ${faltam.join(', ')}`);
  }
  for (const a of adrs) {
    for (const c of ['id', 'titulo', 'status']) if (!a[c]) problemas.push(`${a._arquivo}: falta "${c}" no frontmatter`);
    if (a.status && !['proposta', 'aceita', 'substituida', 'rejeitada'].includes(a.status)) problemas.push(`${a._arquivo}: status "${a.status}" inválido`);
  }

  if (problemas.length) {
    console.log(`✗ ${problemas.length} problema(s):`);
    for (const p of problemas) console.log(`  - ${p}`);
    process.exitCode = 1;
  } else {
    console.log(`✓ ${hus.length} HU(s), ${tasks.length} task(s) e ${adrs.length} ADR(s) válidos.`);
  }
}

function indice() {
  const { hus, tasks, adrs } = coletar();
  const cfg = lerConfig();
  const l = [];
  l.push('# Índice de trabalho', '');
  l.push('> Gerado por `node metodo/nucleo/scripts/tasks.mjs indice`. Não edite à mão.', '');
  if (cfg.rastreador === 'local') {
    l.push('## Tasks por status', '');
    for (const s of cfg.statusValidos) {
      const doStatus = tasks.filter((t) => t.status === s);
      if (!doStatus.length) continue;
      l.push(`### ${s} (${doStatus.length})`, '');
      l.push('| Task | Título | HU | Cobre | Depende de | Risco |', '|---|---|---|---|---|---|');
      for (const t of doStatus) {
        const link = rel(path.relative(TRABALHO, path.join(RAIZ, t._arquivo)));
        const dep = Array.isArray(t.depende_de) && t.depende_de.length ? t.depende_de.join(', ') : '—';
        const cobre = Array.isArray(t.cobre) && t.cobre.length ? t.cobre.join(', ') : '—';
        l.push(`| [${t.id}](${link}) | ${t.titulo} | ${t.hu} | ${cobre} | ${dep} | ${t.risco || 'normal'} |`);
      }
      l.push('');
    }
    const semStatus = tasks.filter((t) => !cfg.statusValidos.includes(t.status));
    if (semStatus.length) {
      l.push('### status inválido ou ausente', '');
      for (const t of semStatus) l.push(`- ${t.id} (${t.status || 'sem status'}) — ${t._arquivo}`);
      l.push('');
    }
  } else {
    l.push(`> Status no rastreador **${cfg.rastreador}**. Esta lista só mostra o conteúdo.`, '');
    l.push('## Tasks', '', '| Task | Título | HU | Cobre | Depende de | Risco |', '|---|---|---|---|---|---|');
    for (const t of tasks) {
      const link = rel(path.relative(TRABALHO, path.join(RAIZ, t._arquivo)));
      l.push(`| [${t.id}](${link}) | ${t.titulo} | ${t.hu} | ${(t.cobre || []).join?.(', ') || '—'} | ${(t.depende_de || []).join?.(', ') || '—'} | ${t.risco || 'normal'} |`);
    }
    l.push('');
  }
  l.push('## HUs', '', '| HU | Título | Trilha | Status | PRD | Tasks (concluídas/total) |', '|---|---|---|---|---|---|');
  for (const h of hus) {
    const ts = tasks.filter((t) => t.hu === h.id);
    const ok = ts.filter((t) => t.status === 'concluida').length;
    l.push(`| [${h.id}](${h._pasta}/hu.md) | ${h.titulo} | ${h.trilha || '—'} | ${h.status} | ${h.prd || 'nao'} | ${ok}/${ts.length} |`);
  }
  l.push('');
  if (adrs.length) {
    l.push('## Decisões (ADR)', '', '| ADR | Título | Status | Data |', '|---|---|---|---|');
    for (const a of adrs) l.push(`| [${a.id}](../decisoes/${path.basename(a._arquivo)}) | ${a.titulo} | ${a.status} | ${a.data || ''} |`);
    l.push('');
  }
  fs.mkdirSync(TRABALHO, { recursive: true });
  fs.writeFileSync(path.join(TRABALHO, 'INDICE.md'), l.join('\n'));
  console.log(`✓ docs/trabalho/INDICE.md atualizado (${hus.length} HUs, ${tasks.length} tasks, ${adrs.length} ADRs).`);
}

function metricas(filtroHu, csv) {
  const { hus, tasks } = coletar();
  const num = (v) => (typeof v === 'number' ? v : null);
  const executadas = tasks.filter((t) => (!filtroHu || t.hu === filtroHu) && num(t.voltas) !== null);
  if (csv) {
    const cols = ['id', 'hu', 'trilha', 'status', 'ferramenta', 'complexidade', 'risco', 'voltas', 'bloqueios', 'ca_nao_verificados', 'testes_criados', 'testes_pegam', 'revisao_humana', 'homologacao'];
    console.log(cols.join(','));
    for (const t of executadas) console.log(cols.map((c) => `"${String(t[c] ?? '').replace(/"/g, '""')}"`).join(','));
    return;
  }
  const soma = (arr, c) => arr.reduce((a, t) => a + (num(t[c]) || 0), 0);
  const pct = (a, b) => (b ? `${Math.round((100 * a) / b)}%` : '—');
  const media = (arr, c) => { const v = arr.map((t) => num(t[c])).filter((x) => x !== null); return v.length ? (v.reduce((a, b) => a + b, 0) / v.length).toFixed(1) : '—'; };
  const l = [];
  l.push(`# Métricas${filtroHu ? ` · ${filtroHu}` : ''}`, '');
  if (!executadas.length) { console.log(`${l[0]}\n\nNenhuma task com métricas preenchidas ainda (campo "voltas").`); return; }
  const rev = executadas.filter((t) => t.revisao_humana);
  const aprov = rev.filter((t) => t.revisao_humana === 'aprovado').length;
  const hom = executadas.filter((t) => ['ok', 'falhou'].includes(t.homologacao));
  l.push('| Métrica | Valor |', '|---|---|');
  l.push(`| Tasks executadas | ${executadas.length} |`);
  l.push(`| **Aprovadas pelo desenvolvedor como vieram** | **${pct(aprov, rev.length)}** (${aprov} de ${rev.length} revisadas; ${executadas.length - rev.length} sem resposta) |`);
  l.push(`| Ajustes · refeitas | ${rev.filter((t) => t.revisao_humana === 'ajustes').length} · ${rev.filter((t) => t.revisao_humana === 'refeito').length} |`);
  l.push(`| Sem nenhuma volta interna | ${pct(executadas.filter((t) => num(t.voltas) === 0).length, executadas.length)} (média ${media(executadas, 'voltas')} voltas) |`);
  l.push(`| Bloqueios | ${soma(executadas, 'bloqueios')} |`);
  l.push(`| CA não verificados | ${soma(executadas, 'ca_nao_verificados')} |`);
  l.push(`| Testes criados · provados que pegam | ${soma(executadas, 'testes_criados')} · ${soma(executadas, 'testes_pegam')} (${pct(soma(executadas, 'testes_pegam'), soma(executadas, 'testes_criados'))}) |`);
  l.push(`| Homologação ok | ${pct(hom.filter((t) => t.homologacao === 'ok').length, hom.length)} (${hom.length} homologadas) |`);
  const husDoFiltro = hus.filter((h) => (!filtroHu || h.id === filtroHu) && num(h.rodadas_entrevista) !== null);
  if (husDoFiltro.length) l.push(`| Entrevista: rodadas · perguntas (média por HU) | ${media(husDoFiltro, 'rodadas_entrevista')} · ${media(husDoFiltro, 'perguntas_entrevista')} |`);
  l.push('');
  const porFerr = new Map();
  for (const t of executadas) { const k = t.ferramenta || '(não informada)'; if (!porFerr.has(k)) porFerr.set(k, []); porFerr.get(k).push(t); }
  if (porFerr.size > 1 || !filtroHu) {
    l.push('## Por ferramenta', '', '| Ferramenta | Tasks | Aprovadas como vieram | Média de voltas |', '|---|---|---|---|');
    for (const [k, ts] of porFerr) { const r = ts.filter((t) => t.revisao_humana); l.push(`| ${k} | ${ts.length} | ${pct(r.filter((t) => t.revisao_humana === 'aprovado').length, r.length)} | ${media(ts, 'voltas')} |`); }
    l.push('');
  }
  const ruins = executadas.filter((t) => t.revisao_humana === 'refeito' || num(t.voltas) >= 2 || num(t.ca_nao_verificados) > 0);
  if (ruins.length) {
    l.push('## Para olhar na retrospectiva', '');
    for (const t of ruins) l.push(`- ${t.id} · ${t.titulo} · voltas ${t.voltas} · revisão ${t.revisao_humana || '—'} · CA não verificados ${t.ca_nao_verificados ?? 0}`);
    l.push('');
  }
  console.log(l.join('\n'));
}

function erroUso() {
  console.log('Uso: node metodo/nucleo/scripts/tasks.mjs listar [status] | proxima | proximo-id hu|task|adr | validar | indice | metricas [HU-XXXX] [--csv]');
  process.exitCode = 1;
}

const [cmd, arg] = process.argv.slice(2);
if (cmd === 'listar') listar(arg);
else if (cmd === 'proxima') proxima();
else if (cmd === 'proximo-id') proximoId(arg);
else if (cmd === 'validar') validar();
else if (cmd === 'indice') indice();
else if (cmd === 'metricas') { const extra = process.argv.slice(3); metricas(extra.find((a) => /^HU-\d+$/.test(a)), extra.includes('--csv')); }
else erroUso();
