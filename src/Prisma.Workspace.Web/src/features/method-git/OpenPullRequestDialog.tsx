import * as Dialog from '@radix-ui/react-dialog';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, GitPullRequest, X } from 'lucide-react';
import { useEffect, useId, useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { pullRequestApi } from './client';
import { Button as BaseButton } from './styles';
import { bodyWithCardLine, cardLink, CARD_LINE_PREFIX, hasTaskPrefix } from './pullRequestEligibility';
import type { CompareUrlInput, OpenPullRequestInput, OpenPullRequestResult, PullRequestSource, PullRequestTask } from './pullRequestTypes';

const TITLE_MAX = 256;
const BODY_MAX = 60_000;
const NO_DRAFT_MESSAGE = 'A task ainda não tem rascunho de PR.';

const Overlay = styled(Dialog.Overlay)`
  position: fixed; inset: 0; z-index: 60;
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.neutral[900]} 45%, transparent)`};
`;
const Content = styled(Dialog.Content)`
  position: fixed; left: 50%; top: 50%; transform: translate(-50%, -50%); z-index: 61;
  width: min(760px, 94vw); max-height: 92vh; overflow-y: auto; box-sizing: border-box;
  padding: ${({ theme }) => theme.space[6]};
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surfaceElevated}; color: ${({ theme }) => theme.color.text};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  h2 { margin: 0; font-size: ${({ theme }) => theme.fontSize.xl}; line-height: 1.3; }
  p { margin: 0; line-height: 1.6; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; overflow-wrap: anywhere; }
  button:focus-visible, input:focus-visible, textarea:focus-visible, a:focus-visible {
    outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px;
  }
  /* No celular o modal ocupa a tela inteira. */
  @media (max-width: 640px) {
    inset: 0; left: 0; top: 0; transform: none; width: 100%; height: 100%; max-height: none;
    border-radius: 0; padding: ${({ theme }) => theme.space[4]};
  }
`;
const Head = styled.div`
  display: flex; align-items: flex-start; justify-content: space-between; gap: ${({ theme }) => theme.space[3]};
  margin-bottom: ${({ theme }) => theme.space[4]};
`;
const Form = styled.form`display: grid; gap: ${({ theme }) => theme.space[4]};`;
const Field = styled.div`
  display: grid; gap: 6px; min-width: 0;
  label { font-size: ${({ theme }) => theme.fontSize.sm}; font-weight: ${({ theme }) => theme.fontWeight.bold}; }
  input[type="text"], textarea {
    width: 100%; min-width: 0; box-sizing: border-box; padding: 10px 12px; font: inherit; line-height: 1.5;
    border: 1px solid ${({ theme }) => theme.color.borderStrong}; border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  }
  input[type="text"] { min-height: 44px; }
  textarea { min-height: 220px; resize: vertical; font-family: ${({ theme }) => theme.font.mono}; font-size: ${({ theme }) => theme.fontSize.sm}; }
  small { color: ${({ theme }) => theme.color.textMutedAccessible}; line-height: 1.5; overflow-wrap: anywhere; }
`;
const Meta = styled.dl`
  display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 4px 12px; margin: 0;
  font-size: ${({ theme }) => theme.fontSize.sm};
  dt { color: ${({ theme }) => theme.color.textMutedAccessible}; }
  dd { margin: 0; font-family: ${({ theme }) => theme.font.mono}; overflow-wrap: anywhere; }
`;
const Check = styled.label`
  display: flex; align-items: center; gap: 10px; min-height: 44px; font-size: ${({ theme }) => theme.fontSize.sm};
  input { width: 18px; height: 18px; accent-color: ${({ theme }) => theme.color.brandAccessible}; }
`;
const Notice = styled.p<{ $tone?: 'error' | 'success' | 'info' }>`
  padding: 12px; overflow-wrap: anywhere; font-size: ${({ theme }) => theme.fontSize.sm};
  border: 1px solid ${({ theme, $tone }) => $tone === 'error' ? theme.color.danger : $tone === 'success' ? theme.color.success : theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface};
