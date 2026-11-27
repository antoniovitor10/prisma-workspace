import { useState } from 'react';
import styled from 'styled-components';
import { DocumentHeaderBar } from './DocumentHeaderBar';
import { splitFrontmatter } from './frontmatter';
import { channelLabels, formatDateTime } from './labels';
import { MarkdownView } from './MarkdownView';
import { Alert, Button, Muted } from './styles';
import { useMethodDocumentRevision, useMethodDocumentRevisions } from './useMethodDocuments';

/** Lista das versões (RF15): versão, autor, data e canal; cada uma abre o texto só leitura. */
const Wrap = styled.section`
  display: grid;
  gap: 10px;
  > h3 {
    margin: 0;
    color: ${({ theme }) => theme.color.neutral[700]};
    font-size: 13px;
    font-weight: 800;
    letter-spacing: .04em;
    text-transform: uppercase;
  }
`;

const List = styled.ol`
  display: grid;
  margin: 0;
  padding: 0;
  list-style: none;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  overflow: hidden;
`;

const Row = styled.li<{ $active: boolean }>`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  min-height: 48px;
  padding: 8px 12px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme, $active }) => ($active ? theme.color.neutral[50] : theme.color.surface)};
  &:last-child { border-bottom: 0; }
  strong { color: ${({ theme }) => theme.color.text}; font-size: 13.5px; }
  small { display: block; margin-top: 2px; color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 12px; }
`;

const Viewer = styled.div`
  display: grid;
  gap: 10px;
  padding: 12px 14px;
  border: 1px solid ${({ theme }) => theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  > header { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px; }
  > header strong { color: ${({ theme }) => theme.color.text}; font-size: 13.5px; }
`;

interface DocumentVersionsProps {
  projectId: string;
  documentKey: string;
  currentVersion: number;
}

export function DocumentVersions({ projectId, documentKey, currentVersion }: DocumentVersionsProps) {
  const revisions = useMethodDocumentRevisions(projectId, documentKey);
  const [opened, setOpened] = useState<number | null>(null);
  const revision = useMethodDocumentRevision(projectId, documentKey, opened);
  const split = revision.data?.content !== undefined ? splitFrontmatter(revision.data.content) : null;

  return (
    <Wrap aria-label="Versões do documento">
      <h3>Versões</h3>
      {revisions.isLoading && <Muted>Carregando versões...</Muted>}
      {revisions.isError && <Alert>Não foi possível carregar as versões.</Alert>}
      {revisions.data && revisions.data.length === 0 && <Muted>Nenhuma versão anterior.</Muted>}
      {revisions.data && revisions.data.length > 0 && (
        <List>
          {revisions.data.map(item => (
            <Row key={item.version} $active={opened === item.version}>
              <div>
                <strong>Versão {item.version}{item.version === currentVersion ? ' (atual)' : ''}</strong>
                <small>{item.authorName} · {formatDateTime(item.createdAtUtc)} · {channelLabels[item.via] ?? item.via}</small>
              </div>
              <Button type="button" $secondary
                onClick={() => setOpened(current => (current === item.version ? null : item.version))}>
                {opened === item.version ? `Fechar versão ${item.version}` : `Ver versão ${item.version}`}
              </Button>
            </Row>
          ))}
        </List>
      )}
      {opened !== null && (
        <Viewer role="region" aria-label={`Texto da versão ${opened}`}>
          <header>
            <strong>Versão {opened} (somente leitura)</strong>
            <Button type="button" $secondary onClick={() => setOpened(null)}>Fechar</Button>
          </header>
          {revision.isLoading && <Muted>Carregando texto da versão...</Muted>}
          {revision.isError && <Alert>Não foi possível carregar o texto desta versão.</Alert>}
          {split && <><DocumentHeaderBar entries={split.entries} /><MarkdownView content={split.body} /></>}
        </Viewer>
      )}
    </Wrap>
  );
}
