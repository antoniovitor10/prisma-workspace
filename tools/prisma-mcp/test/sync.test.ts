// CA-2, CA-5, CA-6, CA-7 (integração): a sincronização contra a API falsa com estado e uma pasta
// temporária. Cada linha da regra é um caso, com o .prisma-sync.json antes e depois.
import { afterEach, beforeEach, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { rm } from 'node:fs/promises';
import { join } from 'node:path';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { BASE, installFakeMethod, sha, type FakeMethod } from './helpers/fake-method.js';
import { EPIC, HU, PRD, TASK, metodoDir, type MetodoDir } from './helpers/metodo-dir.js';
import type { SyncItem } from '../src/sync.js';

let fake: FakePrisma;
let m: FakeMethod;
let env: MetodoDir;
const HU1 = 'HU-0001-exemplo/hu.md';
const T1 = 'HU-0001-exemplo/tasks/T-0001-exemplo.md';
const byKey = (items: SyncItem[]) => Object.fromEntries(items.map(i => [i.chave, i]));
const writes = () => fake.escritas().map(r => `${r.method} ${r.path.replace(BASE, '')}`);

async function synced(files: Record<string, string>) {
  env = await metodoDir(fake, files);
  m.seedTask('T-0001', { hu: 'HU-0001' });
  const report = await env.service.sincronizar({ criarNoPrisma: true });
  assert.ok(report.itens.every(i => !i.erro), JSON.stringify(report.itens.filter(i => i.erro)));
  fake.requisicoes.length = 0;
  return report;
}

describe('sincronizar: regra por documento', () => {
  beforeEach(async () => { fake = await startFakePrisma(); m = installFakeMethod(fake); });
  afterEach(async () => { await fake.close(); await env?.cleanup(); });

  test('linha 1 · nada mudou: nenhuma escrita, estado igual', async () => {
    await synced({ [HU1]: HU('HU-0001'), [T1]: TASK('T-0001', 'HU-0001') });
    const before = await env.snapshot();
    const report = await env.service.sincronizar();
    assert.deepEqual(report.itens.map(i => [i.chave, i.acao, i.passos.length]), [['HU-0001', 'nada', 0], ['T-0001', 'nada', 0]]);
    assert.deepEqual(writes(), []);
    assert.deepEqual(await env.snapshot(), before, 'arquivos e .prisma-sync.json intactos');
  });

  test('linha 2 · mudou no repo: envia com baseVersion = S.versao e atualiza S', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const antes = (await env.state())!.documentos['HU-0001'];
    assert.equal(antes.versao, 1);
    const novo = HU('HU-0001').replace('Como dev, quero sincronizar.', 'Como dev, quero sincronizar já.');
    await env.write(HU1, novo);
    const item = byKey((await env.service.sincronizar({ escopo: 'HU-0001' })).itens)['HU-0001'];
    assert.equal(item.acao, 'enviar');
    const put = fake.escritas().find(r => r.method === 'PUT')!;
    assert.deepEqual(JSON.parse(put.body), { content: novo, baseVersion: 1, filePath: `docs/trabalho/${HU1}` });
    const depois = (await env.state())!.documentos['HU-0001'];
    assert.deepEqual(depois, { ...antes, versao: 2, hash: sha(novo) });
    assert.equal(m.docs.get('HU-0001')!.revisions.at(-1)!.content, novo);
  });

  test('linha 3 · mudou só no Prisma: regrava o arquivo com o texto do Prisma e atualiza S', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const tela = HU('HU-0001').replace('status: planejada', 'status: concluida');
    m.editInPrisma('HU-0001', tela);
    const item = byKey((await env.service.sincronizar({ escopo: 'HU-0001' })).itens)['HU-0001'];
    assert.equal(item.acao, 'puxar');
    assert.equal(await env.text(HU1), tela);
    assert.deepEqual(writes(), [], 'puxar não grava no Prisma');
    assert.deepEqual((await env.state())!.documentos['HU-0001'], { chave: 'HU-0001', tipo: 'hu', workItemId: m.docs.get('HU-0001')!.workItemId, versao: 2, hash: sha(tela) });
  });

  test('linha 4 · mudou nos dois com o mesmo texto: só atualiza S', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const igual = HU('HU-0001').replace('- **CA-1:** ok.', '- **CA-1:** ok.\n- **CA-2:** novo.');
    await env.write(HU1, igual);
    m.editInPrisma('HU-0001', igual);
    const item = byKey((await env.service.sincronizar()).itens)['HU-0001'];
    assert.equal(item.acao, 'atualizar_estado');
    assert.deepEqual(writes(), []);
    assert.equal((await env.state())!.documentos['HU-0001'].versao, 2);
    assert.equal((await env.state())!.documentos['HU-0001'].hash, sha(igual));
  });

  test('linha 5 · mudou nos dois com textos diferentes: conflito, nada gravado, diferença por seção (CA-7)', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const repo = HU('HU-0001').replace('Como dev, quero sincronizar.', 'Como dev (repo).');
    const tela = HU('HU-0001').replace('- **CA-1:** ok.', '- **CA-1:** ok na tela.');
    await env.write(HU1, repo);
    m.editInPrisma('HU-0001', tela);
    const before = await env.snapshot();
    const item = byKey((await env.service.sincronizar()).itens)['HU-0001'];
    assert.equal(item.acao, 'conflito');
    assert.deepEqual(writes(), []);
    assert.deepEqual(await env.snapshot(), before, 'nem arquivo nem estado mudam');
    assert.equal(m.docs.get('HU-0001')!.revisions.length, 2);
    assert.deepEqual(item.diferenca!.map(s => [s.secao, s.classe]), [['cabeçalho', 'igual'], ['História', 'so_repo'], ['Critérios de aceite', 'so_prisma']]);
  });

  test('linha 6 · sem S, só num lado: lista para decidir; com criar, cria no outro lado', async () => {
    env = await metodoDir(fake, { [HU1]: HU('HU-0001') });
    m.seedDoc('HU-0002', HU('HU-0002'), { filePath: 'docs/trabalho/HU-0002-tela/hu.md' });
    const listado = byKey((await env.service.sincronizar()).itens);
    assert.equal(listado['HU-0001'].acao, 'so_no_repo');
    assert.equal(listado['HU-0002'].acao, 'so_no_prisma');
    assert.deepEqual(writes(), []);
    assert.equal(await env.state(), undefined, 'sem estado novo');
    const criado = byKey((await env.service.sincronizar({ criarNoPrisma: true, criarNoRepo: true })).itens);
    assert.deepEqual(criado['HU-0001'].passos, ['criar']);
    assert.deepEqual(criado['HU-0002'].passos, ['gravar_arquivo']);
    assert.equal(JSON.parse(fake.escritas().find(r => r.method === 'PUT')!.body).baseVersion, 0);
    assert.equal(await env.text('HU-0002-tela/hu.md'), HU('HU-0002'));
    const s = (await env.state())!.documentos;
    assert.deepEqual(Object.keys(s).sort(), ['HU-0001', 'HU-0002']);
    assert.equal(s['HU-0002'].hash, sha(HU('HU-0002')));
  });

  test('linha 7 · sem S, nos dois: texto igual cria S; texto diferente é conflito', async () => {
    env = await metodoDir(fake, { [HU1]: HU('HU-0001'), 'HU-0002-x/hu.md': HU('HU-0002') });
    m.seedDoc('HU-0001', HU('HU-0001'));
    m.seedDoc('HU-0002', HU('HU-0002').replace('quero', 'desejo'));
    const items = byKey((await env.service.sincronizar()).itens);
    assert.equal(items['HU-0001'].acao, 'criar_estado');
    assert.equal(items['HU-0002'].acao, 'conflito');
    assert.deepEqual(items['HU-0002'].diferenca!.filter(s => s.classe !== 'igual').map(s => [s.secao, s.classe]), [['História', 'nos_dois']]);
    assert.deepEqual(writes(), []);
    assert.deepEqual(Object.keys((await env.state())!.documentos), ['HU-0001']);
  });
});

