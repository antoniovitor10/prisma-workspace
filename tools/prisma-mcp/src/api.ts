import { readConfig, type PrismaConfig, type PrismaRole } from './config.js';
import type { StageCategory } from './mapping.js';

export interface ProjectDto { id: string; name: string; isArchived?: boolean; customFields?: ProjectCustomFieldDto[]; }
export interface TagDto { id: string; name: string; color: string; }
export interface BoardDto { id: string; name: string; projectId: string; }
export interface StageDto { id: string; name: string; category: StageCategory; position: number; projectId: string; boardId: string | null; }
export interface ProjectCustomFieldDto { id: string; name: string; type: number; isRequired: boolean; optionsJson: string | null; position: number; isActive?: boolean; }
export interface CustomFieldValueDto { fieldId: string; value: string | null; name?: string; }
export interface WorkItemLinkDto { id: string; type: number; isIncoming: boolean; relatedWorkItemId: string; relatedTitle: string; }
export interface WorkItemDto {
  id: string; title: string; boardId: string; stageId: string | null; parentId: string | null;
  kind: number; description: string | null; priority: number; origin: number;
  customFields: CustomFieldValueDto[]; isArchived: boolean; tags: TagDto[];
  taskTypeId?: string | null;
  responsibleId?: string | null; teamId?: string | null; requesterId?: string | null;
  requesterName?: string | null; requesterEmail?: string | null; startDate?: string | null;
  dueDate?: string | null; estimatedHours?: number | null; remainingHours?: number | null;
  points?: number | null; acceptanceCriteria?: string | null;
}
export interface WorkItemDetailsDto extends WorkItemDto { links: WorkItemLinkDto[]; subtasks: { id: string; title: string; isArchived: boolean }[]; }
export interface TaskStateGraphDto {
  nodes: { id: string; name: string; isCurrent: boolean }[];
  edges: { id: string; source: string; target: string; occurredAt: string; actorId: string | null; actorName: string | null; reason: string | null }[];
}
export interface CommentDto { id: string; workItemId: string; userId: string; text: string; createdAt: string; displayName: string; userName: string; }
export interface CreateWorkItemDto {
  boardId: string; stageId: string; title: string; parentId?: string | null;
  description?: string | null; kind?: number; priority?: number; position?: number;
}
export type UpdateWorkItemDto = Partial<Pick<WorkItemDto, 'title' | 'description' | 'kind' | 'stageId' | 'priority' | 'responsibleId' | 'teamId' | 'origin' | 'requesterId' | 'requesterName' | 'requesterEmail' | 'startDate' | 'dueDate' | 'estimatedHours' | 'remainingHours' | 'points' | 'acceptanceCriteria'>>;
export interface CustomFieldDefinition { name: string; type?: number; isRequired?: boolean; optionsJson?: string | null; position?: number; }


export interface MethodIdentityDto {
  authenticationKind: string; role: PrismaRole; principalId: string; userId: string; displayName: string;
  organizationId: string; projectId: string; projectName: string; boardId: string; boardName: string;
  tokenId: string; expiresAtUtc: string; capabilities: string[];
}
export interface MethodProjectDto {
  organizationId: string; projectId: string; projectName: string; boardId: string; boardName: string;
  methodIdFieldId: string; huFieldId: string; prFieldId: string; enabled: boolean;
  gitPhase: number; autonomyLevel: number; policyVersion: number;
  stageMappings: Record<string, string>; fields?: Record<string, string> | null;
}
export interface MethodRulesDto {
  identity: MethodIdentityDto; project: MethodProjectDto;
  allowedTransitions: Record<string, string[]>; conventions: string[];
  requiresInteractiveAuthorization: boolean; requiresInteractiveResumeApproval: boolean;
}
export interface MethodTaskDto {
  workItemId: string; id: string; title: string; status: string; hu: string | null;
  filePath: string | null; covers: string[]; risk: string | null; pr: string | null;
  dependsOn: string[]; authorized: boolean; canResume: boolean;
  cycleId?: string | null; blockingEventId?: string | null;
  blockingPrincipalId?: string | null; blockingOriginStatus?: string | null;
}
export interface CreateMethodTaskDto {
  id: string; title: string; hu: string; filePath: string; covers: string[]; risk: string; status: string;
}
export type UpdateMethodTaskDto = Omit<CreateMethodTaskDto, 'id' | 'status'>;
export interface MethodCommentDto { id: string; workItemId: string; text: string; author: string; createdAtUtc: string; }
export interface MethodPullRequestLinkDto { id: string; workItemId: string; url: string | null; source: string; }

