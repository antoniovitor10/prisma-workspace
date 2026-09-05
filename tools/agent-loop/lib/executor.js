'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { KNOWN_PRODUCT_GATES } = require('./constants');

function parseScalar(rawValue) {
  const value = String(rawValue).trim();
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (value === 'null') return null;
  if (/^-?\d+(\.\d+)?$/.test(value)) return Number(value);
  if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
    return value.slice(1, -1);
  }
  return value;
}

/**
 * Parses only the versioned execution contract used by runrun-loop.yaml.
 * It intentionally is not a general-purpose YAML parser.
 */
function parseExecutionProfile(text) {
  const profile = { gates: {}, gate_execution: {} };
  let section = null;
  let currentGate = null;

  for (const sourceLine of String(text).split(/\r?\n/)) {
    const line = sourceLine.replace(/\s+$/, '');
    if (!line.trim() || line.trimStart().startsWith('#')) continue;

    const topLevel = line.match(/^([a-zA-Z0-9_-]+):\s*(.*)$/);
    if (topLevel) {
      section = topLevel[1];
      currentGate = null;
      continue;
    }

    if (section === 'gates') {
      const gateHeader = line.match(/^  ([a-zA-Z0-9_-]+):\s*$/);
      if (gateHeader) {
        currentGate = gateHeader[1];
        profile.gates[currentGate] = {};
        continue;
      }
      const property = line.match(/^    ([a-zA-Z0-9_-]+):\s*(.*)$/);
      if (currentGate && property) {
        profile.gates[currentGate][property[1]] = parseScalar(property[2]);
      }
      continue;
    }

    if (section === 'gate_execution') {
      const property = line.match(/^  ([a-zA-Z0-9_-]+):\s*(.*)$/);
      if (property) profile.gate_execution[property[1]] = parseScalar(property[2]);
    }
  }

  return profile;
}

function loadExecutionProfile(profilePath) {
  const absolutePath = path.resolve(profilePath);
  const profile = parseExecutionProfile(fs.readFileSync(absolutePath, 'utf8'));
  const gates = Object.entries(profile.gates).map(([id, gate]) => ({ id, ...gate }));
  if (gates.length === 0) throw new Error(`No product gates declared in profile: ${absolutePath}`);
  for (const gate of gates) {
    if (!gate.command || !gate.cwd) throw new Error(`Gate '${gate.id}' requires command and cwd`);
  }
  return { ...profile, path: absolutePath, gates };
}

function resolveSafeWorkingDirectory(repositoryRoot, configuredCwd) {
  const root = path.resolve(repositoryRoot);
  const target = path.resolve(root, configuredCwd);
  const relative = path.relative(root, target);
  if (relative === '..' || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error(`Gate working directory escapes repository root: ${configuredCwd}`);
  }
  if (!fs.existsSync(target) || !fs.statSync(target).isDirectory()) {
    throw new Error(`Gate working directory does not exist: ${configuredCwd}`);
  }
  return { root, target, relative: relative || '.' };
}

function spawnProfileCommand(command, options) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, {
      cwd: options.cwd,
      env: process.env,
      shell: true,
      stdio: 'inherit',
      windowsHide: true
    });
    child.once('error', reject);
    child.once('exit', (code, signal) => resolve({ exitCode: code ?? 1, signal: signal || null }));
  });
}

async function runProductGates(options = {}) {
  const profile = loadExecutionProfile(options.profilePath);
  const selected = options.gates && options.gates.length ? new Set(options.gates) : null;
  const active = profile.gates.filter((gate) => gate.status === 'active' && (!selected || selected.has(gate.id)));

  if (selected) {
    const declared = new Set(profile.gates.map((gate) => gate.id));
    const unknown = [...selected].filter((gate) => !declared.has(gate));
    if (unknown.length) throw new Error(`Unknown profile gates: ${unknown.join(', ')}`);
  }
  if (active.length === 0) throw new Error('No active product gates selected');

  const forbidden = active.filter((gate) => !KNOWN_PRODUCT_GATES.includes(gate.id));
  if (forbidden.length) {
    throw new Error(`Profile contains non-allowlisted executable gates: ${forbidden.map((gate) => gate.id).join(', ')}`);
  }

  const runCommand = options.runCommand || spawnProfileCommand;
  const results = [];
  const stopOnFailure = profile.gate_execution.stop_on_first_gate_failure !== false;

  for (const gate of active) {
    const cwd = resolveSafeWorkingDirectory(options.repositoryRoot, gate.cwd);
    const startedAt = Date.now();
    const processResult = await runCommand(gate.command, { cwd: cwd.target, gate: gate.id });
    const result = {
      gate: gate.id,
      result: processResult.exitCode === 0 ? 'pass' : 'fail',
      exit_code: processResult.exitCode,
      signal: processResult.signal || null,
      duration_ms: Date.now() - startedAt,
      cwd: cwd.relative
    };
    results.push(result);
    if (options.onResult) await options.onResult(result);
    if (result.result === 'fail' && stopOnFailure) break;
  }

  const failed = results.find((result) => result.result === 'fail') || null;
  return {
    ok: !failed,
    profile: path.relative(path.resolve(options.repositoryRoot), profile.path),
    stop_on_first_gate_failure: stopOnFailure,
    failed_gate: failed ? failed.gate : null,
    results
  };
}

module.exports = {
  parseExecutionProfile,
  loadExecutionProfile,
  resolveSafeWorkingDirectory,
  spawnProfileCommand,
  runProductGates
};
