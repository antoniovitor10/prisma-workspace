'use strict';

const crypto = require('node:crypto');
const {
  STATES,
  EDGES,
  TERMINAL_STATES,
  FAIL_CODES,
  DEFAULT_MAX_ATTEMPTS,
  SPEC_STATUSES_BLOCKING_IMPLEMENTATION,
  VALIDATION_RESULTS,
  KNOWN_PRODUCT_GATES,
  GATES,
  GATE_STATES,
  EVENT_TYPES,
  TASK_STATUSES
} = require('./constants');
const { touch, nowIso, cloneEnvelope } = require('./envelope');
const { appendEvent, sanitize } = require('./telemetry');
const { requestHumanGate, pendingGates, hasApprovedGate } = require('./gates');

const VALID_STATES = new Set(Object.values(STATES));
const REVIEW_NODES = new Set([STATES.HUMAN_REVIEW, 'review']);
const DONE_NODES = new Set([STATES.MERGED, 'done', 'completed']);

function emit(envelope, eventType, details, options = {}) {
  return appendEvent(envelope.task_id, eventType, details, {
    telemetryDir: options.telemetryDir,
    agentId: options.agentId,
    humanGates: envelope.human_gates
  });
}

function isValidState(state) {
  return VALID_STATES.has(state);
}

function isTerminal(state) {
  return TERMINAL_STATES.includes(state);
}

function rawTransition(fromState, toState) {
  if (!isValidState(fromState) || !isValidState(toState)) {
    return {
      ok: false,
      code: FAIL_CODES.VALIDATION_ERROR,
      message: `Unknown state: ${!isValidState(fromState) ? fromState : toState}`
    };
  }
  if (isTerminal(fromState)) {
    return {
      ok: false,
      code: FAIL_CODES.EDGE_VIOLATION,
      message: `State '${fromState}' is terminal, no further transitions allowed`
    };
  }
  if (!(EDGES[fromState] || []).includes(toState)) {
    return {
      ok: false,
      code: FAIL_CODES.EDGE_VIOLATION,
      message: `Transition '${fromState}' -> '${toState}' is not allowed by the workflow graph`
    };
  }
  return { ok: true, from: fromState, to: toState };
}

function currentNode(envelope) {
  return envelope.graph.current_node;
}

function ensureGraph(envelope) {
  envelope.graph = envelope.graph || {};
  envelope.graph.history = Array.isArray(envelope.graph.history) ? envelope.graph.history : [];
}

function block(envelope, reason, options = {}) {
  if (!reason || !String(reason).trim()) {
    throw new Error('Block reason is required');
  }
  ensureGraph(envelope);
  if (currentNode(envelope) !== STATES.BLOCKED) {
    envelope.graph.blocked_from = currentNode(envelope);
  }
  envelope.graph.blocked_reason = String(reason);
  envelope.graph.previous_node = currentNode(envelope);
  envelope.graph.current_node = STATES.BLOCKED;
  envelope.graph.history.push({ node: STATES.BLOCKED, entered_at: nowIso(), reason: String(reason) });
  envelope.status = TASK_STATUSES.PAUSED;
  touch(envelope);
  emit(envelope, EVENT_TYPES.BLOCK, {
    blocked_from: envelope.graph.blocked_from,
    reason: envelope.graph.blocked_reason
  }, options);
  return { ok: true, state: STATES.BLOCKED, blocked_from: envelope.graph.blocked_from };
}

function specGuard(envelope, toState, options) {
  if (toState !== STATES.IMPLEMENTING) {
    return null;
  }
  const specStatus = envelope.spec && envelope.spec.status;
  if (!SPEC_STATUSES_BLOCKING_IMPLEMENTATION.includes(specStatus)) {
    return null;
  }
  if (!hasApprovedGate(envelope, GATES.G_SPEC)) {
    requestHumanGate(
      envelope,
      GATES.G_SPEC,
      `Specification status '${specStatus}' requires explicit approval before implementation`,
      options
    );
    return `Specification status '${specStatus}' blocks implementation pending G-SPEC`;
  }
  return null;
}

