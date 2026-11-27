// CA-12: guarda de caminho (recusa sem ler nem gravar) e fim de linha/BOM ao gravar.
import { after, before, describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describeText, resolveMetodoPath, toFileText, PathGuardError } from '../src/files.js';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { installFakeMethod } from './helpers/fake-method.js';
import { HU, metodoDir } from './helpers/metodo-dir.js';

let root: string;
let outside: string;
let fileLink = false;
let dirLink = false;

describe('guarda de caminho', () => {
  before(async () => {
    root = await mkdtemp(join(tmpdir(), 'prisma-guard-'));
    outside = await mkdtemp(join(tmpdir(), 'prisma-fora-'));
    await mkdir(join(root, 'HU-0001-x'), { recursive: true });
    await writeFile(join(root, 'HU-0001-x', 'hu.md'), HU('HU-0001'));
    await writeFile(join(root, 'HU-0001-x', 'notas.txt'), 'x');
    await writeFile(join(outside, 'segredo.md'), 'fora');
    await mkdir(join(outside, 'HU-0002-y'));
    await writeFile(join(outside, 'HU-0002-y', 'hu.md'), HU('HU-0002'));
    try { await symlink(join(outside, 'segredo.md'), join(root, 'link.md'), 'file'); fileLink = true; } catch { /* Windows sem permissão de link de arquivo */ }
    try { await symlink(join(outside, 'HU-0002-y'), join(root, 'HU-0002-y'), 'junction'); dirLink = true; } catch { /* sem junção */ }
  });
  after(async () => { await rm(root, { recursive: true, force: true }); await rm(outside, { recursive: true, force: true }); });

  test('aceita relativo à pasta, docs/trabalho/... e absoluto dentro da pasta', async () => {
    for (const input of ['HU-0001-x/hu.md', './HU-0001-x/hu.md', 'docs/trabalho/HU-0001-x/hu.md', join(root, 'HU-0001-x', 'hu.md'), 'HU-0001-x\\hu.md']) {
      const p = await resolveMetodoPath(root, input);
      assert.equal(p.rel, 'HU-0001-x/hu.md', input);
      assert.equal(p.filePath, 'docs/trabalho/HU-0001-x/hu.md');
    }
  });

  test('recusa "..", extensão que não é .md e caminho absoluto fora da pasta', async () => {
    await assert.rejects(resolveMetodoPath(root, '../prisma-fora/segredo.md'), /"\.\."/);
    await assert.rejects(resolveMetodoPath(root, 'HU-0001-x/../HU-0001-x/hu.md'), /"\.\."/);
    await assert.rejects(resolveMetodoPath(root, 'HU-0001-x/notas.txt'), /\.md/);
    await assert.rejects(resolveMetodoPath(root, join(outside, 'segredo.md')), /fora da pasta/);
    await assert.rejects(resolveMetodoPath(root, '.prisma-sync.json'), /\.md/, 'estado só com allowState');
    await assert.rejects(resolveMetodoPath(root, ''), PathGuardError);
  });

  test('recusa link simbólico de arquivo', async t => {
    if (!fileLink) { t.skip('sem permissão para criar link de arquivo (Windows sem modo de desenvolvedor)'); return; }
    await assert.rejects(resolveMetodoPath(root, 'link.md'), /link simbólico/);
  });

  test('recusa caminho que passa por junção/link de pasta', async t => {
    if (!dirLink) { t.skip('sem junção'); return; }
    await assert.rejects(resolveMetodoPath(root, 'HU-0002-y/hu.md'), /link simbólico/);
  });

  test('enviar_documento recusado não faz nenhuma chamada à API nem grava nada', async () => {
    const fake: FakePrisma = await startFakePrisma();
    installFakeMethod(fake);
    const env = await metodoDir(fake, { 'HU-0001-x/hu.md': HU('HU-0001'), 'HU-0001-x/notas.txt': 'x' });
    try {
      if (dirLink) await symlink(join(outside, 'HU-0002-y'), join(env.dir, 'HU-0002-y'), 'junction');
      const before = await env.snapshot();
      const refused = ['../fora.md', 'HU-0001-x/notas.txt', join(outside, 'segredo.md'), ...(dirLink ? ['HU-0002-y/hu.md'] : [])];
      for (const input of refused) await assert.rejects(env.service.enviarDocumento(input), PathGuardError, input);
      assert.equal(fake.requisicoes.length, 0, 'nenhuma requisição');
      assert.deepEqual(await env.snapshot(), before);
    } finally { await env.cleanup(); await fake.close(); }
  });
});

test('describeText: LF para o Prisma, fim de linha e BOM detectados; recusa UTF-8 inválido', () => {
  const crlf = describeText(Buffer.from('﻿a\r\nb\r\n'), 'x.md');
  assert.equal(crlf.lf, '﻿a\nb\n');
  assert.equal(crlf.eol, '\r\n');
  assert.equal(crlf.bom, true);
  assert.equal(describeText(Buffer.from('a\nb'), 'y.md').eol, '\n');
  assert.throws(() => describeText(Buffer.from([0x61, 0xff, 0x62]), 'z.md'), /UTF-8/);
});

test('toFileText mantém o fim de linha e o BOM que o arquivo usava', () => {
  assert.equal(toFileText('a\nb\n', { eol: '\r\n', bom: false }), 'a\r\nb\r\n');
  assert.equal(toFileText('a\nb\n', { eol: '\n', bom: true }), '﻿a\nb\n');
  assert.equal(toFileText('﻿a\n', { eol: '\n', bom: false }), 'a\n');
  assert.equal(toFileText('a\r\nb\n'), 'a\nb\n', 'arquivo novo: LF');
});

test('readFile do estado gravado é JSON com LF e chaves em ordem', async () => {
  const fake = await startFakePrisma();
  const m = installFakeMethod(fake);
  const env = await metodoDir(fake, { 'HU-0002-b/hu.md': HU('HU-0002'), 'HU-0001-a/hu.md': HU('HU-0001') });
  try {
    m.seedDoc('HU-0001', HU('HU-0001')); m.seedDoc('HU-0002', HU('HU-0002'));
    await env.service.sincronizar();
    const raw = await readFile(join(env.dir, '.prisma-sync.json'), 'utf8');
    assert.ok(!raw.includes('\r'));
    assert.deepEqual(Object.keys(JSON.parse(raw).documentos), ['HU-0001', 'HU-0002']);
    assert.deepEqual(Object.keys(JSON.parse(raw).documentos['HU-0001']), ['chave', 'tipo', 'workItemId', 'versao', 'hash']);
  } finally { await env.cleanup(); await fake.close(); }
});
