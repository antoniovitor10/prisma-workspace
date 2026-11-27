// Cliente das rotas de documentos do método (T-0020/T-0021), no contrato de
// /api/projects/{projectId}/method/documents. O texto dos documentos só trafega na resposta das
// chamadas; nada aqui escreve em stderr ou log.
import { PrismaApi, PrismaApiError } from './api.js';
import type { DocumentKind } from './metodo.js';

export interface MethodDocumentSummary {
  key: string;
  kind: DocumentKind;
  workItemId: string;
  filePath: string | null;
  title: string | null;
  status: string | null;
  version: number;
  contentHash: string;
  updatedByName: string;
  updatedVia: 'ui' | 'mcp';
  updatedAtUtc: string;
}
export interface MethodDocument extends MethodDocumentSummary {
  content: string;
  parentKey: string | null;
  locked: boolean;
  canEdit: boolean;
}
export interface MethodDocumentRevision {
  version: number; contentHash: string; authorName: string; via: 'ui' | 'mcp'; createdAtUtc: string; content?: string;
}
export interface PutMethodDocument { content: string; baseVersion: number; filePath?: string; parentKey?: string; }
export interface RelinkResult { taskId: string; workItemId: string; huKey: string; parentWorkItemId: string; }

/** Chaves aceitas: `EPIC-0001`, `HU-0001`, `AVULSAS`, `HU-0001/prd`, `T-0001`. */
export const DOCUMENT_KEY = /^(EPIC-\d{4,}|HU-\d{4,}|AVULSAS|HU-\d{4,}\/prd|T-\d{4,})$/;
export const HU_KEY = /^(HU-\d{4,}|AVULSAS)$/;

export function assertDocumentKey(key: string): string {
  if (typeof key !== 'string' || !DOCUMENT_KEY.test(key)) throw new Error('Chave de documento inválida. Use EPIC-0001, HU-0001, HU-0001/prd, AVULSAS ou T-0001.');
  return key;
}
/** Segmento da URL: o PRD vai como `HU-0001/prd`, o resto direto. */
export function documentPath(key: string): string {
  assertDocumentKey(key);
  const prd = /^(HU-\d{4,})\/prd$/.exec(key);
  return prd ? `/documents/${prd[1]}/prd` : `/documents/${key}`;
}
export function kindOfKey(key: string): DocumentKind {
  assertDocumentKey(key);
  if (key.startsWith('EPIC-')) return 'epic';
  if (key.endsWith('/prd')) return 'prd';
  if (key.startsWith('T-')) return 'task';
  return 'hu';
}
export function isVersionConflict(error: unknown): boolean {
  return error instanceof PrismaApiError && error.status === 409 && error.code !== 'method_document_locked' && error.code !== 'method_not_provisioned';
}
export function isNotFound(error: unknown): boolean {
  return error instanceof PrismaApiError && error.status === 404;
}
/** 401/403: a sincronização inteira para (pare e avise), não só o documento. */
export function isAuthFailure(error: unknown): boolean {
  return error instanceof PrismaApiError && (error.status === 401 || error.status === 403);
}

export class DocumentsClient {
  constructor(readonly api: PrismaApi) {}

  async list(options: { desde?: string; workItemId?: string } = {}): Promise<MethodDocumentSummary[]> {
    const query = new URLSearchParams();
    if (options.desde) query.set('desde', options.desde);
    if (options.workItemId) query.set('workItemId', options.workItemId);
    const qs = query.toString();
    const suffix = qs ? `?${qs}` : '';
    return (await this.api.methodRequest<MethodDocumentSummary[]>(`/documents${suffix}`)) ?? [];
  }
  async get(key: string): Promise<MethodDocument> {
    return this.api.methodRequest(documentPath(key));
  }
  /** Igual a `get`, mas devolve `undefined` em 404. */
  async find(key: string): Promise<MethodDocument | undefined> {
    try { return await this.get(key); }
    catch (error) { if (isNotFound(error)) return undefined; throw error; }
  }
  async revisions(key: string): Promise<MethodDocumentRevision[]> {
    return (await this.api.methodRequest<MethodDocumentRevision[]>(`${documentPath(key)}/revisions`)) ?? [];
  }
  async revision(key: string, version: number): Promise<MethodDocumentRevision> {
    if (!Number.isInteger(version) || version < 1) throw new Error('Versão inválida.');
    return this.api.methodRequest(`${documentPath(key)}/revisions/${version}`);
  }
  async put(key: string, body: PutMethodDocument): Promise<MethodDocument> {
    return this.api.methodRequest(documentPath(key), 'PUT', body);
  }
  async relink(taskId: string, huKey: string): Promise<RelinkResult> {
    if (!/^T-\d{4,}$/.test(taskId)) throw new Error('ID da task inválido. Use T-0001.');
    if (!HU_KEY.test(huKey)) throw new Error('HU inválida. Use HU-0001 ou AVULSAS.');
    return this.api.methodRequest(`/documents/tasks/${encodeURIComponent(taskId)}/parent`, 'POST', { huKey });
  }
}
