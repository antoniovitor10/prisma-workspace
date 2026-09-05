'use strict';

const { STATES, TASK_STATUSES, DEFAULT_MAX_ATTEMPTS } = require('./constants');

function nowIso() {
  return new Date().toISOString();
}

function deepMerge(target, source) {
  if (source === undefined || source === null) {
    return target;
  }
  if (Array.isArray(source)) {
    return source.slice();
  }
  if (typeof source !== 'object') {
    return source;
  }
  const result = { ...target };
  for (const key of Object.keys(source)) {
    if (
      Object.prototype.hasOwnProperty.call(target, key) &&
      target[key] !== null &&
      typeof target[key] === 'object' &&
      !Array.isArray(target[key]) &&
      typeof source[key] === 'object' &&
      source[key] !== null &&
      !Array.isArray(source[key])
    ) {
      result[key] = deepMerge(target[key], source[key]);
    } else {
      result[key] = source[key] === undefined ? target[key] : source[key];
    }
  }
  return result;
}

/**
 * Creates a task envelope with all mandatory sections. Any field not
 * explicitly supplied via overrides is set to `null` (unknown), never
 * silently omitted.
 */
function createEnvelope(id, title, objective, overrides = {}) {
  if (!id) {
    throw new Error('createEnvelope requires a task id');
  }

  const timestamp = nowIso();
  const maxAttempts =
    overrides.loop && Number.isFinite(overrides.loop.max_attempts)
      ? overrides.loop.max_attempts
      : DEFAULT_MAX_ATTEMPTS;

  const base = {
    task_id: id,
    spec_id: null,
    created_at: timestamp,
    updated_at: timestamp,
    status: TASK_STATUSES.PENDING,

    task: {
      id,
      title: title === undefined ? null : title,
      objective: objective === undefined ? null : objective,
      created_by: null
    },

    classification: {
      domain: null,
      complexity: null,
      risk: null,
      type: null
    },

    graph: {
      current_node: STATES.IDLE,
      previous_node: null,
      history: [{ node: STATES.IDLE, entered_at: timestamp }],
      blocked_from: null,
      blocked_reason: null
    },

    spec: {
      id: null,
      status: null
    },

    context: {
      loaded: false,
      domains: [],
      loaded_at: null
    },

    changes: {
      files: [],
      summary: null
    },

    validation: {
      gates: {},
      coverage_gaps: []
    },

    loop: {
      attempts: 0,
      max_attempts: maxAttempts,
      failures: [],
      repeated_failure_count: {}
    },

    human_gates: []
  };

  const merged = deepMerge(base, overrides);
  merged.task_id = id;
  merged.task.id = id;
  if (merged.spec && merged.spec.id) {
    merged.spec_id = merged.spec.id;
  }
  return merged;
}

function touch(envelope) {
  envelope.updated_at = nowIso();
  return envelope;
}

function cloneEnvelope(envelope) {
  return JSON.parse(JSON.stringify(envelope));
}

module.exports = {
  createEnvelope,
  touch,
  cloneEnvelope,
  nowIso,
  deepMerge
};
