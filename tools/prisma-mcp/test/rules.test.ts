import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  METHOD_STATUSES, canTransition, assertTransition, assertCreationStatus, getProjectRules, parseRole,
  type TaskStatus,
} from '../src/rules.js';
import type { MethodRulesDto } from '../src/api.js';

// Fase 2a: a matriz vem da API; rules.ts é só redundância local e nunca dá mais autoridade que ela.
const TODOS = [...METHOD_STATUSES] as TaskStatus[];

function regras(role: 'agente' | 'humano' | 'sistema', allowed: Record<string, string[]>): MethodRulesDto {
  return {
    identity: { authenticationKind: 'methodToken', role, principalId: 'p-1', userId: 'method:p-1', displayName: 'Agente',
      organizationId: 'o', projectId: 'pr', projectName: 'canais-mobile', boardId: 'b', boardName: 'canais-mobile',
      tokenId: 't', expiresAtUtc: '2099-01-01T00:00:00Z', capabilities: ['read'] },
    project: { stageMappings: Object.fromEntries(TODOS.map(s => [s, `stage-${s}`])), autonomyLevel: 0 },
    allowedTransitions: allowed,
    conventions: ['Use branch task/T-XXXX-slug.', 'Se o MCP não responde, pare e avise.'],
    requiresInteractiveAuthorization: true, requiresInteractiveResumeApproval: true,
  } as unknown as MethodRulesDto;
}

// O que a API publica para o agente no nível 0.
const AGENTE = regras('agente', {
  backlog: ['bloqueada'], pronta: ['bloqueada'], 'em-desenvolvimento': ['em-revisao', 'bloqueada'],
  'em-revisao': ['bloqueada'], 'em-teste': ['bloqueada'], bloqueada: ['em-desenvolvimento'], concluida: [],
});

test('guard local recusa autorizar mesmo que a API anuncie a transição por engano', () => {
  const generosa = regras('agente', Object.fromEntries(TODOS.map(s => [s, TODOS.filter(t => t !== s)])));
  for (const de of ['backlog', 'pronta'] as const) {
    assert.equal(canTransition(generosa, de, 'em-desenvolvimento'), false, de);
    assert.equal(canTransition(generosa, de, 'em-desenvolvimento', true), false, `${de} com canResume`);
    assert.throws(() => assertTransition(generosa, de, 'em-desenvolvimento'), /sessão humana|Pare e avise/);
  }
  for (const papel of ['humano', 'sistema'] as const) {
    const r = regras(papel, Object.fromEntries(TODOS.map(s => [s, TODOS.filter(t => t !== s)])));
    assert.equal(canTransition(r, 'backlog', 'em-desenvolvimento'), false, `token ${papel} também não autoriza`);
  }
});

test('retomada só com canResume do backend, mesmo que a matriz liste a volta', () => {
  assert.equal(canTransition(AGENTE, 'bloqueada', 'em-desenvolvimento', false), false);
  assert.equal(canTransition(AGENTE, 'bloqueada', 'em-desenvolvimento', true), true);
});

test('token de sistema nunca move pelo MCP', () => {
  const sistema = regras('sistema', { 'em-desenvolvimento': ['em-revisao'] });
  assert.equal(canTransition(sistema, 'em-desenvolvimento', 'em-revisao'), false);
});

test('fora das exceções, segue exatamente a matriz publicada pela API', () => {
  for (const de of TODOS) for (const para of TODOS) {
    if (de === para) continue;
    const esperado = (AGENTE.allowedTransitions[de] ?? []).includes(para)
      && !((de === 'backlog' || de === 'pronta') && para === 'em-desenvolvimento')
      && !(de === 'bloqueada' && para === 'em-desenvolvimento');
    assert.equal(canTransition(AGENTE, de, para), esperado, `${de} -> ${para}`);
  }
});

test('transição ausente da matriz (ex.: chave faltando) é recusada', () => {
  const vazia = regras('agente', {});
  for (const de of TODOS) for (const para of TODOS) if (de !== para) assert.equal(canTransition(vazia, de, para), false);
});

test('status inválido é recusado', () => {
  assert.equal(canTransition(AGENTE, 'backlog', 'feito' as TaskStatus), false);
  assert.equal(canTransition(AGENTE, 'xyz' as TaskStatus, 'bloqueada'), false);
});

test('criação: agente só backlog ou concluida; sistema nunca; nenhum token cria autorizada', () => {
  for (const s of TODOS) {
    if (s === 'backlog' || s === 'concluida') assert.doesNotThrow(() => assertCreationStatus('agente', s), s);
    else assert.throws(() => assertCreationStatus('agente', s), s);
    assert.throws(() => assertCreationStatus('sistema', s), `sistema ${s}`);
  }
  assert.throws(() => assertCreationStatus('humano', 'em-desenvolvimento'), /autorização humana/);
});

test('parseRole aceita só os três códigos exatos da API', () => {
  for (const r of ['agente', 'humano', 'sistema'] as const) assert.equal(parseRole(r), r);
  for (const r of ['Agente', 'admin', '', ' agente', 'agent']) assert.throws(() => parseRole(r), /Pare e avise/, r);
});

test('regras_do_projeto mostra o que veio da API e manda parar em 401/403', () => {
  const r = getProjectRules(AGENTE);
  assert.equal(r.papel, 'agente');
  assert.equal(r.projeto, 'canais-mobile');
  assert.deepEqual(r.status, TODOS);
  assert.deepEqual(r.transicoes_permitidas, AGENTE.allowedTransitions);
  assert.deepEqual(r.convencoes, AGENTE.conventions);
  assert.match(r.condicoes.join('\n'), /não concedem autorização humana/);
  assert.match(r.condicoes.join('\n'), /401\/403: pare e avise/);
  assert.ok(!JSON.stringify(r).includes('pmt_'), 'nenhum token nas regras');
});
