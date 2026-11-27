export type MethodRole = 'humano' | 'agente' | 'sistema';
export type MethodStatus = 'backlog' | 'pronta' | 'em-desenvolvimento' | 'em-revisao' | 'em-teste' | 'concluida' | 'bloqueada';

export interface MethodIdentity {
  authenticationKind: 'uiSession' | 'methodToken' | 'integrationWorker';
  role: MethodRole;
  principalId: string | null;
  userId: string;
  displayName: string;
  organizationId: string;
  projectId: string;
  projectName: string;
  boardId: string;
  boardName: string;
  tokenId: string | null;
  expiresAtUtc: string | null;
  capabilities: string[];
}

export interface MethodProjectSettings {
  organizationId: string;
  projectId: string;
  projectName: string;
  boardId: string;
  boardName: string;
  methodIdFieldId: string;
  huFieldId: string;
  prFieldId: string;
  enabled: boolean;
  gitPhase: number;
  autonomyLevel: number;
  policyVersion: number;
  stageMappings: Record<MethodStatus, string>;
  fields: Record<string, string> | null;
  identityIssues?: { id: string; code: string; message: string; workItemIds: string[] }[] | null;
}

export interface MethodToken {
  id: string;
  principalId: string;
  name: string;
  principalDisplayName: string;
  role: MethodRole;
  tokenPrefix: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
  lastUsedAtUtc: string | null;
  permissions: number[];
}

export interface IssueTokenInput {
  name: string;
  role: MethodRole;
  validDays: number;
  principalId?: string;
  principalDisplayName?: string;
  permissions?: number[];
}

export interface IssuedToken { metadata: MethodToken; token: string }

export interface GitRepository {
  id: string;
  projectId: string;
  host: string;
  repositoryId: number;
  repositoryFullName: string;
  defaultBranch: string;
  appId: number | null;
  installationId: number | null;
  systemPrincipalId: string;
  enabled: boolean;
  operationalError: string | null;
  /** "app" (GitHub App + webhook) ou "poll" (consulta com token de leitura). */
  mode: 'app' | 'poll' | string;
  readCredentialId: string | null;
  /** Última consulta com sucesso (modo consulta). */
  lastPolledAtUtc: string | null;
  /** Código da última consulta, sem texto do GitHub. */
  lastPollStatus: string | null;
}

export interface RegisterPollRepositoryInput {
  repositoryFullName: string;
  defaultBranch: string;
  systemPrincipalId: string;
  enabled: boolean;
}

/** Token de leitura de uma organização GitHub: o valor nunca volta da API. */
export interface GitReadCredential {
  id: string;
  gitHubOwner: string;
  tokenPrefix: string;
  createdAtUtc: string;
  createdByName: string | null;
  revokedAtUtc: string | null;
  repositoriesCount: number;
}

export interface GitPollResult {
  integrationId: string;
  repositoryFullName: string;
  status: string;
  success: boolean;
  lastPolledAtUtc: string | null;
  pullRequestsEvaluated: number;
  branchesPushed: number;
}

export interface RegisterRepositoryInput {
  host: string;
  repositoryId: number;
  repositoryFullName: string;
  defaultBranch: string;
  appId: number;
  installationId: number;
  appPrivateKeySecretRef: string;
  webhookSecretRef: string;
  systemPrincipalId: string;
  enabled: boolean;
}

export interface ObservedPullRequest {
  id: string;
  integrationId: string;
  projectId: string;
  pullRequestNumber: number;
  url: string;
  title: string;
  headBranch: string;
  baseBranch: string;
  state: string;
  isDraft: boolean;
  workItemId: string | null;
  methodTaskId: string | null;
  unlinkedReason: string | null;
  updatedAtUtc: string;
}

export interface MethodTask {
  workItemId: string;
  id: string;
  title: string;
  status: MethodStatus;
  hu: string | null;
  filePath: string | null;
  covers: string[];
  risk: string | null;
  pr: string | null;
  dependsOn: string[];
  authorized: boolean;
  canResume: boolean;
  resumeApprovalEventId?: string | null;
  cycleId: string | null;
  blockingEventId: string | null;
  blockingPrincipalId: string | null;
  blockingOriginStatus: string | null;
}

export interface PullRequestPage { items: ObservedPullRequest[]; page: number; pageSize: number; total: number }

/** Aba Git da tarefa (HU-0002, T-0006). */
export interface TaskGitRepositoryOption {
  id: string;
  repositoryFullName: string;
  mode: 'app' | 'poll' | string;
  lastPolledAtUtc: string | null;
  lastPollStatus: string | null;
}
export interface TaskGitBranch {
  linkId: string | null;
  integrationId: string;
  repositoryFullName: string;
  branchName: string;
  branchState: 'local' | 'remote' | string;
  lastSeenAtUtc: string | null;
  source: string;
  createdAtUtc: string;
  url: string;
}
export interface TaskGitPullRequest {
  linkId: string;
  integrationId: string;
  repositoryFullName: string;
  number: number;
  title: string;
  state: 'open' | 'closed' | string;
  isDraft: boolean;
  merged: boolean;
  approvalsCount: number;
  changesRequested: boolean;
  url: string;
  headBranch: string;
  baseBranch: string;
  authorName: string | null;
  source: string;
  updatedAtUtc: string;
  linkedAtUtc: string;
}
export interface TaskGitTimelineEntry {
  kind: string;
  atUtc: string;
  actorName: string | null;
  channel: string | null;
  repositoryFullName: string | null;
  branch: string | null;
  pullRequestNumber: number | null;
  url: string | null;
  factType: string | null;
  fromStatus: string | null;
  toStatus: string | null;
}
export interface TaskGitView {
  workItemId: string;
  methodTaskId: string;
  canLink: boolean;
  repositories: TaskGitRepositoryOption[];
  branches: TaskGitBranch[];
  pullRequests: TaskGitPullRequest[];
  timeline: TaskGitTimelineEntry[];
}
