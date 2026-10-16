import { test } from 'node:test';
import assert from 'node:assert/strict';
import { STATUS_MAP, STATUSES, FIELD_NAMES, isMetodoStatus } from '../src/mapping.js';
import { METHOD_STATUSES } from '../src/rules.js';

// Nomes exatos e ordem das colunas, como fechado pelo PO no plano da fase 1.
const COLUNAS = [
  ['backlog', 'Backlog'],
  ['pronta', 'Pronta'],
  ['em-desenvolvimento', 'Em desenvolvimento'],
  ['em-revisao', 'Em revisão'],
  ['em-teste', 'Em teste'],
  ['concluida', 'Concluída'],
  ['bloqueada', 'Bloqueada'],
] as const;

// Valores de StageCategory em src/Prisma.Workspace.Domain/Enums/StageCategory.cs.
const CATEGORIA = { Backlog: 1, Ready: 2, InProgress: 3, Review: 4, Done: 5 } as const;

test('mapa tem os 7 status na ordem das colunas', () => {
  assert.deepEqual(STATUSES, COLUNAS.map(([s]) => s));
});

test('cada status aponta para o nome exato da coluna', () => {
  for (const [status, nome] of COLUNAS) {
    assert.equal(STATUS_MAP[status].name, nome, status);
  }
});

test('nomes das colunas são únicos', () => {
  const nomes = STATUSES.map(s => STATUS_MAP[s].name);
  assert.equal(new Set(nomes).size, nomes.length);
});

test('concluida é a única coluna de categoria concluído', () => {
  assert.equal(STATUS_MAP.concluida.category, CATEGORIA.Done);
  const done = STATUSES.filter(s => STATUS_MAP[s].category === CATEGORIA.Done);
  assert.deepEqual(done, ['concluida']);
});

test('categorias coerentes com o fluxo', () => {
  assert.equal(STATUS_MAP.backlog.category, CATEGORIA.Backlog);
  assert.equal(STATUS_MAP.pronta.category, CATEGORIA.Ready);
  assert.equal(STATUS_MAP['em-desenvolvimento'].category, CATEGORIA.InProgress);
  assert.equal(STATUS_MAP['em-revisao'].category, CATEGORIA.Review);
  // Bloqueada não pode contar como concluída nem como backlog.
  assert.notEqual(STATUS_MAP.bloqueada.category, CATEGORIA.Done);
  assert.notEqual(STATUS_MAP.bloqueada.category, CATEGORIA.Backlog);
});

test('isMetodoStatus aceita só os 7 status', () => {
  for (const s of STATUSES) assert.ok(isMetodoStatus(s), s);
  for (const s of ['Backlog', 'em revisao', 'em-revisão', 'done', '', null, undefined, 3, 'toString', '__proto__']) {
    assert.equal(isMetodoStatus(s), false, String(s));
  }
});

test('mapping.ts e rules.ts usam a mesma lista de status', () => {
  assert.deepEqual([...METHOD_STATUSES], STATUSES);
});

test('campos personalizados com os nomes do plano', () => {
  assert.deepEqual(FIELD_NAMES, {
    id: 'ID do metodo', arquivo: 'Arquivo', cobre: 'Cobre', risco: 'Risco', pr: 'PR',
  });
});
