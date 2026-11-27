import styled from 'styled-components';
import type { WorkItemGitSummary } from './gitLabels';
import { OpenPullRequestButton } from './OpenPullRequestButton';
import { sourceFromCardGit, taskTitleWithoutId } from './pullRequestEligibility';
import type { OpenPullRequestResult } from './pullRequestTypes';

// CA-1: no celular o botão fica só na aba Git do detalhe; o card não mostra.
const CardButton = styled(OpenPullRequestButton)`
  margin-top: 8px; align-self: flex-start;
  @media (max-width: 640px) { display: none; }
`;

/** HU-0007: botão Abrir PR / Abrir no GitHub no card do quadro (desktop), a partir da projeção Git do card (T-0005). */
export function CardOpenPullRequest({ projectId, workItemId, title, git, canEdit, onOpened }: {
  projectId: string; workItemId: string; title: string; git: WorkItemGitSummary; canEdit: boolean;
  onOpened?: (result: OpenPullRequestResult) => void;
}) {
  // Sempre montado (o botão decide se aparece): o modal aberto sobrevive à recarga do quadro depois do PR.
  if (!git.methodTaskId) return null;
  return (
    <CardButton variant="card" projectId={projectId} source={sourceFromCardGit(git)} canEdit={canEdit} onOpened={onOpened}
      task={{ workItemId, methodTaskId: git.methodTaskId, title: taskTitleWithoutId(title, git.methodTaskId) }} />
  );
}
