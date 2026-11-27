// API do método falsa e com estado (T-0023): documentos com revisões, tasks do método com pai,
// religação e comentários, no contrato de docs/... frente4-CONTRATO-API (T-0020/T-0021).
// Guarda o texto como recebeu (LF) e calcula o hash como o servidor: SHA-256 do texto em UTF-8.
import { createHash } from 'node:crypto';
import type { FakePrisma, Resposta } from './fake-prisma.js';

export const PROJECT = '11111111-2222-4333-8444-555555555555';
export const BOARD = '66666666-7777-4888-8999-aaaaaaaaaaaa';
export const TOKEN = `pmt_${'C'.repeat(43)}`;
export const BASE = `/api/projects/${PROJECT}/method`;

export interface FakeRevision { version: number; content: string; contentHash: string; authorName: string; via: 'ui' | 'mcp'; createdAtUtc: string; }
export interface FakeDoc {
  key: string; kind: 'epic' | 'hu' | 'prd' | 'task'; workItemId: string; filePath: string | null;
  parentKey: string | null; revisions: FakeRevision[];
}
export interface FakeTask {
  workItemId: string; id: string; title: string; status: string; hu: string | null; filePath: string | null;
  covers: string[]; risk: string | null; pr: string | null; dependsOn: string[]; authorized: boolean; canResume: boolean;
  parentId: string | null; comments: number;
}
export interface FakeMethod {
  docs: Map<string, FakeDoc>;
  tasks: Map<string, FakeTask>;
  /** Simula uma edição na tela: nova versão com o texto dado. */
  editInPrisma(key: string, content: string): FakeDoc;
  /** Cria um documento já existente no Prisma (versão 1, via ui). */
  seedDoc(key: string, content: string, extra?: Partial<FakeDoc>): FakeDoc;
  seedTask(id: string, extra?: Partial<FakeTask>): FakeTask;
  /** Antes de um PUT: pode devolver uma resposta (ex.: 409) ou mexer no estado. */
  beforePut?: (key: string, body: { content: string; baseVersion: number }) => Resposta | void;
  identity: Record<string, unknown>;
}

export const sha = (text: string) => createHash('sha256').update(text, 'utf8').digest('hex');
let seq = 0;
const newId = () => `00000000-0000-4000-8000-${(++seq).toString().padStart(12, '0')}`;
const titleOf = (content: string) => /^titulo:\s*"?(.*?)"?\s*$/m.exec(content)?.[1] ?? null;
const statusOf = (content: string) => /^status:\s*(\S+)/m.exec(content)?.[1] ?? null;