describe('sincronizar: ordem, ensaio, 409, religação, avulsas', () => {
  beforeEach(async () => { fake = await startFakePrisma(); m = installFakeMethod(fake); });
  afterEach(async () => { await fake.close(); await env?.cleanup(); });

  test('CA-2/CA-6 · sincronizar() envia épico → HU (com parentKey) → PRD → task', async () => {
    env = await metodoDir(fake, {
      'epicos/EPIC-0001-x.md': EPIC(),
      'HU-0002-filha/hu.md': HU('HU-0002', 'epico: EPIC-0001\n'),
      'HU-0002-filha/prd.md': PRD('HU-0002'),
      'HU-0002-filha/tasks/T-0005-x.md': TASK('T-0005', 'HU-0002'),
      'HU-0001-exemplo/hu.md': HU('HU-0001'),
    });
    const report = await env.service.sincronizar({ criarNoPrisma: true });
    assert.ok(report.itens.every(i => !i.erro), JSON.stringify(report.itens));
    const w = writes();
    const at = (s: string) => { const i = w.indexOf(s); assert.ok(i >= 0, `${s} em ${w.join(', ')}`); return i; };
    assert.ok(at('PUT /documents/EPIC-0001') < at('PUT /documents/HU-0002'), 'épico antes da HU');
    assert.ok(at('PUT /documents/HU-0001') < at('PUT /documents/HU-0002/prd'), 'HUs antes dos PRDs');
    assert.ok(at('PUT /documents/HU-0002/prd') < at('POST /tasks') && at('POST /tasks') < at('PUT /documents/T-0005'), 'PRDs antes das tasks');
    const huPut = fake.escritas().find(r => r.path === `${BASE}/documents/HU-0002`)!;
    assert.equal(JSON.parse(huPut.body).parentKey, 'EPIC-0001');
    assert.equal(JSON.parse(fake.escritas().find(r => r.path === `${BASE}/documents/HU-0001`)!.body).parentKey, undefined);
    assert.equal(m.tasks.get('T-0005')!.parentId, m.docs.get('HU-0002')!.workItemId, 'task nasce sob a História');
    assert.deepEqual(Object.keys((await env.state())!.documentos).sort(), ['EPIC-0001', 'HU-0001', 'HU-0002', 'HU-0002/prd', 'T-0005']);
  });

  test('CA-6 · ensaio: true não grava nada em nenhum lado e mostra o plano', async () => {
    env = await metodoDir(fake, { 'epicos/EPIC-0001-x.md': EPIC(), [HU1]: HU('HU-0001', 'epico: EPIC-0001\n'),
      'HU-0001-exemplo/prd.md': PRD('HU-0001'), [T1]: TASK('T-0001', 'HU-0001'), 'avulsas/tasks/T-0015-x.md': TASK('T-0015', 'avulsa') });
    m.seedTask('T-0001', { hu: 'HU-0001' });
    m.seedDoc('HU-0009', HU('HU-0009'), { filePath: 'docs/trabalho/HU-0009-x/hu.md' });
    const before = await env.snapshot();
    const report = await env.service.sincronizar({ ensaio: true, criarNoPrisma: true, criarNoRepo: true });
    assert.equal(report.ensaio, true);
    assert.deepEqual(writes(), [], 'nenhuma escrita na API');
    assert.deepEqual(await env.snapshot(), before, 'nenhum arquivo criado ou alterado (nem o estado)');
    const items = byKey(report.itens);
    assert.deepEqual(items['EPIC-0001'].passos, ['criar']);
    assert.deepEqual(items['HU-0001'].passos, ['criar']);
    assert.deepEqual(items.AVULSAS.passos, ['criar']);
    assert.deepEqual(items['HU-0001/prd'].passos, ['criar']);
    assert.deepEqual(items['T-0001'].passos, ['religar', 'criar']);
    assert.equal(items['T-0001'].religar_para, 'HU-0001');
    assert.deepEqual(items['T-0015'].passos, ['criar_task', 'criar']);
    assert.deepEqual(items['HU-0009'].passos, ['gravar_arquivo']);
    assert.deepEqual(report.itens.map(i => i.chave), ['EPIC-0001', 'HU-0001', 'HU-0009', 'AVULSAS', 'HU-0001/prd', 'T-0001', 'T-0015']);
  });

  test('409 no envio: trata como "Prisma mudou" e refaz a comparação (vira conflito, nada gravado)', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const repo = HU('HU-0001').replace('quero sincronizar', 'quero enviar');
    await env.write(HU1, repo);
    let puts = 0;
    m.beforePut = () => { puts++; if (puts === 1) m.editInPrisma('HU-0001', HU('HU-0001').replace('Critérios de aceite', 'Critérios de aceite\n\nmudou na tela')); };
    const item = byKey((await env.service.sincronizar()).itens)['HU-0001'];
    assert.equal(puts, 1, 'um único PUT, recusado com 409');
    assert.equal(item.acao, 'conflito');
    assert.equal(item.erro, undefined);
    assert.equal((await env.state())!.documentos['HU-0001'].versao, 1, 'estado não muda');
    assert.equal(await env.text(HU1), repo);
  });

  test('409 no envio com o mesmo texto do outro lado: refaz a comparação e só atualiza S', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const repo = HU('HU-0001').replace('quero sincronizar', 'quero enviar');
    await env.write(HU1, repo);
    let first = true;
    m.beforePut = () => { if (first) { first = false; m.editInPrisma('HU-0001', repo); } };
    const item = byKey((await env.service.sincronizar()).itens)['HU-0001'];
    assert.equal(item.acao, 'atualizar_estado');
    assert.equal((await env.state())!.documentos['HU-0001'].versao, 2);
  });

  test('religação: task que já existe no topo aparece como "religar" e é religada, nunca recriada; 2ª rodada não faz nada', async () => {
    env = await metodoDir(fake, { [HU1]: HU('HU-0001'), [T1]: TASK('T-0001', 'HU-0001'), 'avulsas/tasks/T-0015-x.md': TASK('T-0015', 'avulsa') });
    m.seedTask('T-0001', { hu: 'HU-0001', status: 'em-revisao', authorized: true });
    m.seedTask('T-0015', { hu: 'avulsa', status: 'concluida' });
    const plano = byKey((await env.service.sincronizar({ ensaio: true, criarNoPrisma: true })).itens);
    assert.deepEqual(plano['T-0001'].passos, ['religar', 'criar']);
    assert.deepEqual(plano['T-0015'].passos, ['religar', 'criar']);
    assert.equal(plano['T-0015'].religar_para, 'AVULSAS');
    assert.deepEqual(writes(), []);
    const report = await env.service.sincronizar({ criarNoPrisma: true });
    assert.ok(report.itens.every(i => !i.erro), JSON.stringify(report.itens));
    assert.ok(!fake.escritas().some(r => r.method === 'POST' && r.path === `${BASE}/tasks`), 'nenhuma task criada');
    assert.deepEqual(fake.escritas().filter(r => r.path.endsWith('/parent')).map(r => [r.path.replace(BASE, ''), JSON.parse(r.body).huKey]),
      [['/documents/tasks/T-0001/parent', 'HU-0001'], ['/documents/tasks/T-0015/parent', 'AVULSAS']]);
    assert.equal(m.tasks.get('T-0001')!.parentId, m.docs.get('HU-0001')!.workItemId);
    assert.equal(m.tasks.get('T-0001')!.status, 'em-revisao', 'status preservado');
    assert.equal(m.tasks.get('T-0015')!.parentId, m.docs.get('AVULSAS')!.workItemId);
    fake.requisicoes.length = 0;
    const segunda = await env.service.sincronizar({ criarNoPrisma: true });
    assert.deepEqual(writes(), [], 'segunda rodada não grava nada');
    assert.ok(segunda.itens.every(i => i.acao === 'nada' && !i.passos.length), JSON.stringify(segunda.itens));
  });

  test('task local sem task no Prisma: com criar no Prisma, cria a task sob a História e o corpo', async () => {
    env = await metodoDir(fake, { [HU1]: HU('HU-0001'), [T1]: TASK('T-0001', 'HU-0001') });
    await env.service.sincronizar({ criarNoPrisma: true });
    const post = JSON.parse(fake.escritas().find(r => r.method === 'POST' && r.path === `${BASE}/tasks`)!.body);
    assert.deepEqual(post, { id: 'T-0001', title: 'Task T-0001', hu: 'HU-0001', filePath: `docs/trabalho/${T1}`, covers: ['CA-1'], risk: 'normal', status: 'backlog' });
    assert.equal(m.tasks.get('T-0001')!.parentId, m.docs.get('HU-0001')!.workItemId);
    assert.equal(m.docs.get('T-0001')!.revisions[0].content, TASK('T-0001', 'HU-0001'));
  });

  test('arquivo que some do repositório: só avisa, nunca apaga no Prisma', async () => {
    await synced({ [HU1]: HU('HU-0001'), [T1]: TASK('T-0001', 'HU-0001') });
    await rm(join(env.dir, T1));
    const item = byKey((await env.service.sincronizar()).itens)['T-0001'];
    assert.equal(item.acao, 'sumiu_no_repo');
    assert.match(item.aviso!, /nada é apagado no Prisma/);
    assert.deepEqual(writes(), []);
    assert.ok(!fake.requisicoes.some(r => r.method === 'DELETE'));
    assert.ok(m.docs.has('T-0001'));
  });

  test('escopo HU-0001 cobre a HU, o PRD e as tasks dela, e nada de outra HU', async () => {
    await synced({ [HU1]: HU('HU-0001'), 'HU-0001-exemplo/prd.md': PRD('HU-0001'), [T1]: TASK('T-0001', 'HU-0001'), 'HU-0002-x/hu.md': HU('HU-0002') });
    const report = await env.service.sincronizar({ escopo: 'HU-0001' });
    assert.deepEqual(report.itens.map(i => i.chave), ['HU-0001', 'HU-0001/prd', 'T-0001']);
  });
});

