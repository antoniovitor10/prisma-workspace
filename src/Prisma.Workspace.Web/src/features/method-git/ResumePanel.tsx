import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import { methodKey, useMethodAction } from './useMethodAction';
import { Actions, Button, Feedback, Field, Form, Hint, List, Row } from './styles';

export function ResumePanel({ projectId, enabled }: { projectId: string; enabled: boolean }) {
  const tasks = useQuery({ queryKey: [...methodKey(projectId), 'tasks'], queryFn: () => methodApi.tasks(projectId), retry: false });
  const action = useMethodAction(projectId);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [approved, setApproved] = useState<Set<string>>(new Set());
  const blocked = tasks.data?.filter(task => task.status === 'bloqueada' && task.authorized
    && task.blockingEventId && task.cycleId && task.blockingOriginStatus === 'em-desenvolvimento') ?? [];

  return <section aria-labelledby="method-resume-title">
    <h3 id="method-resume-title">Liberação de retomada</h3>
    <Hint>Libere o agente para retomar o próprio bloqueio de uma execução autorizada. A liberação mantém a task bloqueada até o agente retomar.</Hint>
    {!enabled && <Hint>Reative o método para liberar retomadas para o agente.</Hint>}
    {action.error && <Feedback role="alert">{action.error}</Feedback>}
    {action.success && <Feedback role="status">{action.success}</Feedback>}
    {tasks.isPending ? <Hint role="status">Carregando bloqueios...</Hint> : tasks.isError ? <Feedback role="alert">
      {tasks.error.message} <Button type="button" onClick={() => void tasks.refetch()}>Tentar novamente</Button>
    </Feedback> : blocked.length === 0 ? <Hint>Nenhum bloqueio de execução autorizada aguardando liberação.</Hint> : <List>
      {blocked.map(task => <Row key={task.workItemId}>
        <strong>{task.id} · {task.title}</strong>
        {task.resumeApprovalEventId || approved.has(task.blockingEventId!) ? <Hint>Retomada liberada para este bloqueio.</Hint> : <Form onSubmit={event => {
          event.preventDefault();
          void action.run(task.workItemId, () => methodApi.approveResume(projectId, task, reasons[task.workItemId].trim()),
            'Retomada liberada. O agente pode retomar seu próprio bloqueio.').then(result => {
              if (result) setApproved(current => new Set([...current, task.blockingEventId!]));
            });
        }}>
          <Field>Motivo da liberação de {task.id}<input required maxLength={1000} value={reasons[task.workItemId] ?? ''}
            onChange={event => setReasons(current => ({ ...current, [task.workItemId]: event.target.value }))} /></Field>
          <Actions><Button $primary type="submit" disabled={!enabled || action.pending !== null || !reasons[task.workItemId]?.trim()}>
            Liberar retomada de {task.id}</Button></Actions>
        </Form>}
      </Row>)}
    </List>}
  </section>;
}
