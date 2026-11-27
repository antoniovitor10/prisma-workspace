import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, GitBranch, GitPullRequest, History, Link2, RefreshCw } from 'lucide-react';
import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import styled from 'styled-components';
import { methodApi } from './client';
import { formatDateTime, gitBranchStateLabel, gitPollStatusMessage, gitPrState, gitPrStateLabel } from './gitLabels';
import { gitChannelLabel, gitTimelineText, taskGitQueryKey, useTaskGit } from './taskGit';

const Wrap = styled.div`display:grid;gap:22px;margin-top:20px;`;
const Block = styled.section`
  display:grid;gap:9px;
  >h3{display:flex;align-items:center;gap:7px;margin:0;color:${({ theme }) => theme.color.neutral[700]};font-size:13.5px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;}
`;
const List = styled.ul`display:grid;margin:0;padding:0;list-style:none;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.lg};overflow:hidden;`;
const Item = styled.li`
  display:grid;gap:3px;min-width:0;padding:10px 12px;border-bottom:1px solid ${({ theme }) => theme.color.neutral[100]};
  &:last-child{border-bottom:0;}
  a{display:inline-flex;align-items:center;gap:5px;min-width:0;color:${({ theme }) => theme.color.brandAccessible};font-weight:750;overflow-wrap:anywhere;}
  a:focus-visible{outline:2px solid ${({ theme }) => theme.color.accentBlue};outline-offset:2px;border-radius:4px;}
  small{color:${({ theme }) => theme.color.textMuted};font-size:12.5px;line-height:1.45;overflow-wrap:anywhere;}
`;
const Chips = styled.div`display:flex;flex-wrap:wrap;gap:6px;margin-top:3px;`;
const Chip = styled.span`
  display:inline-flex;align-items:center;min-height:22px;padding:1px 8px;border:1px solid ${({ theme }) => theme.color.border};
  border-radius:${({ theme }) => theme.radius.pill};background:${({ theme }) => theme.color.neutral[50]};
  color:${({ theme }) => theme.color.text};font-size:12px;font-weight:700;
`;
const Timeline = styled.ol`
  display:grid;gap:0;margin:0;padding:0;list-style:none;border-left:2px solid ${({ theme }) => theme.color.border};
  >li{position:relative;display:grid;gap:2px;padding:6px 0 10px 16px;}
  >li::before{content:'';position:absolute;left:-6px;top:11px;width:10px;height:10px;border-radius:999px;background:${({ theme }) => theme.color.brand};}
  strong{color:${({ theme }) => theme.color.text};font-size:13.5px;overflow-wrap:anywhere;}
  small{color:${({ theme }) => theme.color.textMuted};font-size:12.5px;}
`;
const Empty = styled.p`margin:0;padding:12px;border:1px dashed ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.lg};color:${({ theme }) => theme.color.textMuted};font-size:13px;text-align:center;`;
const Form = styled.form`
  display:grid;grid-template-columns:minmax(0,200px) minmax(0,1fr) auto;gap:8px;align-items:end;
  @media(max-width:640px){grid-template-columns:1fr;}
`;
const UrlForm = styled(Form)`grid-template-columns:minmax(0,1fr) auto;@media(max-width:640px){grid-template-columns:1fr;}`;
const Field = styled.label`
  display:grid;gap:5px;min-width:0;color:${({ theme }) => theme.color.textMuted};font-size:12px;font-weight:800;text-transform:uppercase;
  input,select{width:100%;min-height:38px;padding:0 9px;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.md};
    background:${({ theme }) => theme.color.surface};color:${({ theme }) => theme.color.text};font:inherit;font-size:14px;font-weight:500;text-transform:none;outline:none;}
  input:focus,select:focus{border-color:${({ theme }) => theme.color.accentBlue};box-shadow:0 0 0 2px color-mix(in srgb, ${({ theme }) => theme.color.accentBlue} 14%, transparent);}
`;
const Button = styled.button`
  display:inline-flex;min-height:38px;align-items:center;justify-content:center;gap:6px;padding:0 12px;border:0;border-radius:${({ theme }) => theme.radius.md};
  background:${({ theme }) => theme.color.brandControlBackground};color:${({ theme }) => theme.color.onBrand};font-size:13px;font-weight:800;
  &:disabled{opacity:.55;}
  &:focus-visible{outline:2px solid ${({ theme }) => theme.color.accentBlue};outline-offset:2px;}
`;
const Help = styled.p`margin:0;color:${({ theme }) => theme.color.textMuted};font-size:13px;line-height:1.45;`;
const Feedback = styled.p<{ $error?: boolean }>`
  margin:0;padding:8px 10px;border-radius:${({ theme }) => theme.radius.md};font-size:13px;font-weight:700;
  color:${({ theme }) => theme.color.text};
  background:${({ theme, $error }) => `color-mix(in srgb, ${$error ? theme.color.danger : theme.color.success} 12%, ${theme.color.surface})`};
`;

const dateTime = (value: string) => new Date(value).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

