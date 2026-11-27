// HU-0007 · T-0026: rascunho do PR da task (preparar_pr) e a linha "Rascunho de PR" do obter_task.
// O agente prepara o texto e para ali (ADR-0001): abrir o PR é ação de uma pessoa na tela do Prisma.
import { PrismaApiError, type PrismaApi, type MethodPullRequestDraftDto } from './api.js';

export const PR_TITLE_MAX = 256;
export const PR_BODY_MAX = 60_000;

export interface PrDraftResult {
  id: string; titulo: string; gravado_em: string; autor: string | null; mudou: boolean;
}

/** Recusa antes de chamar a API: título vazio ou com mais de 256 caracteres, corpo com mais de 60.000. */
export function validatePrDraft(titulo: string, corpo: string): { titulo: string; corpo: string } {
  const title = typeof titulo === 'string' ? titulo.trim() : '';
  if (!title) throw new Error('Informe o título do PR.');
  if (title.length > PR_TITLE_MAX) throw new Error(`O título do PR deve ter até ${PR_TITLE_MAX} caracteres.`);
  const body = typeof corpo === 'string' ? corpo : '';
  if (body.length > PR_BODY_MAX) throw new Error('O corpo do PR deve ter até 60.000 caracteres.');
  return { titulo: title, corpo: body };
}

export async function prepararPr(api: PrismaApi, id: string, titulo: string, corpo: string): Promise<PrDraftResult> {
  const draft = validatePrDraft(titulo, corpo);
  const saved = await api.saveMethodPullRequestDraft(id, draft.titulo, draft.corpo);
  return { id, titulo: saved.title, gravado_em: saved.updatedAtUtc, autor: saved.actorName, mudou: saved.changed !== false };
}

/** Rascunho atual ou null (a API responde 404 pr_draft_not_found quando não há). */
export async function obterRascunhoPr(api: PrismaApi, id: string): Promise<MethodPullRequestDraftDto | null> {
  try { return await api.getMethodPullRequestDraft(id); }
  catch (error) { if (error instanceof PrismaApiError && error.status === 404) return null; throw error; }
}

/** Linha curta do obter_task: nunca despeja o corpo do rascunho. */
export function linhaRascunhoPr(draft: Pick<MethodPullRequestDraftDto, 'updatedAtUtc' | 'actorName'> | null | undefined): string {
  if (!draft) return 'Rascunho de PR: nenhum';
  return `Rascunho de PR: gravado em ${draft.updatedAtUtc} por ${draft.actorName ?? 'ator não informado'}`;
}
