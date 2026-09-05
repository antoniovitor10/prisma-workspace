'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { buildModel } = require('../lib/build-model');
const canonical = require('../lib/canonical');

const model = buildModel();

test('buildModel gera um objeto com as chaves esperadas', () => {
  assert.equal(typeof model, 'object');
  assert.ok(model.generatedAt);
  assert.ok(model.canonical);
  assert.ok(model.artifactsSummary);
  assert.ok(model.selections);
  assert.ok(Array.isArray(model.userStories));
  assert.ok(model.context);
  assert.ok(model.previews);
  assert.ok(model.graphs);
  assert.ok(model.contextGraph);
  assert.ok(model.loop);
  assert.ok(model.architecture);
  assert.ok(model.readiness);
  assert.ok(Array.isArray(model.gaps));
});

test('artifactsSummary reflete contagens canônicas', () => {
  assert.equal(model.artifactsSummary.domains, 13);
  assert.equal(model.artifactsSummary.specs.canonical, canonical.canonicalSpecs);
  assert.equal(model.artifactsSummary.tasks, canonical.canonicalTasks);
  assert.equal(model.artifactsSummary.agents, 4);
  assert.equal(model.artifactsSummary.process.nodes, 16);
  assert.equal(model.artifactsSummary.process.edges, 21);
  assert.equal(model.artifactsSummary.engine.states, 11);
  assert.equal(model.artifactsSummary.engine.edges, 21);
  assert.equal(model.artifactsSummary.humanGates, 7);
});

test('selections tem specs e tasks canônicas e 13 domínios com id/label', () => {
  assert.equal(model.selections.specs.length, canonical.canonicalSpecs);
  assert.equal(model.selections.tasks.length, canonical.canonicalTasks);
  assert.equal(model.selections.domains.length, 13);

  for (const item of [...model.selections.specs, ...model.selections.tasks, ...model.selections.domains]) {
    assert.equal(typeof item.id, 'string');
    assert.equal(typeof item.label, 'string');
  }
});

test('context tem domínios, specs e tasks com minimum/expanded/categoryStates', () => {
  assert.equal(model.context.domainContexts.length, 13);
  assert.equal(model.context.specContexts.length, canonical.canonicalSpecs);
  assert.equal(model.context.taskContexts.length, canonical.canonicalTasks);

  for (const item of [
    ...model.context.domainContexts,
    ...model.context.specContexts,
    ...model.context.taskContexts
  ]) {
    assert.ok(Array.isArray(item.minimum));
    assert.ok(Array.isArray(item.expanded));
    assert.equal(typeof item.categoryStates, 'object');
  }
});

test('previews tem as tasks canônicas simuladas, incluindo TASK-009', () => {
  assert.equal(model.previews.tasks.length, canonical.canonicalTasks);
  const task009 = model.previews.tasks.find((t) => t.taskId === 'TASK-009');
  assert.ok(task009);
  assert.equal(task009.simulated, true);
  assert.equal(task009.domain, 'workflow');
});

test('contextGraph (TASK-009) só possui relations permitidas', () => {
  const allowedRelations = new Set([
    'governed_by',
    'implemented_by',
    'tested_by',
    'validated_by',
    'depends_on'
  ]);

  assert.ok(model.contextGraph.edges.length > 0);
  for (const edge of model.contextGraph.edges) {
    assert.ok(allowedRelations.has(edge.relation));
  }
});

test('gaps não contém canonical_mismatch', () => {
  const mismatches = model.gaps.filter((gap) => gap.type === 'canonical_mismatch');
  assert.deepEqual(mismatches, []);
});

test('loop reflete valores da política do profile', () => {
  assert.equal(model.loop.profile.loop.max_attempts, 3);
  assert.equal(model.loop.profile.loop.retry_on_fail_code, true);
  assert.equal(model.loop.profile.loop.block_on_repeated_failure, 2);
  assert.equal(model.loop.profile.loop.external_failure_counts_as_attempt, false);
  assert.equal(model.loop.profile.loop.block_on_external_failure, true);
  assert.equal(model.loop.gateExecution.stop_on_first_gate_failure, true);
});

test('architecture está marcada como conceptual: true', () => {
  assert.equal(model.architecture.conceptual, true);
  assert.ok(Array.isArray(model.architecture.nodes));
  assert.ok(Array.isArray(model.architecture.edges));
});

test('model expõe contratos reais de agentes sem fallback', () => {
  assert.ok(Array.isArray(model.agents));
  assert.ok(model.agents.length >= 4);
  assert.deepEqual(
    model.agents.map((agent) => agent.id).sort(),
    ['discovery', 'executor', 'planner', 'reviewer']
  );
  for (const agent of model.agents) {
    assert.ok(agent.role);
    assert.ok(agent.description);
    assert.ok(agent.capability);
    assert.ok(agent.context);
    assert.ok(agent.tools);
    assert.ok(agent.outputs);
    assert.ok(agent.constraints);
    assert.doesNotMatch(agent.id, /^agent-\d+$/);
  }
});

test('model expõe specs completas com body e sections', () => {
  assert.equal(model.specs.length, canonical.canonicalSpecs);
  assert.equal(model.specDetails.length, canonical.canonicalSpecs);
  for (const spec of model.specs) {
    assert.equal(typeof spec.body, 'string');
    assert.ok(spec.body.length > 0);
    assert.ok(Array.isArray(spec.sections));
    assert.ok(spec.sections.length > 0);
  }
});

test('loop graph usa labels descritivos diferentes dos ids', () => {
  const graph = model.graphs.loopGraph;
  assert.equal(graph.nodes.length, 6);
  for (const node of graph.nodes) {
    assert.ok(node.label);
    assert.notEqual(node.label, node.id);
    assert.ok(node.description);
  }
  for (const edge of graph.edges) assert.ok(edge.label);
  assert.ok(graph.edges.some((edge) => edge.from === 'FAIL_EXTERNAL' && edge.to === 'BLOCKED'));
});

test('todos os gaps possuem explanation', () => {
  assert.ok(model.gaps.length > 0);
  for (const gap of model.gaps) {
    assert.equal(typeof gap.explanation, 'string');
    assert.ok(gap.explanation.length > 0);
  }
});
