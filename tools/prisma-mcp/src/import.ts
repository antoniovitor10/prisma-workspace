#!/usr/bin/env node
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { PrismaApi, type WorkItemDto } from './api.js';
import { FIELD_NAMES, isMetodoStatus, type MetodoStatus } from './mapping.js';
import { readMetodo, type MetodoHu, type MetodoTask } from './metodo.js';
import { ensureProjeto, type ProjetoSetup } from './setup.js';
import { assertCreationStatus, assertTransition } from './rules.js';

export interface ImportOptions { dryRun?: boolean; sincronizarStatus?: boolean; }
export interface ImportSummary {
  criados: number;
  atualizados: number;
  semMudanca: number;
  vinculosCriados: number;
  planejados: number;
  dryRun: boolean;
  projectId?: string;
  boardId?: string;
}

/** A chave é exclusivamente o campo ID do metodo, nunca o título do card. */
export async function importMetodo(directory: string, options: ImportOptions = {}, suppliedApi?: PrismaApi): Promise<ImportSummary> {
  // Validar todos os documentos e dependências antes de qualquer escrita remota.
  const metodo = await readMetodo(directory);
  const summary: ImportSummary = { criados: 0, atualizados: 0, semMudanca: 0,
    vinculosCriados: 0, planejados: metodo.hus.length + metodo.tasks.length, dryRun: options.dryRun ?? false };
  if (options.dryRun) return summary;
  if (!summary.planejados) throw new Error('Nenhuma HU encontrada na pasta informada.');
  const api = suppliedApi ?? PrismaApi.fromEnv();
  const setup = await ensureProjeto(api);
  summary.projectId = setup.projectId;
  summary.boardId = setup.boardId;
  const fieldId = (name: string): string => {
    const id = setup.fields.get(name);
    if (!id) throw new Error(`Campo personalizado ausente: ${name}.`);
    return id;
  };
  const items = await api.listWorkItemsByBoard(setup.boardId);
  const byId = new Map<string, WorkItemDto>();
  const outcomes = new Map<string, 'created' | 'updated' | 'unchanged'>();
  for (const item of items) {
    const id = item.customFields.find(x => x.fieldId === fieldId(FIELD_NAMES.id))?.value;
    if (!id) continue;
    if (byId.has(id)) throw new Error(`Chave ${id} duplicada no Prisma. Corrija antes de importar.`);
    byId.set(id, item);
  }
  // Uma task já existente não pode ser silenciosamente recriada ou reparentada.
  for (const task of metodo.tasks) {
    const item = byId.get(task.id);
    if (item && item.parentId !== byId.get(task.hu)?.id) {
      throw new Error(`A task ${task.id} está vinculada a outra HU no Prisma. Corrija o vínculo antes de importar.`);
    }
  }

  // Migrar frontmatter também respeita o papel: uma task nova não pode
  // nascer autorizada por iniciativa do agente. Validar o lote inteiro
  // antes de criar qualquer HU/task ou atualizar conteúdo existente.
  for (const source of [...metodo.hus, ...metodo.tasks]) {
    if (!byId.has(source.id)) {
      assertCreationStatus(api.config.role, isMetodoStatus(source.status) ? source.status : 'backlog');
    }
  }

  // A opção de sincronização não pode contornar a autorização por papel.
  // Alterar um item existente continua sujeito à matriz no CLI.
  if (options.sincronizarStatus) {
    for (const source of [...metodo.hus, ...metodo.tasks]) {
      const item = byId.get(source.id);
      if (!item || !isMetodoStatus(source.status) || item.stageId === setup.stages.get(source.status)) continue;
      const from = statusForStage(setup, item.stageId);
      if (api.config.role === 'agente' && !isMetodoStatus(from)) {
        throw new Error(`A sincronização de ${source.id} fora das colunas do método exige papel humano.`);
      }
      if (isMetodoStatus(from)) assertTransition(api.config.role, from, source.status);
    }
  }

  const upsert = async (source: MetodoHu | MetodoTask, parentId: string | null): Promise<void> => {
    const isTask = 'hu' in source;
    const desiredStatus: MetodoStatus = isMetodoStatus(source.status) ? source.status : 'backlog';
    const title = `${source.id} · ${source.titulo}`;
    if (title.length > 500) throw new Error(`Título de ${source.id} ultrapassa 500 caracteres.`);
    const values: Record<string, string | null> = {
      [fieldId(FIELD_NAMES.id)]: source.id,
      [fieldId(FIELD_NAMES.arquivo)]: source.arquivo,
    };
    if (isTask) {
      values[fieldId(FIELD_NAMES.cobre)] = source.cobre.join(', ');
      values[fieldId(FIELD_NAMES.risco)] = source.risco;
    }
    let item = byId.get(source.id);
    if (!item) {
      const id = await api.createWorkItem({ boardId: setup.boardId, stageId: setup.stages.get(desiredStatus)!,
        parentId, title, kind: isTask ? 5 : 3 });
      try { await api.setCustomFields(id, values); }
      catch {
        throw new Error(`O card ${id} (${source.id}) foi criado, mas seus campos falharam. Pare e avise; confira esse card antes de repetir a importação.`);
      }
      item = await api.getWorkItem(id);
      byId.set(source.id, item);
      outcomes.set(source.id, 'created');
      return;
    }
    let changed = false;
    if (item.title !== title) {
      await api.updateWorkItem(item.id, { title });
      item.title = title;
      changed = true;
    }
    const changedValues = Object.fromEntries(Object.entries(values).filter(([id, value]) =>
      (item.customFields.find(x => x.fieldId === id)?.value ?? '') !== (value ?? '')));
    if (Object.keys(changedValues).length) {
      await api.setCustomFields(item.id, changedValues);
      changed = true;
    }
    // Sem status no frontmatter, mesmo sincronização preserva a verdade remota.
    if (options.sincronizarStatus && isMetodoStatus(source.status) && item.stageId !== setup.stages.get(desiredStatus)) {
      const from = statusForStage(setup, item.stageId);
      await api.moveWorkItem(item.id, setup.stages.get(desiredStatus)!);
      try {
        await api.addComment(item.id, `[status] ${from} -> ${desiredStatus} · ${api.config.role} · import · sincronização explícita do frontmatter`);
      } catch {
        throw new Error(`O status de ${source.id} mudou, mas o comentário de sincronização falhou. Pare e avise; confira a tarefa no Prisma.`);
      }
      changed = true;
    }
    outcomes.set(source.id, changed ? 'updated' : 'unchanged');
  };
  for (const hu of metodo.hus) await upsert(hu, null);
  for (const task of metodo.tasks) await upsert(task, byId.get(task.hu)!.id);
  for (const task of metodo.tasks) {
    const source = byId.get(task.id)!;
    const details = await api.getWorkItem(source.id);
    let linked = false;
    for (const id of new Set(task.depende_de)) {
      const target = byId.get(id)!;
      if (details.links.some(x => !x.isIncoming && x.type === 1 && x.relatedWorkItemId === target.id)) continue;
      await api.addLink(source.id, target.id, 1);
      summary.vinculosCriados++;
      linked = true;
    }
    if (linked && outcomes.get(task.id) === 'unchanged') outcomes.set(task.id, 'updated');
  }
  for (const outcome of outcomes.values()) {
    if (outcome === 'created') summary.criados++;
    else if (outcome === 'updated') summary.atualizados++;
    else summary.semMudanca++;
  }
  return summary;
}

function statusForStage(setup: ProjetoSetup, id: string | null): string {
  return [...setup.stages].find(([, stageId]) => stageId === id)?.[0] ?? 'fora-do-método';
}

export async function main(args = process.argv.slice(2)): Promise<void> {
  const paths = args.filter(x => !x.startsWith('--'));
  const invalid = args.filter(x => x.startsWith('--') && x !== '--dry-run' && x !== '--sincronizar-status');
  if (paths.length !== 1 || invalid.length) {
    throw new Error('Uso: prisma-metodo-import <pasta docs/trabalho> [--dry-run] [--sincronizar-status]');
  }
  const result = await importMetodo(paths[0], { dryRun: args.includes('--dry-run'), sincronizarStatus: args.includes('--sincronizar-status') });
  if (result.dryRun) console.log(`Simulação local: ${result.planejados} itens planejados; nenhuma gravação no Prisma ou nos arquivos.`);
  else console.log(`Criados: ${result.criados}; atualizados: ${result.atualizados}; sem mudança: ${result.semMudanca}; vínculos criados: ${result.vinculosCriados}.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => { console.error(error instanceof Error ? error.message : 'Falha ao importar o método.'); process.exitCode = 1; });
}
