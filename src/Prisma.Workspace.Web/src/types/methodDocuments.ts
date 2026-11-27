/**
 * Documentos do método (HU-0008): épico, HU, PRD e corpo da task guardados no Prisma.
 * Espelha o contrato de `/api/projects/{projectId}/method/documents` (T-0020/T-0021).
 */
export type MethodDocumentKind = 'epic' | 'hu' | 'prd' | 'task';
export type MethodDocumentChannel = 'ui' | 'mcp';

/** Status válidos da HU e do épico (RF16a). */
export const methodDocumentStatuses = ['aberta', 'planejada', 'concluida', 'cancelada'] as const;
export type MethodDocumentStatus = typeof methodDocumentStatuses[number];

export interface MethodDocumentSummary {
  /** 'EPIC-0001' | 'HU-0001' | 'AVULSAS' | 'HU-0001/prd' | 'T-0001' */
  key: string;
  kind: MethodDocumentKind;
  workItemId: string;
  filePath: string | null;
  title: string | null;
  /** Só hu/epic: aberta | planejada | concluida | cancelada. */
  status: string | null;
  version: number;
  contentHash: string;
  updatedByName: string;
  updatedVia: MethodDocumentChannel;
  updatedAtUtc: string;
}

export interface MethodDocument extends MethodDocumentSummary {
  /** Arquivo inteiro, com o cabeçalho YAML, fim de linha LF. */
  content: string;
  parentKey: string | null;
  /** Task em em-desenvolvimento: a sessão humana não grava. */
  locked: boolean;
  /** O ator atual tem Edit no item e o documento não está travado. */
  canEdit: boolean;
}

export interface MethodDocumentRevision {
  version: number;
  contentHash: string;
  authorName: string;
  via: MethodDocumentChannel;
  createdAtUtc: string;
  /** Só em GET .../revisions/{version}. */
  content?: string;
}

export interface SaveMethodDocumentInput {
  content: string;
  baseVersion: number;
}

export interface UpdateMethodDocumentHeaderInput {
  title?: string;
  status?: MethodDocumentStatus;
  baseVersion: number;
}
