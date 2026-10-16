import { readConfig, type PrismaConfig } from './config.js';
import type { StageCategory } from './mapping.js';

export interface ProjectDto { id: string; name: string; isArchived?: boolean; customFields?: ProjectCustomFieldDto[]; }
export interface BoardDto { id: string; name: string; projectId: string; }
export interface StageDto { id: string; name: string; category: StageCategory; position: number; projectId: string; boardId: string | null; }
export interface ProjectCustomFieldDto { id: string; name: string; type: number; isRequired: boolean; optionsJson: string | null; position: number; isActive?: boolean; }
export interface CustomFieldValueDto { fieldId: string; value: string | null; name?: string; }
export interface WorkItemLinkDto { id: string; type: number; isIncoming: boolean; relatedWorkItemId: string; relatedTitle: string; }
export interface WorkItemDto {
  id: string; title: string; boardId: string; stageId: string | null; parentId: string | null;
  kind: number; description: string | null; priority: number; origin: number;
  customFields: CustomFieldValueDto[]; isArchived: boolean;
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

export class PrismaApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(`Prisma HTTP ${status}: ${message}`);
    this.name = 'PrismaApiError';
  }
}

export class PrismaApi {
  #token: string | undefined;
  #organizationId: string | undefined;
  #loginPromise: Promise<void> | undefined;
  #organizationPromise: Promise<void> | undefined;

