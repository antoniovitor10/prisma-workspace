// Integração contra um Prisma real, num projeto próprio criado para o teste.
// Só roda com PRISMA_IT=1, PRISMA_URL e PRISMA_EMAIL/PRISMA_PASSWORD (ou PRISMA_TOKEN). Ver README.
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
import { PrismaApi } from '../src/api.js';
import { readConfig } from '../src/config.js';
import { FIELD_NAMES } from '../src/mapping.js';
import { ensureProjeto } from '../src/setup.js';

const run = promisify(execFile);
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const FIXTURE = join(ROOT, 'test', 'fixtures', 'trabalho');
const ATIVO = process.env.PRISMA_IT === '1';
const PROJETO = process.env.PRISMA_IT_PROJECT
  ?? `metodo-mcp-teste-${new Date().toISOString().replace(/\D/g, '').slice(0, 14)}`;
const OITO = ['regras_do_projeto', 'obter_task', 'listar_tasks', 'proximas_tasks',
  'criar_task', 'mover_status', 'comentar', 'vincular_pr'];

function envPara(papel: 'agente' | 'humano'): Record<string, string> {
  const env: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined) env[k] = v;
  return { ...env, PRISMA_PROJECT: PROJETO, PRISMA_ROLE: papel };
}
const SEGREDOS = [process.env.PRISMA_PASSWORD, process.env.PRISMA_TOKEN].filter((x): x is string => !!x);
function semSegredo(texto: string, onde: string) {
  for (const s of SEGREDOS) assert.ok(!texto.includes(s), `segredo vazou em ${onde}`);
  assert.doesNotMatch(texto, /eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/, `JWT vazou em ${onde}`);
}

async function importar(...flags: string[]) {
  return importarDe(FIXTURE, 'humano', ...flags);
}
async function importarDe(pasta: string, papel: 'agente' | 'humano', ...flags: string[]) {
  const { stdout, stderr } = await run(process.execPath, ['--import', 'tsx', 'src/import.ts', pasta, ...flags],
    { cwd: ROOT, env: envPara(papel), timeout: 120_000 });
  semSegredo(stdout + stderr, 'saída do import');
  const n = (rotulo: string) => Number(new RegExp(`${rotulo}:\\s*(\\d+)`, 'i').exec(stdout)?.[1] ?? NaN);
  return { stdout, criados: n('Criados'), atualizados: n('atualizados'), semMudanca: n('sem mudança'), vinculos: n('vínculos criados') };
}

async function conectar(papel: 'agente' | 'humano') {
  const transport = new StdioClientTransport({
    command: process.execPath, args: ['--import', 'tsx', 'src/server.ts'], cwd: ROOT, env: envPara(papel), stderr: 'pipe',
  });
  let stderr = '';
  transport.stderr?.on('data', (d: Buffer) => { stderr += d.toString(); });
  const client = new Client({ name: 'prisma-mcp-teste', version: '0.0.0' });
  await client.connect(transport);
  const chamar = async (name: string, args: Record<string, unknown> = {}) => {
    const r = await client.callTool({ name, arguments: args }) as {
      isError?: boolean; content: { type: string; text?: string }[]; structuredContent?: Record<string, any>;
    };
    const texto = r.content.map(c => c.text ?? '').join('\n');
    semSegredo(texto + JSON.stringify(r.structuredContent ?? {}), `${name} (${papel})`);
    return { erro: r.isError === true, texto, dados: r.structuredContent ?? {} };
  };
  const ok = async (name: string, args: Record<string, unknown> = {}) => {
    const r = await chamar(name, args);
    assert.equal(r.erro, false, `${name} ${JSON.stringify(args)} falhou: ${r.texto}`);
    return r;
  };
  const recusa = async (name: string, args: Record<string, unknown>, padrao?: RegExp) => {
    const r = await chamar(name, args);
    assert.equal(r.erro, true, `${name} ${JSON.stringify(args)} deveria ser recusado; veio: ${r.texto}`);
    if (padrao) assert.match(r.texto, padrao);
    return r;
  };
  return { client, chamar, ok, recusa, stderr: () => stderr };
}
type Sessao = Awaited<ReturnType<typeof conectar>>;

