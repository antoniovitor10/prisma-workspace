'use strict';

const http = require('http');
const fs = require('fs');
const path = require('path');
const { buildModel } = require('./lib/build-model');
const { readBacklog } = require('./lib/read-artifacts');
const { REPO_ROOT, repoPath } = require('./lib/paths');

const PORT = 3847;
const DECISIONS_PATH = repoPath('.agent-state', 'gate-decisions.json');
const MANUAL_VALIDATION_PATH = repoPath('.agent-state', 'manual-validation.json');
const STORY_TASKS_PATH = repoPath('.agent-state', 'story-tasks.json');
const MANUAL_RESULTS = new Set(['pending', 'conform', 'changes_requested', 'not_implemented', 'blocked']);
const ACTIVE_STORY_TASK_STATUSES = new Set(['pending_analysis', 'ready', 'in_progress', 'blocked']);
const STORY_TASK_CLASSIFICATIONS = new Set(['defect', 'missing_implementation', 'story_change', 'test_blocker']);

function readDecisions() {
  try {
    if (fs.existsSync(DECISIONS_PATH)) {
      return JSON.parse(fs.readFileSync(DECISIONS_PATH, 'utf8'));
    }
  } catch (_) { /* ignora */ }
  return [];
}

function writeDecisions(decisions) {
  const dir = path.dirname(DECISIONS_PATH);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(DECISIONS_PATH, JSON.stringify(decisions, null, 2), 'utf8');
}

function readManualValidation() {
  try {
    if (fs.existsSync(MANUAL_VALIDATION_PATH)) {
      const parsed = JSON.parse(fs.readFileSync(MANUAL_VALIDATION_PATH, 'utf8'));
      return {
        version: 1,
        updatedAt: parsed.updatedAt || null,
        entries: normalizeManualEntries(parsed.entries) || {}
      };
    }
  } catch (_) { /* retorna um estado vazio e recuperável */ }
  return { version: 1, updatedAt: null, entries: {} };
}

function normalizeManualEntries(entries) {
  if (!entries || typeof entries !== 'object' || Array.isArray(entries)) return null;
  const normalized = {};
  for (const [storyId, value] of Object.entries(entries)) {
    if (!/^US-[A-Z0-9-]+-\d{3}$/.test(storyId) || !value || typeof value !== 'object') continue;
    const requestedResult = value.result === 'approved' ? 'conform' : value.result;
    const result = MANUAL_RESULTS.has(requestedResult) ? requestedResult : 'pending';
    normalized[storyId] = {
      result,
      observation: String(value.observation || '').slice(0, 5000),
      updatedAt: typeof value.updatedAt === 'string' ? value.updatedAt : null
    };
  }
  return normalized;
}

function readStoryTasksState() {
  try {
    if (fs.existsSync(STORY_TASKS_PATH)) {
      const parsed = JSON.parse(fs.readFileSync(STORY_TASKS_PATH, 'utf8'));
      return { version: 1, updatedAt: parsed.updatedAt || null, tasks: Array.isArray(parsed.tasks) ? parsed.tasks : [] };
    }
  } catch (_) { /* retorna fila vazia e recuperável */ }
  return { version: 1, updatedAt: null, tasks: [] };
}

function writeStoryTasksState(tasks) {
  const dir = path.dirname(STORY_TASKS_PATH);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const payload = { version: 1, updatedAt: new Date().toISOString(), tasks };
  const temporaryPath = `${STORY_TASKS_PATH}.tmp`;
  fs.writeFileSync(temporaryPath, JSON.stringify(payload, null, 2), 'utf8');
  fs.renameSync(temporaryPath, STORY_TASKS_PATH);
  return payload;
}

