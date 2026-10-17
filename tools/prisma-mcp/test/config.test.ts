import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readConfig } from '../src/config.js';

// Fase 2a (RF-12): só o token opaco do método; e-mail/senha e JWT de login não servem.
const TOKEN = `pmt_${'A'.repeat(43)}`;
const BASE = { PRISMA_URL: 'http://127.0.0.1:13100/', PRISMA_PROJECT: 'canais-mobile', PRISMA_TOKEN: TOKEN };

test('padrões: quadro e papel vêm da API (não presume), URL sem barra final', () => {
  const c = readConfig({ ...BASE });
  assert.equal(c.url, 'http://127.0.0.1:13100');
  assert.equal(c.project, 'canais-mobile');
  assert.equal(c.board, undefined);
  assert.equal(c.role, undefined);
  assert.equal(c.token, TOKEN);
});

test('PRISMA_BOARD e PRISMA_ROLE explícitos são só expectativas', () => {
  const c = readConfig({ ...BASE, PRISMA_BOARD: 'Quadro X', PRISMA_ROLE: 'agente' });
  assert.equal(c.board, 'Quadro X');
  assert.equal(c.role, 'agente');
});

test('e-mail e senha sem token são recusados', () => {
  const { PRISMA_TOKEN, ...semToken } = BASE;
  assert.throws(() => readConfig({ ...semToken, PRISMA_EMAIL: 'a@b.c', PRISMA_PASSWORD: 'p' }), /PRISMA_TOKEN/);
  assert.throws(() => readConfig({ ...semToken }), /PRISMA_TOKEN/);
});

test('e-mail e senha junto com token não entram na configuração', () => {
  const c = readConfig({ ...BASE, PRISMA_EMAIL: 'a@b.c', PRISMA_PASSWORD: 'SenhaQueNaoPodeFicar1' });
  assert.ok(!JSON.stringify(c).includes('SenhaQueNaoPodeFicar1'));
  assert.ok(!JSON.stringify(c).includes('a@b.c'));
});

test('JWT de login, Bearer e tokens malformados são recusados', () => {
  for (const t of [
    'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.c2lnbmF0dXJh',
    `Bearer ${TOKEN}`,
    `pmt_${'A'.repeat(42)}`,
    `pmt_${'A'.repeat(44)}`,
    `PMT_${'A'.repeat(43)}`,
    `pmt_${'A'.repeat(42)}=`,
    `pmt_${'A'.repeat(42)}+`,
    `ghs_${'A'.repeat(43)}`,
  ]) assert.throws(() => readConfig({ ...BASE, PRISMA_TOKEN: t }), /PRISMA_TOKEN/, t);
});

test('mensagem de token inválido não ecoa o valor', () => {
  const segredo = `pmt_${'S'.repeat(40)}xyz!`;
  try { readConfig({ ...BASE, PRISMA_TOKEN: segredo }); assert.fail('deveria recusar'); }
  catch (e) { assert.ok(!(e as Error).message.includes(segredo)); }
});

test('recusa URL, projeto e papel inválidos', () => {
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: '' }), /PRISMA_URL/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_PROJECT: ' ' }), /PRISMA_PROJECT/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_ROLE: 'admin' }), /PRISMA_ROLE/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_ROLE: 'Agente' }), /PRISMA_ROLE/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: 'ftp://x' }));
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: 'http://u:senha@x' }));
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: 'http://x/?token=1' }));
});
