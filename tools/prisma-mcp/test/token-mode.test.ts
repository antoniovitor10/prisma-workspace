// Fase 2a (RF-12, CA-2A-17): o adaptador usa só o token opaco do método, com identidade e regras
// vindas da API. Caixa-preta: sobe o servidor MCP por stdio contra um Prisma falso e confere as
// requisições que ele faz. Contrato de rotas: docs/method-git/contracts-phase2a.md.
import { after, afterEach, before, beforeEach, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const PROJECT = '11111111-2222-4333-8444-555555555555';
const BOARD = '66666666-7777-4888-8999-aaaaaaaaaaaa';
const TOKEN = `pmt_${'A'.repeat(43)}`;
const JWT = 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.c2lnbmF0dXJh';
const OITO = ['regras_do_projeto', 'obter_task', 'listar_tasks', 'proximas_tasks', 'criar_task', 'mover_status', 'comentar', 'vincular_pr'];

function identity(role = 'agente', projectName = 'canais-mobile') {
  return {
    authenticationKind: 'methodToken', role, principalId: '99999999-0000-4000-8000-000000000001',
    userId: 'method:99999999-0000-4000-8000-000000000001', displayName: 'Agente de teste',
    organizationId: '00000000-0000-4000-8000-000000000001', projectId: PROJECT, projectName,
    boardId: BOARD, boardName: projectName, tokenId: '99999999-0000-4000-8000-0000000000aa',
    expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(), capabilities: ['read', 'comment', 'move'],
  };
}
function task(id: string, status: string, extra: Record<string, unknown> = {}) {
  return { workItemId: `00000000-0000-4000-8000-${id.slice(2).padStart(12, '0')}`, id, title: `Task ${id}`, status, hu: 'HU-0001',
    filePath: `docs/trabalho/HU-0001/tasks/${id}.md`, covers: ['CA-1'], risk: 'normal', pr: null, dependsOn: [], authorized: status !== 'backlog', canResume: false, ...extra };
}

let fake: FakePrisma;
const abertos: Client[] = [];
function rotasPadrao(role = 'agente', projectName = 'canais-mobile') {
  fake.on('GET', '/api/method/me', { json: identity(role, projectName) });
  fake.on('GET', `/api/projects/${PROJECT}/method/me`, { json: identity(role, projectName) });
  fake.on('GET', `/api/projects/${PROJECT}/method/rules`, { json: {
    identity: identity(role, projectName), project: { projectId: PROJECT, boardId: BOARD, projectName },
    allowedTransitions: { 'em-desenvolvimento': ['em-revisao', 'bloqueada'], backlog: ['bloqueada'] },
    conventions: ['Use branch task/T-XXXX-slug.'], requiresInteractiveAuthorization: true, requiresInteractiveResumeApproval: true } });
  fake.on('GET', `/api/projects/${PROJECT}/method/tasks`, { json: [task('T-0001', 'em-revisao'), task('T-0002', 'backlog')] });
  fake.on('GET', `/api/projects/${PROJECT}/method/tasks/next`, { json: [task('T-0003', 'em-desenvolvimento')] });
  fake.on('GET', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}$`), r => ({ json: task(r.path.split('/').pop()!, 'em-revisao') }));
  fake.on('POST', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}/move$`), r => ({ json: task(r.path.split('/')[6], JSON.parse(r.body).newStatus) }));
  fake.on('POST', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}/comments$`), r => ({ json: { id: '00000000-0000-4000-8000-0000000000c1', workItemId: task(r.path.split('/')[6], 'x').workItemId, text: JSON.parse(r.body).text, author: 'Agente de teste', createdAtUtc: new Date().toISOString() } }));
  fake.on('POST', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}/pull-requests$`), r => ({ json: { id: '00000000-0000-4000-8000-0000000000d1', integrationId: '00000000-0000-4000-8000-0000000000e1', workItemId: task(r.path.split('/')[6], 'x').workItemId, observationId: null, branchName: null, source: 'explicit', url: JSON.parse(r.body).url, createdByPrincipalId: identity().principalId, createdByUserId: null, createdAtUtc: new Date().toISOString() } }));
  fake.on('POST', `/api/projects/${PROJECT}/method/tasks`, r => ({ status: 201, json: task(JSON.parse(r.body).id, JSON.parse(r.body).status ?? 'backlog') }));
}

