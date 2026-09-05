'use strict';

const { readArtifacts } = require('./read-artifacts');
const {
  deriveContextForDomain,
  deriveContextForSpec,
  deriveContextForTask,
  deriveMinimum,
  deriveExpanded,
  computeCategoryStates,
  previewTask,
  buildContextGraph
} = require('./derive-context');
const { detectGaps } = require('./detect-gaps');
const { deriveUserStories } = require('./derive-user-stories');
const { fileExists } = require('./paths');
const canonical = require('./canonical');

const DOMAIN_ORDER = [
  'organizations',
  'projects',
  'work_nature',
  'work_items',
  'backlog',
  'workflow',
  'history',
  'sprints',
  'dependencies',
  'access',
  'visual_experience',
  'project_queries',
  'open_source_distribution'
];

function domainLabel(domainKey) {
  return domainKey
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function objectCount(value) {
  if (Array.isArray(value)) return value.length;
  return value && typeof value === 'object' ? Object.keys(value).length : 0;
}

function edgeCount(edges) {
  if (Array.isArray(edges)) return edges.length;
  if (!edges || typeof edges !== 'object') return 0;
  return Object.values(edges).reduce(
    (total, destinations) => total + (Array.isArray(destinations) ? destinations.length : 0),
    0
  );
}

function buildArtifactsSummary(artifacts) {
  return {
    domains: DOMAIN_ORDER.length,
    specs: {
      canonical: artifacts.specs.canonicalSpecs.length,
      total: artifacts.specs.canonicalSpecs.length + 1,
      template: artifacts.specs.template
    },
    tasks: artifacts.backlog.tasks.length,
    userStories: artifacts.userStories.stories.length,
    storyTasks: artifacts.storyTasks.tasks.length,
    agents: artifacts.agents.length,
    process: {
      nodes: objectCount(artifacts.processGraph.nodes),
      edges: edgeCount(artifacts.processGraph.edges)
    },
    engine: {
      states: objectCount(artifacts.engineGraph.states),
      edges: edgeCount(artifacts.engineGraph.edges)
    },
    humanGates: canonical.humanGates.length,
    actualContext: artifacts.actualContext.length
  };
}

function buildSelections(artifacts) {
  const specs = artifacts.specs.canonicalSpecs.map((spec) => ({
    id: spec.id,
    label: spec.title || spec.id
  }));

  const tasks = artifacts.backlog.tasks.map((task) => ({
    id: task.id,
    label: task.requirement || task.id
  }));

  const domains = DOMAIN_ORDER.map((domainKey) => ({
    id: domainKey,
    label: domainLabel(domainKey)
  }));

  return { specs, tasks, domains };
}

function buildDomainContext(domainKey, artifacts) {
  const result = deriveContextForDomain(domainKey, artifacts);
  return {
    id: domainKey,
    domain: domainKey,
    minimum: deriveMinimum(result),
    expanded: deriveExpanded(result),
    categoryStates: computeCategoryStates(result)
  };
}

function buildSpecContext(specId, artifacts) {
  const result = deriveContextForSpec(specId, artifacts);
  return {
    id: specId,
    specId,
    domain: result.domain,
    minimum: deriveMinimum(result),
    expanded: deriveExpanded(result),
    categoryStates: computeCategoryStates(result)
  };
}

function buildTaskContext(taskId, artifacts) {
  const result = deriveContextForTask(taskId, artifacts);
  return {
    id: taskId,
    taskId,
    domain: result.domain,
    spec: result.task ? result.task.specId : null,
    minimum: deriveMinimum(result),
    expanded: deriveExpanded(result),
    categoryStates: computeCategoryStates(result)
  };
}

function buildContext(artifacts) {
  const domainContexts = DOMAIN_ORDER.map((domainKey) => buildDomainContext(domainKey, artifacts));
  const specContexts = artifacts.specs.canonicalSpecs.map((spec) =>
    buildSpecContext(spec.id, artifacts)
  );
  const taskContexts = artifacts.backlog.tasks.map((task) => buildTaskContext(task.id, artifacts));

  return { domainContexts, specContexts, taskContexts };
}

function buildPreviews(artifacts) {
  const tasks = artifacts.backlog.tasks.map((task) => previewTask(task.id, artifacts));
  return { tasks };
}

function buildSpecGraph(artifacts) {
  const nodes = new Map();
  const edges = [];

  function addNode(id, type, label) {
    if (!nodes.has(id)) nodes.set(id, { id, type, label });
  }

  const domains = artifacts.contextIndex.domains || {};

  for (const domainKey of Object.keys(domains)) {
    const domainEntry = domains[domainKey];
    addNode(`domain:${domainKey}`, 'domain', domainKey);

    if (domainEntry.spec) {
      const spec = artifacts.specs.canonicalSpecs.find((s) => s.path === domainEntry.spec);
      const specNodeId = spec ? `spec:${spec.id}` : `spec:${domainEntry.spec}`;
      addNode(specNodeId, 'spec', spec ? spec.id : domainEntry.spec);
      edges.push({ from: `domain:${domainKey}`, to: specNodeId, relation: 'governed_by' });
    }
  }

  for (const task of artifacts.backlog.tasks) {
    if (!task.specId) continue;
    addNode(`task:${task.id}`, 'task', task.id);
    addNode(`spec:${task.specId}`, 'spec', task.specId);
    edges.push({ from: `task:${task.id}`, to: `spec:${task.specId}`, relation: 'linked_to' });

    for (const depId of task.dependencies || []) {
      addNode(`task:${depId}`, 'task', depId);
      edges.push({ from: `task:${task.id}`, to: `task:${depId}`, relation: 'depends_on' });
    }
  }

  return { nodes: Array.from(nodes.values()), edges };
}

function buildProcessGraph(artifacts) {
  const nodes = Object.keys(artifacts.processGraph.nodes || {}).map((id) => ({
    id,
    ...artifacts.processGraph.nodes[id]
  }));
  const edges = artifacts.processGraph.edges || [];
  return { nodes, edges };
}

function buildEngineGraph(artifacts) {
  const states = artifacts.engineGraph.states || [];
  const edgesMap = artifacts.engineGraph.edges || {};
  const edges = [];
  for (const [from, destinations] of Object.entries(edgesMap)) {
    for (const to of destinations || []) {
      edges.push({ from, to });
    }
  }
  return {
    states,
    edges,
    mapping: artifacts.processGraph.engineMapping || {}
  };
}

function buildLoopGraph() {
  return {
    nodes: [
      {
        id: 'implementation',
        type: 'process',
        label: 'Implementação',
        description: 'O agente executa a alteração planejada.'
      },
      {
        id: 'gating',
        type: 'validation',
        label: 'Validação / gates',
        description: 'Os gates de produto são executados em sequência.'
      },
      {
        id: 'FAIL_CODE',
        type: 'failure',
        label: 'Falha corrigível',
        description: 'Falha causada pelo código e elegível para nova tentativa.'
      },
      {
        id: 'correction',
        type: 'process',
        label: 'Correção',
        description: 'O agente corrige a causa da falha antes de validar novamente.'
      },
      {
        id: 'FAIL_EXTERNAL',
        type: 'external_failure',
        label: 'Falha externa',
        description: 'Falha de infraestrutura ou dependência externa; não consome tentativa.'
      },
      {
        id: 'BLOCKED',
        type: 'blocked',
        label: 'Bloqueado',
        description: 'A execução para e requer resolução externa ou intervenção humana.'
      }
    ],
    edges: [
      { from: 'implementation', to: 'gating', label: 'Validar alteração' },
      { from: 'gating', to: 'FAIL_CODE', label: 'Falha no código' },
      { from: 'FAIL_CODE', to: 'correction', label: 'Corrigir' },
      { from: 'correction', to: 'gating', label: 'Validar novamente' },
      { from: 'gating', to: 'FAIL_EXTERNAL', label: 'Falha externa' },
      { from: 'FAIL_EXTERNAL', to: 'BLOCKED', label: 'Bloquear execução' }
    ]
  };
}

function buildGraphs(artifacts) {
  return {
    specGraph: buildSpecGraph(artifacts),
    processGraph: buildProcessGraph(artifacts),
    engineGraph: buildEngineGraph(artifacts),
    loopGraph: buildLoopGraph()
  };
}

function buildLoop(artifacts) {
  const activeTaskEntry = artifacts.actualContext.length > 0 ? artifacts.actualContext[0] : null;

  return {
    profile: {
      path: artifacts.loopProfile.path,
      loop: artifacts.loopProfile.loop,
      productGates: artifacts.loopProfile.productGates,
      missingGates: artifacts.loopProfile.missingGates,
      humanGatesEnforcement: artifacts.loopProfile.humanGates
    },
    gateExecution: artifacts.loopProfile.gateExecution,
    activeTask: activeTaskEntry ? activeTaskEntry.data || activeTaskEntry : null,
    attempt: activeTaskEntry ? null : null,
    label: activeTaskEntry ? null : 'Nenhuma tarefa ativa'
  };
}

function buildArchitecture() {
  const nodes = [
    { id: 'HUMAN', type: 'actor', label: 'Human' },
    { id: 'USER_STORY', type: 'artifact', label: 'User Story' },
    { id: 'SPEC', type: 'artifact', label: 'Spec' },
    { id: 'TASK', type: 'artifact', label: 'Task' },
    { id: 'CONTEXT_ENGINEERING', type: 'process', label: 'Context Engineering' },
    { id: 'CONTEXT_INDEX', type: 'artifact', label: 'Context Index' },
    { id: 'CONTEXT_GRAPH', type: 'artifact', label: 'Context Graph' },
    { id: 'PROCESS_GRAPH', type: 'artifact', label: 'Process Graph' },
    { id: 'AGENT', type: 'actor', label: 'Agent' },
    { id: 'LOOP_ENGINE', type: 'process', label: 'Loop Engine' },
    { id: 'PASS', type: 'state', label: 'Pass' },
    { id: 'FAIL', type: 'state', label: 'Fail' },
    { id: 'REVIEW', type: 'process', label: 'Review' },
    { id: 'HUMAN_GATE', type: 'process', label: 'Human Gate' },
    { id: 'HOMOLOGATION', type: 'process', label: 'Homologation' },
    { id: 'FEEDBACK_TASK', type: 'artifact', label: 'Feedback Task' },
    { id: 'AI_TRIAGE', type: 'process', label: 'AI Triage' },
    { id: 'DONE', type: 'state', label: 'Done' },
    { id: 'STATE', type: 'lateral', label: 'State' },
    { id: 'TELEMETRY', type: 'lateral', label: 'Telemetry' },
    { id: 'HANDOFF', type: 'lateral', label: 'Handoff' },
    { id: 'OMNIROUTE_READINESS', type: 'lateral', label: 'OmniRoute Readiness' }
  ];

  const edges = [
    { from: 'HUMAN', to: 'USER_STORY' },
    { from: 'USER_STORY', to: 'SPEC' },
    { from: 'SPEC', to: 'HUMAN_GATE' },
    { from: 'HUMAN_GATE', to: 'TASK' },
    { from: 'TASK', to: 'CONTEXT_ENGINEERING' },
    { from: 'CONTEXT_ENGINEERING', to: 'CONTEXT_INDEX' },
    { from: 'CONTEXT_ENGINEERING', to: 'CONTEXT_GRAPH' },
    { from: 'CONTEXT_INDEX', to: 'PROCESS_GRAPH' },
    { from: 'CONTEXT_GRAPH', to: 'PROCESS_GRAPH' },
    { from: 'PROCESS_GRAPH', to: 'AGENT' },
    { from: 'AGENT', to: 'LOOP_ENGINE' },
    { from: 'LOOP_ENGINE', to: 'PASS' },
    { from: 'LOOP_ENGINE', to: 'FAIL' },
    { from: 'PASS', to: 'REVIEW' },
    { from: 'FAIL', to: 'LOOP_ENGINE' },
    { from: 'REVIEW', to: 'HOMOLOGATION' },
    { from: 'HOMOLOGATION', to: 'DONE' },
    { from: 'HOMOLOGATION', to: 'FEEDBACK_TASK' },
    { from: 'FEEDBACK_TASK', to: 'AI_TRIAGE' },
    { from: 'AI_TRIAGE', to: 'TASK' },
    { from: 'AI_TRIAGE', to: 'USER_STORY' }
  ];

  return {
    conceptual: true,
    description:
      'Visão arquitetural de referência do fluxo AI-Native. Diagrama conceitual explicitamente solicitado; não deve ser confundido com o Context Graph evidencial (baseado em artefatos reais).',
    nodes,
    edges,
    laterals: ['STATE', 'TELEMETRY', 'HANDOFF', 'OMNIROUTE_READINESS']
  };
}

function extractOmniRouteStatus(text) {
  if (!text) return null;
  const match = text.match(/\*\*Modo de Opera[^:]*:\*\*\s*(.+)/);
  return match ? match[1].trim() : null;
}

function buildReadiness(artifacts) {
  const omniroute = artifacts.omniroute;
  return {
    omniroute: {
      path: omniroute.path,
      exists: omniroute.exists,
      status: extractOmniRouteStatus(omniroute.text),
      configExists: fileExists('.omniroute.json'),
      text: omniroute.text
    }
  };
}

function buildModel() {
  const artifacts = readArtifacts();

  return {
    generatedAt: new Date().toISOString(),
    canonical,
    artifactsSummary: buildArtifactsSummary(artifacts),
    selections: buildSelections(artifacts),
    agents: artifacts.agents,
    specs: artifacts.specs.canonicalSpecs,
    specDetails: artifacts.specs.canonicalSpecs,
    userStories: artifacts.userStories.stories.length > 0
      ? artifacts.userStories.stories
      : deriveUserStories(artifacts.specs.canonicalSpecs),
    storyCatalog: {
      path: artifacts.userStories.path,
      exists: artifacts.userStories.exists,
      version: artifacts.userStories.version,
      migrationNote: artifacts.userStories.migrationNote
    },
    storyTasks: artifacts.storyTasks.tasks,
    tasks: artifacts.backlog.tasks,
    humanGates: artifacts.processGraph.humanGates,
    context: buildContext(artifacts),
    previews: buildPreviews(artifacts),
    graphs: buildGraphs(artifacts),
    contextGraph: buildContextGraph(deriveContextForTask('TASK-009', artifacts)),
    loop: buildLoop(artifacts),
    architecture: buildArchitecture(),
    readiness: buildReadiness(artifacts),
    gaps: detectGaps(artifacts)
  };
}

module.exports = { buildModel };