// GET /api/WorkItems/board/{id} direto, sem o PrismaApi: é o que o Kanban usa (só itens de topo).
interface ItemQuadro { id: string; title: string; parentId: string | null; stageId: string;
  customFields: { fieldId: string; value: string | null }[]; tags?: { name: string }[] }
async function quadroCru(boardId: string): Promise<ItemQuadro[]> {
  const url = process.env.PRISMA_URL!.replace(/\/+$/, '');
  let token = process.env.PRISMA_TOKEN;
  if (!token) {
    const r = await fetch(`${url}/api/auth/login`, { method: 'POST', headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ email: process.env.PRISMA_EMAIL, password: process.env.PRISMA_PASSWORD }) });
    token = (await r.json() as { accessToken: string }).accessToken;
  }
  const h: Record<string, string> = { Authorization: `Bearer ${token}`, Accept: 'application/json' };
  const orgs = await (await fetch(`${url}/api/organizations`, { headers: h })).json() as { id: string; isActive?: boolean }[];
  const ativas = orgs.filter(o => o.isActive !== false);
  if (ativas.length) h['X-Organization-Id'] = ativas[0].id;
  const r = await fetch(`${url}/api/WorkItems/board/${boardId}`, { headers: h });
  assert.equal(r.status, 200, 'GET do quadro');
  return await r.json() as ItemQuadro[];
}
async function conferirQuadro(esperado: Record<string, string>) {
  const setup = await ensureProjeto(api0());
  const campoId = setup.fields.get(FIELD_NAMES.id)!;
  const campoHu = setup.fields.get('HU');
  assert.ok(campoHu, 'campo personalizado HU');
  const itens = await quadroCru(setup.boardId);
  const porChave = new Map(itens.map(i => [i.customFields.find(f => f.fieldId === campoId)?.value, i]));
  assert.ok(!porChave.has('HU-9001'), 'a HU não vira work item');
  assert.deepEqual([...porChave.keys()].filter(Boolean).sort(), Object.keys(esperado).sort());
  for (const [chave, status] of Object.entries(esperado)) {
    const i = porChave.get(chave)!;
    assert.equal(i.parentId ?? null, null, `${chave} é item de topo`);
    assert.equal(i.stageId, setup.stages.get(status as never), `${chave} na coluna ${status}`);
    assert.equal(i.customFields.find(f => f.fieldId === campoHu)?.value, 'HU-9001', `${chave} campo HU`);
    assert.ok((i.tags ?? []).some(t => t.name === 'HU-9001'), `${chave} com a tag HU-9001`);
  }
}
let apiGlobal: PrismaApi | undefined;
const api0 = () => apiGlobal ??= new PrismaApi(readConfig(envPara('humano')));
const statusDe = async (s: Sessao, id: string) => (await s.ok('obter_task', { id })).dados.task.status as string;

