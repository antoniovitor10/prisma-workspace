'use strict';

const REASONS = [
  'Constituição do projeto (sempre no contexto mínimo)',
  'Índice determinístico de contexto',
  'Histórias funcionais vinculadas ao domínio e à spec',
  'Spec governante do domínio',
  'Código-fonte declarado do domínio',
  'Teste declarado do domínio',
  'Gate declarado do domínio',
  'Contrato de agente declarado',
  'Dependência explícita entre tarefas',
  'Área afetada declarada pela task',
  'Spec vinculada à task no backlog',
  'Área de schema/migração declarada'
];

const CATEGORIES = [
  'Bootstrap',
  'Story',
  'Spec',
  'Source Code',
  'Tests',
  'Gates',
  'Contracts',
  'Dependencies',
  'Database',
  'Docs',
  'History'
];

const MINIMUM_CATEGORIES = ['Bootstrap', 'Story', 'Spec', 'Source Code', 'Tests', 'Gates'];
const EXPANDED_CATEGORIES = ['Contracts', 'Dependencies', 'Database', 'Docs', 'History'];

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

function candidateKey(candidate) {
  return `${candidate.path}::${candidate.category}`;
}

function addCandidate(list, seen, candidate) {
  const key = candidateKey(candidate);
  if (seen.has(key)) return;
  seen.add(key);
  list.push(candidate);
}

function findSpecByPath(artifacts, specPath) {
  if (!specPath) return null;
  return (
    artifacts.specs.canonicalSpecs.find((spec) => spec.path === specPath) || null
  );
}

function findSpecById(artifacts, specId) {
  if (!specId) return null;
  return artifacts.specs.canonicalSpecs.find((spec) => spec.id === specId) || null;
}

function findDomainKeyForSpecPath(artifacts, specPath) {
  if (!specPath) return null;
  const domains = artifacts.contextIndex.domains || {};
  for (const domainKey of Object.keys(domains)) {
    if (domains[domainKey].spec === specPath) return domainKey;
  }
  return null;
}

function mapTaskDomainToIndexDomain(taskDomain) {
  if (!taskDomain) return null;
  return TASK_DOMAIN_TO_INDEX_DOMAIN[taskDomain] || null;
}

function deriveContextForDomain(domainName, artifacts) {
  const candidates = [];
  const seen = new Set();

  addCandidate(candidates, seen, {
    path: 'AGENTS.md',
    category: 'Bootstrap',
    domain: null,
    source: 'AGENTS.md',
    reason: REASONS[0]
  });

  addCandidate(candidates, seen, {
    path: 'context/index.yaml',
    category: 'Bootstrap',
    domain: null,
    source: 'context/index.yaml',
    reason: REASONS[1]
  });

  const domains = artifacts.contextIndex.domains || {};
  const domainEntry = domains[domainName] || null;
  const domainValid = !!domainEntry;

  if (domainEntry) {
    if (artifacts.userStories && artifacts.userStories.exists) {
      addCandidate(candidates, seen, {
        path: artifacts.userStories.path,
        category: 'Story',
        domain: domainName,
        source: 'context/index.yaml',
        reason: REASONS[2]
      });
    }

    if (domainEntry.spec) {
      addCandidate(candidates, seen, {
        path: domainEntry.spec,
        category: 'Spec',
        domain: domainName,
        source: 'context/index.yaml',
        reason: REASONS[3]
      });
    }

    for (const codePath of domainEntry.code || []) {
      addCandidate(candidates, seen, {
        path: codePath,
        category: 'Source Code',
        domain: domainName,
        source: 'context/index.yaml',
        reason: REASONS[4]
      });
    }

    for (const testPath of domainEntry.tests || []) {
      addCandidate(candidates, seen, {
        path: testPath,
        category: 'Tests',
        domain: domainName,
        source: 'context/index.yaml',
        reason: REASONS[5]
      });
    }

    const gates = domainEntry.gates || {};
    for (const gateName of Object.keys(gates)) {
      addCandidate(candidates, seen, {
        path: `profiles/runrun-loop.yaml#gates.${gateName}`,
        category: 'Gates',
        domain: domainName,
        source: 'context/index.yaml',
        reason: REASONS[6]
      });
    }
  }

  for (const agent of artifacts.agents || []) {
    addCandidate(candidates, seen, {
      path: agent.path,
      category: 'Contracts',
      domain: domainName,
      source: 'agents',
      reason: REASONS[7]
    });
  }

  if (domainName === 'history' && domains.history && domains.history.spec) {
    addCandidate(candidates, seen, {
      path: domains.history.spec,
      category: 'History',
      domain: 'history',
      source: 'context/index.yaml',
      reason: REASONS[3]
    });
  }

  const spec = domainEntry ? findSpecByPath(artifacts, domainEntry.spec) : null;

  return {
    candidates,
    domain: domainName,
    domainValid,
    task: null,
    spec
  };
}

