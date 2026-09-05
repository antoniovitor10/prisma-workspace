'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { detectGaps } = require('../lib/detect-gaps');
const { readArtifacts } = require('../lib/read-artifacts');

const artifacts = readArtifacts();
const gaps = detectGaps(artifacts);

test('detectGaps returns an array', () => {
  assert.ok(Array.isArray(gaps));
});

test('no gap has type canonical_mismatch in the current state', () => {
  const mismatches = gaps.filter((gap) => gap.type === 'canonical_mismatch');
  assert.deepEqual(mismatches, []);
});

test('broken_path gaps, if present, form a valid array; absent is also fine', () => {
  const brokenPaths = gaps.filter((gap) => gap.type === 'broken_path');
  assert.ok(Array.isArray(brokenPaths));
  for (const gap of brokenPaths) {
    assert.equal(typeof gap.subject, 'string');
    assert.equal(typeof gap.detail, 'string');
    assert.equal(typeof gap.source, 'string');
    assert.equal(typeof gap.severity, 'string');
  }
});

test('there is exactly 1 verification_note gap', () => {
  const notes = gaps.filter((gap) => gap.type === 'verification_note');
  assert.equal(notes.length, 1);
});

test('every gap includes a clear explanation and path metadata', () => {
  for (const gap of gaps) {
    assert.equal(typeof gap.explanation, 'string');
    assert.ok(gap.explanation.length > 0);
    assert.ok(Object.prototype.hasOwnProperty.call(gap, 'path'));
    assert.ok(Object.prototype.hasOwnProperty.call(gap, 'resolvedPath'));
    assert.equal(typeof gap.reason, 'string');
  }
});
