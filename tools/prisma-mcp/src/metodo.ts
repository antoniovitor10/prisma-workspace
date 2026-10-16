import { readFile, readdir } from 'node:fs/promises';
import { relative, resolve } from 'node:path';
import { parseDocument } from 'yaml';
import { z } from 'zod';
import { STATUSES, type MetodoStatus } from './mapping.js';

export interface MetodoHu {
  id: string;
  titulo: string;
  arquivo: string;
  status?: string;
  body: string;
}
export interface MetodoTask {
  id: string;
  titulo: string;
  hu: string;
  arquivo: string;
  status?: MetodoStatus;
  depende_de: string[];
  cobre: string[];
  risco: 'normal' | 'alto';
  body: string;
}
export interface Metodo { hus: MetodoHu[]; tasks: MetodoTask[]; }

const huSchema = z.object({
  id: z.string().regex(/^HU-\d{4,}$/),
  titulo: z.string().trim().min(1).max(480),
  status: z.string().optional(),
});
const taskSchema = z.object({
  id: z.string().regex(/^T-\d{4,}$/),
  titulo: z.string().trim().min(1).max(480),
  hu: z.string().regex(/^HU-\d{4,}$/),
  status: z.enum(STATUSES as [MetodoStatus, ...MetodoStatus[]]).optional(),
  depende_de: z.array(z.string().regex(/^T-\d{4,}$/)).default([]),
  cobre: z.array(z.string().regex(/^CA-\d+$/)).default([]),
  risco: z.enum(['normal', 'alto']).default('normal'),
});

export function parseFrontmatter(text: string, file = 'documento'): { metadata: unknown; body: string } {
  const match = /^\uFEFF?---\r?\n([\s\S]*?)\r?\n---(?:\r?\n|$)/.exec(text);
  if (!match) throw new Error(`Frontmatter YAML ausente ou incompleto em ${file}.`);
  const document = parseDocument(match[1]);
  if (document.errors.length) throw new Error(`Frontmatter YAML inválido em ${file}.`);
  try { return { metadata: document.toJS({ maxAliasCount: 0 }), body: text.slice(match[0].length) }; }
  catch { throw new Error(`Frontmatter YAML inválido em ${file}.`); }
}

/** Lê somente hu.md e tasks/*.md; nunca modifica os documentos de origem. */
export async function readMetodo(directory: string): Promise<Metodo> {
  const root = resolve(directory);
  const hus: MetodoHu[] = [];
  const tasks: MetodoTask[] = [];
  const entries = await readdir(root, { withFileTypes: true });
  const pathInRepo = (path: string) => `docs/trabalho/${relative(root, path).replaceAll('\\', '/')}`;
  for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
    if (!entry.isDirectory() || !entry.name.startsWith('HU-')) continue;
    const huPath = resolve(root, entry.name, 'hu.md');
    const parsedHu = parseFrontmatter(await readFile(huPath, 'utf8'), pathInRepo(huPath));
    const huResult = huSchema.safeParse(parsedHu.metadata);
    if (!huResult.success) throw new Error(`Metadados da HU inválidos em ${pathInRepo(huPath)}: ${huResult.error.issues.map(x => x.path.join('.')).join(', ')}.`);
    const hu = { ...huResult.data, arquivo: pathInRepo(huPath), body: parsedHu.body };
    hus.push(hu);
    const taskRoot = resolve(root, entry.name, 'tasks');
    let taskEntries;
    try { taskEntries = await readdir(taskRoot, { withFileTypes: true }); }
    catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') continue;
      throw error;
    }
    for (const taskEntry of taskEntries.sort((a, b) => a.name.localeCompare(b.name))) {
      if (!taskEntry.isFile() || !taskEntry.name.endsWith('.md')) continue;
      const taskPath = resolve(taskRoot, taskEntry.name);
      const parsedTask = parseFrontmatter(await readFile(taskPath, 'utf8'), pathInRepo(taskPath));
      const taskResult = taskSchema.safeParse(parsedTask.metadata);
      if (!taskResult.success) throw new Error(`Metadados da task inválidos em ${pathInRepo(taskPath)}: ${taskResult.error.issues.map(x => x.path.join('.')).join(', ')}.`);
      if (taskResult.data.hu !== hu.id) throw new Error(`${taskResult.data.id} declara HU diferente da pasta: ${hu.id}.`);
      tasks.push({ ...taskResult.data, arquivo: pathInRepo(taskPath), body: parsedTask.body });
    }
  }
  const ids = new Set<string>();
  for (const item of [...hus, ...tasks]) {
    if (ids.has(item.id)) throw new Error(`ID duplicado no método: ${item.id}.`);
    ids.add(item.id);
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
  return { hus, tasks };
}

export const parseMetodo = readMetodo;
