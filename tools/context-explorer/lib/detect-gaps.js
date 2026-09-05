'use strict';

const { fileExists } = require('./paths');
const canonical = require('./canonical');

const TASK_DOMAIN_TO_INDEX_DOMAIN = {
  project: 'projects',
  'work-item': 'work_items',
  backlog: 'backlog',
  dependency: 'dependencies',
  sprint: 'sprints',
  workflow: 'workflow',
  history: 'history',
  board: 'workflow',
  search: 'work_items'
};

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function objectCount(value) {
  if (Array.isArray(value)) return value.length;
  return value && typeof value === 'object' ? Object.keys(value).length : 0;
}

function edgeCount(edges) {
  if (Array.isArray(edges)) return edges.length;
  if (!edges || typeof edges !== 'object') return 0;
  return Object.values(edges).reduce(
    (total, destinations) => total + asArray(destinations).length,
    0
  );
}

function tasksFrom(backlog) {
  if (Array.isArray(backlog)) return backlog;
  return backlog && Array.isArray(backlog.tasks) ? backlog.tasks : [];
}

function domainKeyFor(taskDomain, domains) {
  if (!taskDomain) return null;
  if (Object.prototype.hasOwnProperty.call(domains, taskDomain)) return taskDomain;
  return TASK_DOMAIN_TO_INDEX_DOMAIN[taskDomain] || null;
}

function gapExplanation(type) {
  if (type === 'broken_path') return 'O backlog declarou este caminho, mas ele não existe no disco.';
  if (type === 'missing_test') return 'A task/domínio não possui uma task do tipo test declarada.';
  if (type === 'verification_note') return 'Nota informativa; não é falha de execução.';
  return 'Diferença detectada automaticamente a partir dos artefatos declarados.';
}

function displayResolvedPath(relativePath) {
  return `%WORKSPACE%/${String(relativePath).replace(/\\/g, '/')}`;
}

function addGap(gaps, type, subject, detail, source, severity, metadata = {}) {
  gaps.push({
    type,
    subject,
    detail,
    source,
    severity,
    path: metadata.path || null,
    resolvedPath: metadata.resolvedPath || null,
    reason: metadata.reason || detail,
    explanation: gapExplanation(type)
  });
}

function addMismatch(gaps, subject, actual, expected, source) {
  if (actual === expected) return;
  addGap(
    gaps,
    'canonical_mismatch',
    subject,
    `Esperado ${JSON.stringify(expected)}, encontrado ${JSON.stringify(actual)}.`,
    source,
    'error'
  );
}

