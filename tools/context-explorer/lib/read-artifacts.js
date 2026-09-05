'use strict';

const fs = require('fs');
const path = require('path');

const { repoPath, fileExists } = require('./paths');
const { parseYaml } = require('./yaml-lite');
const { extractYamlBlocks, parseSpecHeader } = require('./md-blocks');

function toPosix(relativePath) {
  return relativePath.split(path.sep).join('/');
}

function readTextFile(relativePath) {
  return fs.readFileSync(repoPath(relativePath), 'utf8');
}

function readContextIndex() {
  const relPath = 'context/index.yaml';
  const text = readTextFile(relPath);
  const parsed = parseYaml(text);
  return {
    path: relPath,
    version: parsed.version || null,
    description: parsed.description || null,
    storyCatalog: parsed.story_catalog || null,
    authorityHierarchy: parsed.authority_hierarchy || [],
    exclusions: parsed.exclusions || [],
    domains: parsed.domains || {}
  };
}

function readUserStories() {
  const relPath = 'stories/catalog.json';
  if (!fileExists(relPath)) return { path: relPath, version: null, stories: [], exists: false };
  const parsed = JSON.parse(readTextFile(relPath));
  return {
    path: relPath,
    version: parsed.version || null,
    generatedAt: parsed.generatedAt || null,
    migrationNote: parsed.migrationNote || null,
    stories: Array.isArray(parsed.stories) ? parsed.stories : [],
    exists: true
  };
}

function readStoryTasks() {
  const relPath = '.agent-state/story-tasks.json';
  if (!fileExists(relPath)) return { path: relPath, version: 1, tasks: [], exists: false };
  try {
    const parsed = JSON.parse(readTextFile(relPath));
    return {
      path: relPath,
      version: parsed.version || 1,
      updatedAt: parsed.updatedAt || null,
      tasks: Array.isArray(parsed.tasks) ? parsed.tasks : [],
      exists: true
    };
  } catch (error) {
    return { path: relPath, version: 1, tasks: [], exists: true, error: error.message };
  }
}

