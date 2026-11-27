import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';
import { previewMode } from '../../preview';
import { ApiRequestError, api } from '../../services/api';
import type {
  MethodDocument, MethodDocumentKind, MethodDocumentStatus, MethodDocumentSummary,
} from '../../types/methodDocuments';
import { buildMethodDocumentTabs } from './labels';

export const methodDocumentsKey = (projectId: string) => ['method-documents', api.getOrganizationId(), projectId] as const;

export const VERSION_CONFLICT_MESSAGE = 'Este documento mudou desde que você abriu. Recarregue para ver a versão nova';
export const LOCKED_MESSAGE = 'O agente está executando esta task; o corpo volta a ser editável quando ela sair de em-desenvolvimento';

export function isVersionConflict(error: unknown) {
  return error instanceof ApiRequestError && error.code === 'method_document_version_conflict';
}

/** Mensagem para a pessoa a partir do erro da gravação (409 de versão e de trava têm texto próprio). */
export function documentErrorMessage(error: unknown): string | null {
  if (!error) return null;
  if (isVersionConflict(error)) return VERSION_CONFLICT_MESSAGE;
  if (error instanceof ApiRequestError && error.code === 'method_document_locked') return LOCKED_MESSAGE;
  return error instanceof Error ? error.message : 'Não foi possível gravar o documento.';
}

const sessionActive = () => Boolean(api.getToken()) && !previewMode;

/** A integração do método está ligada nesta instalação (mesma consulta das configurações do projeto). */
export function useMethodAvailability(enabled = true) {
  const query = useQuery({
    queryKey: ['method-availability'], queryFn: () => api.getSetupStatus(),
    enabled: enabled && sessionActive(), staleTime: 60_000, retry: false,
  });
  return query.data?.methodEnabled === true;
}

/** Abas de documento do item. Erro ou lista vazia: nenhuma aba. */
export function useMethodDocumentTabs(projectId: string | undefined, workItemId: string | undefined) {
  const methodEnabled = useMethodAvailability(Boolean(projectId && workItemId));
  const query = useQuery<MethodDocumentSummary[]>({
    queryKey: [...methodDocumentsKey(projectId ?? ''), 'item', workItemId],
    queryFn: () => api.getMethodDocuments(projectId!, { workItemId }),
    enabled: Boolean(projectId && workItemId) && methodEnabled && sessionActive(),
    retry: false,
  });
  return useMemo(() => (query.isError ? [] : buildMethodDocumentTabs(query.data)), [query.data, query.isError]);
}

export interface MethodItemStatus {
  key: string;
  kind: MethodDocumentKind;
  status: string | null;
}

/** Status da HU e do épico por item, para a etiqueta do backlog. Só os resumos, nunca o texto. */
export function useProjectMethodStatuses(projectId: string | undefined) {
  const methodEnabled = useMethodAvailability(Boolean(projectId));
  const query = useQuery<MethodDocumentSummary[]>({
    queryKey: [...methodDocumentsKey(projectId ?? ''), 'summaries'],
    queryFn: () => api.getMethodDocuments(projectId!),
    enabled: Boolean(projectId) && methodEnabled && sessionActive(),
    retry: false,
  });
  return useMemo(() => {
    const map = new Map<string, MethodItemStatus>();
    if (query.isError) return map;
    for (const doc of query.data ?? []) {
      if (doc.kind === 'hu' || doc.kind === 'epic') map.set(doc.workItemId, { key: doc.key, kind: doc.kind, status: doc.status });
    }
    return map;
  }, [query.data, query.isError]);
}

export function useMethodDocument(projectId: string, key: string) {
  return useQuery<MethodDocument>({
    queryKey: [...methodDocumentsKey(projectId), 'doc', key],
    queryFn: () => api.getMethodDocument(projectId, key),
    retry: false,
  });
}

export function useMethodDocumentRevisions(projectId: string, key: string) {
  return useQuery({
    queryKey: [...methodDocumentsKey(projectId), 'revisions', key],
    queryFn: () => api.getMethodDocumentRevisions(projectId, key),
    retry: false,
  });
}

export function useMethodDocumentRevision(projectId: string, key: string, version: number | null) {
  return useQuery({
    queryKey: [...methodDocumentsKey(projectId), 'revision', key, version],
    queryFn: () => api.getMethodDocumentRevision(projectId, key, version!),
    enabled: version !== null,
    retry: false,
    staleTime: Infinity,
  });
}

/** Depois de gravar: o documento, as versões, os resumos (etiqueta do backlog) e o item (título). */
function useAfterWrite(projectId: string, key: string, workItemId: string) {
  const client = useQueryClient();
  return async (saved: MethodDocument) => {
    client.setQueryData([...methodDocumentsKey(projectId), 'doc', key], saved);
    await Promise.all([
      client.invalidateQueries({ queryKey: [...methodDocumentsKey(projectId), 'revisions', key] }),
      client.invalidateQueries({ queryKey: [...methodDocumentsKey(projectId), 'summaries'] }),
      client.invalidateQueries({ queryKey: ['work-item', workItemId] }),
      client.invalidateQueries({ queryKey: ['project-backlog'] }),
    ]);
  };
}

export function useSaveMethodDocument(projectId: string, key: string, workItemId: string) {
  const afterWrite = useAfterWrite(projectId, key, workItemId);
  return useMutation({
    mutationFn: (input: { content: string; baseVersion: number }) => api.saveMethodDocument(projectId, key, input),
    onSuccess: afterWrite,
  });
}

export function useUpdateMethodDocumentHeader(projectId: string, key: string, workItemId: string) {
  const afterWrite = useAfterWrite(projectId, key, workItemId);
  return useMutation({
    mutationFn: (input: { title?: string; status?: MethodDocumentStatus; baseVersion: number }) =>
      api.updateMethodDocumentHeader(projectId, key, input),
    onSuccess: afterWrite,
  });
}
