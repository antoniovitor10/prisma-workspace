// CLI prisma-metodo-sync (apoio à T-0025): plano no --dry-run sem gravar nada, rodada real, segunda
// rodada sem efeito e retrato antes/depois das tasks. Só API falsa e pasta temporária.
import { afterEach, beforeEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, rm } from 'node:fs/promises';
import { join } from 'node:path';
import { PrismaApi } from '../src/api.js';
import { readConfig } from '../src/config.js';
import { main, parseArgs } from '../src/sync-cli.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { TOKEN, installFakeMethod, type FakeMethod } from './helpers/fake-method.js';
import { HU, PRD, TASK, metodoDir, type MetodoDir } from './helpers/metodo-dir.js';

let fake: FakePrisma;
let m: FakeMethod;
let env: MetodoDir;
const api = () => new PrismaApi(readConfig({ PRISMA_URL: fake.url, PRISMA_PROJECT: 'metodo-teste', PRISMA_TOKEN: TOKEN }));
async function run(args: string[]) {
  const lines: string[] = [];
  const code = await main(args, { api: api(), dir: env.dir, out: l => lines.push(l) });
  return { code, out: lines.join('\n') };
}

beforeEach(async () => {
  fake = await startFakePrisma(); m = installFakeMethod(fake);
  env = await metodoDir(fake, {
    'HU-0001-a/hu.md': HU('HU-0001'), 'HU-0001-a/prd.md': PRD('HU-0001'),
    'HU-0001-a/tasks/T-0001-a.md': TASK('T-0001', 'HU-0001'), 'HU-0001-a/tasks/T-0002-b.md': TASK('T-0002', 'HU-0001'),
    'avulsas/tasks/T-0015-x.md': TASK('T-0015', 'avulsa'),
  });
  // Passado: tasks já no Prisma, no topo, com status, autorização, comentários e PR.
  m.seedTask('T-0001', { hu: 'HU-0001', status: 'concluida', authorized: true, comments: 3, pr: 'https://github.com/o/r/pull/1' });
  m.seedTask('T-0015', { hu: 'avulsa', status: 'concluida', comments: 1 });
  m.seedDoc('HU-0099', HU('HU-0099'), { filePath: 'docs/trabalho/HU-0099-tela/hu.md' });
});
afterEach(async () => { await fake.close(); await env.cleanup(); });

test('parseArgs: opções, escopo único e --snapshot isolado', () => {
  assert.deepEqual(parseArgs(['HU-0001', '--dry-run', '--criar-no-prisma']), { escopo: 'HU-0001', dryRun: true, criarNoPrisma: true, criarNoRepo: false, json: false });
  assert.equal(parseArgs(['--snapshot', 'a.json']).snapshot, 'a.json');
  assert.throws(() => parseArgs(['--snapshot']), /exige/);
  assert.throws(() => parseArgs(['--snapshot', 'a.json', '--dry-run']), /não se combina/);
  assert.throws(() => parseArgs(['--apagar']), /desconhecida/);
  assert.throws(() => parseArgs(['a', 'b']), /no máximo um escopo/);
});

test('T-0025 · dry-run imprime o plano completo sem gravar nada; rodada real; segunda rodada sem efeito', async () => {
  const before = await env.snapshot();
  const plano = await run(['--dry-run', '--criar-no-prisma']);
  assert.equal(plano.code, 0, plano.out);
  assert.deepEqual(fake.escritas(), [], 'dry-run não grava no Prisma');
  assert.deepEqual(await env.snapshot(), before, 'dry-run não grava arquivo nem estado');
  assert.match(plano.out, /^Ensaio \(nada foi gravado em nenhum lado\)/);
  assert.match(plano.out, /Histórias a criar \(1\): HU-0001/);
  assert.match(plano.out, /Avulsas \(1\): criar a História AVULSAS/);
  assert.match(plano.out, /PRDs a criar \(1\): HU-0001\/prd/);
  assert.match(plano.out, /Tasks a religar \(vincular_hu\) \(2\): T-0001 → HU-0001, T-0015 → AVULSAS/);
  assert.match(plano.out, /Tasks a criar no Prisma \(1\): T-0002/);
  assert.match(plano.out, /Corpos de task a criar \(3\): T-0001, T-0002, T-0015/);
  assert.match(plano.out, /Só no Prisma \(1\): HU-0099/);
  assert.ok(!plano.out.includes('Como dev, quero sincronizar'), 'sem conteúdo de documento na saída');

  const antes = join(env.dir, '..', `${env.dir.split(/[\\/]/).pop()}-antes.json`);
  const depois = join(env.dir, '..', `${env.dir.split(/[\\/]/).pop()}-depois.md`);
  try {
    assert.equal((await run(['--snapshot', antes])).code, 0);
    const retratoAntes = JSON.parse(await readFile(antes, 'utf8'));
    assert.deepEqual(retratoAntes.tasks.map((t: any) => [t.id, t.status, t.pai, t.autorizada, t.comentarios, t.pr]), [
      ['T-0001', 'concluida', null, true, 3, 'https://github.com/o/r/pull/1'], ['T-0015', 'concluida', null, false, 1, null]]);
    assert.deepEqual(fake.escritas(), [], 'retrato só lê');

    const real = await run(['--criar-no-prisma']);
    assert.equal(real.code, 0, real.out);
    assert.ok(!fake.escritas().some(r => r.method === 'POST' && /\/tasks$/.test(r.path) && JSON.parse(r.body).id !== 'T-0002'), 'só a T-0002 é criada; T-0001 e T-0015 são religadas');

    assert.equal((await run(['--snapshot', depois])).code, 0);
    const md = await readFile(depois, 'utf8');
    assert.match(md, /\| T-0001 \| concluida \| HU-0001 \| HU-0001 \| sim \| 3 \| https:\/\/github.com\/o\/r\/pull\/1 \|/);
    assert.match(md, /\| T-0015 \| concluida \| AVULSAS \| avulsa \| não \| 1 \| — \|/);
    assert.match(md, /\| T-0002 \| backlog \| HU-0001 \|/);

    fake.requisicoes.length = 0;
    const segunda = await run(['--criar-no-prisma']);
    assert.equal(segunda.code, 0);
    assert.deepEqual(fake.escritas(), [], 'segunda rodada não grava nada');
    assert.match(segunda.out, /Sem mudança: 5/);
  } finally { await rm(antes, { force: true }); await rm(depois, { force: true }); }
});

test('--json não leva o texto das seções em conflito; conflito sai com código 2', async () => {
  await run(['--criar-no-prisma']);
  await env.write('HU-0001-a/hu.md', HU('HU-0001').replace('quero sincronizar', 'quero (repo)'));
  m.editInPrisma('HU-0001', HU('HU-0001').replace('quero sincronizar', 'quero (tela)'));
  const r = await run(['HU-0001', '--json']);
  assert.equal(r.code, 2);
  const report = JSON.parse(r.out);
  const item = report.itens.find((i: any) => i.chave === 'HU-0001');
  assert.equal(item.acao, 'conflito');
  assert.deepEqual(item.diferenca.find((s: any) => s.secao === 'História'), { secao: 'História', classe: 'nos_dois' });
  assert.ok(!r.out.includes('quero (repo)') && !r.out.includes('quero (tela)'));
});