function syncStoryTasks(entries) {
  const model = buildModel();
  const stories = new Map((model.userStories || []).map(story => [story.id, story]));
  const state = readStoryTasksState();
  const touched = [];
  let changed = false;

  for (const [storyId, entry] of Object.entries(entries)) {
    const observation = String(entry.observation || '').trim();
    const actionable = observation.length > 0 || ['changes_requested', 'not_implemented'].includes(entry.result);
    const story = stories.get(storyId);
    if (!actionable || !story) continue;

    let task = state.tasks.find(item => item.storyId === storyId && ACTIVE_STORY_TASK_STATUSES.has(item.status));
    const now = new Date().toISOString();
    const sourceResult = entry.result;
    const title = sourceResult === 'not_implemented'
      ? `Implementar comportamento ausente: ${story.title}`
      : sourceResult === 'changes_requested'
        ? `Analisar divergência: ${story.title}`
        : `Analisar observação: ${story.title}`;
    const requirement = observation || `Analisar o cenário esperado “${story.scenario.then}” e classificar a ação necessária.`;

    if (!task) {
      const sequence = state.tasks.filter(item => item.storyId === storyId).length + 1;
      task = {
        id: `STORY-TASK-${storyId}-${String(sequence).padStart(2, '0')}`,
        storyId,
        specId: story.specId,
        specPath: story.specPath,
        title,
        requirement,
        observation,
        sourceResult,
        classification: 'awaiting_ai_triage',
        status: 'pending_analysis',
        createdAt: now,
        updatedAt: now,
        archivedAt: null
      };
      state.tasks.push(task);
      changed = true;
    } else if (task.observation !== observation || task.sourceResult !== sourceResult || task.title !== title || task.requirement !== requirement) {
      Object.assign(task, { observation, sourceResult, title, requirement, updatedAt: now });
      changed = true;
    }
    touched.push(task);
  }

  if (changed) writeStoryTasksState(state.tasks);
  return { tasks: state.tasks, touched };
}

function writeManualValidation(entries) {
  const dir = path.dirname(MANUAL_VALIDATION_PATH);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const payload = { version: 1, updatedAt: new Date().toISOString(), entries };
  const temporaryPath = `${MANUAL_VALIDATION_PATH}.tmp`;
  fs.writeFileSync(temporaryPath, JSON.stringify(payload, null, 2), 'utf8');
  fs.renameSync(temporaryPath, MANUAL_VALIDATION_PATH);
  return payload;
}

function updateStoryTask(taskId, action, classification, resolution) {
  if (!STORY_TASK_CLASSIFICATIONS.has(classification)) {
    return { error: 'Classificação inválida.' };
  }
  const state = readStoryTasksState();
  const task = state.tasks.find(item => item.id === taskId);
  if (!task) return { error: 'Tarefa de homologação não encontrada.' };
  const now = new Date().toISOString();
  task.classification = classification;
  task.updatedAt = now;

  if (action === 'triage') {
    task.status = classification === 'test_blocker' ? 'blocked' : 'ready';
  } else {
    task.status = 'archived';
    task.resolution = String(resolution || '').slice(0, 5000);
    task.archivedAt = now;
    const validation = readManualValidation();
    validation.entries[task.storyId] = {
      result: 'pending',
      observation: '',
      updatedAt: now
    };
    writeManualValidation(validation.entries);
  }

  writeStoryTasksState(state.tasks);
  regenerateModel();
  return { task };
}

