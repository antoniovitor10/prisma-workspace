#!/usr/bin/env node
'use strict';

const {
  STATES,
  GATE_STATES,
  createEnvelope,
  saveTask,
  loadTask,
  appendEvent,
  transition,
  advance,
  recordGate,
  fail,
  block,
  resume,
  checkpoint,
  requestHumanGate,
  resolveGate,
  pendingGates,
  runProductGates,
  FAIL_CODES
} = require('./lib/engine');
const path = require('node:path');

function usage() {
  return [
    'Usage: node cli.js <command> [arguments] [--store DIR] [--telemetry DIR] [--agent ID] [--profile FILE] [--repo-root DIR]',
    '',
    'Commands:',
    '  create <id> <title> <objective> [overrides-json]',
    '  validate <id>',
    '  status <id>',
    '  advance <id> [to-state]',
    '  transition <id> <to-state>',
    '  record-gate <id> <gate> <pass|fail> [details-json]',
    '  fail <id> <code> [details-json]',
    '  block <id> <reason>',
    '  resume <id>',
    '  request-human-gate <id> <gate> <reason>',
    '  approve <id> <gate-or-id> <resolved-by> [note]',
    '  reject <id> <gate-or-id> <resolved-by> [note]',
    '  checkpoint <id> [label] [data-json]',
    '  run-gates <id> [gate1,gate2,...]',
    '  states',
    '  help'
  ].join('\n');
}

function parseArgs(argv) {
  const options = {};
  const args = [];
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === '--store' || value === '--telemetry' || value === '--agent' || value === '--profile' || value === '--repo-root') {
      if (!argv[index + 1]) {
        throw new Error(`${value} requires a value`);
      }
      options[value.slice(2)] = argv[index + 1];
      index += 1;
    } else {
      args.push(value);
    }
  }
  return { args, options };
}

function parseJson(value, fallback = {}) {
  if (value === undefined) {
    return fallback;
  }
  try {
    return JSON.parse(value);
  } catch (error) {
    throw new Error(`Invalid JSON argument: ${error.message}`);
  }
}

function operationOptions(options) {
  return {
    telemetryDir: options.telemetry,
    agentId: options.agent || 'cli'
  };
}

function requireTask(id, options) {
  if (!id) {
    throw new Error('Task id is required');
  }
  const envelope = loadTask(id, options.store);
  if (!envelope) {
    throw new Error(`Task not found: ${id}`);
  }
  return envelope;
}

function persist(envelope, options) {
  saveTask(envelope, options.store);
  return envelope;
}

function validateEnvelope(envelope) {
  const required = [
    'task',
    'classification',
    'graph',
    'spec',
    'context',
    'changes',
    'validation',
    'loop',
    'human_gates',
    'status'
  ];
  const missing = required.filter((field) => envelope[field] === undefined || envelope[field] === null);
  if (!envelope.task_id) {
    missing.push('task_id');
  }
  if (!envelope.graph || !envelope.graph.current_node) {
    missing.push('graph.current_node');
  }
  return { valid: missing.length === 0, missing };
}

