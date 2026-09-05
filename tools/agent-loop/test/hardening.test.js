'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const engine = require('../lib/engine');
const { validateEnvelope } = require('../cli');

function temporaryTest(t, overrides = {}) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-loop-test-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  const tasksDir = path.join(root, 'tasks');
  const telemetryDir = path.join(root, 'telemetry');
  const envelope = engine.createEnvelope('task-1', 'Title', 'Objective', overrides);
  return { root, tasksDir, telemetryDir, envelope, options: { telemetryDir, agentId: 'test-agent' } };
}

function setNode(envelope, node) {
  envelope.graph.current_node = node;
  envelope.graph.previous_node = null;
  envelope.graph.history = [{ node, entered_at: new Date().toISOString() }];
  envelope.graph.blocked_from = null;
  envelope.graph.blocked_reason = null;
}

test('01 create produces complete envelope and JSON .yaml store', (t) => {
  const { envelope, tasksDir } = temporaryTest(t);
  const required = ['task', 'classification', 'graph', 'spec', 'context', 'changes', 'validation', 'loop', 'human_gates', 'status'];
  for (const field of required) assert.ok(Object.hasOwn(envelope, field));
  const file = engine.saveTask(envelope, tasksDir);
  assert.equal(path.extname(file), '.yaml');
  assert.deepEqual(JSON.parse(fs.readFileSync(file, 'utf8')), envelope);
});

test('02 invalid envelope schema is rejected by validation', (t) => {
  const { envelope } = temporaryTest(t);
  delete envelope.task;
  envelope.graph.current_node = null;
  const result = validateEnvelope(envelope);
  assert.equal(result.valid, false);
  assert.ok(result.missing.includes('task'));
  assert.ok(result.missing.includes('graph.current_node'));
});

test('03 valid graph edge succeeds', () => {
  assert.deepEqual(engine.transition(engine.STATES.IDLE, engine.STATES.SPEC_DRAFT), {
    ok: true,
    from: engine.STATES.IDLE,
    to: engine.STATES.SPEC_DRAFT
  });
});

test('04 invalid graph edge is rejected', () => {
  const result = engine.transition(engine.STATES.IDLE, engine.STATES.IMPLEMENTING);
  assert.equal(result.ok, false);
  assert.equal(result.code, engine.FAIL_CODES.EDGE_VIOLATION);
});

test('05 draft spec blocks implementation and requests G-SPEC', (t) => {
  const { envelope, options } = temporaryTest(t, { spec: { status: 'draft' } });
  setNode(envelope, engine.STATES.CONTEXT_LOADING);
  const result = engine.transition(envelope, engine.STATES.IMPLEMENTING, options);
  assert.equal(result.ok, false);
  assert.equal(envelope.graph.current_node, engine.STATES.BLOCKED);
  assert.equal(envelope.human_gates[0].type, engine.GATES.G_SPEC);
  assert.equal(envelope.human_gates[0].status, engine.GATE_STATES.PENDING);
});

test('06 human G-SPEC approval permits resume and implementation', (t) => {
  const { envelope, options } = temporaryTest(t, { spec: { status: 'draft' } });
  setNode(envelope, engine.STATES.CONTEXT_LOADING);
  engine.transition(envelope, engine.STATES.IMPLEMENTING, options);
  engine.resolveGate(envelope, engine.GATES.G_SPEC, 'approved', 'PO', null, options);
  assert.equal(engine.resume(envelope, options).ok, true);
  assert.equal(engine.transition(envelope, engine.STATES.IMPLEMENTING, options).ok, true);
});

test('07 agent, engine and absent resolver cannot approve', (t) => {
  const { envelope, options } = temporaryTest(t);
  engine.requestHumanGate(envelope, engine.GATES.G_SPEC, 'Approval', options);
  assert.throws(() => engine.resolveGate(envelope, engine.GATES.G_SPEC, 'approved', 'agent', null, options));
  assert.throws(() => engine.resolveGate(envelope, engine.GATES.G_SPEC, 'approved', 'engine', null, options));
  assert.throws(() => engine.resolveGate(envelope, engine.GATES.G_SPEC, 'approved', null, null, options));
  assert.equal(envelope.human_gates[0].status, engine.GATE_STATES.PENDING);
});