async function conectar(env: Record<string, string | undefined>) {
  const base: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined && !k.startsWith('PRISMA_')) base[k] = v;
  for (const [k, v] of Object.entries(env)) if (v !== undefined) base[k] = v;
  const transport = new StdioClientTransport({ command: process.execPath, args: ['--import', 'tsx', 'src/server.ts'], cwd: ROOT, env: base, stderr: 'pipe' });
  let stderr = '';
  transport.stderr?.on('data', (d: Buffer) => { stderr += d.toString(); });
  const client = new Client({ name: 'prisma-mcp-token-test', version: '0.0.0' });
  try { await client.connect(transport); } catch (e) { await transport.close().catch(() => {}); return { client: null, stderr: () => stderr, erroConexao: e as Error }; }
  abertos.push(client);
  const chamar = async (name: string, args: Record<string, unknown> = {}) => {
    const r = await client.callTool({ name, arguments: args }) as { isError?: boolean; content: { text?: string }[]; structuredContent?: Record<string, any> };
    return { erro: r.isError === true, texto: r.content.map(c => c.text ?? '').join('\n'), dados: r.structuredContent ?? {} };
  };
  return { client, chamar, stderr: () => stderr, erroConexao: null };
}
const envToken = (extra: Record<string, string | undefined> = {}) => ({ PRISMA_URL: fake.url, PRISMA_TOKEN: TOKEN, PRISMA_PROJECT: 'canais-mobile', ...extra });