describe('enviar_documento e resolver_conflito', () => {
  beforeEach(async () => { fake = await startFakePrisma(); m = installFakeMethod(fake); });
  afterEach(async () => { await fake.close(); await env?.cleanup(); });

  test('CA-6 · HU editada no repo: enviar_documento manda a versão nova e atualiza o estado', async () => {
    await synced({ [HU1]: HU('HU-0001') });
    const novo = HU('HU-0001').replace('quero sincronizar', 'quero enviar');
    await env.write(HU1, novo);
    const report = await env.service.enviarDocumento(`docs/trabalho/${HU1}`);
    assert.equal(report.itens[0].acao, 'enviar');
    assert.equal(m.docs.get('HU-0001')!.revisions.at(-1)!.content, novo);
    assert.deepEqual((await env.state())!.documentos['HU-0001'].versao, 2);
  });

  test('enviar_documento cria Épico e História na primeira vez e nunca puxa para o arquivo', async () => {
    env = await metodoDir(fake, { 'epicos/EPIC-0001-x.md': EPIC() });
    await env.service.enviarDocumento('epicos/EPIC-0001-x.md');
    assert.equal(m.docs.get('EPIC-0001')!.revisions.length, 1);
    m.editInPrisma('EPIC-0001', EPIC().replace('aberta', 'planejada'));
    const report = await env.service.enviarDocumento(join(env.dir, 'epicos', 'EPIC-0001-x.md'));
    assert.equal(report.itens[0].acao, 'puxar');
    assert.match(report.itens[0].aviso!, /Rode sincronizar/);
    assert.equal(await env.text('epicos/EPIC-0001-x.md'), EPIC(), 'arquivo não é tocado');
  });

  test('CA-5 · enviar_documento de task avulsa: cria a task com hu avulsa, sob a História AVULSAS', async () => {
    env = await metodoDir(fake, { 'avulsas/tasks/T-0015-x.md': TASK('T-0015', 'avulsa') });
    const report = await env.service.enviarDocumento('avulsas/tasks/T-0015-x.md');
    assert.ok(report.itens.every(i => !i.erro), JSON.stringify(report.itens));
    assert.deepEqual(report.itens.map(i => [i.chave, i.passos]), [['AVULSAS', ['criar']], ['T-0015', ['criar_task', 'criar']]]);
    assert.equal(JSON.parse(fake.escritas().find(r => r.path === `${BASE}/tasks`)!.body).hu, 'avulsa');
    assert.equal(m.tasks.get('T-0015')!.parentId, m.docs.get('AVULSAS')!.workItemId);
    assert.ok(m.docs.has('T-0015'));
  });

  test('CA-7 · resolver_conflito com repo, prisma e juntar grava o mesmo texto nos dois lados e atualiza S', async () => {
    const original = HU('HU-0001') + '\n## Notas\n\nbase\n';
    await synced({ [HU1]: original.replaceAll('\n', '\r\n') });
    const repo = original.replace('Como dev, quero sincronizar.', 'Como dev (repo).').replace('base', 'nota repo');
    const tela = original.replace('- **CA-1:** ok.', '- **CA-1:** ok (tela).').replace('base', 'nota tela');
    await env.write(HU1, repo.replaceAll('\n', '\r\n'));
    m.editInPrisma('HU-0001', tela);
    const conflito = byKey((await env.service.sincronizar()).itens)['HU-0001'];
    assert.deepEqual(conflito.diferenca!.filter(s => s.classe !== 'igual').map(s => [s.secao, s.classe]),
      [['História', 'so_repo'], ['Critérios de aceite', 'so_prisma'], ['Notas', 'nos_dois']]);
    await assert.rejects(env.service.resolverConflito('HU-0001', { 'História': 'repo' }), /Falta escolher/);
    assert.equal(m.docs.get('HU-0001')!.revisions.length, 2, 'escolha incompleta não grava');
    const result = await env.service.resolverConflito('HU-0001', {
      'História': 'repo', 'Critérios de aceite': 'prisma', Notas: { juntar: '## Notas\n\nnota repo e nota tela\n' },
    });
    const esperado = original.replace('Como dev, quero sincronizar.', 'Como dev (repo).').replace('- **CA-1:** ok.', '- **CA-1:** ok (tela).').replace('base', 'nota repo e nota tela');
    assert.equal(m.docs.get('HU-0001')!.revisions.at(-1)!.content, esperado, 'Prisma com o texto montado (LF)');
    assert.equal(await env.text(HU1), esperado.replaceAll('\n', '\r\n'), 'arquivo com o texto montado e o CRLF que usava');
    assert.equal(result.versao, 3);
    assert.deepEqual((await env.state())!.documentos['HU-0001'], { chave: 'HU-0001', tipo: 'hu', workItemId: m.docs.get('HU-0001')!.workItemId, versao: 3, hash: sha(esperado) });
    fake.requisicoes.length = 0;
    assert.equal(byKey((await env.service.sincronizar()).itens)['HU-0001'].acao, 'nada');
  });
});