function completionGuard(envelope, fromState, toState, options) {
  if (!REVIEW_NODES.has(fromState) || !DONE_NODES.has(toState)) {
    return null;
  }
  if (!envelope.loop || envelope.loop.human_completion_required !== true) {
    return null;
  }
  if (!hasApprovedGate(envelope, GATES.G_COMPLETION)) {
    requestHumanGate(
      envelope,
      GATES.G_COMPLETION,
      'Human completion approval is required before finishing review',
      options
    );
    emit(envelope, EVENT_TYPES.COMPLETION_REQUESTED, { from: fromState, to: toState }, options);
    return 'Review completion requires approved G-COMPLETION';
  }
  return null;
}

function transition(target, toState, options = {}) {
  if (typeof target === 'string') {
    return rawTransition(target, toState);
  }
  const envelope = target;
  ensureGraph(envelope);
  const fromState = currentNode(envelope);
  const edge = rawTransition(fromState, toState);
  if (!edge.ok) {
    emit(envelope, EVENT_TYPES.TRANSITION_BLOCKED, edge, options);
    return edge;
  }

  const guardReason = specGuard(envelope, toState, options) ||
    completionGuard(envelope, fromState, toState, options);
  if (guardReason) {
    block(envelope, guardReason, options);
    return { ok: false, code: FAIL_CODES.GATE_FAILURE, message: guardReason };
  }

  const unresolved = pendingGates(envelope);
  if (unresolved.length > 0) {
    const reason = `Pending human gates: ${unresolved.map((gate) => gate.type).join(', ')}`;
    block(envelope, reason, options);
    return { ok: false, code: FAIL_CODES.GATE_FAILURE, message: reason };
  }

  envelope.graph.previous_node = fromState;
  envelope.graph.current_node = toState;
  envelope.graph.history.push({ node: toState, entered_at: nowIso() });
  envelope.graph.blocked_from = null;
  envelope.graph.blocked_reason = null;
  envelope.status = isTerminal(toState)
    ? TASK_STATUSES.COMPLETED
    : TASK_STATUSES.IN_PROGRESS;
  touch(envelope);
  emit(envelope, EVENT_TYPES.TRANSITION, { from: fromState, to: toState }, options);
  if (isTerminal(toState)) {
    emit(envelope, EVENT_TYPES.COMPLETED, { state: toState }, options);
  }
  return { ok: true, from: fromState, to: toState };
}

function advance(envelope, toState, options = {}) {
  if (toState) {
    return transition(envelope, toState, options);
  }
  const allowed = EDGES[currentNode(envelope)] || [];
  if (allowed.length !== 1) {
    return {
      ok: false,
      code: FAIL_CODES.EDGE_VIOLATION,
      message: `Advance requires an explicit target; allowed targets: ${allowed.join(', ') || 'none'}`
    };
  }
  return transition(envelope, allowed[0], options);
}

function recordGate(envelope, gateName, result, details = {}, options = {}) {
  envelope.validation = envelope.validation || { gates: {}, coverage_gaps: [] };
  envelope.validation.gates = envelope.validation.gates || {};
  envelope.validation.coverage_gaps = envelope.validation.coverage_gaps || [];

  const known = KNOWN_PRODUCT_GATES.includes(gateName);
  let normalized = String(result || '').toLowerCase();
  if (!known) {
    normalized = VALIDATION_RESULTS.COVERAGE_GAP;
    if (!envelope.validation.coverage_gaps.includes(gateName)) {
      envelope.validation.coverage_gaps.push(gateName);
    }
  } else if (![VALIDATION_RESULTS.PASS, VALIDATION_RESULTS.FAIL].includes(normalized)) {
    throw new Error(`Invalid gate result: ${result}`);
  }

  const record = {
    result: normalized,
    recorded_at: nowIso(),
    details: sanitize(details)
  };
  envelope.validation.gates[gateName] = record;
  touch(envelope);
  emit(envelope, EVENT_TYPES.GATE_RECORDED, { gate: gateName, ...record }, options);
  return { ok: known, gate: gateName, ...record };
}

function failureSignature(code, details) {
  if (details && details.signature) {
    return String(details.signature);
  }
  const stable = JSON.stringify(sanitize(details || {}), Object.keys(sanitize(details || {})).sort());
  return crypto.createHash('sha256').update(`${code}:${stable}`).digest('hex');
}

