'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {
  parseExecutionProfile,
  loadExecutionProfile,
  resolveSafeWorkingDirectory,
  runProductGates
} = require('../lib/executor');
const { createEnvelope, saveTask, loadTask, STATES } = require('../lib/engine');
const { main } = require('../cli');

const repositoryRoot = path.resolve(__dirname, '..', '..', '..');
const profilePath = path.join(repositoryRoot, 'profiles', 'runrun-loop.yaml');

test('42 execution profile exposes the five active allowlisted product gates in order', () => {
  const profile = loadExecutionProfile(profilePath);
  assert.deepEqual(
    profile.gates.filter((gate) => gate.status === 'active').map((gate) => gate.id),
    ['backend_build', 'backend_test', 'frontend_build', 'frontend_test', 'frontend_lint']
  );
  assert.equal(profile.gate_execution.stop_on_first_gate_failure, true);
});

test('43 profile parser reads commands and booleans without a YAML runtime dependency', () => {
  const profile = parseExecutionProfile(`gates:\n  backend_build:\n    command: "dotnet build"\n    cwd: "."\n    required: true\n    status: "active"\ngate_execution:\n  stop_on_first_gate_failure: false\n`);
  assert.equal(profile.gates.backend_build.command, 'dotnet build');
  assert.equal(profile.gates.backend_build.required, true);
  assert.equal(profile.gate_execution.stop_on_first_gate_failure, false);
});

test('44 executor runs profile gates in order and stops at the first failure', async () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-loop-executor-'));
  fs.mkdirSync(path.join(temporaryRoot, 'web'));
  const temporaryProfile = path.join(temporaryRoot, 'profile.yaml');
  fs.writeFileSync(temporaryProfile, `gates:\n  backend_build:\n    command: "first"\n    cwd: "."\n    required: true\n    status: "active"\n  frontend_test:\n    command: "second"\n    cwd: "web"\n    required: true\n    status: "active"\n  frontend_lint:\n    command: "third"\n    cwd: "web"\n    required: true\n    status: "active"\ngate_execution:\n  stop_on_first_gate_failure: true\n`);
  const calls = [];
  const recorded = [];
  const result = await runProductGates({
    repositoryRoot: temporaryRoot,
    profilePath: temporaryProfile,
    runCommand: async (command, options) => {
      calls.push([command, path.relative(temporaryRoot, options.cwd)]);
      return { exitCode: command === 'second' ? 2 : 0, signal: null };
    },
    onResult: async (gate) => recorded.push(gate.gate)
  });
  assert.equal(result.ok, false);
  assert.equal(result.failed_gate, 'frontend_test');
  assert.deepEqual(calls, [['first', ''], ['second', 'web']]);
  assert.deepEqual(recorded, ['backend_build', 'frontend_test']);
});

test('45 executor rejects a gate cwd outside the repository', () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-loop-root-'));
  assert.throws(
    () => resolveSafeWorkingDirectory(temporaryRoot, '..'),
    /escapes repository root/
  );
});

test('46 executor rejects gate names not declared by the versioned profile', async () => {
  await assert.rejects(
    () => runProductGates({
      repositoryRoot,
      profilePath,
      gates: ['arbitrary_command'],
      runCommand: async () => ({ exitCode: 0, signal: null })
    }),
    /Unknown profile gates/
  );
});

test('47 run-gates CLI executes and persists an allowlisted gate result', async () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agent-loop-cli-'));
  const store = path.join(temporaryRoot, 'tasks');
  const telemetry = path.join(temporaryRoot, 'telemetry');
  fs.writeFileSync(path.join(temporaryRoot, 'gate-pass.js'), 'process.exit(0);\n');
  const temporaryProfile = path.join(temporaryRoot, 'profile.yaml');
  fs.writeFileSync(temporaryProfile, `gates:\n  backend_build:\n    command: "node gate-pass.js"\n    cwd: "."\n    required: true\n    status: "active"\ngate_execution:\n  stop_on_first_gate_failure: true\n`);
  const envelope = createEnvelope('TASK-EXECUTOR', 'Executor', 'Run a governed gate', {
    graph: { current_node: STATES.GATING },
    spec: { id: 'SPEC-EXECUTOR', status: 'approved' }
  });
  saveTask(envelope, store);

  const originalLog = console.log;
  console.log = () => {};
  try {
    const exitCode = await main([
      'run-gates', 'TASK-EXECUTOR',
      '--store', store,
      '--telemetry', telemetry,
      '--profile', temporaryProfile,
      '--repo-root', temporaryRoot
    ]);
    assert.equal(exitCode, 0);
  } finally {
    console.log = originalLog;
  }

  const persisted = loadTask('TASK-EXECUTOR', store);
  assert.equal(persisted.validation.gates.backend_build.result, 'pass');
  assert.equal(persisted.validation.gates.backend_build.details.exit_code, 0);
});
