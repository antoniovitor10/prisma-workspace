import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import { Actions, Button, Feedback, Field, Form, Hint, List, Row } from './styles';

function externalUrl(value: string): string | undefined {
  try { const url = new URL(value); return url.protocol === 'https:' ? url.href : undefined; }
  catch { return undefined; }
}

const unlinkedReasons: Record<string, string> = {
  task_not_identified: 'A branch e o título não identificam uma task. Confirme o vínculo abaixo.',
  task_not_found: 'O ID informado não foi encontrado neste projeto. Confira a branch e a task.',
  ambiguous_method_task_id: 'O ID do método está duplicado no projeto, inclusive em cards arquivados ou de outros quadros. Resolva a duplicidade antes de vincular.',
  explicit_task_unavailable: 'A task vinculada está arquivada, fora do quadro do método ou com ID duplicado. Confira a configuração e os cards arquivados.',
  ambiguous_title: 'O título contém IDs de tasks diferentes. Confirme a task correta.',
  invalid_task_branch: 'A branch precisa seguir o formato task/T-0001-descricao, ou o PR precisa de vínculo explícito.',
  invalid_method_task_id: 'O ID do método é inválido. Use T- seguido de pelo menos quatro dígitos.',
  task_outside_project: 'A task precisa estar ativa no topo do quadro do método deste projeto.',
  head_repository_requires_explicit_link: 'O PR vem de outro repositório. Confirme explicitamente a task deste projeto.',
  legacy_confirmation_required: 'Este PR foi encontrado no campo legado. Confirme o vínculo para usar a evidência Git.',
  explicit_confirmation_pending: 'Confirme o vínculo do PR com a task.',
};

export function PullRequestsPanel({ projectId, canEdit }: { projectId: string; canEdit: boolean }) {
  const [page, setPage] = useState(1);
  const requests = useQuery({ queryKey: [...methodKey(projectId), 'unlinked', page], queryFn: () => methodApi.unlinked(projectId, page), retry: false });
  const tasks = useQuery({ queryKey: [...methodKey(projectId), 'tasks'], queryFn: () => methodApi.tasks(projectId), retry: false, enabled: canEdit });
  const action = useMethodAction(projectId);
  const [selection, setSelection] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});

  return <section aria-labelledby="method-unlinked-title">
    <h3 id="method-unlinked-title">PRs não vinculados</h3>
    <Hint>Confira a branch e escolha a task deste projeto. Vincular um PR mantém a autorização humana necessária para iniciar a execução.</Hint>
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {tasks.isError && <Feedback role="alert">{tasks.error.message}</Feedback>}
    {requests.isPending ? <Hint role="status">Carregando PRs...</Hint> : requests.isError ? <Feedback role="alert">
      {requests.error.message} <Button type="button" onClick={() => void requests.refetch()}>Tentar novamente</Button>
    </Feedback> : <>
      {requests.data.items.length === 0 ? <Hint>Nenhum PR aguardando vínculo.</Hint> : <List>
        {requests.data.items.map(request => <Row key={request.id}>
          <strong>{externalUrl(request.url) ? <a href={externalUrl(request.url)} target="_blank" rel="noreferrer">#{request.pullRequestNumber} · {request.title}</a>
            : `#${request.pullRequestNumber} · ${request.title}`}</strong>
          <small>Branch: {request.headBranch} · Destino: {request.baseBranch}</small>
          <Hint>{request.unlinkedReason ? unlinkedReasons[request.unlinkedReason] ?? 'Confira a identidade da task e a configuração do método antes de confirmar o vínculo.'
            : 'Escolha a task correspondente.'}</Hint>
          {canEdit && <Form onSubmit={event => {
            event.preventDefault();
            void action.run(request.id, () => methodApi.link(projectId, request.id, selection[request.id], reasons[request.id].trim()), 'PR vinculado.').then(result => {
              if (result && requests.data.items.length === 1 && page > 1) setPage(current => current - 1);
            });
          }}>
            <Field>Task do PR #{request.pullRequestNumber}<select required value={selection[request.id] ?? ''}
              onChange={event => setSelection(current => ({ ...current, [request.id]: event.target.value }))}>
              <option value="">Selecione a task</option>{(tasks.data ?? []).map(task => <option key={task.workItemId} value={task.workItemId}>{task.id} · {task.title}</option>)}
            </select></Field>
            <Field>Motivo do vínculo do PR #{request.pullRequestNumber}<input required maxLength={1000} value={reasons[request.id] ?? ''}
              onChange={event => setReasons(current => ({ ...current, [request.id]: event.target.value }))} /></Field>
            <Actions><Button $primary type="submit" disabled={action.pending !== null || !selection[request.id] || !reasons[request.id]?.trim()}>
              {action.pending === request.id ? 'Vinculando...' : `Vincular PR #${request.pullRequestNumber}`}</Button></Actions>
          </Form>}
        </Row>)}
      </List>}
      {requests.data.total > requests.data.pageSize && <Actions>
        <Button type="button" disabled={page <= 1} onClick={() => setPage(current => current - 1)}>Página anterior</Button>
        <span>Página {page} de {Math.ceil(requests.data.total / requests.data.pageSize)}</span>
        <Button type="button" disabled={page * requests.data.pageSize >= requests.data.total} onClick={() => setPage(current => current + 1)}>Próxima página</Button>
      </Actions>}
    </>}
  </section>;
}