`;
const Actions = styled.div`display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px;`;
// Reaproveita o botão da área do método (tokens de tema, alvo de 44px).
const Button = styled(BaseButton)`display: inline-flex; align-items: center; justify-content: center; gap: 6px;`;
const IconButton = styled(Button)`min-width: 44px; padding: 0;`;

export interface OpenPullRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  projectId: string;
  task: PullRequestTask;
  source: PullRequestSource;
  /** Chamado depois de abrir ou vincular o PR (o quadro recarrega). */
  onOpened?: (result: OpenPullRequestResult) => void;
}

/**
 * HU-0007 · T-0028/T-0029: modal do botão Abrir PR. Mostra o rascunho do agente (ou o texto padrão), a branch e a base,
 * a prévia da linha que o Prisma acrescenta e, no modo App, abre o PR pela API; no modo consulta, abre a página de
 * comparação do GitHub numa aba nova. O texto é exibido inteiro antes de confirmar: o corpo do PR fica público.
 */
export function OpenPullRequestDialog({ open, onOpenChange, projectId, task, source, onOpened }: OpenPullRequestDialogProps) {
  const ids = useId();
  const queryClient = useQueryClient();
  const isApp = source.mode === 'app';
  const link = cardLink(projectId, task.workItemId);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [asDraft, setAsDraft] = useState(false);
  const [initialized, setInitialized] = useState(false);
  const [result, setResult] = useState<OpenPullRequestResult | null>(null);
  const [compare, setCompare] = useState<{ truncated: boolean } | null>(null);
  const [copied, setCopied] = useState(false);

  const draft = useQuery({
    queryKey: ['method-git', 'pr-draft', projectId, task.methodTaskId],
    queryFn: () => pullRequestApi.draft(projectId, task.methodTaskId),
    enabled: open, retry: false, staleTime: 0,
  });

  useEffect(() => {
    if (!open) { setInitialized(false); setResult(null); setCompare(null); setCopied(false); setAsDraft(false); return; }
    if (initialized || draft.isLoading) return;
    if (draft.data) { setTitle(draft.data.title); setBody(draft.data.body); }
    else { setTitle(`[${task.methodTaskId}] ${task.title}`); setBody(`${CARD_LINE_PREFIX}${link}`); }
    setInitialized(true);
  }, [open, initialized, draft.isLoading, draft.data, task.methodTaskId, task.title, link]);

  const openPr = useMutation({
    mutationFn: (input: OpenPullRequestInput) => pullRequestApi.open(projectId, task.methodTaskId, input),
    onSuccess: async data => {
      setResult(data);
      onOpened?.(data);
      // Quadro, aba Git e histórico mostram o PR e a coluna nova.
      await queryClient.invalidateQueries();
    },
  });
  const openCompare = useMutation({
    mutationFn: (input: CompareUrlInput) => pullRequestApi.compareUrl(projectId, task.methodTaskId, input),
    onSuccess: data => {
      window.open(data.url, '_blank', 'noopener');
      setCompare({ truncated: data.truncated });
    },
  });
  const pending = openPr.isPending || openCompare.isPending;
  const error = (openPr.error ?? openCompare.error) as Error | null;
  const draftLoadFailed = draft.isError && (draft.error as Error | null)?.message !== NO_DRAFT_MESSAGE;
  const done = Boolean(result);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (pending || done || !title.trim()) return;
    // O texto vai como variável da mutação: sempre o que está no formulário no momento do clique.
    if (isApp) openPr.mutate({ linkId: source.linkId, title, body, draft: asDraft });
    else openCompare.mutate({ linkId: source.linkId, title, body });
  };
  const copyBody = async () => {
    try { await navigator.clipboard.writeText(bodyWithCardLine(body, link)); setCopied(true); }
    catch { setCopied(false); }
  };

  const heading = isApp ? `Abrir PR da task ${task.methodTaskId}` : `Abrir PR no GitHub da task ${task.methodTaskId}`;
  return (
    <Dialog.Root open={open} onOpenChange={next => { if (!pending) onOpenChange(next); }}>
      <Dialog.Portal>
        <Overlay />
        <Content aria-describedby={`${ids}-description`}>
          <Head>
            <div>
              <Dialog.Title asChild><h2>{heading}</h2></Dialog.Title>
              <Dialog.Description asChild>
                <p id={`${ids}-description`}>
                  {isApp ? 'O Prisma abre o PR pela GitHub App, vincula à task e move o card.' : 'O GitHub abre numa aba nova com o texto preenchido; você confirma lá.'}
                </p>
              </Dialog.Description>
            </div>
            <Dialog.Close asChild><IconButton type="button" aria-label="Fechar" disabled={pending}><X size={18} aria-hidden /></IconButton></Dialog.Close>
          </Head>

          {draft.isLoading && !initialized
            ? <p role="status" aria-live="polite" aria-busy="true">Carregando o rascunho do PR…</p>
            : (
              <Form onSubmit={submit} aria-label={heading}>
                <Meta>
                  <dt>Repositório</dt><dd>{source.repositoryFullName}</dd>
                  <dt>Origem</dt><dd>{source.branch}</dd>
                  <dt>Base</dt><dd>{source.defaultBranch}</dd>
                </Meta>
                {draft.data
                  ? <small>Texto do rascunho gravado pelo agente{draft.data.actorName ? ` (${draft.data.actorName})` : ''}.</small>
                  : <small>Sem rascunho do agente: texto padrão.</small>}
                {draftLoadFailed && <Notice $tone="info" role="status">Não consegui carregar o rascunho; usei o texto padrão.</Notice>}
                <Field>
                  <label htmlFor={`${ids}-title`}>Título do PR</label>
                  <input id={`${ids}-title`} type="text" value={title} maxLength={TITLE_MAX} required disabled={pending || done}
                    onChange={event => setTitle(event.target.value)} />
                  {!hasTaskPrefix(title, task.methodTaskId) && <small>O Prisma põe [{task.methodTaskId}] no início do título ao abrir o PR.</small>}
                </Field>
                <Field>
                  <label htmlFor={`${ids}-body`}>Corpo do PR</label>
                  <textarea id={`${ids}-body`} value={body} maxLength={BODY_MAX} disabled={pending || done}
                    onChange={event => setBody(event.target.value)} />
                  <small>O Prisma acrescenta no fim do corpo: <span>{CARD_LINE_PREFIX}{link}</span></small>
                  <small>O texto do PR fica visível para quem vê o repositório no GitHub.</small>
                </Field>
                {isApp && (
                  <Check>
                    <input type="checkbox" checked={asDraft} disabled={pending || done} onChange={event => setAsDraft(event.target.checked)} />
                    Abrir como rascunho
                  </Check>
                )}
                {error && <Notice $tone="error" role="alert">{error.message}</Notice>}
                {result && (
                  <Notice $tone="success" role="status" aria-live="polite">
                    {result.outcome === 'opened' && result.moved ? `PR #${result.number} aberto. ` : ''}{result.message}{' '}
                    <a href={result.url} target="_blank" rel="noopener noreferrer">Ver PR #{result.number} no GitHub</a>
                  </Notice>
                )}
                {compare && (
                  <Notice $tone={compare.truncated ? 'info' : 'success'} role="status" aria-live="polite">
                    {compare.truncated
                      ? 'O corpo é longo demais para o link; ele foi cortado. Copie o corpo completo e cole no GitHub.'
                      : 'Página do GitHub aberta numa aba nova.'}
                  </Notice>
                )}
                <Actions>
                  {compare?.truncated && <Button type="button" onClick={copyBody}>{copied ? 'Corpo copiado' : 'Copiar corpo'}</Button>}
                  <Dialog.Close asChild><Button type="button" disabled={pending}>{done ? 'Fechar' : 'Cancelar'}</Button></Dialog.Close>
                  {!done && (
                    <Button $primary type="submit" disabled={pending || !title.trim()} aria-busy={pending}>
                      {isApp ? <GitPullRequest size={16} aria-hidden /> : <ExternalLink size={16} aria-hidden />}
                      {pending ? (isApp ? 'Abrindo PR…' : 'Abrindo…') : (isApp ? 'Abrir PR' : 'Abrir no GitHub')}
                    </Button>
                  )}
                </Actions>
              </Form>
            )}
        </Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
