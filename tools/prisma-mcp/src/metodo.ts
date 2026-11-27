import { readFile, readdir } from 'node:fs/promises';
import { relative, resolve } from 'node:path';
import { parseDocument } from 'yaml';
import { z } from 'zod';
import { STATUSES, type MetodoStatus } from './mapping.js';

export interface MetodoEpico {
  id: string;
  titulo: string;
  arquivo: string;
  status?: string;
  body: string;
}
export interface MetodoHu {
  id: string;
  titulo: string;
  arquivo: string;
  status?: string;
  /** Épico pai (`epico: EPIC-XXXX`), opcional. */
  epico?: string;
  body: string;
}
export interface MetodoPrd { hu: string; arquivo: string; body: string; }
export interface MetodoTask {
  id: string;
  titulo: string;
  /** `HU-XXXX` ou `avulsa` (task em `avulsas/tasks/`, sob a História "Avulsas" no Prisma). */
  hu: string;
  arquivo: string;
  status?: MetodoStatus;
  depende_de: string[];
  cobre: string[];
  risco: 'normal' | 'alto';
  body: string;
}
export interface Metodo { hus: MetodoHu[]; tasks: MetodoTask[]; epicos: MetodoEpico[]; prds: MetodoPrd[]; }

export const EPIC_ID = /^EPIC-\d{4,}$/;
export const HU_ID = /^HU-\d{4,}$/;
export const TASK_ID = /^T-\d{4,}$/;
export const AVULSA = 'avulsa';
export const EPIC_STATUSES = ['aberta', 'planejada', 'concluida', 'cancelada'] as const;

const epicoSchema = z.object({
  id: z.string().regex(EPIC_ID),
  titulo: z.string().trim().min(1).max(480),
  status: z.enum(EPIC_STATUSES).optional(),
});
const huSchema = z.object({
  id: z.string().regex(HU_ID),
  titulo: z.string().trim().min(1).max(480),
  status: z.string().optional(),
  epico: z.string().regex(EPIC_ID).optional(),
});
const taskSchema = z.object({
  id: z.string().regex(TASK_ID),
  titulo: z.string().trim().min(1).max(480),
  hu: z.string().regex(/^(HU-\d{4,}|avulsa)$/),
  status: z.enum(STATUSES as [MetodoStatus, ...MetodoStatus[]]).optional(),
  depende_de: z.array(z.string().regex(TASK_ID)).default([]),
  cobre: z.array(z.string().regex(/^CA-\d+$/)).default([]),
  risco: z.enum(['normal', 'alto']).default('normal'),
});

export function parseFrontmatter(text: string, file = 'documento'): { metadata: unknown; body: string } {
  const match = /^﻿?---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(text);
  if (!match) throw new Error(`Frontmatter YAML ausente ou incompleto em ${file}.`);
  const document = parseDocument(match[1]);
  if (document.errors.length) throw new Error(`Frontmatter YAML inválido em ${file}.`);
  try { return { metadata: document.toJS({ maxAliasCount: 0 }), body: text.slice(match[0].length) }; }
  catch { throw new Error(`Frontmatter YAML inválido em ${file}.`); }
}

async function listDir(path: string) {
  try { return (await readdir(path, { withFileTypes: true })).sort((a, b) => a.name.localeCompare(b.name)); }
  catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return [];
    throw error;
  }
}
const issues = (error: z.ZodError) => error.issues.map(x => x.path.join('.')).join(', ');

/**
 * Lê `epicos/EPIC-*.md`, `HU-*\/hu.md`, `HU-*\/prd.md`, `HU-*\/tasks/*.md` e `avulsas/tasks/*.md`;
 * nunca modifica os documentos de origem.
 */
