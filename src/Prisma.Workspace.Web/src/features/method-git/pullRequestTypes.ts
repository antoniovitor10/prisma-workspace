// HU-0007 (abrir PR pelo card): contratos do rascunho, de abrir o PR e do link de comparação.

/** Rascunho atual do PR da task (último git_pr_draft_saved), gravado pelo agente com preparar_pr. */
export interface PullRequestDraft {
  title: string;
  body: string;
  updatedAtUtc: string;
  actorName: string | null;
}

export interface OpenPullRequestInput { linkId: string; title: string; body: string; draft: boolean }

export interface OpenPullRequestResult {
  outcome: 'opened' | 'linked_existing';
  number: number;
  url: string;
  moved: boolean;
  message: string;
}

export interface CompareUrlInput { linkId: string; title: string; body: string }
export interface CompareUrlResult { url: string; truncated: boolean }

/** "app": GitHub App (o Prisma abre o PR); "poll": modo consulta (abre a página de comparação do GitHub). */
export type GitIntegrationMode = 'app' | 'poll';

/** Branch da task que pode virar PR: enviada ao GitHub, num repositório habilitado do projeto, sem PR aberto. */
export interface PullRequestSource {
  linkId: string;
  branch: string;
  repositoryFullName: string;
  defaultBranch: string;
  mode: GitIntegrationMode;
}

/** Task do método vista pelo botão e pelo modal. */
export interface PullRequestTask { workItemId: string; methodTaskId: string; title: string }

/** Vínculo Git da task como a API devolve (TaskGitLinkDto). */
export interface TaskGitLinkView {
  id: string;
  integrationId: string;
  observationId?: string | null;
  branchName: string | null;
  branchState?: string | null;
  repositoryFullName?: string | null;
  pullRequestNumber?: number | null;
  pullRequestState?: string | null;
}

/** Repositório do projeto como a tela precisa para decidir o botão. */
export interface PullRequestRepositoryView {
  id: string;
  repositoryFullName: string;
  defaultBranch: string;
  enabled: boolean;
  mode?: GitIntegrationMode | null;
}