export class PrismaApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(`Prisma HTTP ${status}: ${message}`);
    this.name = 'PrismaApiError';
  }
}

export class PrismaApi {
  readonly #token: string;
  #organizationId?: string;
  #identity?: MethodIdentityDto;
  #initialization?: Promise<MethodIdentityDto>;

  constructor(public readonly config: PrismaConfig) {
    if (!/^pmt_[A-Za-z0-9_-]{43}$/.test(config.token ?? '')) {
      throw new Error('PRISMA_TOKEN deve ser um token opaco do método; JWT de login não é aceito. Pare e avise.');
    }
    this.#token = config.token;
  }
  static fromEnv(): PrismaApi { return new PrismaApi(readConfig()); }

  get identity(): MethodIdentityDto {
    if (!this.#identity) throw new Error('Inicialize a identidade do método pela API antes de operar.');
    return this.#identity;
  }
  get userId(): string { return this.identity.userId; }

  private validateIdentity(identity: MethodIdentityDto): void {
    if (identity.authenticationKind !== 'methodToken' || !['agente', 'humano', 'sistema'].includes(identity.role)
      || !identity.principalId || !identity.userId || !identity.organizationId || !identity.projectId || !identity.boardId
      || !Number.isFinite(Date.parse(identity.expiresAtUtc)) || Date.parse(identity.expiresAtUtc) <= Date.now()) {
      throw new Error('A API não confirmou um token de método válido e vigente. Pare e avise.');
    }
    const matches = (expected: string, id: string, name: string) => expected === name || expected.toLowerCase() === id.toLowerCase();
    if (!matches(this.config.project, identity.projectId, identity.projectName)
      || (this.config.board && !matches(this.config.board, identity.boardId, identity.boardName))) {
      throw new Error('PRISMA_PROJECT ou PRISMA_BOARD diverge do escopo do token. Pare e avise.');
    }
    if (this.config.role && this.config.role !== identity.role) {
      throw new Error('PRISMA_ROLE diverge do papel autenticado pela API. Pare e avise.');
    }
  }

  async initialize(): Promise<MethodIdentityDto> {
    if (this.#identity) return this.#identity;
    if (this.#initialization) return this.#initialization;
    this.#initialization = (async () => {
      const identity = await this.raw<MethodIdentityDto>('/api/method/me');
      this.validateIdentity(identity);
      this.#organizationId = identity.organizationId;
      this.#identity = identity;
      return identity;
    })();
    try { return await this.#initialization; } finally { this.#initialization = undefined; }
  }

  private redact(message: string): string {
    return message.split(this.#token).join('[redigido]')
      .replace(/Bearer\s+\S+/gi, 'Bearer [redigido]')
      .replace(/pmt_[A-Za-z0-9_-]{43}/g, '[token redigido]')
      .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[token redigido]');
  }

  private async raw<T>(path: string, method = 'GET', body?: unknown, authorized = true): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (authorized && this.#token) headers.Authorization = `Bearer ${this.#token}`;
    if (authorized && this.#organizationId) headers['X-Organization-Id'] = this.#organizationId;
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    let response: Response;
    try {
      response = await fetch(`${this.config.url}${path}`, {
        method, headers, body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(30_000), redirect: 'error',
      });
    } catch {
      throw new Error('Não foi possível conectar ao Prisma no prazo. Confira PRISMA_URL e a disponibilidade da API.');
    }
    if (!response.ok) {
      let message = ({ 400: 'Requisição inválida.', 401: 'Autenticação inválida ou expirada.',
        403: 'Sem permissão para esta operação.', 404: 'Registro não encontrado.',
        409: 'Conflito ao salvar o registro.', 429: 'Limite de requisições atingido. Tente novamente em instantes.' } as Record<number, string>)[response.status]
        ?? 'A API não conseguiu concluir a operação.';
      // Erros de servidor não expõem stack trace, SQL ou configuração da instalação.
      if (response.status < 500) {
        try {
          const problem = await response.json() as { detail?: string; title?: string; errors?: Record<string, string[]> };
          const details = problem.errors ? Object.values(problem.errors).flat().filter(x => typeof x === 'string').join(' ') : '';
          message = details || problem.detail || problem.title || message;
        } catch { /* Resposta não estruturada: mantém a mensagem segura. */ }
      }
      throw new PrismaApiError(response.status, this.redact(message) + ([401, 403].includes(response.status) ? ' Pare e avise; confira o token e as permissões com o administrador.' : ''));
    }
    if (response.status === 204) return undefined as T;
    const text = await response.text();
    if (!text) return undefined as T;
    try { return JSON.parse(text) as T; }
    catch { throw new Error('A API Prisma retornou uma resposta JSON inválida.'); }
  }

  private async request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
    await this.initialize();
    return this.raw<T>(path, method, body);
  }

  private async methodPath(suffix = ''): Promise<string> {
    const identity = await this.initialize();
    return `/api/projects/${encodeURIComponent(identity.projectId)}/method${suffix}`;
  }
  async getMethodRules(): Promise<MethodRulesDto> {
    const rules = await this.request<MethodRulesDto>(await this.methodPath('/rules'));
    this.validateIdentity(rules.identity);
    if (rules.identity.projectId !== this.identity.projectId || rules.identity.organizationId !== this.identity.organizationId
      || rules.identity.boardId !== this.identity.boardId || rules.identity.principalId !== this.identity.principalId
      || rules.project.projectId !== this.identity.projectId || rules.project.boardId !== this.identity.boardId) {
      throw new Error('A identidade ou o projeto das regras diverge do token. Pare e avise.');
    }
    return rules;
  }
  async listMethodTasks(status?: string): Promise<MethodTaskDto[]> {
    return this.request(await this.methodPath(`/tasks${status ? `?status=${encodeURIComponent(status)}` : ''}`));
  }
  async nextMethodTasks(): Promise<MethodTaskDto[]> { return this.request(await this.methodPath('/tasks/next')); }
  async getMethodTask(id: string): Promise<MethodTaskDto> { return this.request(await this.methodPath(`/tasks/${encodeURIComponent(id)}`)); }
  async createMethodTask(dto: CreateMethodTaskDto): Promise<MethodTaskDto> {
    return this.request(await this.methodPath('/tasks'), 'POST', dto);
  }
  async updateMethodTask(id: string, dto: UpdateMethodTaskDto): Promise<MethodTaskDto> {
    return this.request(await this.methodPath(`/tasks/${encodeURIComponent(id)}`), 'PUT', dto);
  }
  async moveMethodTask(id: string, newStatus: string, reason: string): Promise<MethodTaskDto> {
    return this.request(await this.methodPath(`/tasks/${encodeURIComponent(id)}/move`), 'POST', { newStatus, reason });
  }
  async commentMethodTask(id: string, text: string): Promise<MethodCommentDto> {
    return this.request(await this.methodPath(`/tasks/${encodeURIComponent(id)}/comments`), 'POST', { text });
  }
  async linkMethodPullRequest(id: string, url: string): Promise<MethodPullRequestLinkDto> {
    return this.request(await this.methodPath(`/tasks/${encodeURIComponent(id)}/pull-requests`), 'POST', { url, reason: 'Vínculo explícito do PR pelo MCP.' });
  }

  // Somente dependências ainda usam as rotas legadas, sempre com escopo e permissão da API.
  async getWorkItem(id: string): Promise<WorkItemDetailsDto> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}`);
  }
  async addLink(id: string, targetId: string, type = 1): Promise<string> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/links`, 'POST', { targetWorkItemId: targetId, type });
  }
}
