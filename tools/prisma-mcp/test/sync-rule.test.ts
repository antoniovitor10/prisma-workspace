// Regra por documento (PRD "Sincronização"): função pura, linha a linha.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { decideSync } from '../src/sync.js';

const S = { versao: 3, hash: 'h-base' };
const cases: [string, Parameters<typeof decideSync>[0], ReturnType<typeof decideSync>][] = [
  ['1 · não/não → nada', { local: { hash: 'h-base' }, estado: S, remoto: { versao: 3, hash: 'h-base' } }, 'nada'],
  ['2 · repo mudou → enviar', { local: { hash: 'h-novo' }, estado: S, remoto: { versao: 3, hash: 'h-base' } }, 'enviar'],
  ['3 · Prisma mudou → puxar', { local: { hash: 'h-base' }, estado: S, remoto: { versao: 4, hash: 'h-tela' } }, 'puxar'],
  ['4 · os dois, mesmo texto → atualizar_estado', { local: { hash: 'h-igual' }, estado: S, remoto: { versao: 4, hash: 'h-igual' } }, 'atualizar_estado'],
  ['5 · os dois, textos diferentes → conflito', { local: { hash: 'h-repo' }, estado: S, remoto: { versao: 4, hash: 'h-tela' } }, 'conflito'],
  ['6a · sem S, só no repo', { local: { hash: 'h' } }, 'so_no_repo'],
  ['6b · sem S, só no Prisma', { remoto: { versao: 1, hash: 'h' } }, 'so_no_prisma'],
  ['7a · sem S, nos dois, iguais → criar_estado', { local: { hash: 'h' }, remoto: { versao: 2, hash: 'h' } }, 'criar_estado'],
  ['7b · sem S, nos dois, diferentes → conflito', { local: { hash: 'h1' }, remoto: { versao: 2, hash: 'h2' } }, 'conflito'],
  ['arquivo sumiu (com S) → só avisa', { estado: S, remoto: { versao: 3, hash: 'h-base' } }, 'sumiu_no_repo'],
  ['com S e sem Prisma → lista como só no repo', { local: { hash: 'h-base' }, estado: S }, 'so_no_repo'],
  ['nada em lugar nenhum → nada', {}, 'nada'],
];
for (const [name, input, expected] of cases) {
  test(`decideSync ${name}`, () => { assert.equal(decideSync(input), expected); });
}
