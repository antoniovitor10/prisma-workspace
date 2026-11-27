import styled from 'styled-components';
import type { WorkItemGitSummary } from './gitLabels';
import { OpenPullRequestButton } from './OpenPullRequestButton';
import { sourceFromCardGit, taskTitleWithoutId } from './pullRequestEligibility';
import type { PullRequestSource, PullRequestTask } from './pullRequestTypes';

// CA-1: no celular o botão fica só na aba Git do detalhe; o card não mostra.
const CardButton = styled(OpenPullRequestButton)`
  margin-top: 8px; align-self: flex-start;
  @media (max-width: 640px) { display: none; }
`;

/** HU-0007: botão Abrir PR / Abrir no GitHub no card do quadro (desktop), a partir da projeção Git do card (T-0005). */
export function CardOpenPullRequest({ projectId, workItemId, title, git, canEdit, onOpenDialog }: {
  projectId: string; workItemId: string; title: string; git: WorkItemGitSummary; canEdit: boolean;
  /** O quadro abre o modal num ponto fixo da página (o card muda de coluna e é remontado ao abrir o PR). */
  onOpenDialog: (task: PullRequestTask, source: PullRequestSource) => void;
}) {
  if (!git.methodTaskId) return null;
  const task = { workItemId, methodTaskId: git.methodTaskId, title: taskTitleWithoutId(title, git.methodTaskId) };
  return (
    <CardButton variant="card" projectId={projectId} source={sourceFromCardGit(git)} canEdit={canEdit} task={task}
      onOpenDialog={source => onOpenDialog(task, source)} />
  );
}