export async function readMetodo(directory: string): Promise<Metodo> {
  const root = resolve(directory);
  const hus: MetodoHu[] = [];
  const tasks: MetodoTask[] = [];
  const epicos: MetodoEpico[] = [];
  const prds: MetodoPrd[] = [];
  const pathInRepo = (path: string) => `docs/trabalho/${relative(root, path).replaceAll('\\', '/')}`;
  const readTasks = async (taskRoot: string, hu: string) => {
    for (const taskEntry of await listDir(taskRoot)) {
      if (!taskEntry.isFile() || !taskEntry.name.endsWith('.md')) continue;
      const taskPath = resolve(taskRoot, taskEntry.name);
      const parsedTask = parseFrontmatter(await readFile(taskPath, 'utf8'), pathInRepo(taskPath));
      const taskResult = taskSchema.safeParse(parsedTask.metadata);
      if (!taskResult.success) throw new Error(`Metadados da task inválidos em ${pathInRepo(taskPath)}: ${issues(taskResult.error)}.`);
      if (taskResult.data.hu !== hu) {
        throw new Error(hu === AVULSA ? `${taskResult.data.id} está em avulsas/ e deve declarar hu: avulsa.`
          : `${taskResult.data.id} declara HU diferente da pasta: ${hu}.`);
      }
      tasks.push({ ...taskResult.data, arquivo: pathInRepo(taskPath), body: parsedTask.body });
    }
  };
  for (const entry of await listDir(resolve(root, 'epicos'))) {
    if (!entry.isFile() || !/^EPIC-\d{4,}.*\.md$/.test(entry.name)) continue;
    const path = resolve(root, 'epicos', entry.name);
    const parsed = parseFrontmatter(await readFile(path, 'utf8'), pathInRepo(path));
    const result = epicoSchema.safeParse(parsed.metadata);
    if (!result.success) throw new Error(`Metadados do épico inválidos em ${pathInRepo(path)}: ${issues(result.error)}.`);
    epicos.push({ ...result.data, arquivo: pathInRepo(path), body: parsed.body });
  }
  for (const entry of await listDir(root)) {
    if (!entry.isDirectory() || !entry.name.startsWith('HU-')) continue;
    const huPath = resolve(root, entry.name, 'hu.md');
    const parsedHu = parseFrontmatter(await readFile(huPath, 'utf8'), pathInRepo(huPath));
    const huResult = huSchema.safeParse(parsedHu.metadata);
    if (!huResult.success) throw new Error(`Metadados da HU inválidos em ${pathInRepo(huPath)}: ${issues(huResult.error)}.`);
    const hu = { ...huResult.data, arquivo: pathInRepo(huPath), body: parsedHu.body };
    hus.push(hu);
    const prdPath = resolve(root, entry.name, 'prd.md');
    let prdText: string | undefined;
    try { prdText = await readFile(prdPath, 'utf8'); }
    catch (error) { if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error; }
    if (prdText !== undefined) prds.push({ hu: hu.id, arquivo: pathInRepo(prdPath), body: prdText });
    await readTasks(resolve(root, entry.name, 'tasks'), hu.id);
  }
  await readTasks(resolve(root, 'avulsas', 'tasks'), AVULSA);
  const ids = new Set<string>();
  for (const item of [...epicos, ...hus, ...tasks]) {
    if (ids.has(item.id)) throw new Error(`ID duplicado no método: ${item.id}.`);
    ids.add(item.id);
  }
  const knownEpics = new Set(epicos.map(e => e.id));
  for (const hu of hus) {
    if (hu.epico && !knownEpics.has(hu.epico)) throw new Error(`${hu.id} aponta para o épico ${hu.epico}, que não existe em epicos/.`);
  }
  const byTask = new Map(tasks.map(task => [task.id, task]));
  const visited = new Set<string>();
  const visiting = new Set<string>();
  const visit = (task: MetodoTask): void => {
    if (visited.has(task.id)) return;
    if (visiting.has(task.id)) throw new Error(`Dependência cíclica envolvendo ${task.id}.`);
    visiting.add(task.id);
    for (const id of task.depende_de) {
      const dependency = byTask.get(id);
      if (!dependency) throw new Error(`Dependência ${id} de ${task.id} não existe nos arquivos importados.`);
      visit(dependency);
    }
    visiting.delete(task.id);
    visited.add(task.id);
  };
  tasks.forEach(visit);
  return { hus, tasks, epicos, prds };
}

export const parseMetodo = readMetodo;

// ---------------------------------------------------------------------------------------------
// Varredura tolerante para a sincronização (T-0023): um documento com cabeçalho inválido vira um
// erro listado e não impede a sincronização dos outros. Links simbólicos são ignorados.

export type DocumentKind = 'epic' | 'hu' | 'prd' | 'task';
export interface LocalDocument {
  /** `EPIC-0001`, `HU-0001`, `HU-0001/prd` ou `T-0001`. */
  key: string;
  kind: DocumentKind;
  /** Caminho relativo à pasta do método, com `/`. */
  rel: string;
  /** hu: o épico (`epico:`); prd: a HU; task: a HU ou `AVULSAS`; épico: null. */
  parentKey: string | null;
  meta: Record<string, unknown>;
}
export interface DocumentScan { documents: LocalDocument[]; erros: { arquivo: string; erro: string }[]; }

export const AVULSAS_KEY = 'AVULSAS';
export const KIND_ORDER: Record<DocumentKind, number> = { epic: 0, hu: 1, prd: 2, task: 3 };