async function main(argv) {
  const parsed = parseArgs(argv);
  const [command, ...args] = parsed.args;
  const options = parsed.options;
  const op = operationOptions(options);

  if (!command || command === 'help' || command === '--help' || command === '-h') {
    console.log(usage());
    return 0;
  }

  if (command === 'states') {
    console.log(Object.values(STATES).join('\n'));
    return 0;
  }

  if (command === 'create') {
    const [id, title, objective, overridesJson] = args;
    if (!id || title === undefined || objective === undefined) {
      throw new Error('create requires <id> <title> <objective>');
    }
    const envelope = createEnvelope(id, title, objective, parseJson(overridesJson));
    persist(envelope, options);
    appendEvent(id, 'task_created', { title, objective }, {
      telemetryDir: options.telemetry,
      agentId: op.agentId,
      humanGates: envelope.human_gates
    });
    console.log(JSON.stringify(envelope, null, 2));
    return 0;
  }

  const id = args[0];
  const envelope = requireTask(id, options);

  if (command === 'validate') {
    const result = validateEnvelope(envelope);
    appendEvent(id, 'envelope_validated', result, {
      telemetryDir: options.telemetry,
      agentId: op.agentId,
      humanGates: envelope.human_gates
    });
    console.log(JSON.stringify(result, null, 2));
    return result.valid ? 0 : 1;
  }

  if (command === 'status') {
    const result = {
      task_id: id,
      status: envelope.status,
      node: envelope.graph.current_node,
      blocked_from: envelope.graph.blocked_from,
      blocked_reason: envelope.graph.blocked_reason,
      pending_gates: envelope.human_gates.filter((gate) => gate.status === GATE_STATES.PENDING)
    };
    appendEvent(id, 'status_read', result, {
      telemetryDir: options.telemetry,
      agentId: op.agentId,
      humanGates: envelope.human_gates
    });
    console.log(JSON.stringify(result, null, 2));
    return 0;
  }

  if (command === 'run-gates') {
    if (envelope.graph.current_node !== STATES.GATING) {
      const result = {
        ok: false,
        code: FAIL_CODES.EDGE_VIOLATION,
        message: `Product gates can run only in state '${STATES.GATING}'`
      };
      console.log(JSON.stringify(result, null, 2));
      return 1;
    }
    const unresolved = pendingGates(envelope);
    if (unresolved.length) {
      const result = {
        ok: false,
        code: FAIL_CODES.GATE_FAILURE,
        message: `Pending human gates: ${unresolved.map((gate) => gate.type).join(', ')}`
      };
      console.log(JSON.stringify(result, null, 2));
      return 1;
    }

    const repositoryRoot = path.resolve(options['repo-root'] || path.join(__dirname, '..', '..'));
    const profilePath = path.resolve(options.profile || path.join(repositoryRoot, 'profiles', 'runrun-loop.yaml'));
    const selectedGates = args[1] ? args[1].split(',').map((gate) => gate.trim()).filter(Boolean) : null;
    const summary = await runProductGates({
      repositoryRoot,
      profilePath,
      gates: selectedGates,
      onResult: async (gateResult) => {
        recordGate(envelope, gateResult.gate, gateResult.result, {
          exit_code: gateResult.exit_code,
          signal: gateResult.signal,
          duration_ms: gateResult.duration_ms,
          cwd: gateResult.cwd
        }, op);
        persist(envelope, options);
      }
    });

    if (!summary.ok) {
      const failed = summary.results.find((gate) => gate.result === 'fail');
      summary.loop_failure = fail(envelope, FAIL_CODES.GATE_FAILURE, {
        signature: `product-gate:${failed.gate}:${failed.exit_code}`,
        gate: failed.gate,
        exit_code: failed.exit_code
      }, op);
    }
    persist(envelope, options);
    console.log(JSON.stringify(summary, null, 2));
    return summary.ok ? 0 : 1;
  }

  let result;
  if (command === 'advance') {
    result = advance(envelope, args[1], op);
  } else if (command === 'transition') {
    if (!args[1]) throw new Error('transition requires <to-state>');
    result = transition(envelope, args[1], op);
  } else if (command === 'record-gate') {
    if (!args[1] || !args[2]) throw new Error('record-gate requires <gate> <pass|fail>');
    result = recordGate(envelope, args[1], args[2], parseJson(args[3]), op);
  } else if (command === 'fail') {
    if (!args[1]) throw new Error('fail requires <code>');
    result = fail(envelope, args[1], parseJson(args[2]), op);
  } else if (command === 'block') {
    if (!args[1]) throw new Error('block requires <reason>');
    result = block(envelope, args[1], op);
  } else if (command === 'resume') {
    result = resume(envelope, op);
  } else if (command === 'request-human-gate') {
    if (!args[1] || !args[2]) throw new Error('request-human-gate requires <gate> <reason>');
    result = requestHumanGate(envelope, args[1], args[2], op);
  } else if (command === 'approve' || command === 'reject') {
    if (!args[1] || !args[2]) throw new Error(`${command} requires <gate-or-id> <resolved-by>`);
    result = resolveGate(
      envelope,
      args[1],
      command === 'approve' ? GATE_STATES.APPROVED : GATE_STATES.REJECTED,
      args[2],
      args[3] === undefined ? null : args[3],
      op
    );
  } else if (command === 'checkpoint') {
    result = checkpoint(envelope, args[1] || null, parseJson(args[2]), op);
  } else {
    throw new Error(`Unknown command: ${command}`);
  }

  persist(envelope, options);
  console.log(JSON.stringify(result, null, 2));
  return result && result.ok === false ? 1 : 0;
}

if (require.main === module) {
  main(process.argv.slice(2))
    .then((code) => { process.exitCode = code; })
    .catch((error) => {
      console.error(error.message);
      process.exitCode = 1;
    });
}

module.exports = { main, usage, parseArgs, validateEnvelope };
