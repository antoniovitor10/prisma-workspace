import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import styled from 'styled-components';
import type { MethodDocumentKind } from '../../types/methodDocuments';
import { DocumentHeaderBar } from './DocumentHeaderBar';
import { DocumentHeaderFields } from './DocumentHeaderFields';
import { DocumentVersions } from './DocumentVersions';
import { splitFrontmatter } from './frontmatter';
import { channelLabels, documentNoun, formatDateTime } from './labels';
import { MarkdownEditor } from './MarkdownEditor';
import { MarkdownView } from './MarkdownView';
import { ActionButton } from '../../components/ActionButton';
import { Alert, Button, Muted, Notice } from './styles';
import {
  LOCKED_MESSAGE, documentErrorMessage, isVersionConflict, methodDocumentsKey, useMethodDocument, useSaveMethodDocument,
} from './useMethodDocuments';

const Panel = styled.section`
  display: grid;
  gap: 14px;
  margin-top: 20px;
`;

const Toolbar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
`;

const Body = styled.div`
  padding: 14px 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  @media (max-width: 600px) { padding: 12px; }
`;

interface MethodDocumentTabProps {
  projectId: string;
  workItemId: string;
  documentKey: string;
  kind: MethodDocumentKind;
}

/**
 * Aba de um documento do método no detalhe do item: faixa de metadados, título e status,
 * markdown formatado, editor para quem pode editar e a lista de versões.
 */
export function MethodDocumentTab({ projectId, workItemId, documentKey, kind }: MethodDocumentTabProps) {
  const client = useQueryClient();
  const document = useMethodDocument(projectId, documentKey);
  const save = useSaveMethodDocument(projectId, documentKey, workItemId);
  // Versão, cabeçalho e corpo fixados ao clicar Editar: se o documento mudar enquanto a pessoa
  // escreve, o Salvar manda a versão de quando ela abriu e recebe o 409 (RF14), sem sobrescrever.
  const [editing, setEditing] = useState<{ version: number; head: string; body: string } | null>(null);
  const noun = documentNoun[kind];

  if (document.isLoading) return <Panel aria-label={noun}><Muted>Carregando documento...</Muted></Panel>;
  if (document.isError || !document.data) {
    return <Panel aria-label={noun}><Alert>Não foi possível carregar o documento. {document.error instanceof Error ? document.error.message : ''}</Alert></Panel>;
  }

  const doc = document.data;
  const split = splitFrontmatter(doc.content);
  const conflict = isVersionConflict(save.error);

  const reload = async () => {
    setEditing(null);
    save.reset();
    await Promise.all([
      client.invalidateQueries({ queryKey: [...methodDocumentsKey(projectId), 'doc', documentKey] }),
      client.invalidateQueries({ queryKey: [...methodDocumentsKey(projectId), 'revisions', documentKey] }),
    ]);
  };

  // O editor mexe só no corpo; o cabeçalho volta exatamente como estava (D4, CA-9).
  const onSave = (body: string) => {
    if (!editing) return;
    save.mutate({ content: editing.head + body, baseVersion: editing.version }, { onSuccess: () => setEditing(null) });
  };

  return (
    <Panel aria-label={noun}>
      <DocumentHeaderBar entries={split.entries} />
      <DocumentHeaderFields projectId={projectId} workItemId={workItemId} document={doc} />
      <Toolbar>
        <Muted>
          Versão {doc.version} · gravada por {doc.updatedByName} em {formatDateTime(doc.updatedAtUtc)} · via {channelLabels[doc.updatedVia] ?? doc.updatedVia}
        </Muted>
        {doc.canEdit && !doc.locked && !editing && (
          <Button type="button" aria-label={`Editar ${noun}`} onClick={() => { save.reset(); setEditing({ version: doc.version, head: split.head, body: split.body }); }}>Editar</Button>
        )}
      </Toolbar>
      {doc.locked && <Notice role="note">{LOCKED_MESSAGE}</Notice>}
      {editing
        ? <>
          <MarkdownEditor initialValue={editing.body} documentLabel={noun} saving={save.isPending}
            error={documentErrorMessage(save.error)} onSave={onSave}
            onCancel={() => { save.reset(); setEditing(null); }} />
          {conflict && <div><Button as={ActionButton} type="button" $secondary loadingLabel="Recarregando documento" onClick={() => reload()}>Recarregar documento</Button></div>}
        </>
        : <Body><MarkdownView content={split.body} aria-label={`Conteúdo do ${noun}`} /></Body>}
      <DocumentVersions projectId={projectId} documentKey={documentKey} currentVersion={doc.version} />
    </Panel>
  );
}
