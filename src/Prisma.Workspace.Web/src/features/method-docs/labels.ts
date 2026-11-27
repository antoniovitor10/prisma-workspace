import type { MethodDocumentChannel, MethodDocumentKind, MethodDocumentStatus, MethodDocumentSummary } from '../../types/methodDocuments';
import { methodDocumentStatuses } from '../../types/methodDocuments';

/** Nome do documento usado nos rótulos acessíveis: "Editar documento da HU", "Salvar PRD". */
export const documentNoun: Record<MethodDocumentKind, string> = {
  epic: 'documento do épico',
  hu: 'documento da HU',
  prd: 'PRD',
  task: 'corpo da task',
};

/** Complemento dos campos do cabeçalho: "Título da HU", "Status do épico". */
export const headerNoun: Record<MethodDocumentKind, string> = {
  epic: 'do épico',
  hu: 'da HU',
  prd: 'do PRD',
  task: 'da task',
};

export const statusLabels: Record<MethodDocumentStatus, string> = {
  aberta: 'Aberta',
  planejada: 'Planejada',
  concluida: 'Concluída',
  cancelada: 'Cancelada',
};

export const channelLabels: Record<MethodDocumentChannel, string> = { ui: 'tela', mcp: 'MCP' };

export function isMethodDocumentStatus(value: string | null | undefined): value is MethodDocumentStatus {
  return Boolean(value) && (methodDocumentStatuses as readonly string[]).includes(value!);
}

export function statusLabel(value: string | null | undefined): string {
  return isMethodDocumentStatus(value) ? statusLabels[value] : value ?? '';
}

export function formatDateTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

export type MethodDocumentTabLabel = 'Documento' | 'PRD' | 'Corpo da task';

export interface MethodDocumentTab {
  /** Identificador da aba no detalhe: `method:<chave>`. */
  id: `method:${string}`;
  key: string;
  kind: MethodDocumentKind;
  label: MethodDocumentTabLabel;
}

/**
 * Abas de documento de um item (RF12): Épico → Documento; História com HU → Documento e, se houver,
 * PRD; Tarefa do método → Corpo da task. Sem documento, nenhuma aba (itens fora do método não mudam).
 */
export function buildMethodDocumentTabs(documents: MethodDocumentSummary[] | undefined): MethodDocumentTab[] {
  if (!documents?.length) return [];
  const tab = (doc: MethodDocumentSummary, label: MethodDocumentTabLabel): MethodDocumentTab =>
    ({ id: `method:${doc.key}`, key: doc.key, kind: doc.kind, label });
  const epic = documents.find(doc => doc.kind === 'epic');
  if (epic) return [tab(epic, 'Documento')];
  const hu = documents.find(doc => doc.kind === 'hu');
  if (hu) {
    const prd = documents.find(doc => doc.kind === 'prd');
    return prd ? [tab(hu, 'Documento'), tab(prd, 'PRD')] : [tab(hu, 'Documento')];
  }
  const task = documents.find(doc => doc.kind === 'task');
  return task ? [tab(task, 'Corpo da task')] : [];
}
