import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readConfig } from '../src/config.js';

const BASE = { PRISMA_URL: 'http://127.0.0.1:13100/', PRISMA_PROJECT: 'canais-mobile', PRISMA_TOKEN: 'x' };

test('padrões: quadro = projeto, papel = agente, URL sem barra final', () => {
  const c = readConfig({ ...BASE });
  assert.equal(c.url, 'http://127.0.0.1:13100');
  assert.equal(c.board, 'canais-mobile');
  assert.equal(c.role, 'agente');
});

test('PRISMA_BOARD e PRISMA_ROLE explícitos', () => {
  const c = readConfig({ ...BASE, PRISMA_BOARD: 'Quadro X', PRISMA_ROLE: 'humano' });
  assert.equal(c.board, 'Quadro X');
  assert.equal(c.role, 'humano');
});

test('aceita e-mail e senha no lugar do token', () => {
  const { PRISMA_TOKEN, ...semToken } = BASE;
  assert.doesNotThrow(() => readConfig({ ...semToken, PRISMA_EMAIL: 'a@b.c', PRISMA_PASSWORD: 'p' }));
});

test('recusa configuração incompleta ou inválida', () => {
  const { PRISMA_TOKEN, ...semToken } = BASE;
  assert.throws(() => readConfig({ ...semToken }), /PRISMA_TOKEN/);
  assert.throws(() => readConfig({ ...semToken, PRISMA_EMAIL: 'a@b.c' }), /PRISMA_PASSWORD/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: '' }), /PRISMA_URL/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_PROJECT: ' ' }), /PRISMA_PROJECT/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_ROLE: 'admin' }), /PRISMA_ROLE/);
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: 'ftp://x' }));
  assert.throws(() => readConfig({ ...BASE, PRISMA_URL: 'http://u:senha@x' }));
});

test('mensagens de erro não ecoam segredos', () => {
  const segredo = 'S3gr3d0-nao-pode-vazar';
  try {
    readConfig({ ...BASE, PRISMA_URL: `http://u:${segredo}@x` });
    assert.fail('deveria recusar');
  } catch (e) {
    assert.ok(!String((e as Error).message).includes(segredo));
  }
});