function parseMarkdownSections(markdown) {
  const lines = markdown.split(/\r?\n/);
  const sections = [];
  let current = null;

  for (const line of lines) {
    const match = line.match(/^(#{1,6})\s+(.+?)\s*$/);
    if (match) {
      if (current) current.body = current.lines.join('\n').trim();
      current = {
        heading: match[2],
        level: match[1].length,
        body: '',
        lines: []
      };
      sections.push(current);
    } else if (current) {
      current.lines.push(line);
    }
  }

  if (current) current.body = current.lines.join('\n').trim();
  return sections.map(({ heading, level, body }) => ({ heading, level, body }));
}

function readSpecs() {
  const specsDir = repoPath('specs');
  const entries = fs.readdirSync(specsDir).filter((name) => name.endsWith('.md'));

  const canonicalSpecs = [];
  for (const entry of entries) {
    if (entry === '_template.md') continue;
    const relPath = toPosix(path.join('specs', entry));
    const text = readTextFile(relPath);
    const header = parseSpecHeader(text);
    canonicalSpecs.push({
      id: header.id,
      title: header.title,
      status: header.status,
      path: relPath,
      body: text,
      sections: parseMarkdownSections(text)
    });
  }

  const templatePath = 'specs/_template.md';
  const template = {
    path: templatePath,
    isTemplate: true,
    exists: fileExists(templatePath)
  };

  return {
    canonicalSpecs,
    template
  };
}

function splitSpecReference(rawSpec) {
  const result = { specId: null, specPath: null };
  if (typeof rawSpec !== 'string') return result;
  const match = rawSpec.match(/^(\S+)\s*\((.+)\)\s*$/);
  if (match) {
    result.specId = match[1].trim();
    result.specPath = match[2].trim();
  } else {
    result.specId = rawSpec.trim();
  }
  return result;
}

function normalizeTask(rawTask) {
  const { specId, specPath } = splitSpecReference(rawTask.spec);
  return {
    id: rawTask.id || null,
    specId,
    specPath,
    domain: rawTask.domain !== undefined ? rawTask.domain : null,
    type: rawTask.type !== undefined ? rawTask.type : null,
    risk: rawTask.risk !== undefined ? rawTask.risk : null,
    dependencies: Array.isArray(rawTask.dependencies) ? rawTask.dependencies : [],
    affected_areas: Array.isArray(rawTask.affected_areas) ? rawTask.affected_areas : [],
    gates: Array.isArray(rawTask.gates) ? rawTask.gates : [],
    human_gate: rawTask.human_gate !== undefined ? rawTask.human_gate : null,
    status: rawTask.status !== undefined ? rawTask.status : null,
    priority: rawTask.priority !== undefined ? rawTask.priority : null,
    requirement: rawTask.requirement !== undefined ? rawTask.requirement : null
  };
}

function readBacklog() {
  const relPath = 'backlog.md';
  const text = readTextFile(relPath);
  const blocks = extractYamlBlocks(text);

  const tasks = [];
  for (const block of blocks) {
    const parsed = parseYaml(block);
    if (Array.isArray(parsed)) {
      for (const rawTask of parsed) {
        if (rawTask && typeof rawTask === 'object') {
          tasks.push(normalizeTask(rawTask));
        }
      }
    }
  }

  return {
    path: relPath,
    tasks
  };
}

function readAgents() {
  const agentsDir = repoPath('agents');
  const entries = fs.readdirSync(agentsDir).filter((name) => name.endsWith('.yaml'));

  return entries.map((entry) => {
    const relPath = toPosix(path.join('agents', entry));
    const text = readTextFile(relPath);
    const parsed = parseYaml(text);
    return {
      id: parsed.id !== undefined ? parsed.id : null,
      path: relPath,
      role: parsed.role !== undefined ? parsed.role : null,
      description: parsed.description !== undefined ? parsed.description : null,
      capability: parsed.capability !== undefined ? parsed.capability : null,
      context: parsed.context !== undefined ? parsed.context : null,
      tools: parsed.tools !== undefined ? parsed.tools : null,
      outputs: parsed.outputs !== undefined ? parsed.outputs : null,
      constraints: parsed.constraints !== undefined ? parsed.constraints : null
    };
  });
}

function readProcessGraph() {
  const relPath = 'workflows/feature.yaml';
  const text = readTextFile(relPath);
  const parsed = parseYaml(text);
  return {
    path: relPath,
    nodes: parsed.nodes || {},
    edges: parsed.edges || [],
    humanGates: parsed.human_gates || {},
    engineMapping: parsed.engine_mapping || {}
  };
}

function readLoopProfile() {
  const relPath = 'profiles/runrun-loop.yaml';
  const text = readTextFile(relPath);
  const parsed = parseYaml(text);
  return {
    path: relPath,
    productGates: parsed.gates || {},
    missingGates: parsed.missing_gates || {},
    humanGates: parsed.human_gates_enforcement || {},
    gateExecution: parsed.gate_execution || {},
    loop: parsed.loop || {}
  };
}

function readEngineGraph() {
  const relPath = 'tools/agent-loop/lib/constants.js';
  const constants = require(repoPath('tools', 'agent-loop', 'lib', 'constants.js'));
  return {
    path: toPosix(relPath),
    states: Object.values(constants.STATES),
    edges: constants.EDGES,
    gates: Object.values(constants.GATES),
    maxAttempts: constants.DEFAULT_MAX_ATTEMPTS,
    terminalStates: constants.TERMINAL_STATES,
    failCodes: constants.FAIL_CODES
  };
}

function readActualContext() {
  const tasksDir = repoPath('.agent-state', 'tasks');
  if (!fs.existsSync(tasksDir)) return [];

  const entries = fs
    .readdirSync(tasksDir)
    .filter((name) => name !== '.gitkeep')
    .filter((name) => name.endsWith('.yaml') || name.endsWith('.json'));

  return entries.map((entry) => {
    const relPath = toPosix(path.join('.agent-state', 'tasks', entry));
    const text = readTextFile(relPath);
    try {
      const data = JSON.parse(text);
      return { path: relPath, data };
    } catch (err) {
      return { path: relPath, error: err.message };
    }
  });
}

function readOmniroute() {
  const relPath = 'OMNIROUTE-READINESS.md';
  const exists = fileExists(relPath);
  return {
    path: relPath,
    exists,
    text: exists ? readTextFile(relPath) : null
  };
}

function readArtifacts() {
  return {
    contextIndex: readContextIndex(),
    userStories: readUserStories(),
    storyTasks: readStoryTasks(),
    specs: readSpecs(),
    backlog: readBacklog(),
    agents: readAgents(),
    processGraph: readProcessGraph(),
    loopProfile: readLoopProfile(),
    engineGraph: readEngineGraph(),
    actualContext: readActualContext(),
    omniroute: readOmniroute()
  };
}

module.exports = {
  readArtifacts,
  readContextIndex,
  readUserStories,
  readStoryTasks,
  readSpecs,
  readBacklog,
  readAgents,
  readProcessGraph,
  readLoopProfile,
  readEngineGraph,
  readActualContext,
  readOmniroute
};