function readSpecFiles() {
  const specsDir = repoPath('specs');
  if (!fs.existsSync(specsDir)) return [];
  return fs.readdirSync(specsDir)
    .filter(name => name.endsWith('.md') && name !== '_template.md')
    .map(name => {
      const filePath = path.join(specsDir, name);
      const text = fs.readFileSync(filePath, 'utf8');
      const titleMatch = text.match(/^#\s*(SPEC-[^:\n]+):\s*(.+?)\s*$/m);
      const statusMatch = text.match(/\*\*Status:\*\*\s*(.+?)\s*$/m);
      return {
        id: titleMatch ? titleMatch[1].trim() : name,
        title: titleMatch ? titleMatch[2].trim() : name,
        status: statusMatch ? statusMatch[1].trim() : 'unknown',
        path: 'specs/' + name,
        fileName: name,
        body: text
      };
    });
}

function readBacklogTasks() {
  try {
    return readBacklog().tasks || [];
  } catch (_) {
    return [];
  }
}

function getGatesResponse() {
  const specs = readSpecFiles();
  const decisions = readDecisions();
  const tasks = readBacklogTasks();

  const gateDefinitions = [
    { id: 'G-SPEC', description: 'Aprovacao de especificacao (draft -> approved)', always_required: true, condition: 'Spec em status draft ou review bloqueia implementacao' },
    { id: 'G-MIGRATION', description: 'Alteracoes de schema no SQL Server ou EF Core Migrations', always_required: false, condition: 'Qualquer alteracao de schema, migration ou estrutura de banco' },
    { id: 'G-WORKFLOW', description: 'Alteracoes nas regras de workflow, transicoes de status e limites WIP', always_required: false, condition: 'Mudanca em estados, transicoes ou limites de WIP' },
    { id: 'G-HISTORY', description: 'Alteracoes na estrutura imutavel de auditoria ou historico', always_required: false, condition: 'Qualquer mudanca em StageHistory, AuditLog ou eventos imutaveis' },
    { id: 'G-SCOPE', description: 'Duvidas ou alteracoes de escopo nao previstas em DECISIONS.md', always_required: false, condition: 'Requisito, arquitetura ou regra nao coberta em DECISIONS.md ou specs' },
    { id: 'G-DEPLOY', description: 'Liberacao ou promocao para ambientes institucionais', always_required: false, condition: 'Deploy, promocao ou liberacao para ambiente de producao' },
    { id: 'G-COMPLETION', description: 'Aprovacao humana antes da conclusao (condicional)', always_required: false, condition: 'Quando human_completion_required == true no perfil da task' }
  ];

  const gates = gateDefinitions.map(def => {
    const gateDecisions = decisions.filter(d => d.gateId === def.id);
    const relatedTasks = tasks.filter(t => {
      const hg = String(t.human_gate || '').toLowerCase();
      return hg.includes(def.id.toLowerCase()) || hg.includes('sim');
    });

    let status = 'sem pendencias';
    if (def.id === 'G-SPEC') {
      const pending = specs.filter(s => ['draft', 'review'].includes(s.status.toLowerCase()));
      status = pending.length > 0 ? `${pending.length} spec(s) aguardando aprovacao` : 'todas aprovadas';
    } else if (relatedTasks.length > 0) {
      const approvedForAll = relatedTasks.every(t =>
        gateDecisions.some(d => d.targetId === t.id && d.status === 'approved')
      );
      status = approvedForAll ? 'aprovado' : `${relatedTasks.length} tarefa(s) relacionada(s)`;
    }

    return {
      ...def,
      status,
      specs: def.id === 'G-SPEC' ? specs : undefined,
      tasks: relatedTasks.map(t => ({ id: t.id, requirement: t.requirement, priority: t.priority })),
      decisions: gateDecisions
    };
  });

  return { gates, specs, decisions };
}

function approveSpec(specPath) {
  const fullPath = repoPath(specPath);
  if (!fs.existsSync(fullPath)) return { success: false, message: 'Arquivo nao encontrado: ' + specPath };
  let text = fs.readFileSync(fullPath, 'utf8');
  const statusRegex = /(\*\*Status:\*\*)\s*.+?\s*$/m;
  if (!statusRegex.test(text)) return { success: false, message: 'Campo Status nao encontrado no arquivo' };
  text = text.replace(statusRegex, '$1 approved');
  fs.writeFileSync(fullPath, text, 'utf8');
  return { success: true, message: 'Spec aprovada: ' + specPath };
}

function regenerateModel() {
  try {
    const model = buildModel();
    const outputPath = path.join(__dirname, 'model.json');
    fs.writeFileSync(outputPath, JSON.stringify(model, null, 2), 'utf8');
    // Copia para o public da SPA
    const publicPath = path.join(__dirname, 'web', 'public', 'model.json');
    if (fs.existsSync(path.dirname(publicPath))) {
      fs.writeFileSync(publicPath, JSON.stringify(model, null, 2), 'utf8');
    }
    return true;
  } catch (err) {
    console.error('Erro ao regenerar model.json:', err.message);
    return false;
  }
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', chunk => chunks.push(chunk));
    req.on('end', () => {
      try { resolve(JSON.parse(Buffer.concat(chunks).toString())); }
      catch (err) { reject(err); }
    });
    req.on('error', reject);
  });
}

function json(res, status, data) {
  res.writeHead(status, {
    'Content-Type': 'application/json',
    'Access-Control-Allow-Origin': '*',
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type'
  });
  res.end(JSON.stringify(data));
}

