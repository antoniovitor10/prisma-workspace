import styled from 'styled-components';
import { OpenPullRequestButton } from './OpenPullRequestButton';
import { sourceFromTaskGit, taskTitleWithoutId } from './pullRequestEligibility';
import type { OpenPullRequestResult } from './pullRequestTypes';
import type { TaskGitView } from './types';

const Bar = styled.div`
  &[hidden] { display: none; }
  display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px;
  padding: 10px 12px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  p { margin: 0; color: ${({ theme }) => theme.color.text}; font-size: 13.5px; line-height: 1.45; overflow-wrap: anywhere; }
`;

const dateTime = (value: string) => new Date(value).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/**
 * HU-0007 na aba Git: linha "Rascunho do PR" (CA-2) e o botão Abrir PR / Abrir no GitHub (CA-1), que aparece também
 * no celular. Repositório com App exige quem pode editar, comentar e mudar status; modo consulta, quem pode editar.
 */
export function TaskGitPullRequestActions({ projectId, view, taskTitle, onOpened }: {
  projectId: string; view: TaskGitView; taskTitle?: string; onOpened?: (result: OpenPullRequestResult) => void;
}) {
  const source = sourceFromTaskGit(view);
  const canEdit = source?.mode === 'poll' ? view.canLink : view.canOpenPullRequest === true;
  const draft = view.pullRequestDraft;
  // A barra fica montada mesmo vazia (oculta): o modal aberto sobrevive à releitura da aba depois do PR.
  return (
    <Bar hidden={!draft && !(source && canEdit)}>
      <p>{draft ? <>Rascunho do PR: gravado em <time dateTime={draft.updatedAtUtc}>{dateTime(draft.updatedAtUtc)}</time> por {draft.actorName ?? 'ator não informado'}</> : 'Sem rascunho do PR.'}</p>
      <OpenPullRequestButton projectId={projectId} source={source} canEdit={canEdit} onOpened={onOpened}
        task={{ workItemId: view.workItemId, methodTaskId: view.methodTaskId, title: taskTitleWithoutId(taskTitle ?? view.methodTaskId, view.methodTaskId) }} />
    </Bar>
  );
}
