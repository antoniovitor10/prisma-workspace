// Fase 2a: a importação usa o mesmo token opaco e a API como autoridade. Contra um Prisma falso:
// valida o lote antes de escrever, não cria estrutura, não concede autorização e é idempotente.
import { after, before, beforeEach, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PrismaApi } from '../src/api.js';
import { readConfig } from '../src/config.js';
import { importMetodo } from '../src/import.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';

const FIXTURE = join(dirname(fileURLToPath(import.meta.url)), 'fixtures', 'trabalho');
const PROJECT = '11111111-2222-4333-8444-555555555555';
const BOARD = '66666666-7777-4888-8999-aaaaaaaaaaaa';
const TOKEN = `pmt_${'B'.repeat(43)}`;
const STATUSES = ['backlog', 'pronta', 'em-desenvolvimento', 'em-revisao', 'em-teste', 'concluida', 'bloqueada'];

let fake: FakePrisma;
let tasks: Map<string, any>;
let links: Map<string, { id: string; type: number; isIncoming: boolean; relatedWorkItemId: string; relatedTitle: string }[]>;
const wid = (id: string) => `00000000-0000-4000-8000-${id.slice(2).padStart(12, '0')}`;

function servidor(role: 'agente' | 'humano') {
  const identity = { authenticationKind: 'methodToken', role, principalId: 'p-1', userId: 'method:p-1', displayName: 'Token',
    organizationId: 'org-1', projectId: PROJECT, projectName: 'metodo-teste', boardId: BOARD, boardName: 'metodo-teste',
    tokenId: 't-1', expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(), capabilities: ['read'] };
  fake.on('GET', '/api/method/me', { json: identity });
  fake.on('GET', `/api/projects/${PROJECT}/method/rules`, { json: { identity,
    project: { organizationId: 'org-1', projectId: PROJECT, projectName: 'metodo-teste', boardId: BOARD, boardName: 'metodo-teste',
      methodIdFieldId: 'f-id', huFieldId: 'f-hu', prFieldId: 'f-pr', enabled: true, gitPhase: 1, autonomyLevel: 0, policyVersion: 1,
      stageMappings: Object.fromEntries(STATUSES.map(s => [s, `stage-${s}`])),
      fields: { 'ID do metodo': 'f-id', HU: 'f-hu', Arquivo: 'f-arq', Cobre: 'f-cob', Risco: 'f-ris', PR: 'f-pr' } },
    allowedTransitions: role === 'agente'
      ? { 'em-desenvolvimento': ['em-revisao', 'bloqueada'], backlog: ['bloqueada'], bloqueada: ['em-desenvolvimento'] }
      : Object.fromEntries(STATUSES.map(s => [s, STATUSES.filter(t => t !== s)])),
    conventions: [], requiresInteractiveAuthorization: true, requiresInteractiveResumeApproval: true } });
  fake.on('GET', `/api/projects/${PROJECT}/method/tasks`, () => ({ json: [...tasks.values()] }));
  fake.on('POST', `/api/projects/${PROJECT}/method/tasks`, r => {
    const b = JSON.parse(r.body);
    const t = { workItemId: wid(b.id), id: b.id, title: `${b.id} · ${b.title}`, status: b.status, hu: b.hu, filePath: b.filePath,
      covers: b.covers, risk: b.risk, pr: null, dependsOn: [], authorized: false, canResume: false };
    tasks.set(b.id, t);
    return { status: 201, json: t };
  });
  fake.on('PUT', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}$`), r => {
    const id = r.path.split('/').pop()!; const b = JSON.parse(r.body); const t = { ...tasks.get(id), ...b, title: `${id} · ${b.title}` };
    tasks.set(id, t); return { json: t };
  });
  fake.on('GET', /^\/api\/WorkItems\/[0-9a-f-]+$/, r => ({ json: { links: links.get(r.path.split('/')[3]) ?? [], subtasks: [] } }));
  fake.on('POST', /^\/api\/WorkItems\/[0-9a-f-]+\/links$/, r => {
    const de = r.path.split('/')[3]; const b = JSON.parse(r.body);
    links.set(de, [...(links.get(de) ?? []), { id: `l-${links.size}`, type: b.type, isIncoming: false, relatedWorkItemId: b.targetWorkItemId, relatedTitle: '' }]);
    return { status: 201, json: `l-${links.size}` };
  });
}

const api = () => new PrismaApi(readConfig({ PRISMA_URL: fake.url, PRISMA_PROJECT: 'metodo-teste', PRISMA_TOKEN: TOKEN }));
const estruturais = () => fake.escritas().filter(r => !/\/method\/tasks(\/T-\d{4})?$/.test(r.path) && !/\/api\/WorkItems\/[0-9a-f-]+\/links$/.test(r.path));

async function copiaComStatus(trocas: [string, string][]) {
  const dir = await mkdtemp(join(tmpdir(), 'prisma-mcp-import-'));
  await cp(FIXTURE, dir, { recursive: true });
  for (const [arquivo, status] of trocas) {
    const f = join(dir, 'HU-9001-exemplo-teste', 'tasks', arquivo);
    await writeFile(f, (await readFile(f, 'utf8')).replace(/^status: .*$/m, `status: ${status}`));
  }
  return dir;
}

describe('importação com token opaco (fase 2a)', () => {
  before(async () => { fake = await startFakePrisma(); });
  after(async () => { await fake.close(); });
  beforeEach(() => { fake.requisicoes.length = 0; tasks = new Map(); links = new Map(); });

  test('dry-run não faz nenhuma requisição', async () => {
    servidor('agente');
    const r = await importMetodo(FIXTURE, { dryRun: true }, api());
    assert.equal(r.planejados, 3);
    assert.equal(fake.requisicoes.length, 0);
  });

  test('agente: lote com task autorizada no frontmatter é recusado antes de qualquer escrita', async () => {
    servidor('agente');
    // A fixture tem T-9002 em em-desenvolvimento.
    await assert.rejects(importMetodo(FIXTURE, {}, api()), /autoriza|Pare e avise/i);
    assert.deepEqual(fake.escritas().map(r => `${r.method} ${r.path}`), []);
  });

  test('agente: só backlog/concluida, cria pelas rotas do método e não provisiona estrutura', async () => {
    servidor('agente');
    const dir = await copiaComStatus([['T-9002-em-andamento.md', 'backlog']]);
    try {
      const r = await importMetodo(dir, {}, api());
      assert.equal(r.criados, 3);
      assert.equal(r.vinculosCriados, 2);
      assert.deepEqual(estruturais().map(x => `${x.method} ${x.path}`), [], 'nenhuma escrita fora de tasks/links');
      const criados = fake.escritas().filter(x => x.method === 'POST' && x.path.endsWith('/method/tasks')).map(x => JSON.parse(x.body));
      assert.deepEqual(criados.map(c => [c.id, c.status]).sort(), [['T-9001', 'concluida'], ['T-9002', 'backlog'], ['T-9003', 'backlog']]);
      for (const c of criados) { assert.equal(c.hu, 'HU-9001'); assert.match(c.filePath, /^docs\/trabalho\/HU-9001-exemplo-teste\/tasks\/T-900\d-/); }
    } finally { await rm(dir, { recursive: true, force: true }); }
  });

  test('segunda rodada não cria nem atualiza nada', async () => {
    servidor('agente');
    const dir = await copiaComStatus([['T-9002-em-andamento.md', 'backlog']]);
    try {
      await importMetodo(dir, {}, api());
      fake.requisicoes.length = 0;
      const r = await importMetodo(dir, {}, api());
      assert.equal(r.criados, 0); assert.equal(r.atualizados, 0); assert.equal(r.semMudanca, 3);
      assert.deepEqual(fake.escritas().filter(x => !x.path.endsWith('/links')).map(x => `${x.method} ${x.path}`), []);
    } finally { await rm(dir, { recursive: true, force: true }); }
  });

  test('agente: --sincronizar-status para em-desenvolvimento é recusado antes de mover', async () => {
    servidor('agente');
    const base = await copiaComStatus([['T-9002-em-andamento.md', 'backlog']]);
    try {
      await importMetodo(base, {}, api());
      const autorizar = await copiaComStatus([['T-9002-em-andamento.md', 'em-desenvolvimento']]);
      fake.requisicoes.length = 0;
      await assert.rejects(importMetodo(autorizar, { sincronizarStatus: true }, api()), /Pare e avise|recusad/i);
      assert.equal(fake.escritas().filter(x => x.path.endsWith('/move')).length, 0);
      await rm(autorizar, { recursive: true, force: true });
    } finally { await rm(base, { recursive: true, force: true }); }
  });

  test('token humano também não cria task já autorizada (em-desenvolvimento)', async () => {
    servidor('humano');
    await assert.rejects(importMetodo(FIXTURE, {}, api()), /autoriza|Pare e avise/i);
    assert.deepEqual(fake.escritas().map(r => `${r.method} ${r.path}`), []);
  });
});
