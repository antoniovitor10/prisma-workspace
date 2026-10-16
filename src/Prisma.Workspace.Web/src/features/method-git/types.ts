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
  appId: number;
  installationId: number;
  systemPrincipalId: string;
  enabled: boolean;
  operationalError: string | null;
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
