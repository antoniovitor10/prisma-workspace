'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { STATES } = require('../lib/constants');
const { transition, isValidState, isTerminal } = require('../lib/engine');

test('isValidState recognizes known states', () => {
  assert.equal(isValidState(STATES.IDLE), true);
  assert.equal(isValidState('not-a-state'), false);
});

test('isTerminal flags merged and failed as terminal', () => {
  assert.equal(isTerminal(STATES.MERGED), true);
  assert.equal(isTerminal(STATES.FAILED), true);
  assert.equal(isTerminal(STATES.IDLE), false);
});

test('transition allows idle -> spec-draft', () => {
  const result = transition(STATES.IDLE, STATES.SPEC_DRAFT);
  assert.equal(result.ok, true);
  assert.equal(result.to, STATES.SPEC_DRAFT);
});

test('transition rejects skipping states (idle -> implementing)', () => {
  const result = transition(STATES.IDLE, STATES.IMPLEMENTING);
  assert.equal(result.ok, false);
  assert.equal(result.code, 'EDGE_VIOLATION');
});

test('transition rejects moving out of a terminal state', () => {
  const result = transition(STATES.MERGED, STATES.IMPLEMENTING);
  assert.equal(result.ok, false);
  assert.equal(result.code, 'EDGE_VIOLATION');
});

test('transition rejects unknown states', () => {
  const result = transition('bogus', STATES.SPEC_DRAFT);
  assert.equal(result.ok, false);
  assert.equal(result.code, 'VALIDATION_ERROR');
});

test('human-review can go back to implementing on rejection', () => {
  const result = transition(STATES.HUMAN_REVIEW, STATES.IMPLEMENTING);
  assert.equal(result.ok, true);
});