function deriveContextForTask(taskId, artifacts) {
  const task = artifacts.backlog.tasks.find((t) => t.id === taskId) || null;
  const taskFound = !!task;
  const domainName = taskFound ? mapTaskDomainToIndexDomain(task.domain) : null;

  const domainResult = domainName
    ? deriveContextForDomain(domainName, artifacts)
    : { candidates: [], domain: null, domainValid: false, task: null, spec: null };

  const candidates = domainResult.candidates.slice();
  const seen = new Set(candidates.map(candidateKey));

  if (taskFound && task.specPath) {
    addCandidate(candidates, seen, {
      path: task.specPath,
      category: 'Spec',
      domain: domainName,
      source: 'backlog.md',
      reason: REASONS[10]
    });
  }

  if (taskFound) {
    for (const areaPath of task.affected_areas || []) {
      if (/Migrations/.test(areaPath)) {
        addCandidate(candidates, seen, {
          path: areaPath,
          category: 'Database',
          domain: domainName,
          source: 'backlog.md',
          reason: REASONS[11]
        });
      } else {
        addCandidate(candidates, seen, {
          path: areaPath,
          category: 'Source Code',
          domain: domainName,
          source: 'backlog.md',
          reason: REASONS[9]
        });
      }
    }

    for (const depId of task.dependencies || []) {
      addCandidate(candidates, seen, {
        path: `backlog.md#${depId}`,
        category: 'Dependencies',
        domain: domainName,
        source: 'backlog.md',
        reason: REASONS[8]
      });
    }
  }

  const spec = taskFound ? findSpecById(artifacts, task.specId) : null;

  return {
    candidates,
    domain: domainName,
    domainValid: domainResult.domainValid,
    task,
    taskFound,
    spec
  };
}

function deriveContextForSpec(specId, artifacts) {
  const spec = findSpecById(artifacts, specId);

  if (!spec) {
    return {
      candidates: [],
      domain: null,
      domainValid: false,
      task: null,
      spec: null
    };
  }

  const domainName = findDomainKeyForSpecPath(artifacts, spec.path);

  if (!domainName) {
    return {
      candidates: [],
      domain: null,
      domainValid: false,
      task: null,
      spec
    };
  }

  const domainResult = deriveContextForDomain(domainName, artifacts);

  return {
    candidates: domainResult.candidates,
    domain: domainName,
    domainValid: domainResult.domainValid,
    task: null,
    spec
  };
}

function deriveMinimum(result) {
  return (result.candidates || []).filter((c) => MINIMUM_CATEGORIES.includes(c.category));
}

function deriveExpanded(result) {
  return (result.candidates || []).filter((c) => EXPANDED_CATEGORIES.includes(c.category));
}

