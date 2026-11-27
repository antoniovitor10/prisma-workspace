// CA-12 e CA-5 pela interface MCP (stdio): as 7 ferramentas novas existem pelo nome, as 9 antigas
// mantêm o contrato, criar_task aceita hu: avulsa, e enviar_documento respeita PRISMA_METODO_DIR.
import { afterEach, beforeEach, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { BASE, TOKEN, installFakeMethod, type FakeMethod } from './helpers/fake-method.js';
import { HU, TASK, metodoDir, type MetodoDir } from './helpers/metodo-dir.js';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const NOVAS = ['obter_documento', 'listar_mudancas', 'enviar_documento', 'sincronizar', 'resolver_conflito', 'revisoes_documento', 'vincular_hu'];
// Contrato das 9 antigas: nome e parâmetros (obrigatórios e opcionais) como antes da T-0023.
const ANTIGAS: Record<string, { required: string[]; all: string[] }> = {
  regras_do_projeto: { required: [], all: [] },
  obter_task: { required: ['id'], all: ['id'] },
  listar_tasks: { required: [], all: ['status'] },
  proximas_tasks: { required: [], all: [] },
  criar_task: { required: ['arquivo', 'cobre', 'hu', 'id', 'risco', 'titulo'], all: ['arquivo', 'cobre', 'hu', 'id', 'risco', 'status', 'titulo'] },
  mover_status: { required: ['id', 'motivo', 'novo_status'], all: ['id', 'motivo', 'novo_status'] },
  comentar: { required: ['id', 'texto'], all: ['id', 'texto'] },
  vincular_pr: { required: ['id', 'url'], all: ['id', 'url'] },
  vincular_branch: { required: ['branch', 'id', 'repositorio'], all: ['branch', 'id', 'repositorio'] },
};

let fake: FakePrisma;
let m: FakeMethod;
let env: MetodoDir;
let client: Client | undefined;
let stderr = '';

async function connect(metodo: string) {
  const base: Record<string, string> = {};
  for (const [k, v] of Object.entries(process.env)) if (v !== undefined && !k.startsWith('PRISMA_')) base[k] = v;
  Object.assign(base, { PRISMA_URL: fake.url, PRISMA_TOKEN: TOKEN, PRISMA_PROJECT: 'metodo-teste', PRISMA_METODO_DIR: metodo });
  const transport = new StdioClientTransport({ command: process.execPath, args: ['--import', 'tsx', 'src/server.ts'], cwd: ROOT, env: base, stderr: 'pipe' });
  transport.stderr?.on('data', (d: Buffer) => { stderr += d.toString(); });
  client = new Client({ name: 'prisma-mcp-docs-test', version: '0.0.0' });
  await client.connect(transport);
  return async (name: string, args: Record<string, unknown> = {}) => {
    const r = await client!.callTool({ name, arguments: args }) as { isError?: boolean; content: { text?: string }[]; structuredContent?: Record<string, any> };
    return { erro: r.isError === true, texto: r.content.map(c => c.text ?? '').join('\n'), dados: r.structuredContent ?? {} };
  };
}

describe('ferramentas de documento pelo MCP', () => {
  beforeEach(async () => { fake = await startFakePrisma(); m = installFakeMethod(fake); stderr = ''; });
  afterEach(async () => { await client?.close().catch(() => {}); client = undefined; await fake.close(); await env?.cleanup(); });

  test('CA-12 · as 7 novas existem pelo nome e as 9 antigas mantêm o contrato', async () => {
    env = await metodoDir(fake);
    await connect(env.dir);
    const { tools } = await client!.listTools();
    const byName = new Map(tools.map(t => [t.name, t]));
    for (const nome of NOVAS) assert.ok(byName.has(nome), `${nome} exposta`);
    for (const [nome, contrato] of Object.entries(ANTIGAS)) {
      const tool = byName.get(nome);
      assert.ok(tool, `${nome} continua exposta`);
      const schema = tool!.inputSchema as { properties?: Record<string, unknown>; required?: string[] };
      assert.deepEqual(Object.keys(schema.properties ?? {}).sort(), contrato.all, `${nome}: parâmetros`);
      assert.deepEqual([...(schema.required ?? [])].sort(), contrato.required, `${nome}: obrigatórios`);
    }
  });

  test('CA-5 · criar_task aceita hu: avulsa e chama a API com hu avulsa; HU fora do formato continua recusada', async () => {
    env = await metodoDir(fake);
    const call = await connect(env.dir);
    const r = await call('criar_task', { id: 'T-0015', titulo: 'Avulsa', hu: 'avulsa', arquivo: 'docs/trabalho/avulsas/tasks/T-0015-x.md', cobre: [], risco: 'normal' });
    assert.equal(r.erro, false, r.texto);
    assert.equal(JSON.parse(fake.escritas().find(x => x.path === `${BASE}/tasks`)!.body).hu, 'avulsa');
    assert.equal(m.tasks.get('T-0015')!.parentId, m.docs.get('AVULSAS')!.workItemId, 'fica sob a História AVULSAS');
    const ruim = await call('criar_task', { id: 'T-0016', titulo: 'x', hu: 'avulsas', arquivo: 'x.md', cobre: [], risco: 'normal' });
    assert.equal(ruim.erro, true);
    assert.equal(fake.escritas().length, 1);
  });

  test('CA-12 · enviar_documento recusa .., .txt e caminho fora de PRISMA_METODO_DIR sem chamar a API', async () => {
    env = await metodoDir(fake, { 'HU-0001-x/hu.md': HU('HU-0001'), 'HU-0001-x/n.txt': 'x' });
    const call = await connect(env.dir);
    fake.requisicoes.length = 0;
    for (const caminho of ['../fora.md', 'HU-0001-x/n.txt', join(ROOT, 'README.md')]) {
      const r = await call('enviar_documento', { caminho });
      assert.equal(r.erro, true, caminho);
      assert.match(r.texto, /recusado/);
    }
    assert.equal(fake.requisicoes.filter(x => x.path.includes('/documents')).length, 0);
    assert.deepEqual(fake.escritas(), []);
  });

  test('enviar_documento, sincronizar (ensaio), obter_documento, revisoes_documento, listar_mudancas e vincular_hu', async () => {
    env = await metodoDir(fake, { 'HU-0001-x/hu.md': HU('HU-0001'), 'HU-0001-x/tasks/T-0001-x.md': TASK('T-0001', 'HU-0001') });
    m.seedTask('T-0001');
    const call = await connect(env.dir);
    const enviado = await call('enviar_documento', { caminho: 'docs/trabalho/HU-0001-x/hu.md' });
    assert.equal(enviado.erro, false, enviado.texto);
    assert.match(enviado.texto, /Histórias a criar \(1\): HU-0001/);
    const ensaio = await call('sincronizar', { ensaio: true, criar_no_prisma: true });
    assert.equal(ensaio.erro, false, ensaio.texto);
    assert.match(ensaio.texto, /Tasks a religar \(vincular_hu\) \(1\): T-0001 → HU-0001/);
    assert.equal(fake.escritas().filter(x => x.path.endsWith('/parent')).length, 0, 'ensaio não religa');
    const doc = await call('obter_documento', { chave: 'HU-0001' });
    assert.equal(doc.dados.documento.content, HU('HU-0001'));
    assert.equal(doc.dados.documento.version, 1);
    const revs = await call('revisoes_documento', { chave: 'HU-0001' });
    assert.equal(revs.dados.total, 1);
    const rev = await call('revisoes_documento', { chave: 'HU-0001', versao: 1 });
    assert.equal(rev.dados.revisao.content, HU('HU-0001'));
    m.editInPrisma('HU-0001', HU('HU-0001').replace('planejada', 'concluida'));
    const mudancas = await call('listar_mudancas');
    assert.deepEqual(mudancas.dados.documentos.filter((d: any) => d.mudou_no_prisma).map((d: any) => d.key), ['HU-0001']);
    assert.ok(mudancas.dados.documentos.every((d: any) => d.content === undefined), 'só metadados');
    const vinculo = await call('vincular_hu', { id_task: 'T-0001', chave_hu: 'HU-0001' });
    assert.equal(vinculo.erro, false, vinculo.texto);
    assert.equal(m.tasks.get('T-0001')!.parentId, m.docs.get('HU-0001')!.workItemId);
    assert.ok(!stderr.includes('Como dev, quero sincronizar'), 'conteúdo de documento não vai para stderr');
    assert.ok(!stderr.includes(TOKEN));
  });

  test('401 nas ferramentas de documento: pare e avise, sem nova tentativa', async () => {
    env = await metodoDir(fake, { 'HU-0001-x/hu.md': HU('HU-0001') });
    const call = await connect(env.dir);
    fake.on('GET', `${BASE}/documents`, { status: 401, json: { title: 'Token revogado.' } });
    fake.requisicoes.length = 0;
    const r = await call('sincronizar');
    assert.equal(r.erro, true);
    assert.match(r.texto, /pare e avise/i);
    assert.equal(fake.requisicoes.filter(x => x.path === `${BASE}/documents`).length, 1);
  });
});