test('08 multiple simultaneous gates and historical resolutions are retained', (t) => {
  const { envelope, options } = temporaryTest(t);
  engine.requestHumanGate(envelope, engine.GATES.G_MIGRATION, 'Migration', options);
  engine.requestHumanGate(envelope, engine.GATES.G_WORKFLOW, 'Workflow', options);
  engine.resolveGate(envelope, engine.GATES.G_MIGRATION, 'approved', 'PO', null, options);
  engine.requestHumanGate(envelope, engine.GATES.G_MIGRATION, 'Second migration', options);
  assert.equal(envelope.human_gates.length, 3);
  assert.equal(engine.pendingGates(envelope).length, 2);
  assert.equal(envelope.human_gates[0].status, engine.GATE_STATES.APPROVED);
});

test('09 migration gate pauses until explicit human resolution', (t) => {
  const { envelope, options } = temporaryTest(t);
  const gate = engine.requestHumanGate(envelope, engine.GATES.G_MIGRATION, 'Schema change', options);
  assert.equal(envelope.status, engine.TASK_STATUSES.PAUSED);
  assert.equal(engine.resolveGate(envelope, gate.id, 'approved', 'DBA Human', null, options).status, 'approved');
});

test('10 workflow gate is recognized and resolvable', (t) => {
  const { envelope, options } = temporaryTest(t);
  const gate = engine.requestHumanGate(envelope, engine.GATES.G_WORKFLOW, 'Workflow change', options);
  assert.equal(gate.type, 'G-WORKFLOW');
  assert.equal(engine.resolveGate(envelope, gate.id, 'approved', 'Lead Architect', null, options).resolved_by, 'Lead Architect');
});

test('11 FAIL_CODE increments attempts and signature count', (t) => {
  const { envelope, options } = temporaryTest(t);
  engine.fail(envelope, 'FAIL_CODE', { signature: 'compile-a' }, options);
  assert.equal(envelope.loop.attempts, 1);
  assert.equal(envelope.loop.repeated_failure_count['compile-a'], 1);
});

test('12 repeated failure signature blocks', (t) => {
  const { envelope, options } = temporaryTest(t, { loop: { max_attempts: 5 } });
  setNode(envelope, engine.STATES.IMPLEMENTING);
  assert.equal(engine.fail(envelope, 'FAIL_CODE', { signature: 'same' }, options).blocked, false);
  assert.equal(engine.fail(envelope, 'FAIL_CODE', { signature: 'same' }, options).blocked, true);
  assert.equal(envelope.graph.current_node, engine.STATES.BLOCKED);
});

test('13 configured max attempts blocks distinct failures', (t) => {
  const { envelope, options } = temporaryTest(t, { loop: { max_attempts: 2 } });
  setNode(envelope, engine.STATES.IMPLEMENTING);
  engine.fail(envelope, 'FAIL_CODE', { signature: 'one' }, options);
  const result = engine.fail(envelope, 'FAIL_CODE', { signature: 'two' }, options);
  assert.equal(result.blocked, true);
  assert.match(envelope.graph.blocked_reason, /Maximum attempts/);
});

test('14 FAIL_EXTERNAL blocks immediately', (t) => {
  const { envelope, options } = temporaryTest(t);
  setNode(envelope, engine.STATES.IMPLEMENTING);
  const result = engine.fail(envelope, 'FAIL_EXTERNAL', { message: 'service unavailable' }, options);
  assert.equal(result.blocked, true);
  assert.equal(result.external, true);
  assert.equal(envelope.graph.current_node, engine.STATES.BLOCKED);
});

test('15 external failure does not increment attempts or retry count', (t) => {
  const { envelope, options } = temporaryTest(t);
  engine.fail(envelope, 'FAIL_EXTERNAL', { signature: 'outside' }, options);
  assert.equal(envelope.loop.attempts, 0);
  assert.equal(envelope.loop.repeated_failure_count.outside, undefined);
});

test('16 block preserves origin and reason when already blocked', (t) => {
  const { envelope, options } = temporaryTest(t);
  setNode(envelope, engine.STATES.GATING);
  engine.block(envelope, 'first reason', options);
  engine.block(envelope, 'second reason', options);
  assert.equal(envelope.graph.blocked_from, engine.STATES.GATING);
  assert.equal(envelope.graph.blocked_reason, 'second reason');
});

test('17 resume returns to prior allowed node without pending gates', (t) => {
  const { envelope, options } = temporaryTest(t, { spec: { status: 'approved' } });
  setNode(envelope, engine.STATES.IMPLEMENTING);
  engine.block(envelope, 'manual pause', options);
  const result = engine.resume(envelope, options);
  assert.equal(result.ok, true);
  assert.equal(envelope.graph.current_node, engine.STATES.IMPLEMENTING);
});

