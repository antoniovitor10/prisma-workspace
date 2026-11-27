import { useQuery } from '@tanstack/react-query';
import { methodApi } from './client';
import type { TaskGitTimelineEntry, TaskGitView } from './types';

export const taskGitQueryKey = (projectId: string, workItemId: string) => ['task-git', projectId, workItemId] as const;

/** Leitura da aba Git; 404 (task fora do método ou método desligado) apenas esconde a aba, sem nova tentativa. */
export function useTaskGit(projectId: string | null | undefined, workItemId: string | null | undefined, enabled = true) {
  return useQuery<TaskGitView>({
    queryKey: taskGitQueryKey(projectId ?? '', workItemId ?? ''),
    queryFn: () => methodApi.taskGit(projectId!, workItemId!),
    enabled: Boolean(projectId && workItemId) && enabled,
    retry: false,
  });
}

const channelLabels: Record<string, string> = { ui: 'tela', mcp: 'MCP', webhook: 'webhook', consulta: 'consulta', reconciliacao: 'reconciliação' };
const statusLabels: Record<string, string> = {
  backlog: 'Backlog', pronta: 'Pronta', 'em-desenvolvimento': 'Em desenvolvimento', 'em-revisao': 'Em revisão',
  'em-teste': 'Em teste', concluida: 'Concluída', bloqueada: 'Bloqueada',
};
const factLabels: Record<string, (n: string) => string> = {
  pull_request_opened: n => `PR ${n} aberto`,
  pull_request_ready_for_review: n => `PR ${n} pronto para revisão`,
  pull_request_reopened: n => `PR ${n} reaberto`,
  review_changes_requested: n => `Mudanças pedidas no PR ${n}`,
  pull_request_merged: n => `PR ${n} mergeado`,
  pull_request_closed_without_merge: n => `PR ${n} fechado sem merge`,
};

export function gitChannelLabel(channel: string | null | undefined): string {
  return channel ? channelLabels[channel] ?? channel : 'automático';
}

export function gitTimelineText(entry: TaskGitTimelineEntry): string {
  const pr = entry.pullRequestNumber != null ? `#${entry.pullRequestNumber}` : '';
  const branch = entry.branch ?? '';
  switch (entry.kind) {
    case 'git_branch_linked': return `Branch ${branch} vinculada`;
    case 'git_branch_pushed': return `Branch ${branch} enviada ao GitHub`;
    case 'git_pr_linked': return `PR ${pr} vinculado`.replace('  ', ' ');
    case 'git_pr_unlinked': return `PR ${pr} desvinculado`.replace('  ', ' ');
    case 'git_pr_inferred': return pr ? `PR ${pr} vinculado pelo nome da branch ou pelo título` : `Branch ${branch} vinculada pelo nome`;
    case 'git_pr_merged': return `PR ${pr} mergeado`;
    case 'git_pr_closed': return `PR ${pr} fechado sem merge`;
    case 'git_fact': {
      const text = (entry.factType && factLabels[entry.factType]?.(pr)) || `Fato do Git no PR ${pr}`;
      return entry.toStatus ? `${text}; card para ${statusLabels[entry.toStatus] ?? entry.toStatus}` : text;
    }
    default: return entry.kind;
  }
}