export function installFakeMethod(fake: FakePrisma, role = 'agente'): FakeMethod {
  const docs = new Map<string, FakeDoc>();
  const tasks = new Map<string, FakeTask>();
  const identity = {
    authenticationKind: 'methodToken', role, principalId: '99999999-0000-4000-8000-000000000001',
    userId: 'method:99999999-0000-4000-8000-000000000001', displayName: 'Agente de teste',
    organizationId: '00000000-0000-4000-8000-000000000001', projectId: PROJECT, projectName: 'metodo-teste',
    boardId: BOARD, boardName: 'metodo-teste', tokenId: '99999999-0000-4000-8000-0000000000aa',
    expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(), capabilities: ['read', 'comment', 'move'],
  };
  const state: FakeMethod = {
    docs, tasks, identity,
    editInPrisma(key, content) {
      const doc = docs.get(key);
      if (!doc) throw new Error(`fake: ${key} não existe`);
      doc.revisions.push(revision(doc.revisions.length + 1, content, 'ui'));
      return doc;
    },
    seedDoc(key, content, extra = {}) {
      const kind = kindOf(key);
      const doc: FakeDoc = { key, kind, workItemId: extra.workItemId ?? workItemFor(key, kind), filePath: extra.filePath ?? null,
        parentKey: extra.parentKey ?? null, revisions: [revision(1, content, 'ui')] };
      docs.set(key, doc);
      return doc;
    },
    seedTask(id, extra = {}) {
      const t: FakeTask = { workItemId: newId(), id, title: `${id} · Task ${id}`, status: 'backlog', hu: 'HU-0001', filePath: null,
        covers: [], risk: 'normal', pr: null, dependsOn: [], authorized: false, canResume: false, parentId: null, comments: 0, ...extra };
      tasks.set(id, t);
      return t;
    },
  };
  function revision(version: number, content: string, via: 'ui' | 'mcp'): FakeRevision {
    return { version, content, contentHash: sha(content), authorName: via === 'ui' ? 'Vitor' : 'Agente de teste', via,
      createdAtUtc: new Date(Date.UTC(2026, 10, 1, 0, 0, version)).toISOString() };
  }
  function kindOf(key: string): FakeDoc['kind'] {
    if (key.startsWith('EPIC-')) return 'epic';
    if (key.endsWith('/prd')) return 'prd';
    if (key.startsWith('T-')) return 'task';
    return 'hu';
  }
  function workItemFor(key: string, kind: FakeDoc['kind']): string {
    if (kind === 'prd') return docs.get(key.replace('/prd', ''))?.workItemId ?? newId();
    if (kind === 'task') return tasks.get(key)?.workItemId ?? newId();
    return newId();
  }
  function historyKeyOf(workItemId: string | null): string | null {
    if (!workItemId) return null;
    for (const d of docs.values()) if (d.kind === 'hu' && d.workItemId === workItemId) return d.key;
    return null;
  }
  function summary(doc: FakeDoc) {
    const last = doc.revisions[doc.revisions.length - 1];
    return { key: doc.key, kind: doc.kind, workItemId: doc.workItemId, filePath: doc.filePath, title: titleOf(last.content),
      status: doc.kind === 'hu' || doc.kind === 'epic' ? statusOf(last.content) : null, version: last.version,
      contentHash: last.contentHash, updatedByName: last.authorName, updatedVia: last.via, updatedAtUtc: last.createdAtUtc };
  }
  function full(doc: FakeDoc) {
    const last = doc.revisions[doc.revisions.length - 1];
    const parentKey = doc.kind === 'task' ? historyKeyOf(tasks.get(doc.key)?.parentId ?? null) : doc.kind === 'hu' ? doc.parentKey : null;
    return { ...summary(doc), content: last.content, parentKey, locked: false, canEdit: true };
  }
  const problem = (status: number, code: string): Resposta => ({ status, json: { status, code, title: code } });
  const taskDto = (t: FakeTask) => { const { parentId: _p, comments: _c, ...dto } = t; return dto; };

  fake.on('GET', '/api/method/me', { json: identity });
  fake.on('GET', `${BASE}/rules`, { json: { identity, project: { projectId: PROJECT, boardId: BOARD, projectName: 'metodo-teste' },
    allowedTransitions: { backlog: ['bloqueada'] }, conventions: [], requiresInteractiveAuthorization: true, requiresInteractiveResumeApproval: true } });
  fake.on('GET', `${BASE}/tasks`, () => ({ json: [...tasks.values()].map(taskDto) }));
  fake.on('POST', `${BASE}/tasks`, r => {
    const b = JSON.parse(r.body);
    let parentId: string | null = null;
    if (b.hu === 'avulsa') {
      if (!docs.has('AVULSAS')) state.seedDoc('AVULSAS', '---\nid: AVULSAS\ntitulo: "Avulsas"\n---\n');
      parentId = docs.get('AVULSAS')!.workItemId;
    } else parentId = docs.get(b.hu)?.workItemId ?? null;
    const t = state.seedTask(b.id, { title: `${b.id} · ${b.title}`, status: b.status, hu: b.hu, filePath: b.filePath, covers: b.covers, risk: b.risk, parentId });
    return { status: 201, json: taskDto(t) };
  });
  fake.on('GET', /^\/api\/WorkItems\/[0-9a-f-]+$/, r => {
    const id = r.path.split('/')[3];
    const t = [...tasks.values()].find(x => x.workItemId === id);
    return t ? { json: { id, parentId: t.parentId, links: [], subtasks: [] } } : { status: 404, json: { title: 'x' } };
  });
  fake.on('GET', /^\/api\/WorkItems\/[0-9a-f-]+\/comments$/, r => {
    const id = r.path.split('/')[3];
    const t = [...tasks.values()].find(x => x.workItemId === id);
    return { json: Array.from({ length: t?.comments ?? 0 }, (_, i) => ({ id: `c${i}`, workItemId: id, userId: 'u', text: 'x', createdAt: '', displayName: 'u', userName: 'u' })) };
  });
  fake.on('GET', `${BASE}/documents`, r => {
    const query = new URL(r.url ?? r.path, 'http://x').searchParams;
    const desde = query.get('desde');
    return { json: [...docs.values()].map(summary).filter(s => !desde || s.updatedAtUtc >= desde) };
  });
  const route = new RegExp(`^${BASE}/documents/(.+)$`);
  const handler = (r: { method: string; path: string; body: string }): Resposta => {
    const rest = route.exec(r.path)![1];
    const relink = /^tasks\/(T-\d{4,})\/parent$/.exec(rest);
    if (relink) {
      if (r.method !== 'POST') return { status: 405 };
      const t = tasks.get(relink[1]);
      if (!t) return problem(404, 'task_not_found');
      const { huKey } = JSON.parse(r.body);
      const history = docs.get(huKey);
      if (!history || history.kind !== 'hu') return problem(404, 'method_document_not_found');
      t.parentId = history.workItemId;
      return { json: { taskId: t.id, workItemId: t.workItemId, huKey, parentWorkItemId: history.workItemId } };
    }
    const m = /^(EPIC-\d{4,}|HU-\d{4,}|AVULSAS|T-\d{4,})(\/prd)?(?:\/revisions(?:\/(\d+))?)?$/.exec(rest);
    if (!m) return problem(400, 'invalid_method_document');
    const key = m[1] + (m[2] ?? '');
    const isRevisions = rest.includes('/revisions');
    const doc = docs.get(key);
    if (r.method === 'GET') {
      if (!doc) return problem(404, 'method_document_not_found');
      if (!isRevisions) return { json: full(doc) };
      if (m[3]) {
        const rev = doc.revisions.find(x => x.version === Number(m[3]));
        return rev ? { json: rev } : problem(404, 'method_document_not_found');
      }
      return { json: [...doc.revisions].reverse().map(({ content: _c, ...x }) => x) };
    }
    if (r.method === 'PUT' && !isRevisions) {
      const body = JSON.parse(r.body) as { content: string; baseVersion: number; filePath?: string; parentKey?: string };
      const injected = state.beforePut?.(key, body);
      if (injected) return injected;
      const current = docs.get(key);
      const currentVersion = current ? current.revisions.length : 0;
      if (body.baseVersion !== currentVersion) return problem(409, 'method_document_version_conflict');
      if (!current) {
        const kind = kindOf(key);
        if (kind === 'prd' && !docs.has(key.replace('/prd', ''))) return problem(404, 'method_document_not_found');
        if (kind === 'task' && !tasks.has(key)) return problem(404, 'task_not_found');
        const created: FakeDoc = { key, kind, workItemId: workItemFor(key, kind), filePath: body.filePath ?? null,
          parentKey: kind === 'hu' ? body.parentKey ?? null : null, revisions: [revision(1, body.content, 'mcp')] };
        docs.set(key, created);
        return { json: full(created) };
      }
      current.revisions.push(revision(currentVersion + 1, body.content, 'mcp'));
      if (body.filePath) current.filePath = body.filePath;
      return { json: full(current) };
    }
    return { status: 405 };
  };
  fake.on('GET', route, handler);
  fake.on('PUT', route, handler);
  fake.on('POST', route, handler);
  return state;
}