describe('integração com o Prisma', { skip: !ATIVO && 'defina PRISMA_IT=1 (ver README)' }, () => {
  let api: PrismaApi;
  let agente: Sessao;
  let humano: Sessao;

  before(async () => {
    api = new PrismaApi(readConfig(envPara('humano')));
    process.stderr.write(`# projeto de teste: ${PROJETO}\n`);
  });
  after(async () => {
    await agente?.client.close();
    await humano?.client.close();
    if (agente) semSegredo(agente.stderr(), 'stderr do servidor');
  });

  test('import cria HU e tasks; segunda rodada não duplica nada', async () => {
    const um = await importar();
    assert.equal(um.criados, 3, um.stdout);
    assert.equal(um.vinculos, 2, um.stdout);
    const dois = await importar();
    assert.equal(dois.criados, 0, dois.stdout);
    assert.equal(dois.atualizados, 0, dois.stdout);
    assert.equal(dois.semMudanca, 3, dois.stdout);
    assert.equal(dois.vinculos, 0, dois.stdout);

    const setup = await ensureProjeto(api);
    assert.equal(setup.stages.size, 7);
    await conferirQuadro({ 'T-9001': 'concluida', 'T-9002': 'em-desenvolvimento', 'T-9003': 'backlog' });
    const t2 = (await api.listWorkItemsByBoard(setup.boardId)).find(i => i.title.startsWith('T-9002'))!;
    const det = await api.getWorkItem(t2.id);
    assert.equal(det.links.filter(l => !l.isIncoming && l.type === 1).length, 1, 'T-9002 depende de T-9001');
  });

  test('ensureProjeto é idempotente', async () => {
    const a = await ensureProjeto(api);
    const b = await ensureProjeto(api);
    assert.equal(a.projectId, b.projectId);
    assert.equal(a.boardId, b.boardId);
    assert.deepEqual([...a.stages], [...b.stages]);
    assert.deepEqual([...a.fields], [...b.fields]);
    const nomes = (await api.listStages(a.boardId)).sort((x, y) => x.position - y.position).map(s => s.name);
    assert.deepEqual(nomes, ['Backlog', 'Pronta', 'Em desenvolvimento', 'Em revisão', 'Em teste', 'Concluída', 'Bloqueada']);
  });

  test('servidor stdio expõe exatamente as 8 operações', async () => {
    agente = await conectar('agente');
    humano = await conectar('humano');
    const { tools } = await agente.client.listTools();
    assert.deepEqual(tools.map(t => t.name).sort(), [...OITO].sort());
  });

  test('regras_do_projeto: papel agente e matriz sem autorização', async () => {
    const r = await agente.ok('regras_do_projeto');
    assert.equal(r.dados.papel, 'agente');
    assert.ok(!r.dados.transicoes_permitidas.backlog.includes('em-desenvolvimento'));
    assert.ok(!r.dados.transicoes_permitidas.pronta.includes('em-desenvolvimento'));
    assert.match(r.texto, /pare e avise/i);
    assert.equal((await humano.ok('regras_do_projeto')).dados.papel, 'humano');
  });

  test('listar_tasks, filtro por status, obter_task e proximas_tasks', async () => {
    const todas = await agente.ok('listar_tasks');
    assert.deepEqual(todas.dados.tasks.map((t: any) => t.id).sort(), ['T-9001', 'T-9002', 'T-9003']);
    assert.match(todas.texto, /T-9002/);
    const backlog = await agente.ok('listar_tasks', { status: 'backlog' });
    assert.deepEqual(backlog.dados.tasks.map((t: any) => t.id), ['T-9003']);
    const t2 = (await agente.ok('obter_task', { id: 'T-9002' })).dados.task;
    assert.equal(t2.status, 'em-desenvolvimento');
    assert.equal(t2.titulo, 'Em andamento com dependência concluída');
    assert.equal(t2.hu, 'HU-9001');
    assert.equal(t2.arquivo, 'docs/trabalho/HU-9001-exemplo-teste/tasks/T-9002-em-andamento.md');
    const prox = await agente.ok('proximas_tasks');
    assert.deepEqual(prox.dados.tasks.map((t: any) => t.id), ['T-9002']);
    await agente.recusa('obter_task', { id: 'T-0999' });
  });

  test('agente não autoriza: backlog -> em-desenvolvimento recusado e status intacto', async () => {
    await agente.recusa('mover_status', { id: 'T-9003', novo_status: 'em-desenvolvimento', motivo: 'teste' }, /autoriza/i);
    assert.equal(await statusDe(agente, 'T-9003'), 'backlog');
  });

  test('agente não se autoautoriza por backlog -> bloqueada -> em-desenvolvimento', async () => {
    await agente.ok('mover_status', { id: 'T-9003', novo_status: 'bloqueada', motivo: 'teste de bloqueio a partir do backlog' });
    assert.equal(await statusDe(agente, 'T-9003'), 'bloqueada');
    await agente.recusa('mover_status', { id: 'T-9003', novo_status: 'em-desenvolvimento', motivo: 'tentativa de atalho' });
    assert.equal(await statusDe(agente, 'T-9003'), 'bloqueada');
    await humano.ok('mover_status', { id: 'T-9003', novo_status: 'backlog', motivo: 'humano desfaz o teste' });
  });

  test('agente bloqueia e volta do próprio bloqueio; comentário [status] gravado', async () => {
    await agente.ok('mover_status', { id: 'T-9002', novo_status: 'bloqueada', motivo: 'aguardando credencial' });
    assert.equal(await statusDe(agente, 'T-9002'), 'bloqueada');
    await agente.ok('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'credencial recebida' });
    assert.equal(await statusDe(agente, 'T-9002'), 'em-desenvolvimento');
    const item = (await api.listWorkItemsByBoard((await ensureProjeto(api)).boardId))
      .find(i => i.title.startsWith('T-9002'))!;
    const textos = (await api.listComments(item.id)).map(c => c.text);
    assert.ok(textos.some(t => t.startsWith('[status] em-desenvolvimento -> bloqueada · agente · mcp · aguardando credencial')), textos.join('\n'));
    assert.ok(textos.some(t => t.startsWith('[status] bloqueada -> em-desenvolvimento · agente · mcp · credencial recebida')), textos.join('\n'));
  });

  test('agente não volta de bloqueio que não registrou', async () => {
    // Bloqueio feito direto pela API (UI), sem marcador do agente.
    const setup = await ensureProjeto(api);
    const item = (await api.listWorkItemsByBoard(setup.boardId)).find(i => i.title.startsWith('T-9002'))!;
    await api.moveWorkItem(item.id, setup.stages.get('bloqueada')!);
    await agente.recusa('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'tentativa' }, /bloqueio/i);
    assert.equal(await statusDe(agente, 'T-9002'), 'bloqueada');
    await humano.ok('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'humano libera' });
  });

  test('agente não volta de bloqueio que o humano registrou pelo MCP (mesmo usuário)', async () => {
    await humano.ok('mover_status', { id: 'T-9002', novo_status: 'bloqueada', motivo: 'humano bloqueia' });
    await agente.recusa('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'tentativa' }, /bloqueio/i);
    assert.equal(await statusDe(agente, 'T-9002'), 'bloqueada');
    await humano.ok('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'humano libera' });
  });

  test('bloqueio do agente desfeito pela UI não serve para uma volta posterior', async () => {
    const setup = await ensureProjeto(api);
    await agente.ok('mover_status', { id: 'T-9002', novo_status: 'bloqueada', motivo: 'bloqueio legítimo' });
    const item = (await api.listWorkItemsByBoard(setup.boardId)).find(i => i.title.startsWith('T-9002'))!;
    await api.moveWorkItem(item.id, setup.stages.get('backlog')!);
    await api.moveWorkItem(item.id, setup.stages.get('bloqueada')!);
    await agente.recusa('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'reuso do marcador' });
    await humano.ok('mover_status', { id: 'T-9002', novo_status: 'em-desenvolvimento', motivo: 'humano libera' });
  });

  test('agente move em-desenvolvimento -> em-revisao e não move o que o git prova', async () => {
    await agente.ok('mover_status', { id: 'T-9002', novo_status: 'em-revisao', motivo: 'nível 0, sem push' });
    await agente.recusa('mover_status', { id: 'T-9002', novo_status: 'em-teste', motivo: 'x' });
    await agente.recusa('mover_status', { id: 'T-9001', novo_status: 'bloqueada', motivo: 'x' });
    assert.equal(await statusDe(agente, 'T-9001'), 'concluida');
  });

  test('criar_task: agente cria em backlog, não cria já autorizada; não duplica id', async () => {
    const r = await agente.ok('criar_task', {
      id: 'T-9004', titulo: 'Criada pelo MCP', hu: 'HU-9001',
      arquivo: 'docs/trabalho/HU-9001-exemplo-teste/tasks/T-9004-criada-pelo-mcp.md', cobre: ['CA-1'], risco: 'normal',
    });
    assert.equal(r.dados.task.status, 'backlog');
    await agente.recusa('criar_task', {
      id: 'T-9005', titulo: 'Autorizada', hu: 'HU-9001', arquivo: 'x.md', cobre: ['CA-1'], risco: 'normal', status: 'em-desenvolvimento',
    });
    await agente.recusa('criar_task', {
      id: 'T-9004', titulo: 'Duplicada', hu: 'HU-9001', arquivo: 'x.md', cobre: ['CA-1'], risco: 'normal',
    });
    const ids = (await agente.ok('listar_tasks')).dados.tasks.map((t: any) => t.id).sort();
    assert.deepEqual(ids, ['T-9001', 'T-9002', 'T-9003', 'T-9004']);
    assert.equal((await agente.ok('obter_task', { id: 'T-9004' })).dados.task.hu, 'HU-9001');
    // mover_status anteriores e a task criada aparecem no GET do quadro (o que o Kanban mostra).
    await conferirQuadro({ 'T-9001': 'concluida', 'T-9002': 'em-revisao', 'T-9003': 'backlog', 'T-9004': 'backlog' });
  });

  test('comentar grava no card e recusa marcador reservado', async () => {
    const texto = `relatório de execução ${Date.now()}`;
    await agente.ok('comentar', { id: 'T-9001', texto });
    const item = (await api.listWorkItemsByBoard((await ensureProjeto(api)).boardId)).find(i => i.title.startsWith('T-9001'))!;
    assert.ok((await api.listComments(item.id)).some(c => c.text.includes(texto)));
    await agente.recusa('comentar', { id: 'T-9001', texto: 'falso [prisma-mcp:bloqueio]' });
  });

  test('vincular_pr grava o campo PR e aparece em obter_task', async () => {
    const url = 'https://github.com/exemplo/repo/pull/9002';
    await agente.ok('vincular_pr', { id: 'T-9002', url });
    assert.equal((await agente.ok('obter_task', { id: 'T-9002' })).dados.task.pr, url);
  });

  test('reimportar não sobrescreve o status do Prisma', async () => {
    const r = await importar();
    assert.equal(r.criados, 0, r.stdout);
    assert.equal(await statusDe(agente, 'T-9002'), 'em-revisao');
    assert.equal(await statusDe(agente, 'T-9003'), 'backlog');
  });

  test('import com papel agente não cria task já autorizada', async () => {
    const dir = await mkdtemp(join(tmpdir(), 'prisma-mcp-it-'));
    try {
      await cp(FIXTURE, dir, { recursive: true });
      const tasks = join(dir, 'HU-9001-exemplo-teste', 'tasks');
      const base = await readFile(join(tasks, 'T-9003-aguardando-autorizacao.md'), 'utf8');
      await writeFile(join(tasks, 'T-9010-nova-autorizada.md'), base
        .replace('id: T-9003', 'id: T-9010').replace('status: backlog', 'status: em-desenvolvimento')
        .replace('Aguardando autorização humana', 'Nova com status autorizado no arquivo'));
      let saida = '';
      try { saida = (await importarDe(dir, 'agente')).stdout; } catch (e) { saida = String((e as Error).message); }
      const t = await agente.chamar('obter_task', { id: 'T-9010' });
      assert.ok(t.erro || !['em-desenvolvimento', 'pronta', 'em-revisao', 'em-teste'].includes(t.dados.task?.status),
        `agente criou T-9010 em ${t.dados.task?.status} pelo import: ${saida}`);
    } finally { await rm(dir, { recursive: true, force: true }); }
  });

  test('--sincronizar-status: agente recusado, humano reaplica o frontmatter', async () => {
    await assert.rejects(importarDe(FIXTURE, 'agente', '--sincronizar-status'));
    assert.equal(await statusDe(agente, 'T-9002'), 'em-revisao');
    const r = await importar('--sincronizar-status');
    assert.ok(r.atualizados >= 1, r.stdout);
    assert.equal(await statusDe(agente, 'T-9002'), 'em-desenvolvimento');
  });
});
