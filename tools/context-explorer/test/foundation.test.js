'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { repoPath, fileExists } = require('../lib/paths');
const { parseYaml } = require('../lib/yaml-lite');
const { extractYamlBlocks, parseSpecHeader } = require('../lib/md-blocks');
const canonical = require('../lib/canonical');

test('repoPath resolves to a file that exists (AGENTS.md)', () => {
  assert.equal(fileExists('AGENTS.md'), true);
  const fs = require('fs');
  assert.equal(fs.existsSync(repoPath('AGENTS.md')), true);
});

test('parseYaml parses a map', () => {
  const result = parseYaml('a: 1\nb: two\n');
  assert.deepEqual(result, { a: 1, b: 'two' });
});

test('parseYaml parses a list', () => {
  const result = parseYaml('items:\n  - one\n  - two\n  - three\n');
  assert.deepEqual(result, { items: ['one', 'two', 'three'] });
});

test('parseYaml parses an inline array', () => {
  const result = parseYaml('items: ["a", "b", "c"]\n');
  assert.deepEqual(result, { items: ['a', 'b', 'c'] });
});

test('parseYaml parses booleans', () => {
  const result = parseYaml('yes_flag: true\nno_flag: false\n');
  assert.deepEqual(result, { yes_flag: true, no_flag: false });
});

test('extractYamlBlocks extracts exactly one yaml block', () => {
  const markdown = [
    '# Title',
    '',
    'Some text.',
    '',
    '```yaml',
    'foo: bar',
    'baz: 1',
    '```',
    '',
    'More text.'
  ].join('\n');
  const blocks = extractYamlBlocks(markdown);
  assert.equal(blocks.length, 1);
  assert.equal(blocks[0], 'foo: bar\nbaz: 1');
});

test('parseSpecHeader extracts id, title and status', () => {
  const text = [
    '# SPEC-WORK-ITEMS: Ocultar Story Points para Metodologia Kanban na UI',
    '',
    '**Status:** draft',
    '',
    '## Objective'
  ].join('\n');
  const header = parseSpecHeader(text);
  assert.equal(header.id, 'SPEC-WORK-ITEMS');
  assert.equal(header.title, 'Ocultar Story Points para Metodologia Kanban na UI');
  assert.equal(header.status, 'draft');
});

test('canonical constants match expected counts', () => {
  assert.equal(canonical.processNodes, 16);
  assert.equal(canonical.processEdges, 21);
  assert.equal(canonical.engineStates, 11);
  assert.equal(canonical.engineEdges, 21);
  assert.equal(canonical.humanGates.length, 7);
  assert.equal(canonical.maxAttempts, 3);
  assert.equal(canonical.stopOnFirstGateFailure, true);
  assert.equal(canonical.canonicalSpecs, 39);
  assert.equal(canonical.canonicalTasks, 41);
  assert.equal(canonical.loopEngineTests, 47);
});
