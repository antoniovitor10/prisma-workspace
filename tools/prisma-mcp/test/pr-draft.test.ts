// HU-0007 · T-0026 (CA-2): preparar_pr grava o rascunho do PR pelo PUT e obter_task mostra a linha do rascunho.
// Caixa-preta: sobe o servidor MCP por stdio contra um Prisma falso e confere as requisições.
import { after, afterEach, before, beforeEach, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { linhaRascunhoPr, validatePrDraft } from '../src/pr-draft.js';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const PROJECT = '11111111-2222-4333-8444-555555555555';
const BOARD = '66666666-7777-4888-8999-aaaaaaaaaaaa';
const TOKEN = `pmt_${'B'.repeat(43)}`;
const DRAFT = new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}/pull-request-draft$`);

function identity() {
  return {
    authenticationKind: 'methodToken', role: 'agente', principalId: '99999999-0000-4000-8000-000000000001',
    userId: 'method:99999999-0000-4000-8000-000000000001', displayName: 'Agente de teste',
    organizationId: '00000000-0000-4000-8000-000000000001', projectId: PROJECT, projectName: 'canais-mobile',
    boardId: BOARD, boardName: 'canais-mobile', tokenId: '99999999-0000-4000-8000-0000000000aa',
    expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(), capabilities: ['read', 'comment', 'move'],
  };
}
function task(id: string) {
  return { workItemId: `00000000-0000-4000-8000-${id.slice(2).padStart(12, '0')}`, id, title: `Task ${id}`, status: 'em-desenvolvimento', hu: 'HU-0007',
    filePath: `docs/trabalho/HU-0007/tasks/${id}.md`, covers: ['CA-2'], risk: 'alto', pr: null, dependsOn: [], authorized: true, canResume: false, gitLinks: [] };
}

let fake: FakePrisma;
const abertos: Client[] = [];
function rotas() {
  fake.on('GET', '/api/method/me', { json: identity() });
  fake.on('GET', `/api/projects/${PROJECT}/method/rules`, { json: {
    identity: identity(), project: { projectId: PROJECT, boardId: BOARD, projectName: 'canais-mobile' },
    allowedTransitions: { 'em-desenvolvimento': ['em-revisao', 'bloqueada'] }, conventions: [],
    requiresInteractiveAuthorization: true, requiresInteractiveResumeApproval: true } });
  fake.on('GET', new RegExp(`^/api/projects/${PROJECT}/method/tasks/T-\\d{4}$`), r => ({ json: task(r.path.split('/').pop()!) }));
}
async function conectar() {
  const env: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined && !k.startsWith('PRISMA_')) env[k] = v;
  Object.assign(env, { PRISMA_URL: fake.url, PRISMA_TOKEN: TOKEN, PRISMA_PROJECT: 'canais-mobile' });
  const transport = new StdioClientTransport({ command: process.execPath, args: ['--import', 'tsx', 'src/server.ts'], cwd: ROOT, env, stderr: 'pipe' });
  let stderr = '';
  transport.stderr?.on('data', (d: Buffer) => { stderr += d.toString(); });
  const client = new Client({ name: 'prisma-mcp-pr-draft-test', version: '0.0.0' });
  await client.connect(transport);
  abertos.push(client);
  const chamar = async (name: string, args: Record<string, unknown> = {}) => {
    const r = await client.callTool({ name, arguments: args }) as { isError?: boolean; content: { text?: string }[]; structuredContent?: Record<string, any> };
    return { erro: r.isError === true, texto: r.content.map(c => c.text ?? '').join('\n'), dados: r.structuredContent ?? {} };
  };
  return { chamar, stderr: () => stderr };
}
const puts = () => fake.requisicoes.filter(r => r.method === 'PUT' && DRAFT.test(r.path));

describe('preparar_pr e rascunho no obter_task (HU-0007)', () => {
  before(async () => { fake = await startFakePrisma(); });
  after(async () => { await fake.close(); });
  beforeEach(() => { fake.requisicoes.length = 0; });
  afterEach(async () => { await Promise.all(abertos.splice(0).map(c => c.close().catch(() => {}))); });

  test('preparar_pr chama o PUT com título e corpo e responde curto', async () => {
    rotas();
    fake.on('PUT', DRAFT, r => ({ json: { ...JSON.parse(r.body), updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente de teste', changed: true } }));
    const s = await conectar();
    const r = await s.chamar('preparar_pr', { id: 'T-0026', titulo: '  Corrige consulta  ', corpo: '## Resumo\nTexto do relatório.' });
    assert.equal(r.erro, false, r.texto);
    assert.match(r.texto, /^Rascunho do PR gravado na T-0026\./);
    const put = puts();
    assert.equal(put.length, 1);
    assert.equal(put[0].path, `/api/projects/${PROJECT}/method/tasks/T-0026/pull-request-draft`);
    assert.deepEqual(JSON.parse(put[0].body), { title: 'Corrige consulta', body: '## Resumo\nTexto do relatório.' });
    assert.equal(put[0].headers.authorization, `Bearer ${TOKEN}`);
    assert.equal(r.dados.rascunho.mudou, true);
  });

  test('texto igual: a API diz changed=false e o MCP diz "sem mudança"', async () => {
    rotas();
    fake.on('PUT', DRAFT, r => ({ json: { ...JSON.parse(r.body), updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente de teste', changed: false } }));
    const s = await conectar();
    const r = await s.chamar('preparar_pr', { id: 'T-0026', titulo: 'Corrige', corpo: 'Corpo' });
    assert.equal(r.erro, false, r.texto);
    assert.match(r.texto, /sem mudança na T-0026/);
  });

  test('título vazio, título de 257 e corpo de 60.001 são recusados antes de chamar a API', async () => {
    rotas();
    fake.on('PUT', DRAFT, { json: {} });
    const s = await conectar();
    for (const args of [{ titulo: '   ', corpo: 'x' }, { titulo: 't'.repeat(257), corpo: 'x' }, { titulo: 'Título', corpo: 'c'.repeat(60_001) }]) {
      const r = await s.chamar('preparar_pr', { id: 'T-0026', ...args });
      assert.equal(r.erro, true, `deveria recusar: ${JSON.stringify(args).slice(0, 60)}`);
    }
    assert.equal(puts().length, 0, 'nenhum PUT');
    assert.throws(() => validatePrDraft('', 'x'));
    assert.throws(() => validatePrDraft('t'.repeat(257), 'x'));
    assert.throws(() => validatePrDraft('ok', 'c'.repeat(60_001)));
    assert.deepEqual(validatePrDraft(' ok ', 'c'.repeat(60_000)).titulo, 'ok');
  });

  test('erro 401/403 da API não carrega o token', async () => {
    rotas();
    fake.on('PUT', DRAFT, { status: 403, json: { title: `Recusado para Bearer ${TOKEN} (${TOKEN})` } });
    const s = await conectar();
    const r = await s.chamar('preparar_pr', { id: 'T-0026', titulo: 'Corrige', corpo: 'Corpo' });
    assert.equal(r.erro, true);
    assert.ok(!r.texto.includes(TOKEN), r.texto);
    assert.match(r.texto, /Pare e avise/);
    fake.on('PUT', DRAFT, { status: 401, json: { title: TOKEN } });
    const r401 = await s.chamar('preparar_pr', { id: 'T-0026', titulo: 'Corrige', corpo: 'Corpo' });
    assert.equal(r401.erro, true);
    assert.ok(!r401.texto.includes(TOKEN), r401.texto);
    assert.ok(!s.stderr().includes(TOKEN));
  });

  test('obter_task mostra "Rascunho de PR: gravado em … por …" sem o corpo, e "nenhum" no 404', async () => {
    rotas();
    fake.on('GET', DRAFT, { json: { title: 'Corrige', body: 'CORPO-QUE-NAO-APARECE', updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente de teste' } });
    const s = await conectar();
    const r = await s.chamar('obter_task', { id: 'T-0026' });
    assert.equal(r.erro, false, r.texto);
    assert.match(r.texto, /Rascunho de PR: gravado em 2026-11-27T12:00:00Z por Agente de teste/);
    assert.ok(!r.texto.includes('CORPO-QUE-NAO-APARECE'), 'o corpo não é despejado');
    fake.on('GET', DRAFT, { status: 404, json: { title: 'A task ainda não tem rascunho de PR.', code: 'pr_draft_not_found' } });
    const vazio = await s.chamar('obter_task', { id: 'T-0026' });
    assert.equal(vazio.erro, false, vazio.texto);
    assert.match(vazio.texto, /Rascunho de PR: nenhum/);
  });

  test('linha do rascunho', () => {
    assert.equal(linhaRascunhoPr(null), 'Rascunho de PR: nenhum');
    assert.equal(linhaRascunhoPr({ updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Ana' }), 'Rascunho de PR: gravado em 2026-11-27T12:00:00Z por Ana');
  });
});