describe('MCP com token opaco do método (fase 2a)', () => {
  before(async () => { fake = await startFakePrisma(); });
  after(async () => { await fake.close(); });
  beforeEach(() => { fake.requisicoes.length = 0; });
  afterEach(async () => { await Promise.all(abertos.splice(0).map(c => c.close().catch(() => {}))); });

  test('sem PRISMA_TOKEN, e-mail e senha não servem: nenhuma tentativa de login', async () => {
    rotasPadrao();
    const s = await conectar({ PRISMA_URL: fake.url, PRISMA_PROJECT: 'canais-mobile', PRISMA_EMAIL: 'a@b.c', PRISMA_PASSWORD: 'SenhaQueNaoPodeVazar1' });
    if (s.client) {
      const r = await s.chamar('listar_tasks');
      assert.equal(r.erro, true, 'sem token o adaptador não opera');
      assert.ok(!r.texto.includes('SenhaQueNaoPodeVazar1'));
      await s.client.close();
    }
    assert.equal(fake.requisicoes.filter(x => x.path.startsWith('/api/auth')).length, 0, 'não chama /api/auth');
    assert.ok(!s.stderr().includes('SenhaQueNaoPodeVazar1'));
  });

  test('JWT de login em PRISMA_TOKEN é recusado sem login nem decodificação', async () => {
    rotasPadrao();
    const s = await conectar(envToken({ PRISMA_TOKEN: JWT }));
    if (s.client) {
      const r = await s.chamar('listar_tasks');
      assert.equal(r.erro, true);
      assert.ok(!r.texto.includes(JWT));
      await s.client.close();
    }
    assert.equal(fake.requisicoes.filter(x => x.path.startsWith('/api/auth')).length, 0);
    assert.equal(fake.escritas().length, 0);
  });

  test('expõe as 8 operações e usa o papel do servidor (sem PRISMA_ROLE)', async () => {
    rotasPadrao('agente');
    const s = await conectar(envToken());
    assert.ok(s.client, `conectou: ${s.erroConexao?.message ?? ''}`);
    const { tools } = await s.client!.listTools();
    assert.deepEqual(tools.map(t => t.name).sort(), [...OITO].sort());
    const r = await s.chamar!('regras_do_projeto');
    assert.equal(r.erro, false, r.texto);
    assert.match(r.texto, /agente/);
    assert.ok(fake.requisicoes.some(x => x.path === '/api/method/me'), 'consulta a identidade na API');
    assert.ok(fake.requisicoes.every(x => x.headers.authorization === `Bearer ${TOKEN}`), 'Bearer opaco em todas as chamadas');
    await s.client!.close();
  });

  test('PRISMA_ROLE divergente do servidor falha sem mutação', async () => {
    rotasPadrao('agente');
    const s = await conectar(envToken({ PRISMA_ROLE: 'humano' }));
    if (s.client) {
      const r = await s.chamar('mover_status', { id: 'T-0001', novo_status: 'bloqueada', motivo: 'x' });
      assert.equal(r.erro, true, 'papel local divergente não opera');
      await s.client.close();
    }
    assert.equal(fake.escritas().length, 0);
  });

  test('projeto configurado diferente do escopo do token falha antes de mutar', async () => {
    rotasPadrao('agente', 'outro-projeto');
    const s = await conectar(envToken());
    if (s.client) {
      const r = await s.chamar('mover_status', { id: 'T-0001', novo_status: 'bloqueada', motivo: 'x' });
      assert.equal(r.erro, true);
      await s.client.close();
    }
    assert.equal(fake.escritas().length, 0);
  });

  test('leituras não escrevem nada no Prisma (sem provisionar estrutura)', async () => {
    rotasPadrao();
    const s = await conectar(envToken());
    for (const [nome, args] of [['regras_do_projeto', {}], ['listar_tasks', {}], ['listar_tasks', { status: 'backlog' }], ['proximas_tasks', {}], ['obter_task', { id: 'T-0001' }]] as const) {
      const r = await s.chamar!(nome, args);
      assert.equal(r.erro, false, `${nome}: ${r.texto}`);
    }
    assert.deepEqual(fake.escritas().map(x => `${x.method} ${x.path}`), []);
    await s.client!.close();
  });

  test('mover, comentar, vincular e criar usam as rotas do método com o contrato do F0', async () => {
    rotasPadrao();
    const s = await conectar(envToken());
    assert.equal((await s.chamar!('mover_status', { id: 'T-0003', novo_status: 'bloqueada', motivo: 'aguardando' })).erro, false);
    assert.equal((await s.chamar!('comentar', { id: 'T-0003', texto: 'relatório' })).erro, false);
    assert.equal((await s.chamar!('vincular_pr', { id: 'T-0003', url: 'https://github.com/org/repo/pull/7' })).erro, false);
    assert.equal((await s.chamar!('criar_task', { id: 'T-0009', titulo: 'Nova', hu: 'HU-0001', arquivo: 'docs/trabalho/x.md', cobre: ['CA-1'], risco: 'alto' })).erro, false);
    const w = fake.escritas();
    const move = w.find(x => x.path.endsWith('/tasks/T-0003/move'));
    assert.ok(move, 'POST .../tasks/T-0003/move');
    assert.deepEqual(JSON.parse(move!.body), { newStatus: 'bloqueada', reason: 'aguardando' });
    assert.deepEqual(JSON.parse(w.find(x => x.path.endsWith('/tasks/T-0003/comments'))!.body), { text: 'relatório' });
    assert.equal(JSON.parse(w.find(x => x.path.endsWith('/tasks/T-0003/pull-requests'))!.body).url, 'https://github.com/org/repo/pull/7');
    const criar = JSON.parse(w.find(x => x.path === `/api/projects/${PROJECT}/method/tasks`)!.body);
    assert.equal(criar.id, 'T-0009'); assert.equal(criar.title, 'Nova'); assert.equal(criar.hu, 'HU-0001');
    assert.equal(criar.filePath, 'docs/trabalho/x.md'); assert.deepEqual(criar.covers, ['CA-1']); assert.equal(criar.risk, 'alto');
    assert.ok(!w.some(x => /\/api\/WorkItems|\/api\/Stages|\/api\/tags|custom-fields/i.test(x.path)), 'não usa rotas genéricas para mutar');
    await s.client!.close();
  });

  test('401 manda parar e avisar, com uma única chamada e sem login de fallback', async () => {
    rotasPadrao();
    const s = await conectar(envToken());
    fake.requisicoes.length = 0;
    fake.on('GET', `/api/projects/${PROJECT}/method/tasks`, { status: 401, json: { title: 'Token revogado.' } });
    const r = await s.chamar!('listar_tasks');
    assert.equal(r.erro, true);
    assert.match(r.texto, /pare e avise/i);
    assert.equal(fake.requisicoes.filter(x => x.path === `/api/projects/${PROJECT}/method/tasks`).length, 1, 'sem nova tentativa');
    assert.equal(fake.requisicoes.filter(x => x.path.startsWith('/api/auth')).length, 0, 'sem login/refresh');
    await s.client!.close();
  });

  test('403 da API é respeitado mesmo quando a regra local permitiria (guard local só redundante)', async () => {
    rotasPadrao();
    fake.on('POST', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-0003/move$`), { status: 403, json: { title: 'Transição recusada pela matriz do servidor.' } });
    const s = await conectar(envToken());
    const r = await s.chamar!('mover_status', { id: 'T-0003', novo_status: 'em-revisao', motivo: 'nível 0' });
    assert.equal(r.erro, true);
    assert.match(r.texto, /recusad|pare e avise/i);
    await s.client!.close();
  });

  test('erro da API que ecoa o token não vaza o token', async () => {
    rotasPadrao();
    fake.on('GET', `/api/projects/${PROJECT}/method/tasks`, { status: 400, json: { title: `Cabeçalho inválido: Bearer ${TOKEN}`, detail: TOKEN } });
    const s = await conectar(envToken());
    const r = await s.chamar!('listar_tasks');
    assert.equal(r.erro, true);
    assert.ok(!r.texto.includes(TOKEN) && !JSON.stringify(r.dados).includes(TOKEN), 'token mascarado');
    await s.client!.close();
    assert.ok(!s.stderr().includes(TOKEN));
  });
});
