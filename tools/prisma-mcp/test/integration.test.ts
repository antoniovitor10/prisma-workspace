// Integração da fase 2a contra um Prisma de verdade, num projeto próprio criado para o teste.
//
// Só roda com PRISMA_IT=1 e:
//   PRISMA_URL                   API (ex.: http://127.0.0.1:3130, instância da frente)
//   PRISMA_IT_EMAIL / _PASSWORD  administrador humano da instância de teste. Usado SÓ para o papel
//                                da pessoa na UI (criar e provisionar o projeto, emitir o token,
//                                autorizar, liberar retomada, revogar). O MCP e o import recebem
//                                apenas o token opaco do agente.
//   PRISMA_IT_PROJECT            opcional; padrão metodo-mcp-it-<data e hora>
//   PRISMA_IT_GITHUB_FAKE        opcional; URL do GitHub falso da frente vista daqui (ex.: http://127.0.0.1:3134).
//                                Com ela, o teste cadastra um repositório (App 424242, instalação 1) e exercita
//                                vincular_pr; sem ela, vincular_pr só é chamado para conferir a recusa sem repositório.
import { after, before, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const run = promisify(execFile);
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const FIXTURE = join(ROOT, 'test', 'fixtures', 'trabalho');
const ATIVO = process.env.PRISMA_IT === '1';
const URL_API = (process.env.PRISMA_URL ?? '').replace(/\/+$/, '');
const PROJETO = process.env.PRISMA_IT_PROJECT ?? `metodo-mcp-it-${new Date().toISOString().replace(/\D/g, '').slice(0, 14)}`;
const OITO = ['regras_do_projeto', 'obter_task', 'listar_tasks', 'proximas_tasks', 'criar_task', 'mover_status', 'comentar', 'vincular_pr'];

let uiHeaders: Record<string, string> = {};
let projectId = '';
let agentToken = '';
let agentTokenId = '';
const segredos = () => [agentToken, process.env.PRISMA_IT_PASSWORD].filter((x): x is string => !!x);
function semSegredo(texto: string, onde: string) {
  for (const s of segredos()) assert.ok(!texto.includes(s), `segredo vazou em ${onde}`);
  assert.doesNotMatch(texto, /pmt_[A-Za-z0-9_-]{43}|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/, `credencial vazou em ${onde}`);
}

// ---- papel da pessoa na UI (sessão humana) ----
async function uiFetch(method: string, path: string, body?: unknown) {
  const r = await fetch(`${URL_API}${path}`, { method, headers: { ...uiHeaders, ...(body ? { 'content-type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text();
  return { status: r.status, json: t ? JSON.parse(t) : undefined, text: t };
}
async function uiOk(method: string, path: string, body?: unknown) {
  const r = await uiFetch(method, path, body);
  assert.ok(r.status < 300, `${method} ${path} → ${r.status} ${r.text}`);
  return r.json;
}
async function tokenFetch(method: string, path: string, body?: unknown) {
  const r = await fetch(`${URL_API}${path}`, { method, headers: { Authorization: `Bearer ${agentToken}`, ...(body ? { 'content-type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  return { status: r.status, text: await r.text() };
}

// ---- processos com o token do agente ----
function envAgente(): Record<string, string> {
  const env: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined && !k.startsWith('PRISMA_')) env[k] = v;
  return { ...env, PRISMA_URL: URL_API, PRISMA_PROJECT: PROJETO, PRISMA_TOKEN: agentToken };
}
async function importar(pasta: string) {
  try {
    const { stdout, stderr } = await run(process.execPath, ['--import', 'tsx', 'src/import.ts', pasta], { cwd: ROOT, env: envAgente(), timeout: 120_000 });
    semSegredo(stdout + stderr, 'import');
    const n = (r: string) => Number(new RegExp(`${r}:\\s*(\\d+)`, 'i').exec(stdout)?.[1] ?? NaN);
    return { ok: true, saida: stdout, criados: n('Criados'), atualizados: n('atualizados'), semMudanca: n('sem mudança'), vinculos: n('vínculos criados') };
  } catch (e) {
    const saida = String((e as { stderr?: string }).stderr ?? (e as Error).message);
    semSegredo(saida, 'import (erro)');
    return { ok: false, saida, criados: NaN, atualizados: NaN, semMudanca: NaN, vinculos: NaN };
  }
}
async function copiaComStatus(trocas: [string, string][]) {
  const dir = await mkdtemp(join(tmpdir(), 'prisma-mcp-it-'));
  await cp(FIXTURE, dir, { recursive: true });
  for (const [arquivo, status] of trocas) {
    const f = join(dir, 'HU-9001-exemplo-teste', 'tasks', arquivo);
    await writeFile(f, (await readFile(f, 'utf8')).replace(/^status: .*$/m, `status: ${status}`));
  }
  return dir;
}

let client: Client | undefined;
async function chamar(name: string, args: Record<string, unknown> = {}) {
  const r = await client!.callTool({ name, arguments: args }) as { isError?: boolean; content: { text?: string }[]; structuredContent?: Record<string, any> };
  const texto = r.content.map(c => c.text ?? '').join('\n');
  semSegredo(texto + JSON.stringify(r.structuredContent ?? {}), name);
  return { erro: r.isError === true, texto, dados: r.structuredContent ?? {} };
}
const ok = async (n: string, a: Record<string, unknown> = {}) => { const r = await chamar(n, a); assert.equal(r.erro, false, `${n}: ${r.texto}`); return r; };
const recusa = async (n: string, a: Record<string, unknown>) => { const r = await chamar(n, a); assert.equal(r.erro, true, `${n} deveria ser recusado: ${r.texto}`); return r; };
const statusDe = async (id: string) => (await ok('obter_task', { id })).dados.task.status as string;

describe('integração 2a com o Prisma (token opaco)', { skip: !ATIVO && 'defina PRISMA_IT=1 (ver cabeçalho)' }, () => {
  before(async () => {
    const login = await fetch(`${URL_API}/api/auth/login`, { method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ email: process.env.PRISMA_IT_EMAIL, password: process.env.PRISMA_IT_PASSWORD }) });
    const jwt = (await login.json() as { accessToken: string }).accessToken;
    const orgs = await (await fetch(`${URL_API}/api/organizations`, { headers: { Authorization: `Bearer ${jwt}` } })).json() as { id: string; isActive?: boolean }[];
    uiHeaders = { Authorization: `Bearer ${jwt}`, 'X-Organization-Id': orgs.filter(o => o.isActive !== false)[0].id };
    const criado = await uiOk('POST', '/api/projects', { key: null, name: PROJETO, description: 'Integração do prisma-mcp (fase 2a).', nature: 1, workType: 1 });
    projectId = typeof criado === 'string' ? criado : criado.id;
    await uiOk('POST', `/api/projects/${projectId}/method/provision`, { autonomyLevel: 0 });
    await uiOk('PUT', `/api/projects/${projectId}/method/settings`, { enabled: true, autonomyLevel: 0 });
    const emitido = await uiOk('POST', `/api/projects/${projectId}/method/tokens`, { name: 'agente it', role: 'agente', validDays: 1 });
    agentToken = emitido.token; agentTokenId = emitido.metadata.id;
    process.stderr.write(`# projeto de teste: ${PROJETO}\n`);
  });
  after(async () => { await client?.close(); });

  test('import com token do agente: lote com task autorizada é recusado sem gravar nada', async () => {
    const r = await importar(FIXTURE); // T-9002 está em em-desenvolvimento no frontmatter
    assert.equal(r.ok, false, r.saida);
    assert.deepEqual(await uiOk('GET', `/api/projects/${projectId}/method/tasks`), []);
  });

  test('import em backlog/concluida cria as 3 tasks; segunda rodada não duplica', async () => {
    const dir = await copiaComStatus([['T-9002-em-andamento.md', 'backlog']]);
    try {
      const um = await importar(dir);
      assert.equal(um.ok, true, um.saida);
      assert.equal(um.criados, 3, um.saida);
      assert.equal(um.vinculos, 2, um.saida);
      const dois = await importar(dir);
      assert.equal(dois.criados, 0, dois.saida);
      assert.equal(dois.semMudanca, 3, dois.saida);
      assert.equal(dois.vinculos, 0, dois.saida);
    } finally { await rm(dir, { recursive: true, force: true }); }
    const tasks = await uiOk('GET', `/api/projects/${projectId}/method/tasks`) as { id: string; status: string; hu: string }[];
    assert.deepEqual(tasks.map(t => [t.id, t.status, t.hu]).sort(),
      [['T-9001', 'concluida', 'HU-9001'], ['T-9002', 'backlog', 'HU-9001'], ['T-9003', 'backlog', 'HU-9001']]);
  });

  test('MCP por stdio só com o token: 8 operações, papel e projeto vindos da API', async () => {
    const transport = new StdioClientTransport({ command: process.execPath, args: ['--import', 'tsx', 'src/server.ts'], cwd: ROOT, env: envAgente(), stderr: 'pipe' });
    client = new Client({ name: 'prisma-mcp-it', version: '0.0.0' });
    await client.connect(transport);
    assert.deepEqual((await client.listTools()).tools.map(t => t.name).sort(), [...OITO].sort());
    const r = await ok('regras_do_projeto');
    assert.match(r.texto, /agente/);
    assert.ok(r.texto.includes(PROJETO), r.texto);
    assert.deepEqual((await ok('listar_tasks')).dados.tasks.map((t: any) => t.id).sort(), ['T-9001', 'T-9002', 'T-9003']);
    assert.deepEqual((await ok('proximas_tasks')).dados.tasks, [], 'nada autorizado ainda');
  });

  test('agente não autoriza: recusado pelo MCP e por REST direto com o mesmo token', async () => {
    await recusa('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'um humano pediu' });
    const direto = await tokenFetch('POST', `/api/projects/${projectId}/method/tasks/T-9002/move`, { newStatus: 'em-desenvolvimento', reason: 'REST direto' });
    assert.equal(direto.status, 403, direto.text);
    assert.equal(await statusDe('T-9002'), 'backlog');
  });

  test('humano autoriza na sessão; agente vê em proximas_tasks, comenta e bloqueia', async () => {
    await uiOk('POST', `/api/projects/${projectId}/method/tasks/T-9002/move`, { newStatus: 'em-desenvolvimento', reason: 'humano autoriza' });
    assert.deepEqual((await ok('proximas_tasks')).dados.tasks.map((t: any) => t.id), ['T-9002']);
    await ok('comentar', { id: 'T-9002', texto: 'relatório de execução do teste de integração' });
    await ok('mover_status', { id: 'T-9002', novo_status: 'bloqueada', motivo: 'aguardando credencial' });
    assert.equal(await statusDe('T-9002'), 'bloqueada');
  });

  test('retomada: recusada sem liberação; aceita depois que o humano libera na sessão', async () => {
    await recusa('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'retomando sozinho' });
    const t = await uiOk('GET', `/api/projects/${projectId}/method/tasks/T-9002`);
    const pelaCredencial = await tokenFetch('POST', `/api/projects/${projectId}/method/tasks/${t.workItemId}/resume-approval`,
      { blockingEventId: t.blockingEventId, cycleId: t.cycleId, reason: 'auto' });
    assert.equal(pelaCredencial.status, 403, pelaCredencial.text);
    await uiOk('POST', `/api/projects/${projectId}/method/tasks/${t.workItemId}/resume-approval`,
      { blockingEventId: t.blockingEventId, cycleId: t.cycleId, reason: 'credencial entregue' });
    await ok('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'credencial recebida' });
    await ok('mover_status', { id: 'T-9002', novo_status: 'em-revisao', motivo: 'nível 0, sem push' });
    assert.equal(await statusDe('T-9002'), 'em-revisao');
  });

  test('criar_task: agente cria em backlog; não cria autorizada nem duplica', async () => {
    const r = await ok('criar_task', { id: 'T-9004', titulo: 'Criada pelo MCP', hu: 'HU-9001',
      arquivo: 'docs/trabalho/HU-9001-exemplo-teste/tasks/T-9004.md', cobre: ['CA-1'], risco: 'normal' });
    assert.equal(r.dados.task.status, 'backlog');
    await recusa('criar_task', { id: 'T-9005', titulo: 'Autorizada', hu: 'HU-9001', arquivo: 'x.md', cobre: ['CA-1'], risco: 'normal', status: 'em-desenvolvimento' });
    await recusa('criar_task', { id: 'T-9004', titulo: 'Duplicada', hu: 'HU-9001', arquivo: 'x.md', cobre: ['CA-1'], risco: 'normal' });
  });

  test('vincular_pr: URL de repositório não cadastrado é recusada; com repositório cadastrado, vincula', async () => {
    await recusa('vincular_pr', { id: 'T-9002', url: 'https://github.com/outro/repo/pull/1' });
    const fakeUrl = process.env.PRISMA_IT_GITHUB_FAKE?.replace(/\/+$/, '');
    if (!fakeUrl) return;
    const repositoryId = 910000 + Math.floor(Math.random() * 80000);
    const fullName = `qa/mcp-it-${Date.now().toString(36)}`;
    const fx = (method: string, path: string, body: unknown) => fetch(`${fakeUrl}${path}`, { method, headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
    assert.ok((await fx('PUT', '/__fixture/repository', { id: repositoryId, full_name: fullName, default_branch: 'main', installation_id: 1 })).ok);
    assert.ok((await fx('PUT', '/__fixture/pulls/42', { title: 'Ajuste da T-9002', head: { ref: 'feature/livre' } })).ok);
    const sistema = await uiOk('POST', `/api/projects/${projectId}/method/tokens`, { name: 'sistema it', role: 'sistema', validDays: 1 });
    await uiOk('POST', `/api/projects/${projectId}/method/repositories`, { host: 'github.com', repositoryId, repositoryFullName: fullName,
      defaultBranch: 'main', appId: 424242, installationId: 1, appPrivateKeySecretRef: 'test-app-key', webhookSecretRef: 'test-webhook-secret',
      systemPrincipalId: sistema.metadata.principalId, enabled: true });
    const url = `https://github.com/${fullName}/pull/42`;
    await ok('vincular_pr', { id: 'T-9002', url });
    assert.equal((await ok('obter_task', { id: 'T-9002' })).dados.task.pr, url);
    await ok('vincular_pr', { id: 'T-9002', url }); // repetir é idempotente
    await recusa('vincular_pr', { id: 'T-9003', url }); // o mesmo PR não vai para outra task pelo agente
  });

  test('revogação vale na chamada seguinte: MCP manda parar e avisar', async () => {
    await uiOk('POST', `/api/projects/${projectId}/method/tokens/${agentTokenId}/revoke`);
    const r = await recusa('listar_tasks', {});
    assert.match(r.texto, /pare e avise/i);
    assert.equal((await tokenFetch('GET', '/api/method/me')).status, 401);
  });
});