function detectGaps(artifacts) {
  const gaps = [];
  const domains = artifacts.contextIndex.domains || {};
  const tasks = tasksFrom(artifacts.backlog);

  for (const [domainName, domain] of Object.entries(domains)) {
    const code = asArray(domain.code);
    const tests = asArray(domain.tests);
    const gates = domain.gates || {};

    if (domain.spec && code.length === 0) {
      addGap(
        gaps,
        'spec_without_source',
        domainName,
        `O domínio possui a spec ${domain.spec}, mas não declara código-fonte.`,
        'context/index.yaml',
        'warning'
      );
    }

    if (code.length > 0 && tests.length === 0) {
      addGap(
        gaps,
        'source_without_test',
        domainName,
        'O domínio declara código-fonte, mas não declara testes.',
        'context/index.yaml',
        'warning'
      );
    }

    if (objectCount(gates) === 0) {
      addGap(
        gaps,
        'domain_without_gate',
        domainName,
        'O domínio não declara gates de validação.',
        'context/index.yaml',
        'warning'
      );
    }

    const declaredPaths = [
      ['spec', domain.spec],
      ...code.map((relativePath) => ['code', relativePath]),
      ...tests.map((relativePath) => ['tests', relativePath])
    ];

    for (const [kind, relativePath] of declaredPaths) {
      if (typeof relativePath !== 'string' || fileExists(relativePath)) continue;
      addGap(
        gaps,
        'broken_path',
        relativePath,
        `O caminho declarado em ${domainName}.${kind} não existe.`,
        'context/index.yaml',
        'error',
        { path: relativePath, resolvedPath: displayResolvedPath(relativePath) }
      );
    }

    if (tests.length === 0) {
      addGap(
        gaps,
        'missing_test',
        domainName,
        'O domínio não possui testes declarados.',
        'context/index.yaml',
        'warning'
      );
    }
  }

  const taskIds = new Set(tasks.map((task) => task.id).filter(Boolean));
  const domainsWithTestTask = new Set(
    tasks
      .filter((task) => task.type === 'test')
      .map((task) => domainKeyFor(task.domain, domains))
      .filter(Boolean)
  );

  for (const task of tasks) {
    for (const relativePath of asArray(task.affected_areas)) {
      if (typeof relativePath !== 'string' || fileExists(relativePath)) continue;
      addGap(
        gaps,
        'broken_path',
        relativePath,
        `A área afetada declarada pela task ${task.id} não existe.`,
        'backlog.md',
        'error',
        { path: relativePath, resolvedPath: displayResolvedPath(relativePath) }
      );
    }

    for (const dependencyId of asArray(task.dependencies)) {
      if (taskIds.has(dependencyId)) continue;
      addGap(
        gaps,
        'missing_dependency',
        task.id,
        `A dependência ${dependencyId} não existe no backlog.`,
        'backlog.md',
        'error'
      );
    }

    const domainKey = domainKeyFor(task.domain, domains);
    if (task.type !== 'test' && domainKey && !domainsWithTestTask.has(domainKey)) {
      addGap(
        gaps,
        'missing_test',
        task.id,
        `A task não possui uma task de teste no domínio ${task.domain}.`,
        'backlog.md',
        'warning'
      );
    }
  }

  const processGraph = artifacts.processGraph || {};
  const engineGraph = artifacts.engineGraph || {};
  const loopProfile = artifacts.loopProfile || {};
  const specs = artifacts.specs || {};
  const canonicalSpecs = asArray(specs.canonicalSpecs || specs.canonical);
  const profileGates =
    (loopProfile.humanGates && loopProfile.humanGates.registered_human_gates) || [];

  addMismatch(
    gaps,
    'process_nodes',
    objectCount(processGraph.nodes),
    canonical.processNodes,
    processGraph.path || 'workflows/feature.yaml'
  );
  addMismatch(
    gaps,
    'process_edges',
    edgeCount(processGraph.edges),
    canonical.processEdges,
    processGraph.path || 'workflows/feature.yaml'
  );
  addMismatch(
    gaps,
    'engine_states',
    objectCount(engineGraph.states),
    canonical.engineStates,
    engineGraph.path || 'tools/agent-loop/lib/constants.js'
  );
  addMismatch(
    gaps,
    'engine_edges',
    edgeCount(engineGraph.edges),
    canonical.engineEdges,
    engineGraph.path || 'tools/agent-loop/lib/constants.js'
  );
  addMismatch(
    gaps,
    'graph_gate_count',
    objectCount(processGraph.humanGates),
    canonical.humanGates.length,
    processGraph.path || 'workflows/feature.yaml'
  );
  addMismatch(
    gaps,
    'profile_gate_count',
    objectCount(profileGates),
    canonical.humanGates.length,
    loopProfile.path || 'profiles/runrun-loop.yaml'
  );
  addMismatch(
    gaps,
    'engine_gate_count',
    objectCount(engineGraph.gates),
    canonical.humanGates.length,
    engineGraph.path || 'tools/agent-loop/lib/constants.js'
  );
  addMismatch(
    gaps,
    'engine_max_attempts',
    engineGraph.maxAttempts,
    canonical.maxAttempts,
    engineGraph.path || 'tools/agent-loop/lib/constants.js'
  );
  addMismatch(
    gaps,
    'profile_max_attempts',
    loopProfile.loop && loopProfile.loop.max_attempts,
    canonical.maxAttempts,
    loopProfile.path || 'profiles/runrun-loop.yaml'
  );
  addMismatch(
    gaps,
    'stop_on_first_gate_failure',
    loopProfile.gateExecution && loopProfile.gateExecution.stop_on_first_gate_failure,
    canonical.stopOnFirstGateFailure,
    loopProfile.path || 'profiles/runrun-loop.yaml'
  );
  addMismatch(
    gaps,
    'canonical_specs_count',
    canonicalSpecs.length,
    canonical.canonicalSpecs,
    'specs/'
  );
  addMismatch(
    gaps,
    'spec_template',
    specs.template && specs.template.isTemplate,
    canonical.templateNotSpec,
    (specs.template && specs.template.path) || 'specs/_template.md'
  );

  addGap(
    gaps,
    'verification_note',
    'loop_engine_tests',
    'O scanner não executa o Loop Engine; verificar com `npm test` em `tools/agent-loop`.',
    'tools/agent-loop',
    'info'
  );

  return gaps;
}

module.exports = { detectGaps };
