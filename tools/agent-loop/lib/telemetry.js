'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const { SANITIZE_KEY_PATTERNS, SANITIZE_VALUE_PATTERNS } = require('./constants');

const DEFAULT_TELEMETRY_DIR = path.join(process.cwd(), '.agent-state', 'telemetry');
const REDACTED = '[REDACTED]';

function resolveTelemetryDir(baseDir) {
  return baseDir ? path.resolve(baseDir) : DEFAULT_TELEMETRY_DIR;
}

function telemetryFilePath(telemetryDir, taskId) {
  const dir = resolveTelemetryDir(telemetryDir);
  return path.join(dir, `${taskId}.jsonl`);
}

function isSensitiveKey(key) {
  return SANITIZE_KEY_PATTERNS.some((pattern) => pattern.test(key));
}

function isSensitiveStringValue(value) {
  return SANITIZE_VALUE_PATTERNS.some((pattern) => pattern.test(value));
}

/**
 * Recursively removes/redacts any secret, token, password, credential,
 * .env reference or chain-of-thought content from a checkpoint payload.
 */
function sanitize(value) {
  if (value === null || value === undefined) {
    return value;
  }
  if (typeof value === 'string') {
    return isSensitiveStringValue(value) ? REDACTED : value;
  }
  if (Array.isArray(value)) {
    return value.map((item) => sanitize(item));
  }
  if (typeof value === 'object') {
    const result = {};
    for (const key of Object.keys(value)) {
      if (isSensitiveKey(key)) {
        result[key] = REDACTED;
        continue;
      }
      result[key] = sanitize(value[key]);
    }
    return result;
  }
  return value;
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

/**
 * Appends a single telemetry event as one JSON line. Never overwrites
 * or rewrites previous events (append-only audit trail).
 */
function appendEvent(taskId, eventType, details = {}, options = {}) {
  const telemetryDir = resolveTelemetryDir(options.telemetryDir);
  ensureDir(telemetryDir);

  const event = {
    event_id: options.eventId || crypto.randomUUID(),
    task_id: taskId,
    agent_id: options.agentId || null,
    event_type: eventType,
    timestamp: new Date().toISOString(),
    details: sanitize(details || {}),
    human_gates: options.humanGates || []
  };

  const filePath = telemetryFilePath(telemetryDir, taskId);
  fs.appendFileSync(filePath, JSON.stringify(event) + '\n', 'utf8');
  return event;
}

function readEvents(taskId, telemetryDir) {
  const filePath = telemetryFilePath(telemetryDir, taskId);
  if (!fs.existsSync(filePath)) {
    return [];
  }
  const raw = fs.readFileSync(filePath, 'utf8');
  return raw
    .split('\n')
    .filter((line) => line.trim().length > 0)
    .map((line) => JSON.parse(line));
}

module.exports = {
  DEFAULT_TELEMETRY_DIR,
  resolveTelemetryDir,
  telemetryFilePath,
  sanitize,
  appendEvent,
  readEvents
};
