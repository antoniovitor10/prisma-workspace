#!/usr/bin/env node
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { PrismaApi, type MethodTaskDto } from './api.js';
import { isMetodoStatus } from './mapping.js';
import { readMetodo } from './metodo.js';
import { ensureProjeto } from './setup.js';
import { assertCreationStatus, assertTransition } from './rules.js';

export interface ImportOptions { dryRun?: boolean; sincronizarStatus?: boolean; }
export interface ImportSummary {
  criados: number; atualizados: number; semMudanca: number; vinculosCriados: number;
  planejados: number; dryRun: boolean; projectId?: string; boardId?: string;
}
/** O ID do método no projeto é a chave; conteúdo local não concede autorização. */
export async function importMetodo(directory: string, options: ImportOptions = {}, suppliedApi?: PrismaApi): Promise<ImportSummary> {
  const metodo = await readMetodo(directory);
  const summary: ImportSummary = { criados: 0, atualizados: 0, semMudanca: 0,
    vinculosCriados: 0, planejados: metodo.tasks.length, dryRun: options.dryRun ?? false };
  if (options.dryRun) return summary;
  if (!summary.planejados) throw new Error('Nenhuma task encontrada na pasta informada.');
  const api = suppliedApi ?? PrismaApi.fromEnv();
  const setup = await ensureProjeto(api);
  const rules = await api.getMethodRules();
  summary.projectId = setup.projectId; summary.boardId = setup.boardId;
  const byId = new Map<string, MethodTaskDto>();
  const outcomes = new Map<string, 'created' | 'updated' | 'unchanged'>();
  for (const item of await api.listMethodTasks()) {
    if (!/^T-\d{4,}$/.test(item.id) || byId.has(item.id)) throw new Error('Identidade de task inválida ou duplicada na API. Pare e avise.');
    byId.set(item.id, item);
  }
  // Valida o lote antes das escritas; a API revalida cada operação na transação.
  for (const source of metodo.tasks) {
    if (source.titulo.length + source.id.length + 3 > 500) throw new Error(`Título de ${source.id} ultrapassa 500 caracteres.`);
    const current = byId.get(source.id);
    const desired = isMetodoStatus(source.status) ? source.status : 'backlog';
    if (!current) assertCreationStatus(rules.identity.role, desired);
    else if (options.sincronizarStatus && isMetodoStatus(source.status) && source.status !== current.status) {
      if (!isMetodoStatus(current.status)) throw new Error(`Status remoto inválido para ${source.id}. Pare e avise.`);
      assertTransition(rules, current.status, source.status, current.canResume);
    }
  }
  for (const source of metodo.tasks) {
    let current = byId.get(source.id);
    const content = { title: source.titulo, hu: source.hu, filePath: source.arquivo, covers: source.cobre, risk: source.risco };
    if (!current) {
      current = await api.createMethodTask({ id: source.id, ...content, status: isMetodoStatus(source.status) ? source.status : 'backlog' });
      byId.set(source.id, current); outcomes.set(source.id, 'created'); continue;
    }
    const title = current.title.startsWith(`${source.id} · `) ? current.title.slice(source.id.length + 3) : current.title;
    let changed = false;
    if (title !== content.title || current.hu !== content.hu || current.filePath !== content.filePath
      || current.risk !== content.risk || JSON.stringify(current.covers) !== JSON.stringify(content.covers)) {
      current = await api.updateMethodTask(source.id, content);
      byId.set(source.id, current); changed = true;
    }
    if (options.sincronizarStatus && isMetodoStatus(source.status) && current.status !== source.status) {
      current = await api.moveMethodTask(source.id, source.status, 'Sincronização explícita do frontmatter pela importação.');
      byId.set(source.id, current); changed = true;
    }
    outcomes.set(source.id, changed ? 'updated' : 'unchanged');
  }
  for (const source of metodo.tasks) {
    const current = byId.get(source.id)!;
    const details = await api.getWorkItem(current.workItemId);
    for (const dependencyId of new Set(source.depende_de)) {
      const target = byId.get(dependencyId);
      if (!target) throw new Error(`Dependência ${dependencyId} ausente. Pare e avise.`);
      if (details.links.some(link => !link.isIncoming && link.type === 1 && link.relatedWorkItemId === target.workItemId)) continue;
      await api.addLink(current.workItemId, target.workItemId, 1);
      summary.vinculosCriados++;
      if (outcomes.get(source.id) === 'unchanged') outcomes.set(source.id, 'updated');
    }
  }
  for (const outcome of outcomes.values()) {
    if (outcome === 'created') summary.criados++;
    else if (outcome === 'updated') summary.atualizados++;
    else summary.semMudanca++;
  }
  return summary;
}

export async function main(args = process.argv.slice(2)): Promise<void> {
  const paths = args.filter(x => !x.startsWith('--'));
  const invalid = args.filter(x => x.startsWith('--') && x !== '--dry-run' && x !== '--sincronizar-status');
  if (paths.length !== 1 || invalid.length) throw new Error('Uso: prisma-metodo-import <pasta docs/trabalho> [--dry-run] [--sincronizar-status]');
  const result = await importMetodo(paths[0], { dryRun: args.includes('--dry-run'), sincronizarStatus: args.includes('--sincronizar-status') });
  if (result.dryRun) console.log(`Simulação local: ${result.planejados} itens planejados; nenhuma gravação no Prisma ou nos arquivos.`);
  else console.log(`Criados: ${result.criados}; atualizados: ${result.atualizados}; sem mudança: ${result.semMudanca}; vínculos criados: ${result.vinculosCriados}.`);
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => { console.error(error instanceof Error ? error.message : 'Falha ao importar o método. Pare e avise.'); process.exitCode = 1; });
}
