'use strict';

const crypto = require('node:crypto');
const {
  GATES,
  GATE_STATES,
  FORBIDDEN_GATE_RESOLVERS,
  EVENT_TYPES,
  TASK_STATUSES
} = require('./constants');
const { touch, nowIso } = require('./envelope');
const { appendEvent } = require('./telemetry');

const KNOWN_HUMAN_GATES = new Set(Object.values(GATES));

function emit(envelope, eventType, details, options = {}) {
  return appendEvent(envelope.task_id, eventType, details, {
    telemetryDir: options.telemetryDir,
    agentId: options.agentId,
    humanGates: envelope.human_gates
  });
}

function ensureGates(envelope) {
  if (!Array.isArray(envelope.human_gates)) {
    envelope.human_gates = [];
  }
  return envelope.human_gates;
}

function pendingGates(envelope) {
  return ensureGates(envelope).filter((gate) => gate.status === GATE_STATES.PENDING);
}

function hasApprovedGate(envelope, type) {
  return ensureGates(envelope).some(
    (gate) => gate.type === type && gate.status === GATE_STATES.APPROVED
  );
}

function hasPendingGate(envelope, type) {
  return ensureGates(envelope).some(
    (gate) => gate.type === type && gate.status === GATE_STATES.PENDING
  );
}

function requestHumanGate(envelope, type, reason, options = {}) {
  if (!KNOWN_HUMAN_GATES.has(type)) {
    throw new Error(`Unknown human gate: ${type}`);
  }
  if (!reason || !String(reason).trim()) {
    throw new Error('Human gate reason is required');
  }

  const existing = ensureGates(envelope).find(
    (gate) => gate.type === type && gate.status === GATE_STATES.PENDING
  );
  if (existing) {
    return existing;
  }

  const gate = {
    id: crypto.randomUUID(),
    type,
    reason: String(reason),
    status: GATE_STATES.PENDING,
    requested_at: nowIso(),
    resolved_at: null,
    resolved_by: null,
    note: null
  };
  envelope.human_gates.push(gate);
  envelope.status = TASK_STATUSES.PAUSED;
  touch(envelope);
  emit(envelope, EVENT_TYPES.GATE_REQUESTED, { gate }, options);
  return gate;
}

function isHumanResolver(resolvedBy) {
  if (typeof resolvedBy !== 'string' || !resolvedBy.trim()) {
    return false;
  }
  const normalized = resolvedBy.trim().toLowerCase();
  return !FORBIDDEN_GATE_RESOLVERS.some(
    (forbidden) => normalized === forbidden || normalized.startsWith(`${forbidden}:`)
  );
}

function findPendingGate(envelope, gateRef) {
  const candidates = ensureGates(envelope).filter(
    (gate) =>
      (gate.id === gateRef || gate.type === gateRef) && gate.status === GATE_STATES.PENDING
  );
  return candidates[candidates.length - 1] || null;
}

function resolveGate(envelope, gateRef, decision, resolvedBy, note = null, options = {}) {
  const normalized = String(decision || '').toLowerCase();
  if (![GATE_STATES.APPROVED, GATE_STATES.REJECTED].includes(normalized)) {
    throw new Error("Gate decision must be 'approved' or 'rejected'");
  }
  if (!isHumanResolver(resolvedBy)) {
    throw new Error('resolvedBy must identify an explicit human resolver');
  }

  const gate = findPendingGate(envelope, gateRef);
  if (!gate) {
    throw new Error(`Pending human gate not found: ${gateRef}`);
  }

  gate.status = normalized;
  gate.resolved_at = nowIso();
  gate.resolved_by = resolvedBy.trim();
  gate.note = note === undefined ? null : note;
  if (normalized === GATE_STATES.REJECTED) {
    envelope.status = TASK_STATUSES.PAUSED;
  }
  touch(envelope);
  emit(envelope, EVENT_TYPES.GATE_RESOLVED, {
    gate_id: gate.id,
    gate_type: gate.type,
    decision: normalized,
    resolved_by: gate.resolved_by,
    note: gate.note
  }, options);
  return gate;
}

module.exports = {
  KNOWN_HUMAN_GATES,
  pendingGates,
  hasApprovedGate,
  hasPendingGate,
  requestHumanGate,
  resolveGate,
  isHumanResolver
};