test('18 known gate recording stores PASS result', (t) => {
  const { envelope, options } = temporaryTest(t);
  const result = engine.recordGate(envelope, 'backend_build', 'pass', { duration_ms: 10 }, options);
  assert.equal(result.ok, true);
  assert.equal(envelope.validation.gates.backend_build.result, engine.VALIDATION_RESULTS.PASS);
});

test('19 unknown gate is coverage gap and never PASS', (t) => {
  const { envelope, options } = temporaryTest(t);
  const result = engine.recordGate(envelope, 'invented_gate', 'pass', {}, options);
  assert.equal(result.ok, false);
  assert.equal(result.result, engine.VALIDATION_RESULTS.COVERAGE_GAP);
  assert.notEqual(envelope.validation.gates.invented_gate.result, engine.VALIDATION_RESULTS.PASS);
});

test('20 telemetry is append-only JSONL', (t) => {
  const { envelope, telemetryDir, options } = temporaryTest(t);
  engine.appendEvent(envelope.task_id, 'first', { sequence: 1 }, options);
  const file = engine.telemetryFilePath(telemetryDir, envelope.task_id);
  const first = fs.readFileSync(file, 'utf8');
  engine.appendEvent(envelope.task_id, 'second', { sequence: 2 }, options);
  const second = fs.readFileSync(file, 'utf8');
  assert.ok(second.startsWith(first));
  assert.deepEqual(engine.readEvents(envelope.task_id, telemetryDir).map((event) => event.event_type), ['first', 'second']);
});

test('21 checkpoint records sanitized state and event', (t) => {
  const { envelope, telemetryDir, options } = temporaryTest(t);
  const result = engine.checkpoint(envelope, 'ready', { stage: 'local' }, options);
  assert.equal(result.label, 'ready');
  assert.equal(result.checkpoint.stage, 'local');
  assert.equal(engine.readEvents(envelope.task_id, telemetryDir)[0].event_type, 'checkpoint');
});

test('22 checkpoint persists no secrets, .env or chain-of-thought', (t) => {
  const { envelope, telemetryDir, options } = temporaryTest(t);
  engine.checkpoint(envelope, 'safe', {
    token: 'raw-token-value',
    password: 'raw-password-value',
    credential: 'raw-credential-value',
    file: '.env.local',
    reasoning: 'chain-of-thought private'
  }, options);
  const raw = fs.readFileSync(engine.telemetryFilePath(telemetryDir, envelope.task_id), 'utf8');
  for (const forbidden of ['raw-token-value', 'raw-password-value', 'raw-credential-value', '.env.local', 'chain-of-thought']) {
    assert.equal(raw.includes(forbidden), false);
  }
});

test('23 human completion gate blocks review to done until approved', (t) => {
  const { envelope, options } = temporaryTest(t, { loop: { human_completion_required: true } });
  setNode(envelope, engine.STATES.HUMAN_REVIEW);
  const blocked = engine.transition(envelope, engine.STATES.MERGED, options);
  assert.equal(blocked.ok, false);
  assert.equal(envelope.human_gates[0].type, engine.GATES.G_COMPLETION);
  engine.resolveGate(envelope, engine.GATES.G_COMPLETION, 'approved', 'PO', null, options);
  assert.equal(engine.resume(envelope, options).ok, true);
  assert.equal(engine.transition(envelope, engine.STATES.MERGED, options).ok, true);
});

test('24 .agent-state runtime is ignored or structurally separated', (t) => {
  const repositoryRoot = path.resolve(__dirname, '..', '..', '..');
  const marker = path.join(repositoryRoot, '.agent-state', 'tasks', 'runtime-test.yaml');
  let ignored = false;
  try {
    execFileSync('git', ['check-ignore', '-q', marker], { cwd: repositoryRoot, stdio: 'ignore' });
    ignored = true;
  } catch {
    const { tasksDir, telemetryDir } = temporaryTest(t);
    ignored = !tasksDir.includes(`${path.sep}schemas${path.sep}`) &&
      !telemetryDir.includes(`${path.sep}schemas${path.sep}`);
  }
  assert.equal(ignored, true);
});

test('25 schemas are versioned under tools and absent from runtime schema path', (t) => {
  const toolRoot = path.resolve(__dirname, '..');
  const schemas = ['task-envelope.yaml', 'telemetry-event.yaml', 'handoff.yaml'];
  for (const schema of schemas) {
    assert.equal(fs.existsSync(path.join(toolRoot, 'schemas', schema)), true);
  }
  const { root } = temporaryTest(t);
  assert.equal(fs.existsSync(path.join(root, '.agent-state', 'schema')), false);
});
