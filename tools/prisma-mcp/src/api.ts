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
export interface CommentDto { id: string; workItemId: string; userId: string; text: string; createdAt: string; displayName: string; userName: string; }
export interface CreateWorkItemDto {
  boardId: string; stageId: string; title: string; parentId?: string | null;
  description?: string | null; kind?: number; priority?: number; position?: number;
}
export type UpdateWorkItemDto = Partial<Pick<WorkItemDto, 'title' | 'description' | 'kind' | 'stageId' | 'priority' | 'responsibleId' | 'teamId' | 'origin' | 'requesterId' | 'requesterName' | 'requesterEmail' | 'startDate' | 'dueDate' | 'estimatedHours' | 'remainingHours' | 'points' | 'acceptanceCriteria'>>;
export interface CustomFieldDefinition { name: string; type?: number; isRequired?: boolean; optionsJson?: string | null; position?: number; }

// Contrato publicado antes da implementação para integração com tasks.ts.
export class PrismaApi {
  constructor(public readonly config: PrismaConfig) {}
  static fromEnv(): PrismaApi { return new PrismaApi(readConfig()); }
  get userId(): string { throw new Error('Implementação em andamento.'); }
  async login(): Promise<void> { throw new Error('Implementação em andamento.'); }
  async listProjects(): Promise<ProjectDto[]> { throw new Error('Implementação em andamento.'); }
  async createProject(name: string): Promise<string> { throw new Error('Implementação em andamento.'); }
  async listBoards(): Promise<BoardDto[]> { throw new Error('Implementação em andamento.'); }
  async createBoard(projectId: string, name: string): Promise<string> { throw new Error('Implementação em andamento.'); }
  async listStages(boardId: string): Promise<StageDto[]> { throw new Error('Implementação em andamento.'); }
  async createStage(boardId: string, name: string, category: StageCategory): Promise<string> { throw new Error('Implementação em andamento.'); }
  async listWorkItemsByBoard(boardId: string): Promise<WorkItemDto[]> { throw new Error('Implementação em andamento.'); }
  async getWorkItem(id: string): Promise<WorkItemDetailsDto> { throw new Error('Implementação em andamento.'); }
  async createWorkItem(dto: CreateWorkItemDto): Promise<string> { throw new Error('Implementação em andamento.'); }
  async updateWorkItem(id: string, dto: UpdateWorkItemDto): Promise<void> { throw new Error('Implementação em andamento.'); }
  async moveWorkItem(id: string, stageId: string): Promise<void> { throw new Error('Implementação em andamento.'); }
  async setCustomFields(id: string, values: Record<string, string | null>): Promise<void> { throw new Error('Implementação em andamento.'); }
  async listProjectCustomFields(projectId: string): Promise<ProjectCustomFieldDto[]> { throw new Error('Implementação em andamento.'); }
  async createProjectCustomField(projectId: string, def: CustomFieldDefinition): Promise<string> { throw new Error('Implementação em andamento.'); }
  async addLink(id: string, targetId: string, type: number = 1): Promise<string> { throw new Error('Implementação em andamento.'); }
  async listComments(id: string): Promise<CommentDto[]> { throw new Error('Implementação em andamento.'); }
  async addComment(id: string, text: string): Promise<CommentDto> { throw new Error('Implementação em andamento.'); }
}
