import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  METHOD_STATUSES, canTransition, assertTransition, assertCreationStatus, getProjectRules, parseRole,
  type TaskStatus,
} from '../src/rules.js';

const TODOS = [...METHOD_STATUSES] as TaskStatus[];

// Matriz do agente fechada pelo PO: o que não está aqui é recusado.
function agentePode(de: TaskStatus, para: TaskStatus, proprio: boolean): boolean {
  if (de === para) return false;
  if (para === 'bloqueada') return de !== 'concluida';
  if (de === 'em-desenvolvimento' && para === 'em-revisao') return true;
  if (de === 'bloqueada' && para === 'em-desenvolvimento') return proprio;
  return false;
}

test('agente nunca autoriza: backlog/pronta -> em-desenvolvimento recusado, mesmo com ownBlock', () => {
  for (const de of ['backlog', 'pronta'] as const) {
    assert.equal(canTransition('agente', de, 'em-desenvolvimento'), false, de);
    assert.equal(canTransition('agente', de, 'em-desenvolvimento', true), false, `${de} ownBlock`);
    assert.throws(() => assertTransition('agente', de, 'em-desenvolvimento'), /autoriza/i);
  }
});

test('agente volta do próprio bloqueio', () => {
  assert.equal(canTransition('agente', 'bloqueada', 'em-desenvolvimento', true), true);
  assert.doesNotThrow(() => assertTransition('agente', 'bloqueada', 'em-desenvolvimento', true));
});

test('agente não volta de bloqueio alheio', () => {
  assert.equal(canTransition('agente', 'bloqueada', 'em-desenvolvimento', false), false);
  assert.equal(canTransition('agente', 'bloqueada', 'em-desenvolvimento'), false);
  assert.throws(() => assertTransition('agente', 'bloqueada', 'em-desenvolvimento', false), /bloqueio/i);
});

test('agente bloqueia qualquer status exceto concluida', () => {
  for (const de of TODOS.filter(s => s !== 'bloqueada')) {
    assert.equal(canTransition('agente', de, 'bloqueada'), de !== 'concluida', de);
  }
});

test('agente move em-desenvolvimento -> em-revisao (nível 0)', () => {
  assert.equal(canTransition('agente', 'em-desenvolvimento', 'em-revisao'), true);
});

test('matriz completa do agente: 7x7 x ownBlock confere com a regra do PO', () => {
  for (const de of TODOS) for (const para of TODOS) for (const proprio of [false, true]) {
    assert.equal(canTransition('agente', de, para, proprio), agentePode(de, para, proprio),
      `${de} -> ${para} ownBlock=${proprio}`);
  }
});

test('agente não move o que o git prova nem reabre concluida', () => {
  const proibidas: [TaskStatus, TaskStatus][] = [
    ['em-revisao', 'em-teste'], ['em-teste', 'concluida'], ['em-revisao', 'em-desenvolvimento'],
    ['concluida', 'em-desenvolvimento'], ['concluida', 'bloqueada'], ['bloqueada', 'concluida'],
    ['bloqueada', 'backlog'], ['bloqueada', 'pronta'], ['backlog', 'pronta'], ['em-desenvolvimento', 'concluida'],
  ];
  for (const [de, para] of proibidas) {
    assert.equal(canTransition('agente', de, para, true), false, `${de} -> ${para}`);
    assert.throws(() => assertTransition('agente', de, para, true), `${de} -> ${para}`);
  }
});

test('humano faz qualquer transição entre status diferentes', () => {
  for (const de of TODOS) for (const para of TODOS) {
    if (de === para) continue;
    assert.equal(canTransition('humano', de, para), true, `${de} -> ${para}`);
  }
});

test('status inválido é recusado para os dois papéis', () => {
  for (const papel of ['agente', 'humano'] as const) {
    assert.equal(canTransition(papel, 'backlog', 'feito' as TaskStatus), false);
    assert.equal(canTransition(papel, 'xyz' as TaskStatus, 'bloqueada'), false);
  }
});

test('criação: agente só em backlog ou concluida; humano em qualquer status', () => {
  for (const s of TODOS) {
    if (s === 'backlog' || s === 'concluida') assert.doesNotThrow(() => assertCreationStatus('agente', s), s);
    else assert.throws(() => assertCreationStatus('agente', s), s);
    assert.doesNotThrow(() => assertCreationStatus('humano', s), s);
  }
});

test('parseRole: padrão agente, aceita humano, recusa outros', () => {
  assert.equal(parseRole(undefined), 'agente');
  assert.equal(parseRole('humano'), 'humano');
  assert.throws(() => parseRole('admin'));
  assert.throws(() => parseRole('Agente'));
});

test('regras_do_projeto do agente não oferece autorização', () => {
  const r = getProjectRules('agente');
  assert.equal(r.papel, 'agente');
  assert.deepEqual(r.status, TODOS);
  assert.ok(!r.transicoes_permitidas.backlog.includes('em-desenvolvimento'));
  assert.ok(!r.transicoes_permitidas.pronta.includes('em-desenvolvimento'));
  assert.deepEqual(r.transicoes_permitidas['em-desenvolvimento'].sort(), ['bloqueada', 'em-revisao']);
  assert.deepEqual(r.transicoes_permitidas.concluida, []);
  assert.deepEqual(r.criacao_permitida, ['backlog', 'concluida']);
});

test('regras_do_projeto traz as convenções do método', () => {
  const texto = getProjectRules('agente').convencoes.join('\n');
  assert.match(texto, /task\/T-XXXX-slug/);
  assert.match(texto, /\[T-XXXX\]/);
  assert.match(texto, /status:.*frontmatter/);
  assert.match(texto, /MCP não responde.*pare e avise/i);
});

test('regras_do_projeto do humano libera todas as transições', () => {
  const r = getProjectRules('humano');
  for (const de of TODOS) assert.equal(r.transicoes_permitidas[de].length, TODOS.length - 1, de);
});
