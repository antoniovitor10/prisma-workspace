'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const { STATES, GATES, DEFAULT_MAX_ATTEMPTS } = require('../lib/constants');

const FEATURE_YAML_PATH = path.join(__dirname, '..', '..', '..', 'workflows', 'feature.yaml');
const PROFILE_YAML_PATH = path.join(__dirname, '..', '..', '..', 'profiles', 'runrun-loop.yaml');

const featureYaml = fs.readFileSync(FEATURE_YAML_PATH, 'utf8');
const profileYaml = fs.readFileSync(PROFILE_YAML_PATH, 'utf8');

function extractSectionBlock(source, sectionKey) {
  const lines = source.split(/\r?\n/);
  const startIndex = lines.findIndex((line) => new RegExp(`^\\s*${sectionKey}:\\s*$`).test(line));
  if (startIndex === -1) {
    throw new Error(`Section "${sectionKey}" not found`);
  }
  const startIndentMatch = lines[startIndex].match(/^(\s*)/);
  const startIndent = startIndentMatch ? startIndentMatch[1].length : 0;
  const blockLines = [];
  for (let i = startIndex + 1; i < lines.length; i += 1) {
    const line = lines[i];
    if (line.trim() === '') {
      blockLines.push(line);
      continue;
    }
    const indentMatch = line.match(/^(\s*)/);
    const indent = indentMatch ? indentMatch[1].length : 0;
    if (indent <= startIndent) {
      break;
    }
    blockLines.push(line);
  }
  return blockLines.join('\n');
}

function extractTopLevelKeys(blockSource) {
  const lines = blockSource.split(/\r?\n/);
  const keys = [];
  let baseIndent = null;
  for (const line of lines) {
    if (line.trim() === '') continue;
    const indentMatch = line.match(/^(\s*)/);
    const indent = indentMatch ? indentMatch[1].length : 0;
    if (baseIndent === null) baseIndent = indent;
    if (indent !== baseIndent) continue;
    const match = line.match(/^\s*([A-Za-z0-9_.-]+):/);
    if (match) keys.push(match[1]);
  }
  return keys;
}

function extractGraphNodes() {
  const nodesBlock = extractSectionBlock(featureYaml, 'nodes');
  return extractTopLevelKeys(nodesBlock);
}

function extractGraphToEngine() {
  const engineMappingBlock = extractSectionBlock(featureYaml, 'engine_mapping');
  const graphToEngineBlock = extractSectionBlock(engineMappingBlock, 'graph_to_engine');
  const lines = graphToEngineBlock.split(/\r?\n/);
  const map = {};
  for (const line of lines) {
    const match = line.match(/^\s*([A-Za-z0-9_.-]+):\s*\[(.*)\]\s*$/);
    if (!match) continue;
    const key = match[1];
    const arrayContent = match[2].trim();
    const values = arrayContent === ''
      ? []
      : arrayContent.split(',').map((v) => v.trim().replace(/^"(.*)"$/, '$1'));
    map[key] = values;
  }
  return map;
}

function extractKeys(blockSource) {
  return extractTopLevelKeys(blockSource);
}

test('33 engine_mapping references only existing graph nodes', () => {
  const graphNodes = extractGraphNodes();
  const graphToEngine = extractGraphToEngine();
  for (const key of Object.keys(graphToEngine)) {
    assert.ok(graphNodes.includes(key), `graph_to_engine key "${key}" is not a real graph node`);
  }
});

test('34 engine_mapping references only existing engine states', () => {
  const graphToEngine = extractGraphToEngine();
  const validStates = Object.values(STATES);
  for (const [node, engineStates] of Object.entries(graphToEngine)) {
    for (const state of engineStates) {
      assert.ok(validStates.includes(state), `engine state "${state}" mapped from "${node}" does not exist in STATES`);
    }
  }
});

test('35 engine_only_states are real engine states absent from graph', () => {
  const engineMappingBlock = extractSectionBlock(featureYaml, 'engine_mapping');
  const engineOnlyBlock = extractSectionBlock(engineMappingBlock, 'engine_only_states');
  const engineOnlyKeys = extractKeys(engineOnlyBlock);
  const validStates = Object.values(STATES);
  const graphToEngine = extractGraphToEngine();
  const allMappedStates = Object.values(graphToEngine).flat();

  assert.deepEqual(engineOnlyKeys.sort(), ['failed', 'idle']);
  for (const stateKey of engineOnlyKeys) {
    assert.ok(validStates.includes(stateKey), `"${stateKey}" is not a real engine state`);
    assert.ok(!allMappedStates.includes(stateKey), `"${stateKey}" should not appear in graph_to_engine mappings`);
  }
});