  constructor(public readonly config: PrismaConfig) { this.#token = config.token; }
  static fromEnv(): PrismaApi { return new PrismaApi(readConfig()); }

  get userId(): string {
    try {
      const payload = JSON.parse(Buffer.from(this.#token!.split('.')[1], 'base64url').toString());
      const id = payload.sub ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? payload.nameid;
      if (typeof id === 'string' && id) return id;
    } catch { /* A identidade só é usada após autenticação pela API. */ }
    throw new Error('Não foi possível identificar o autor no token Prisma. Faça login novamente.');
  }

  async login(): Promise<void> {
    if (this.#token) return;
    await this.authenticate();
  }

  private async authenticate(): Promise<void> {
    if (this.#loginPromise) return this.#loginPromise;
    this.#loginPromise = (async () => {
      if (!this.config.email || !this.config.password) {
        throw new PrismaApiError(401, 'Token expirado ou inválido. Renove PRISMA_TOKEN.');
      }
      const auth = await this.raw<{ accessToken: string }>('/api/auth/login', 'POST', {
        email: this.config.email, password: this.config.password,
      }, false);
      if (!auth.accessToken) throw new Error('Resposta de autenticação Prisma inválida.');
      this.#token = auth.accessToken;
    })();
    try { await this.#loginPromise; } finally { this.#loginPromise = undefined; }
  }

  private redact(message: string): string {
    for (const secret of [this.#token, this.config.token, this.config.password]) {
      if (secret) message = message.split(secret).join('[redigido]');
    }
    return message.replace(/Bearer\s+\S+/gi, 'Bearer [redigido]')
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
      throw new PrismaApiError(response.status, this.redact(message));
    }
    if (response.status === 204) return undefined as T;
    const text = await response.text();
    if (!text) return undefined as T;
    try { return JSON.parse(text) as T; }
    catch { throw new Error('A API Prisma retornou uma resposta JSON inválida.'); }
  }

  private async authorized<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
    await this.login();
    try { return await this.raw<T>(path, method, body); }
    catch (error) {
      if (!(error instanceof PrismaApiError) || error.status !== 401) throw error;
      // Repetição única: novo login em vez de persistir cookie de refresh.
      await this.authenticate();
      return this.raw<T>(path, method, body);
    }
  }

  private async ensureOrganization(): Promise<void> {
    if (this.#organizationId) return;
    if (this.#organizationPromise) return this.#organizationPromise;
    this.#organizationPromise = (async () => {
      const organizations = (await this.authorized<{ id: string; isActive?: boolean }[]>('/api/organizations'))
        .filter(x => x.isActive !== false);
      if (organizations.length === 1) { this.#organizationId = organizations[0].id; return; }
      if (!organizations.length) throw new Error('A conta Prisma não possui uma organização ativa.');
      const matches: string[] = [];
      for (const organization of organizations) {
        this.#organizationId = organization.id;
        try {
          const projects = await this.authorized<ProjectDto[]>('/api/projects');
          if (projects.some(x => x.name === this.config.project && !x.isArchived)) matches.push(organization.id);
        } finally { this.#organizationId = undefined; }
      }
      if (matches.length !== 1) throw new Error('Não foi possível determinar uma organização única para PRISMA_PROJECT. Use uma conta com acesso apenas à organização desejada.');
      this.#organizationId = matches[0];
    })();
    try { await this.#organizationPromise; } finally { this.#organizationPromise = undefined; }
  }

  private async request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
    await this.ensureOrganization();
    return this.authorized<T>(path, method, body);
  }

  async listProjects(): Promise<ProjectDto[]> { return this.request('/api/projects'); }
  async createProject(name: string): Promise<string> {
    return this.request('/api/projects', 'POST', { name, nature: 1, workType: 1, methodology: 1 });
  }
  async listBoards(): Promise<BoardDto[]> { return this.request('/api/Boards'); }
  async createBoard(projectId: string, name: string): Promise<string> {
    return this.request('/api/Boards', 'POST', { projectId, name });
  }
  async listStages(boardId: string): Promise<StageDto[]> {
    return this.request(`/api/Stages/board/${encodeURIComponent(boardId)}`);
  }
  async createStage(boardId: string, name: string, category: StageCategory): Promise<string> {
    const board = await this.request<BoardDto>(`/api/Boards/${encodeURIComponent(boardId)}`);
    const stages = await this.listStages(boardId);
    return this.request(`/api/Stages/board/${encodeURIComponent(boardId)}`, 'POST', {
      projectId: board.projectId, name, category, position: Math.max(0, ...stages.map(x => x.position)) + 100,
    });
  }
  async updateStage(id: string, name: string, category: StageCategory): Promise<void> {
    return this.request(`/api/Stages/${encodeURIComponent(id)}`, 'PUT', { name, category });
  }
  async reorderStages(boardId: string, ids: string[]): Promise<void> {
    return this.request(`/api/Stages/board/${encodeURIComponent(boardId)}/order`, 'PUT', ids);
  }
  async listWorkItemsByBoard(boardId: string): Promise<WorkItemDto[]> {
    const roots = await this.request<(WorkItemDto & { subItemsCount?: number })[]>(`/api/WorkItems/board/${encodeURIComponent(boardId)}`);
    const result: WorkItemDto[] = [];
    const seen = new Set<string>();
    const visit = async (items: (WorkItemDto & { subItemsCount?: number })[]): Promise<void> => {
      for (const item of items) {
        if (seen.has(item.id)) continue;
        seen.add(item.id);
        // O DTO de subitems não projeta CustomFields; o detalhe é necessário
        // para identificar a chave do método nas tasks filhas.
        result.push(item.parentId ? { ...item, ...await this.getWorkItem(item.id) } : item);
        if (item.subItemsCount !== 0) {
          await visit(await this.request(`/api/WorkItems/${encodeURIComponent(item.id)}/subitems`));
        }
      }
    };
    await visit(roots);
    return result;
  }
  async getWorkItem(id: string): Promise<WorkItemDetailsDto> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}`);
  }
  async createWorkItem(dto: CreateWorkItemDto): Promise<string> {
    return this.request('/api/WorkItems', 'POST', {
      parentId: null, description: null, priority: 1, position: 100, kind: 5, ...dto,
    });
  }
  async updateWorkItem(id: string, dto: UpdateWorkItemDto): Promise<void> {
    // PUT é substituição completa; preservar dados que não pertencem ao método.
    const current = await this.getWorkItem(id);
    const payload: UpdateWorkItemDto = {};
    const keys: (keyof UpdateWorkItemDto)[] = ['title', 'description', 'kind', 'stageId', 'priority', 'responsibleId',
      'teamId', 'origin', 'requesterId', 'requesterName', 'requesterEmail', 'startDate', 'dueDate',
      'estimatedHours', 'remainingHours', 'points', 'acceptanceCriteria'];
    for (const key of keys) Object.assign(payload, { [key]: current[key] ?? null });
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}`, 'PUT', { ...payload, ...dto });
  }
  async moveWorkItem(id: string, stageId: string): Promise<void> {
    return this.request('/api/WorkItems/move', 'POST', { workItemId: id, destinationStageId: stageId, position: 100 });
  }
  async setCustomFields(id: string, values: Record<string, string | null>): Promise<void> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/custom-fields`, 'PUT', { values });
  }
  async listProjectCustomFields(projectId: string): Promise<ProjectCustomFieldDto[]> {
    const project = await this.request<ProjectDto>(`/api/projects/${encodeURIComponent(projectId)}`);
    return (project.customFields ?? []).filter(x => x.isActive !== false);
  }
  async createProjectCustomField(projectId: string, def: CustomFieldDefinition): Promise<string> {
    return this.request(`/api/projects/${encodeURIComponent(projectId)}/custom-fields`, 'POST', {
      type: 1, isRequired: false, optionsJson: null, position: 100, ...def,
    });
  }
  async addLink(id: string, targetId: string, type = 1): Promise<string> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/links`, 'POST', { targetWorkItemId: targetId, type });
  }
  async getCurrentUserId(): Promise<string> {
    await this.ensureOrganization();
    return this.userId;
  }
  async getStateGraph(id: string): Promise<TaskStateGraphDto> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/state-graph`);
  }
  async listComments(id: string): Promise<CommentDto[]> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/comments`);
  }
  async addComment(id: string, text: string): Promise<CommentDto> {
    return this.request(`/api/WorkItems/${encodeURIComponent(id)}/comments`, 'POST', { text });
  }
}
