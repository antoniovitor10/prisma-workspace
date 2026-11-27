import { ExternalLink, GitPullRequest } from 'lucide-react';
import { useState, type MouseEvent, type PointerEvent } from 'react';
import styled from 'styled-components';
import { OpenPullRequestDialog } from './OpenPullRequestDialog';
import type { OpenPullRequestResult, PullRequestSource, PullRequestTask } from './pullRequestTypes';

const Trigger = styled.button<{ $compact?: boolean }>`
  display: inline-flex; align-items: center; justify-content: center; gap: 6px; max-width: 100%;
  min-height: ${({ $compact }) => $compact ? '32px' : '44px'};
  padding: ${({ $compact }) => $compact ? '4px 10px' : '9px 14px'};
  font: inherit; font-size: ${({ theme, $compact }) => $compact ? theme.fontSize.sm : theme.fontSize.md};
  font-weight: ${({ theme }) => theme.fontWeight.bold}; cursor: pointer; white-space: nowrap;
  border: 1px solid ${({ theme }) => theme.color.brandAccessible}; border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.brandAccessible};
  &:hover { background: ${({ theme }) => theme.color.surfaceSubtle}; }
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px; }
`;

export interface OpenPullRequestButtonProps {
  projectId: string;
  task: PullRequestTask;
  /** Branch elegível (findPullRequestSource); sem ela o botão não aparece (CA-3). */
  source: PullRequestSource | null | undefined;
  /** Quem não pode editar a task não vê o botão (CA-8). */
  canEdit: boolean;
  /** "card": compacto e sem propagar clique/arrasto para o card. */
  variant?: 'card' | 'tab';
  className?: string;
  onOpened?: (result: OpenPullRequestResult) => void;
  /**
   * Quando informado, o modal fica com quem chamou (ex.: a página do quadro), fora do card: o card muda de coluna
   * ao abrir o PR e seria remontado, fechando o modal antes do resultado.
   */
  onOpenDialog?: (source: PullRequestSource) => void;
}

/**
 * HU-0007: botão Abrir PR (repositório com App) ou Abrir no GitHub (modo consulta), no card do quadro e na aba Git.
 * A regra de exibição (branch enviada, sem PR aberto, pessoa que pode editar) vem pronta em `source` e `canEdit`.
 */
export function OpenPullRequestButton({ projectId, task, source, canEdit, variant = 'tab', className, onOpened, onOpenDialog }: OpenPullRequestButtonProps) {
  // A branch fica guardada no clique: depois do PR aberto o quadro recarrega e a branch deixa de ser elegível
  // (o botão some), mas o modal continua aberto até a pessoa ler o resultado e fechar.
  const [openSource, setOpenSource] = useState<PullRequestSource | null>(null);
  const isApp = source?.mode === 'app';
  const stop = (event: MouseEvent | PointerEvent) => { if (variant === 'card') event.stopPropagation(); };
  return (
    <>
      {canEdit && source && (
        <Trigger type="button" className={className} $compact={variant === 'card'}
          aria-label={isApp ? `Abrir PR da task ${task.methodTaskId}` : `Abrir PR no GitHub da task ${task.methodTaskId}`}
          onPointerDown={stop} onClick={event => { stop(event); if (onOpenDialog) onOpenDialog(source); else setOpenSource(source); }}>
          {isApp ? <GitPullRequest size={variant === 'card' ? 14 : 16} aria-hidden /> : <ExternalLink size={variant === 'card' ? 14 : 16} aria-hidden />}
          {isApp ? 'Abrir PR' : 'Abrir no GitHub'}
        </Trigger>
      )}
      {openSource && (
        // O modal vai num portal, mas os eventos sobem pela árvore do React até o card: não deixa abrir o detalhe nem arrastar.
        <span style={{ display: 'contents' }} onClick={event => event.stopPropagation()} onPointerDown={event => event.stopPropagation()}
          onPointerUp={event => event.stopPropagation()} onPointerMove={event => event.stopPropagation()}
          onPointerCancel={event => event.stopPropagation()}>
          <OpenPullRequestDialog open onOpenChange={next => { if (!next) setOpenSource(null); }} projectId={projectId} task={task}
            source={openSource} onOpened={onOpened} />
        </span>
      )}
    </>
  );
}