test('36 graph_only_nodes are real graph nodes with empty engine mapping', () => {
  const engineMappingBlock = extractSectionBlock(featureYaml, 'engine_mapping');
  const graphOnlyBlock = extractSectionBlock(engineMappingBlock, 'graph_only_nodes');
  const graphOnlyKeys = extractKeys(graphOnlyBlock);
  const graphNodes = extractGraphNodes();
  const graphToEngine = extractGraphToEngine();

  assert.deepEqual(graphOnlyKeys.sort(), ['ai_triage', 'feedback_task', 'story_creation', 'task_planning', 'validation_decision']);
  for (const nodeKey of graphOnlyKeys) {
    assert.ok(graphNodes.includes(nodeKey), `"${nodeKey}" is not a real graph node`);
    assert.deepEqual(graphToEngine[nodeKey], [], `"${nodeKey}" should have an empty engine mapping`);
  }
});

test('37 every graph node appears in engine_mapping', () => {
  const graphNodes = extractGraphNodes();
  const graphToEngine = extractGraphToEngine();
  for (const node of graphNodes) {
    assert.ok(Object.prototype.hasOwnProperty.call(graphToEngine, node), `graph node "${node}" is missing from engine_mapping`);
  }
});

test('38 G-COMPLETION is declared in graph, profile and engine', () => {
  assert.ok(featureYaml.includes('G-COMPLETION'), 'G-COMPLETION missing from feature.yaml');
  assert.ok(profileYaml.includes('G-COMPLETION'), 'G-COMPLETION missing from runrun-loop.yaml');
  assert.equal(GATES.G_COMPLETION, 'G-COMPLETION');
});

test('39 all seven human gates are declared in profile and graph', () => {
  const expectedGates = ['G-SPEC', 'G-MIGRATION', 'G-WORKFLOW', 'G-HISTORY', 'G-DEPLOY', 'G-SCOPE', 'G-COMPLETION'];
  for (const gate of expectedGates) {
    assert.ok(featureYaml.includes(gate), `"${gate}" missing from feature.yaml`);
    assert.ok(profileYaml.includes(gate), `"${gate}" missing from runrun-loop.yaml`);
  }
  assert.deepEqual(Object.values(GATES).sort(), expectedGates.sort());
});

test('40 gate_execution.stop_on_first_gate_failure and loop.max_attempts are independent concepts', () => {
  assert.ok(profileYaml.includes('stop_on_first_gate_failure: true'));
  assert.ok(profileYaml.includes('max_attempts: 3'));
  assert.ok(!profileYaml.includes('stop_on_first_failure:'), 'legacy key "stop_on_first_failure" should no longer exist');

  const gateExecutionIndex = profileYaml.indexOf('gate_execution:');
  const loopIndex = profileYaml.indexOf('\nloop:');
  const stopOnFirstGateFailureIndex = profileYaml.indexOf('stop_on_first_gate_failure:');
  const maxAttemptsIndex = profileYaml.indexOf('max_attempts:');

  assert.ok(gateExecutionIndex !== -1 && loopIndex !== -1);
  assert.ok(stopOnFirstGateFailureIndex > gateExecutionIndex && stopOnFirstGateFailureIndex < loopIndex,
    'stop_on_first_gate_failure should be inside gate_execution section, before loop section');
  assert.ok(maxAttemptsIndex > loopIndex, 'max_attempts should be inside loop section');

  assert.equal(DEFAULT_MAX_ATTEMPTS, 3);
});

test('41 profile loop policy matches engine implementation', () => {
  const loopBlock = extractSectionBlock(profileYaml, 'loop');
  assert.ok(loopBlock.includes('max_attempts: 3'));
  assert.equal(DEFAULT_MAX_ATTEMPTS, 3);
  assert.ok(loopBlock.includes('block_on_repeated_failure: 2'));
  assert.ok(loopBlock.includes('external_failure_counts_as_attempt: false'));
  assert.ok(loopBlock.includes('block_on_external_failure: true'));
});