const server = http.createServer(async (req, res) => {
  // CORS preflight
  if (req.method === 'OPTIONS') {
    res.writeHead(204, {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type'
    });
    return res.end();
  }

  const url = new URL(req.url, `http://localhost:${PORT}`);

  try {
    // GET /api/gates
    if (req.method === 'GET' && url.pathname === '/api/gates') {
      return json(res, 200, getGatesResponse());
    }

    // POST /api/gates/approve
    if (req.method === 'POST' && url.pathname === '/api/gates/approve') {
      const body = await readBody(req);
      const { gateId, targetId, approvedBy, reason } = body;
      if (!gateId || !targetId || !approvedBy) {
        return json(res, 400, { success: false, message: 'Campos obrigatorios: gateId, targetId, approvedBy' });
      }

      const decision = {
        gateId,
        targetId,
        status: 'approved',
        approvedBy,
        reason: reason || null,
        timestamp: new Date().toISOString()
      };

      // Para G-SPEC, altera o arquivo da spec
      if (gateId === 'G-SPEC') {
        const result = approveSpec(targetId);
        if (!result.success) return json(res, 400, result);
      }

      // Registra a decisao
      const decisions = readDecisions();
      decisions.push(decision);
      writeDecisions(decisions);

      // Regenera model.json
      regenerateModel();

      return json(res, 200, { success: true, message: `Gate ${gateId} aprovado para ${targetId}`, decision });
    }

    // POST /api/gates/reject
    if (req.method === 'POST' && url.pathname === '/api/gates/reject') {
      const body = await readBody(req);
      const { gateId, targetId, rejectedBy, reason } = body;
      if (!gateId || !targetId || !rejectedBy) {
        return json(res, 400, { success: false, message: 'Campos obrigatorios: gateId, targetId, rejectedBy' });
      }

      const decision = {
        gateId,
        targetId,
        status: 'rejected',
        approvedBy: rejectedBy,
        reason: reason || null,
        timestamp: new Date().toISOString()
      };

      const decisions = readDecisions();
      decisions.push(decision);
      writeDecisions(decisions);

      return json(res, 200, { success: true, message: `Gate ${gateId} rejeitado para ${targetId}`, decision });
    }

    // GET /api/model
    if (req.method === 'GET' && url.pathname === '/api/model') {
      regenerateModel();
      const modelPath = path.join(__dirname, 'model.json');
      const model = JSON.parse(fs.readFileSync(modelPath, 'utf8'));
      return json(res, 200, model);
    }

    // GET /api/manual-validation
    if (req.method === 'GET' && url.pathname === '/api/manual-validation') {
      return json(res, 200, readManualValidation());
    }

    if (req.method === 'GET' && url.pathname === '/api/story-tasks') {
      return json(res, 200, readStoryTasksState());
    }

    if (req.method === 'POST' && (url.pathname === '/api/story-tasks/triage' || url.pathname === '/api/story-tasks/resolve')) {
      const body = await readBody(req);
      const action = url.pathname.endsWith('/triage') ? 'triage' : 'resolve';
      const result = updateStoryTask(String(body.taskId || ''), action, String(body.classification || ''), body.resolution);
      if (result.error) return json(res, 400, { success: false, message: result.error });
      return json(res, 200, { success: true, ...result });
    }

    // POST /api/manual-validation
    if (req.method === 'POST' && url.pathname === '/api/manual-validation') {
      const body = await readBody(req);
      const entries = normalizeManualEntries(body.entries);
      if (!entries) {
        return json(res, 400, { success: false, message: 'O campo entries deve ser um objeto.' });
      }
      const saved = writeManualValidation(entries);
      const storyTaskResult = syncStoryTasks(entries);
      regenerateModel();
      return json(res, 200, { success: true, ...saved, storyTasks: storyTaskResult.touched });
    }

    json(res, 404, { error: 'Rota nao encontrada' });
  } catch (err) {
    console.error('Erro no servidor:', err.stack || err.message || err);
    json(res, 500, { error: String(err.message || err) });
  }
});

// Migra homologações já salvas para a fila story-first sem exigir nova edição no navegador.
syncStoryTasks(readManualValidation().entries);

server.listen(PORT, () => {
  console.log(`Context Explorer API rodando em http://localhost:${PORT}`);
  console.log('Endpoints:');
  console.log('  GET  /api/gates          - listar gates e status');
  console.log('  POST /api/gates/approve  - aprovar gate');
  console.log('  POST /api/gates/reject   - rejeitar gate');
  console.log('  GET  /api/model          - regenerar e retornar model.json');
  console.log('  GET  /api/manual-validation  - carregar homologacao por historia');
  console.log('  POST /api/manual-validation  - salvar homologacao por historia');
  console.log('  GET  /api/story-tasks        - listar tarefas geradas pela homologacao');
  console.log('  POST /api/story-tasks/triage - classificar tarefa gerada pela homologacao');
  console.log('  POST /api/story-tasks/resolve - arquivar tarefa, limpar observacao e pedir revalidacao');
});