function fail(envelope, code, details = {}, options = {}) {
  envelope.loop = envelope.loop || {};
  envelope.loop.attempts = Number(envelope.loop.attempts || 0);
  envelope.loop.max_attempts = Number(envelope.loop.max_attempts || DEFAULT_MAX_ATTEMPTS);
  envelope.loop.failures = Array.isArray(envelope.loop.failures) ? envelope.loop.failures : [];
  envelope.loop.repeated_failure_count = envelope.loop.repeated_failure_count || {};

  const external = code === 'FAIL_EXTERNAL' || code === FAIL_CODES.EXTERNAL_FAILURE;
  const signature = failureSignature(code, details);
  if (!external) {
    envelope.loop.attempts += 1;
    envelope.loop.repeated_failure_count[signature] =
      Number(envelope.loop.repeated_failure_count[signature] || 0) + 1;
  }

  const repeatedCount = Number(envelope.loop.repeated_failure_count[signature] || 0);
  const failure = {
    code,
    signature,
    details: sanitize(details),
    attempt: envelope.loop.attempts,
    repeated_failure_count: repeatedCount,
    failed_at: nowIso(),
    external
  };
  envelope.loop.failures.push(failure);
  touch(envelope);
  emit(envelope, EVENT_TYPES.FAIL, failure, options);

  const maxed = !external && envelope.loop.attempts >= envelope.loop.max_attempts;
  const repeated = !external && repeatedCount >= 2;
  const reason = external
    ? `External failure: ${details.message || code}`
    : repeated
      ? `Repeated failure limit reached for ${signature}`
      : maxed
        ? `Maximum attempts reached (${envelope.loop.max_attempts})`
        : null;

  if (reason) {
    block(envelope, reason, options);
  }
  return { ok: false, blocked: Boolean(reason), external, failure };
}

function resume(envelope, options = {}) {
  ensureGraph(envelope);
  if (currentNode(envelope) !== STATES.BLOCKED) {
    return { ok: false, code: FAIL_CODES.EDGE_VIOLATION, message: 'Task is not blocked' };
  }
  const unresolved = pendingGates(envelope);
  if (unresolved.length > 0) {
    const message = `Cannot resume with pending human gates: ${unresolved.map((gate) => gate.type).join(', ')}`;
    emit(envelope, EVENT_TYPES.TRANSITION_BLOCKED, { message }, options);
    return { ok: false, code: FAIL_CODES.GATE_FAILURE, message };
  }
  const rejected = envelope.human_gates.filter((gate) => gate.status === GATE_STATES.REJECTED);
  if (rejected.length > 0) {
    const message = `Cannot resume after rejected human gate: ${rejected[rejected.length - 1].type}`;
    emit(envelope, EVENT_TYPES.TRANSITION_BLOCKED, { message }, options);
    return { ok: false, code: FAIL_CODES.GATE_FAILURE, message };
  }

  const target = envelope.graph.blocked_from;
  if (!target || target === STATES.BLOCKED || !isValidState(target)) {
    return { ok: false, code: FAIL_CODES.EDGE_VIOLATION, message: 'Blocked origin is invalid' };
  }
  if (target === STATES.IMPLEMENTING) {
    const guardReason = specGuard(envelope, target, options);
    if (guardReason) {
      return { ok: false, code: FAIL_CODES.GATE_FAILURE, message: guardReason };
    }
  }

  envelope.graph.previous_node = STATES.BLOCKED;
  envelope.graph.current_node = target;
  envelope.graph.history.push({ node: target, entered_at: nowIso(), resumed: true });
  const blockedFrom = envelope.graph.blocked_from;
  const blockedReason = envelope.graph.blocked_reason;
  envelope.graph.blocked_from = null;
  envelope.graph.blocked_reason = null;
  envelope.status = TASK_STATUSES.IN_PROGRESS;
  touch(envelope);
  emit(envelope, EVENT_TYPES.RESUME, {
    to: target,
    blocked_from: blockedFrom,
    blocked_reason: blockedReason
  }, options);
  return { ok: true, to: target };
}

function checkpoint(envelope, label = null, data = {}, options = {}) {
  const safe = {
    label,
    state: currentNode(envelope),
    status: envelope.status,
    checkpoint: sanitize(data),
    envelope: sanitize(cloneEnvelope(envelope))
  };
  emit(envelope, 'checkpoint', safe, options);
  return safe;
}

module.exports = {
  isValidState,
  isTerminal,
  rawTransition,
  transition,
  advance,
  recordGate,
  fail,
  block,
  resume,
  checkpoint,
  failureSignature
};
