import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { homedir, tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readMetodo, parseFrontmatter } from '../src/metodo.js';

const here = dirname(fileURLToPath(import.meta.url));
const FIXTURE = join(here, 'fixtures', 'trabalho');
// Dados reais da demo (somente leitura). Fora do repositório: o teste pula se a pasta não existir.
const CANAIS = process.env.PRISMA_TEST_CANAIS_MOBILE
  ?? join(homedir(), 'projetos', 'prisma-handoff', 'canais-mobile-docs', 'trabalho');
const temCanais = existsSync(CANAIS);
const HU1 = 'docs/trabalho/HU-0001-restaurar-identidade-institucional';

test('canais-mobile: 1 HU e as 4 tasks T-0001..T-0004', { skip: !temCanais && 'sem os arquivos do canais-mobile' }, async () => {
  const { hus, tasks } = await readMetodo(CANAIS);
  assert.equal(hus.length, 1);
  assert.equal(hus[0].id, 'HU-0001');
  assert.equal(hus[0].titulo, 'Restaurar a identidade visual institucional após o período eleitoral');
  assert.equal(hus[0].arquivo, `${HU1}/hu.md`);
  assert.equal(hus[0].status, 'planejada');
  assert.deepEqual(tasks.map(t => t.id), ['T-0001', 'T-0002', 'T-0003', 'T-0004']);
  for (const t of tasks) {
    assert.equal(t.hu, 'HU-0001', t.id);
    assert.equal(t.status, 'em-revisao', t.id);
    assert.match(t.arquivo, new RegExp(`^${HU1}/tasks/${t.id}-[a-z0-9-]+\\.md$`), t.id);
    assert.ok(t.body.includes(`# ${t.id} · ${t.titulo}`), `${t.id}: corpo sem o frontmatter`);
    assert.ok(!t.body.startsWith('---'), t.id);
  }
});

test('canais-mobile: títulos, dependências, cobre e risco', { skip: !temCanais && 'sem os arquivos do canais-mobile' }, async () => {
  const { tasks } = await readMetodo(CANAIS);
  const t = Object.fromEntries(tasks.map(x => [x.id, x]));
  assert.equal(t['T-0001'].titulo, 'Identidade institucional: troca por chave e paleta original');
  assert.equal(t['T-0002'].titulo, 'Logo do splash, banners e card da CNH Social');
  assert.equal(t['T-0003'].titulo, 'Ícone original do app e versão 2.25.0');
  assert.equal(t['T-0004'].titulo, 'Imagens das lojas com a identidade original');
  assert.deepEqual(t['T-0001'].depende_de, []);
  assert.deepEqual(t['T-0002'].depende_de, ['T-0001']);
  assert.deepEqual(t['T-0003'].depende_de, ['T-0002']);
  assert.deepEqual(t['T-0004'].depende_de, ['T-0002']);
  assert.deepEqual(t['T-0001'].cobre, ['CA-1', 'CA-3', 'CA-4', 'CA-8']);
  assert.deepEqual(t['T-0002'].cobre, ['CA-2', 'CA-3', 'CA-4', 'CA-8']);
  assert.deepEqual(t['T-0003'].cobre, ['CA-5', 'CA-7', 'CA-8']);
  assert.deepEqual(t['T-0004'].cobre, ['CA-6', 'CA-8']);
  assert.equal(t['T-0003'].risco, 'alto');
  for (const id of ['T-0001', 'T-0002', 'T-0004']) assert.equal(t[id].risco, 'normal', id);
});

test('leitura não altera os arquivos de origem', { skip: !temCanais && 'sem os arquivos do canais-mobile' }, async () => {
  const f = join(CANAIS, 'HU-0001-restaurar-identidade-institucional', 'hu.md');
  const antes = await readFile(f, 'utf8');
  await readMetodo(CANAIS);
  assert.equal(await readFile(f, 'utf8'), antes);
});

test('fixture: 1 HU e 3 tasks com status, dependências e risco', async () => {
  const { hus, tasks } = await readMetodo(FIXTURE);
  assert.deepEqual(hus.map(h => h.id), ['HU-9001']);
  assert.deepEqual(tasks.map(t => [t.id, t.status, t.depende_de, t.risco]), [
    ['T-9001', 'concluida', [], 'normal'],
    ['T-9002', 'em-desenvolvimento', ['T-9001'], 'alto'],
    ['T-9003', 'backlog', ['T-9002'], 'normal'],
  ]);
});

async function copiaFixture(): Promise<string> {
  const dir = await mkdtemp(join(tmpdir(), 'prisma-mcp-metodo-'));
  await cp(FIXTURE, dir, { recursive: true });
  return dir;
}
const TASK = join('HU-9001-exemplo-teste', 'tasks', 'T-9002-em-andamento.md');

async function trocar(dir: string, de: string, para: string) {
  const f = join(dir, TASK);
  const txt = await readFile(f, 'utf8');
  assert.ok(txt.includes(de), `fixture sem "${de}"`);
  await writeFile(f, txt.replace(de, para));
}

test('task sem status: (modo prisma) é aceita sem status', async () => {
  const dir = await copiaFixture();
  try {
    await trocar(dir, 'status: em-desenvolvimento\n', '');
    const { tasks } = await readMetodo(dir);
    assert.equal(tasks.find(t => t.id === 'T-9002')?.status, undefined);
  } finally { await rm(dir, { recursive: true, force: true }); }
});

test('frontmatter com CRLF (arquivo editado no Windows) é lido', async () => {
  const dir = await copiaFixture();
  try {
    const f = join(dir, TASK);
    await writeFile(f, (await readFile(f, 'utf8')).replaceAll('\n', '\r\n'));
    const { tasks } = await readMetodo(dir);
    assert.deepEqual(tasks.find(t => t.id === 'T-9002')?.depende_de, ['T-9001']);
  } finally { await rm(dir, { recursive: true, force: true }); }
});

const invalidos: [string, string, string, RegExp][] = [
  ['status fora do mapa', 'status: em-desenvolvimento', 'status: fazendo', /status/],
  ['risco fora de normal/alto', 'risco: alto', 'risco: altissimo', /risco/],
  ['dependência inexistente', 'depende_de: [T-9001]', 'depende_de: [T-9999]', /T-9999/],
  ['HU diferente da pasta', 'hu: HU-9001', 'hu: HU-9002', /HU/],
  ['id duplicado', 'id: T-9002', 'id: T-9001', /duplicado/i],
  ['dependência cíclica', 'depende_de: [T-9001]', 'depende_de: [T-9003]', /c[ií]clica/i],
];
for (const [nome, de, para, erro] of invalidos) {
  test(`recusa: ${nome}`, async () => {
    const dir = await copiaFixture();
    try {
      await trocar(dir, de, para);
      await assert.rejects(readMetodo(dir), erro);
    } finally { await rm(dir, { recursive: true, force: true }); }
  });
}

test('parseFrontmatter recusa documento sem frontmatter ou com YAML quebrado', () => {
  assert.throws(() => parseFrontmatter('# sem frontmatter\n', 'x.md'), /x\.md/);
  assert.throws(() => parseFrontmatter('---\nid: [aberto\n---\n', 'y.md'), /y\.md/);
  assert.deepEqual(parseFrontmatter('---\nid: T-0001\n---\ncorpo').metadata, { id: 'T-0001' });
});