function computeCategoryStates(result) {
  const candidates = result.candidates || [];
  const states = {};

  for (const category of CATEGORIES) {
    const has = candidates.some((c) => c.category === category);
    if (has) {
      states[category] = 'selected';
      continue;
    }

    switch (category) {
      case 'Bootstrap':
        states[category] = 'UNKNOWN';
        break;
      case 'Spec':
        states[category] = result.domainValid === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Story':
        states[category] = result.domainValid === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Source Code':
        states[category] = result.domainValid === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Tests':
        states[category] = result.domainValid === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Gates':
        states[category] = result.domainValid === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Contracts':
        states[category] = 'UNKNOWN';
        break;
      case 'Dependencies':
        states[category] = result.taskFound === false ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      case 'Database':
        states[category] = 'NOT SELECTED';
        break;
      case 'Docs':
        states[category] = 'NOT SELECTED';
        break;
      case 'History':
        states[category] = result.domain === 'history' ? 'UNKNOWN' : 'NOT SELECTED';
        break;
      default:
        states[category] = 'UNKNOWN';
    }
  }

  return states;
}

function previewTask(taskId, artifacts) {
  const result = deriveContextForTask(taskId, artifacts);
  const minimum = deriveMinimum(result);
  const expanded = deriveExpanded(result);
  const categoryStates = computeCategoryStates(result);

  return {
    simulated: true,
    taskId,
    domain: result.domain,
    spec: result.task ? result.task.specId : null,
    minimum,
    expanded,
    categoryStates
  };
}

const ALLOWED_RELATIONS = new Set([
  'governed_by',
  'implemented_by',
  'tested_by',
  'validated_by',
  'depends_on'
]);

function buildContextGraph(selection) {
  const nodes = new Map();
  const edges = [];

  function addNode(id, type, label) {
    if (!nodes.has(id)) nodes.set(id, { id, type, label });
  }

  function addEdge(from, to, relation) {
    if (!ALLOWED_RELATIONS.has(relation)) return;
    edges.push({ from, to, relation });
  }

  const task = selection.task || null;
  const spec = selection.spec || null;
  const domain = selection.domain || null;
  const candidates = selection.candidates || [];

  if (task && task.id) {
    addNode(`task:${task.id}`, 'task', task.id);
  }

  if (spec && spec.id) {
    addNode(`spec:${spec.id}`, 'spec', spec.id);
  }

  if (task && task.id && spec && spec.id) {
    addEdge(`task:${task.id}`, `spec:${spec.id}`, 'governed_by');
  }

  if (spec && spec.id) {
    for (const c of candidates) {
      if (c.category === 'Source Code') {
        addNode(`code:${c.path}`, 'code', c.path);
        addEdge(`spec:${spec.id}`, `code:${c.path}`, 'implemented_by');
      }
      if (c.category === 'Tests') {
        addNode(`test:${c.path}`, 'test', c.path);
        addEdge(`spec:${spec.id}`, `test:${c.path}`, 'tested_by');
      }
    }
  }

  if (domain) {
    for (const c of candidates) {
      if (c.category === 'Gates') {
        addNode(`domain:${domain}`, 'domain', domain);
        addNode(`gate:${c.path}`, 'gate', c.path);
        addEdge(`domain:${domain}`, `gate:${c.path}`, 'validated_by');
      }
    }
  }

  if (task && task.id && Array.isArray(task.dependencies)) {
    for (const depId of task.dependencies) {
      addNode(`task:${depId}`, 'task', depId);
      addEdge(`task:${task.id}`, `task:${depId}`, 'depends_on');
    }
  }

  return {
    nodes: Array.from(nodes.values()),
    edges
  };
}

module.exports = {
  REASONS,
  CATEGORIES,
  MINIMUM_CATEGORIES,
  EXPANDED_CATEGORIES,
  deriveContextForDomain,
  deriveContextForTask,
  deriveContextForSpec,
  deriveMinimum,
  deriveExpanded,
  computeCategoryStates,
  previewTask,
  buildContextGraph
};