export async function scanMetodoDocuments(directory: string): Promise<DocumentScan> {
  const root = resolve(directory);
  const documents: LocalDocument[] = [];
  const erros: DocumentScan['erros'] = [];
  const rel = (path: string) => relative(root, path).replaceAll('\\', '/');
  const meta = async (path: string): Promise<Record<string, unknown> | undefined> => {
    try {
      const { metadata } = parseFrontmatter(await readFile(path, 'utf8'), rel(path));
      if (!metadata || typeof metadata !== 'object' || Array.isArray(metadata)) throw new Error(`Frontmatter YAML inválido em ${rel(path)}.`);
      return metadata as Record<string, unknown>;
    } catch (error) {
      erros.push({ arquivo: rel(path), erro: error instanceof Error ? error.message : 'Falha ao ler o arquivo.' });
      return undefined;
    }
  };
  const fail = (path: string, erro: string) => { erros.push({ arquivo: rel(path), erro }); };
  const scanTasks = async (taskRoot: string, huKey: string, declared: string) => {
    for (const entry of await listDir(taskRoot)) {
      if (!entry.isFile() || !entry.name.endsWith('.md')) continue;
      const path = resolve(taskRoot, entry.name);
      const m = await meta(path);
      if (!m) continue;
      if (typeof m.id !== 'string' || !TASK_ID.test(m.id)) { fail(path, 'id da task ausente ou inválido (use T-0001).'); continue; }
      if (m.hu !== declared) { fail(path, `${m.id} declara hu: ${String(m.hu)}, mas está em ${declared === AVULSA ? 'avulsas/' : `${declared}`}.`); continue; }
      documents.push({ key: m.id, kind: 'task', rel: rel(path), parentKey: huKey, meta: m });
    }
  };
  for (const entry of await listDir(resolve(root, 'epicos'))) {
    if (!entry.isFile() || !/^EPIC-\d{4,}.*\.md$/.test(entry.name)) continue;
    const path = resolve(root, 'epicos', entry.name);
    const m = await meta(path);
    if (!m) continue;
    if (typeof m.id !== 'string' || !EPIC_ID.test(m.id) || !entry.name.startsWith(m.id)) { fail(path, 'id do épico ausente, inválido ou diferente do nome do arquivo.'); continue; }
    documents.push({ key: m.id, kind: 'epic', rel: rel(path), parentKey: null, meta: m });
  }
  for (const entry of await listDir(root)) {
    if (!entry.isDirectory() || !entry.name.startsWith('HU-')) continue;
    const huPath = resolve(root, entry.name, 'hu.md');
    const files = await listDir(resolve(root, entry.name));
    if (!files.some(f => f.isFile() && f.name === 'hu.md')) { fail(huPath, 'pasta de HU sem hu.md.'); continue; }
    const m = await meta(huPath);
    if (!m) continue;
    if (typeof m.id !== 'string' || !HU_ID.test(m.id)) { fail(huPath, 'id da HU ausente ou inválido (use HU-0001).'); continue; }
    if (m.epico !== undefined && m.epico !== null && (typeof m.epico !== 'string' || !EPIC_ID.test(m.epico))) { fail(huPath, `${m.id}: epico inválido (use EPIC-0001).`); continue; }
    const huKey = m.id;
    documents.push({ key: huKey, kind: 'hu', rel: rel(huPath), parentKey: typeof m.epico === 'string' ? m.epico : null, meta: m });
    if (files.some(f => f.isFile() && f.name === 'prd.md')) {
      const prdPath = resolve(root, entry.name, 'prd.md');
      const pm = await meta(prdPath);
      if (pm) {
        if (pm.hu !== undefined && pm.hu !== huKey) fail(prdPath, `PRD declara hu: ${String(pm.hu)}, mas está na pasta de ${huKey}.`);
        else documents.push({ key: `${huKey}/prd`, kind: 'prd', rel: rel(prdPath), parentKey: huKey, meta: pm });
      }
    }
    await scanTasks(resolve(root, entry.name, 'tasks'), huKey, huKey);
  }
  await scanTasks(resolve(root, 'avulsas', 'tasks'), AVULSAS_KEY, AVULSA);
  const counts = new Map<string, number>();
  for (const d of documents) counts.set(d.key, (counts.get(d.key) ?? 0) + 1);
  const unique = documents.filter(d => {
    if (counts.get(d.key) === 1) return true;
    erros.push({ arquivo: d.rel, erro: `chave ${d.key} duplicada em mais de um arquivo; nenhum deles é sincronizado.` });
    return false;
  });
  unique.sort((a, b) => KIND_ORDER[a.kind] - KIND_ORDER[b.kind] || a.key.localeCompare(b.key));
  return { documents: unique, erros };
}
