'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { readArtifacts } = require('../lib/read-artifacts');
const {
  REASONS,
  deriveContextForDomain,
  computeCategoryStates,
  previewTask,
  buildContextGraph
} = require('../lib/derive-context');

const artifacts = readArtifacts();

test('deriveContextForDomain(dependencies) minimum has stories, spec, source code, tests and gates', () => {
  const result = deriveContextForDomain('dependencies', artifacts);
  const paths = result.candidates.map((c) => c.path);

  assert.ok(paths.includes('AGENTS.md'));
  assert.ok(paths.includes('context/index.yaml'));
  assert.ok(paths.includes('stories/catalog.json'));
  assert.ok(paths.includes('specs/dependencies.md'));

  assert.ok(result.candidates.filter((c) => c.category === 'Source Code').length >= 1);
  assert.ok(result.candidates.filter((c) => c.category === 'Tests').length >= 1);
  assert.ok(result.candidates.filter((c) => c.category === 'Gates').length >= 1);
  assert.ok(result.candidates.filter((c) => c.category === 'Story').length >= 1);
});

test('previewTask(TASK-009) has simulated true, domain workflow, spec SPEC-WORKFLOW-STATUS', () => {
  const preview = previewTask('TASK-009', artifacts);
  assert.equal(preview.simulated, true);
  assert.equal(preview.domain, 'workflow');
  assert.equal(preview.spec, 'SPEC-WORKFLOW-STATUS');
});

test('every candidate reason belongs to REASONS', () => {
  const domainsToCheck = ['dependencies', 'workflow', 'history', 'projects', 'work_items', 'backlog', 'sprints', 'access'];
  for (const domainName of domainsToCheck) {
    const result = deriveContextForDomain(domainName, artifacts);
    for (const candidate of result.candidates) {
      assert.ok(
        REASONS.includes(candidate.reason),
        `reason "${candidate.reason}" for candidate ${candidate.path} not in REASONS`
      );
    }
  }

  const preview = previewTask('TASK-009', artifacts);
  for (const candidate of [...preview.minimum, ...preview.expanded]) {
    assert.ok(REASONS.includes(candidate.reason));
  }
});

test('buildContextGraph for TASK-009 preview only has governed_by/implemented_by/tested_by/validated_by/depends_on relations', () => {
  const allowedRelations = new Set([
    'governed_by',
    'implemented_by',
    'tested_by',
    'validated_by',
    'depends_on'
  ]);

  const task = artifacts.backlog.tasks.find((t) => t.id === 'TASK-009');
  const spec = artifacts.specs.canonicalSpecs.find((s) => s.id === task.specId) || null;
  const domainResult = deriveContextForDomain('workflow', artifacts);

  const graph = buildContextGraph({
    task,
    spec,
    domain: 'workflow',
    candidates: domainResult.candidates
  });

  assert.ok(graph.edges.length > 0);
  for (const edge of graph.edges) {
    assert.ok(allowedRelations.has(edge.relation));
  }
});

test('deriveContextForDomain with invalid domain brings at least categoryStates UNKNOWN', () => {
  const result = deriveContextForDomain('nao-existe-domain', artifacts);
  assert.equal(result.domainValid, false);

  const states = computeCategoryStates(result);
  const unknownCategories = Object.values(states).filter((state) => state === 'UNKNOWN');
  assert.ok(unknownCategories.length >= 1);
});
