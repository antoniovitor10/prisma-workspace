'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { readArtifacts } = require('../lib/read-artifacts');
const canonical = require('../lib/canonical');

const artifacts = readArtifacts();

test('contextIndex has 13 domains', () => {
  assert.equal(Object.keys(artifacts.contextIndex.domains).length, 13);
});

test('specs: canonical collection and template are separate', () => {
  assert.equal(artifacts.specs.canonicalSpecs.length, canonical.canonicalSpecs);
  assert.equal(
    artifacts.specs.canonicalSpecs.some((spec) => spec.path.endsWith('_template.md')),
    false
  );
  assert.equal(artifacts.specs.template.path, 'specs/_template.md');
  assert.equal(artifacts.specs.template.isTemplate, true);
  assert.equal(artifacts.specs.template.exists, true);
  for (const spec of artifacts.specs.canonicalSpecs) {
    assert.equal(typeof spec.id, 'string');
    assert.equal(typeof spec.title, 'string');
    assert.equal(typeof spec.status, 'string');
    assert.equal(typeof spec.path, 'string');
    assert.equal(typeof spec.body, 'string');
    assert.ok(spec.body.length > 0);
    assert.ok(Array.isArray(spec.sections));
    assert.ok(spec.sections.length > 0);
  }
});

test('backlog has the canonical tasks with normalized spec references', () => {
  assert.equal(artifacts.backlog.tasks.length, canonical.canonicalTasks);
  for (const t of artifacts.backlog.tasks) {
    assert.equal(typeof t.id, 'string');
    assert.equal(typeof t.specId, 'string');
    assert.equal(typeof t.specPath, 'string');
    assert.ok(Array.isArray(t.dependencies));
    assert.ok(Array.isArray(t.affected_areas));
    assert.ok(Array.isArray(t.gates));
  }
  const first = artifacts.backlog.tasks[0];
  assert.equal(first.id, 'TASK-001');
  assert.equal(first.specId, 'SPEC-PROJECT-STRUCTURE');
  assert.equal(first.specPath, 'specs/project-structure.md');
});

test('agents: 4 agents present', () => {
  assert.equal(artifacts.agents.length, 4);
  const ids = artifacts.agents.map((a) => a.id).sort();
  assert.deepEqual(ids, ['discovery', 'executor', 'planner', 'reviewer']);
});

test('processGraph: 16 nodes, 21 edges, 7 human gates', () => {
  assert.equal(Object.keys(artifacts.processGraph.nodes).length, 16);
  assert.equal(artifacts.processGraph.edges.length, 21);
  assert.equal(Object.keys(artifacts.processGraph.humanGates).length, 7);
});

test('engineGraph: 11 states, 21 edges, 7 gates', () => {
  assert.equal(artifacts.engineGraph.states.length, 11);
  const totalEdges = Object.values(artifacts.engineGraph.edges).reduce(
    (sum, arr) => sum + arr.length,
    0
  );
  assert.equal(totalEdges, 21);
  assert.equal(artifacts.engineGraph.gates.length, 7);
  assert.equal(artifacts.engineGraph.maxAttempts, 3);
});

test('loopProfile: max_attempts 3, stop_on_first_gate_failure true', () => {
  assert.equal(artifacts.loopProfile.loop.max_attempts, 3);
  assert.equal(artifacts.loopProfile.gateExecution.stop_on_first_gate_failure, true);
});

test('actualContext is an array', () => {
  assert.ok(Array.isArray(artifacts.actualContext));
});

test('omniroute readiness file exists and has text', () => {
  assert.equal(artifacts.omniroute.path, 'OMNIROUTE-READINESS.md');
  assert.equal(artifacts.omniroute.exists, true);
  assert.equal(typeof artifacts.omniroute.text, 'string');
});