/**
 * Aba Git do detalhe da tarefa (HU-0002, CA-5 a CA-7): branches e PRs com links, linha do tempo
 * e vínculo manual para quem pode editar a tarefa. `actions` recebe controles extras da aba
 * (o "Consultar agora" do modo consulta).
 */
export function TaskGitTab({ projectId, workItemId, onLinked, actions }: {
  projectId: string; workItemId: string; onLinked?: () => void; actions?: ReactNode;
}) {
  const queryClient = useQueryClient();
  const query = useTaskGit(projectId, workItemId);
  const view = query.data;
  const [repository, setRepository] = useState('');
  const [branch, setBranch] = useState('');
  const [url, setUrl] = useState('');
  const [feedback, setFeedback] = useState<{ text: string; error?: boolean } | null>(null);
  useEffect(() => {
    if (view && !view.repositories.some(r => r.repositoryFullName === repository))
      setRepository(view.repositories[0]?.repositoryFullName ?? '');
  }, [view, repository]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: taskGitQueryKey(projectId, workItemId) });
    onLinked?.();
  };
  const linkBranch = useMutation({
    mutationFn: () => methodApi.linkBranch(projectId, view!.methodTaskId, repository, branch.trim()),
    onMutate: () => setFeedback(null),
    onSuccess: async () => { setFeedback({ text: `Branch ${branch.trim()} vinculada.` }); setBranch(''); await refresh(); },
    onError: (error: Error) => setFeedback({ text: error.message, error: true }),
  });
  const [pollFeedback, setPollFeedback] = useState<{ text: string; error?: boolean } | null>(null);
  const pollNow = useMutation({
    mutationFn: () => methodApi.pollTaskGit(projectId, workItemId),
    onMutate: () => setPollFeedback(null),
    onSuccess: async results => {
      const failed = results.filter(result => !result.success);
      const done = results.filter(result => result.success);
      const at = done[0]?.lastPolledAtUtc ? formatDateTime(done[0].lastPolledAtUtc) : null;
      setPollFeedback(failed.length > 0
        ? { text: failed.map(result => `${result.repositoryFullName}: ${gitPollStatusMessage(result.status)}`).join(' '), error: true }
        : { text: `Consulta concluída${at ? ` em ${at}` : ''}: ${done.reduce((sum, result) => sum + result.pullRequestsEvaluated, 0)} PR(s) reavaliado(s) e ${done.reduce((sum, result) => sum + result.branchesPushed, 0)} branch(es) enviada(s). Os cards se movem em instantes, se o GitHub mudou.` });
      await refresh();
    },
    onError: (error: Error) => setPollFeedback({ text: error.message, error: true }),
  });
  const linkPr = useMutation({
    mutationFn: () => methodApi.linkPullRequestUrl(projectId, view!.methodTaskId, url.trim()),
    onMutate: () => setFeedback(null),
    onSuccess: async () => { setFeedback({ text: 'PR vinculado.' }); setUrl(''); await refresh(); },
    onError: (error: Error) => setFeedback({ text: error.message, error: true }),
  });

  if (query.isPending) return <Help role="status">Carregando o Git da tarefa...</Help>;
  if (query.isError || !view) return <Feedback $error role="alert">{query.error?.message ?? 'Não foi possível carregar o Git da tarefa.'}</Feedback>;

  const submitBranch = (event: FormEvent) => { event.preventDefault(); if (repository && branch.trim()) linkBranch.mutate(); };
  const submitPr = (event: FormEvent) => { event.preventDefault(); if (url.trim()) linkPr.mutate(); };

  const pollRepositories = view.repositories.filter(r => r.mode === 'poll');

  return (
    <Wrap>
      {actions}
      {pollRepositories.length > 0 && <Block aria-labelledby="task-git-poll">
        <h3 id="task-git-poll"><RefreshCw size={14} aria-hidden="true" />Consulta ao GitHub</h3>
        <Help>Repositórios sem App são consultados de hora em hora. Use "Consultar agora" para ler o GitHub na hora (uma vez por minuto).</Help>
        <List aria-label="Repositórios consultados">
          {pollRepositories.map(r => (
            <Item key={r.id}>
              <small>{r.repositoryFullName} · {r.lastPolledAtUtc
                ? <>última consulta com sucesso: <time dateTime={r.lastPolledAtUtc}>{formatDateTime(r.lastPolledAtUtc)}</time></>
                : 'nenhuma consulta com sucesso ainda'}</small>
              {gitPollStatusMessage(r.lastPollStatus) && <small role="alert">{gitPollStatusMessage(r.lastPollStatus)}</small>}
            </Item>
          ))}
        </List>
        <div><Button type="button" onClick={() => pollNow.mutate()} disabled={pollNow.isPending}>
          <RefreshCw size={13} aria-hidden="true" />{pollNow.isPending ? 'Consultando...' : 'Consultar agora'}
        </Button></div>
        {pollFeedback && <Feedback $error={pollFeedback.error} role={pollFeedback.error ? 'alert' : 'status'}>{pollFeedback.text}</Feedback>}
      </Block>}
      <Block aria-labelledby="task-git-branches">
        <h3 id="task-git-branches"><GitBranch size={14} aria-hidden="true" />Branches</h3>
        {view.branches.length === 0 ? <Empty>Nenhuma branch vinculada.</Empty> : (
          <List aria-label="Branches da tarefa">
            {view.branches.map(b => (
              <Item key={`${b.integrationId}:${b.branchName}`}>
                <a href={b.url} target="_blank" rel="noopener noreferrer" aria-label={`Abrir a branch ${b.branchName} no GitHub`}>
                  {b.branchName}<ExternalLink size={12} aria-hidden="true" />
                </a>
                <small>{b.repositoryFullName}</small>
                <Chips><Chip>{gitBranchStateLabel(b.branchState)}</Chip></Chips>
              </Item>
            ))}
          </List>
        )}
      </Block>

      <Block aria-labelledby="task-git-prs">
        <h3 id="task-git-prs"><GitPullRequest size={14} aria-hidden="true" />Pull requests</h3>
        {view.pullRequests.length === 0 ? <Empty>Nenhum PR vinculado.</Empty> : (
          <List aria-label="PRs da tarefa">
            {view.pullRequests.map(pr => (
              <Item key={pr.linkId}>
                <a href={pr.url} target="_blank" rel="noopener noreferrer" aria-label={`Abrir o PR #${pr.number} no GitHub`}>
                  #{pr.number} · {pr.title}<ExternalLink size={12} aria-hidden="true" />
                </a>
                <small>{pr.repositoryFullName} · {pr.headBranch} → {pr.baseBranch}{pr.authorName ? ` · ${pr.authorName}` : ''}</small>
                <Chips>
                  <Chip>{gitPrStateLabel(gitPrState(pr))}</Chip>
                  <Chip>Aprovações: {pr.approvalsCount}</Chip>
                  <Chip>Mudanças pedidas: {pr.changesRequested ? 'sim' : 'não'}</Chip>
                </Chips>
              </Item>
            ))}
          </List>
        )}
      </Block>

      <Block aria-labelledby="task-git-timeline">
        <h3 id="task-git-timeline"><History size={14} aria-hidden="true" />Linha do tempo do Git</h3>
        {view.timeline.length === 0 ? <Empty>Nada aconteceu no Git desta tarefa ainda.</Empty> : (
          <Timeline aria-label="Linha do tempo do Git">
            {view.timeline.map((entry, index) => (
              <li key={`${entry.kind}:${entry.atUtc}:${index}`}>
                <strong>{gitTimelineText(entry)}</strong>
                <small>
                  <time dateTime={entry.atUtc}>{dateTime(entry.atUtc)}</time>
                  {' · '}{entry.actorName ?? 'Integração Git'}{' · '}canal: {gitChannelLabel(entry.channel)}
                  {entry.repositoryFullName ? ` · ${entry.repositoryFullName}` : ''}
                </small>
              </li>
            ))}
          </Timeline>
        )}
      </Block>

      <Block aria-labelledby="task-git-link">
        <h3 id="task-git-link"><Link2 size={14} aria-hidden="true" />Vincular</h3>
        {!view.canLink ? <Help>Você pode ver o Git desta tarefa, mas só quem pode editá-la vincula uma branch ou um PR.</Help>
          : view.repositories.length === 0 ? <Help>Nenhum repositório habilitado neste projeto. Cadastre um em Configurações do método.</Help>
          : <>
            <Help>Cole o nome de uma branch de um repositório do projeto ou a URL de um PR. O vínculo segue as mesmas regras do vínculo pelo MCP.</Help>
            <Form onSubmit={submitBranch} aria-label="Vincular branch">
              <Field>Repositório da branch
                <select value={repository} onChange={event => setRepository(event.target.value)}>
                  {view.repositories.map(r => <option key={r.id} value={r.repositoryFullName}>{r.repositoryFullName}</option>)}
                </select>
              </Field>
              <Field>Nome da branch
                <input value={branch} onChange={event => setBranch(event.target.value)} placeholder="feat/minha-branch" autoComplete="off" spellCheck={false} />
              </Field>
              <Button type="submit" disabled={!branch.trim() || !repository || linkBranch.isPending}>
                <GitBranch size={13} aria-hidden="true" />{linkBranch.isPending ? 'Vinculando...' : 'Vincular branch'}
              </Button>
            </Form>
            <UrlForm onSubmit={submitPr} aria-label="Vincular PR">
              <Field>URL do PR
                <input value={url} onChange={event => setUrl(event.target.value)} placeholder="https://github.com/dono/repositorio/pull/42"
                  inputMode="url" autoComplete="off" spellCheck={false} />
              </Field>
              <Button type="submit" disabled={!url.trim() || linkPr.isPending}>
                <GitPullRequest size={13} aria-hidden="true" />{linkPr.isPending ? 'Vinculando...' : 'Vincular PR'}
              </Button>
            </UrlForm>
          </>}
        {feedback && <Feedback $error={feedback.error} role={feedback.error ? 'alert' : 'status'}>{feedback.text}</Feedback>}
      </Block>
    </Wrap>
  );
}
