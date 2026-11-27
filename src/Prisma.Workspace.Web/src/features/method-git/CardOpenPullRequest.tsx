import styled from 'styled-components';
import type { WorkItemGitSummary } from './gitLabels';
import { OpenPullRequestButton } from './OpenPullRequestButton';
import { sourceFromCardGit, taskTitleWithoutId } from './pullRequestEligibility';
import type { OpenPullRequestResult } from './pullRequestTypes';

// CA-1: no celular o botão fica só na aba Git do detalhe; o card não mostra.
const Slot = styled.div`
  display: flex; margin-top: 8px;
  @media (max-width: 640px) { display: none; }
`;

/** HU-0007: botão Abrir PR / Abrir no GitHub no card do quadro (desktop), a partir da projeção Git do card (T-0005). */
export function CardOpenPullRequest({ projectId, workItemId, title, git, canEdit, onOpened }: {
  projectId: string; workItemId: string; title: string; git: WorkItemGitSummary; canEdit: boolean;
  onOpened?: (result: OpenPullRequestResult) => void;
}) {
  const source = sourceFromCardGit(git);
  if (!canEdit || !source || !git.methodTaskId) return null;
  return (
    <Slot>
      <OpenPullRequestButton variant="card" projectId={projectId} source={source} canEdit={canEdit} onOpened={onOpened}
        task={{ workItemId, methodTaskId: git.methodTaskId, title: taskTitleWithoutId(title, git.methodTaskId) }} />
    </Slot>
  );
}
