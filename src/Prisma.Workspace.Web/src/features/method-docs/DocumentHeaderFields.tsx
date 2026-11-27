import { useEffect, useState } from 'react';
import styled from 'styled-components';
import type { MethodDocument, MethodDocumentStatus } from '../../types/methodDocuments';
import { methodDocumentStatuses } from '../../types/methodDocuments';
import { headerNoun, isMethodDocumentStatus, statusLabel, statusLabels } from './labels';
import { Alert, Muted } from './styles';
import { documentErrorMessage, useUpdateMethodDocumentHeader } from './useMethodDocuments';

/**
 * Título e status do cabeçalho (RF16): a gravação regrava só `titulo:` e `status:` (D4), pela rota
 * PATCH .../header, numa versão nova. Status só na HU e no épico, e só os quatro valores válidos.
 */
const Grid = styled.div`
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(160px, 220px);
  gap: 10px;
  @media (max-width: 560px) { grid-template-columns: 1fr; }
`;

const Field = styled.label<{ $wide?: boolean }>`
  display: grid;
  grid-column: ${({ $wide }) => ($wide ? '1 / -1' : 'auto')};
  gap: 5px;
  color: ${({ theme }) => theme.color.textMutedAccessible};
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
  input, select {
    width: 100%;
    min-height: 40px;
    padding: 8px 10px;
    border: 1px solid ${({ theme }) => theme.color.borderStrong};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-family: ${({ theme }) => theme.font.body};
    font-size: 14px;
    font-weight: 600;
    text-transform: none;
    outline: none;
    &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; box-shadow: 0 0 0 2px color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 14%, transparent); }
    &:disabled { opacity: .6; }
  }
  @media (max-width: 640px) { input, select { min-height: 44px; font-size: 16px; } }
`;

const ReadOnly = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  h3 { margin: 0; color: ${({ theme }) => theme.color.text}; font-size: 16px; font-weight: 800; }
`;

export const StatusPill = styled.span`
  display: inline-flex;
  align-items: center;
  min-height: 22px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.text};
  font-size: 11.5px;
  font-weight: 800;
  white-space: nowrap;
`;

interface DocumentHeaderFieldsProps {
  projectId: string;
  workItemId: string;
  document: MethodDocument;
}

export function DocumentHeaderFields({ projectId, workItemId, document }: DocumentHeaderFieldsProps) {
  const update = useUpdateMethodDocumentHeader(projectId, document.key, workItemId);
  const [title, setTitle] = useState(document.title ?? '');
  const withStatus = document.kind === 'hu' || document.kind === 'epic';
  const noun = headerNoun[document.kind];

  useEffect(() => setTitle(document.title ?? ''), [document.title, document.version]);

  if (document.kind === 'prd') return null;

  if (!document.canEdit) {
    return (
      <ReadOnly>
        <h3>{document.title || 'Sem título'}</h3>
        {withStatus && document.status && <StatusPill aria-label={`Status ${noun}: ${statusLabel(document.status)}`}>{statusLabel(document.status)}</StatusPill>}
      </ReadOnly>
    );
  }

  const commitTitle = () => {
    if (update.isPending) return;
    const next = title.trim();
    if (!next) { setTitle(document.title ?? ''); return; }
    if (next === (document.title ?? '')) { if (next !== title) setTitle(next); return; }
    update.mutate({ title: next, baseVersion: document.version });
  };
  const commitStatus = (status: MethodDocumentStatus) => {
    if (update.isPending || status === document.status) return;
    update.mutate({ status, baseVersion: document.version });
  };
  const error = documentErrorMessage(update.error);

  return (
    <>
      <Grid>
        <Field $wide={!withStatus}>
          {`Título ${noun}`}
          <input value={title} maxLength={300} readOnly={update.isPending}
            onChange={event => setTitle(event.target.value)}
            onBlur={commitTitle}
            onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); commitTitle(); } }} />
        </Field>
        {withStatus && (
          <Field>
            {`Status ${noun}`}
            <select value={isMethodDocumentStatus(document.status) ? document.status : ''} disabled={update.isPending}
              onChange={event => { if (isMethodDocumentStatus(event.target.value)) commitStatus(event.target.value); }}>
              {!isMethodDocumentStatus(document.status) && <option value="" disabled>Status inválido no arquivo</option>}
              {methodDocumentStatuses.map(status => <option key={status} value={status}>{statusLabels[status]}</option>)}
            </select>
          </Field>
        )}
      </Grid>
      {update.isPending && <Muted role="status">Gravando cabeçalho...</Muted>}
      {error && <Alert>{error}</Alert>}
    </>
  );
}
