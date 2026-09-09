export interface BacklogItem {
  id: string;
  number?: number;
  boardId: string;
  boardName: string;
  stageId?: string;
  stageName?: string;
  workflowStatusId?: string;
  workflowStatusName?: string;
  parentId?: string;
  sprintId?: string;
  teamId?: string;
  kind: number;
  title: string;
  description?: string;
  priority: number;
  origin?: number;
  responsibleId?: string;
  requesterId?: string;
  requesterName?: string;
  requesterEmail?: string;
  points?: number;
  estimatedHours?: number;
  remainingHours?: number;
  rank: number;
  position?: number;
  dueDate?: string;
  startDate?: string;
  acceptanceCriteria?: string;
  createdAt?: string;
  completedAt?: string;
  assigneeIds?: string[];
  links?: BacklogLink[];
  isBlocked?: boolean;
  version?: string;
}

export interface BacklogLink {
  id: string;
  type: number;
  isIncoming: boolean;
  relatedWorkItemId: string;
  relatedNumber: number;
  relatedTitle: string;
  isOpen: boolean;
}

export interface WorkItemLink {
  id: string;
  type: number;
  isIncoming: boolean;
  relatedWorkItemId: string;
  relatedNumber: number;
  relatedTitle: string;
  relatedProjectKey?: string;
}

export interface WorkItemCustomField {
  fieldId: string;
  name: string;
  type: number;
  isRequired: boolean;
  optionsJson?: string;
  value?: string;
}

export interface WorkItemExternalCommunication {
  protocol: string;
  triageStatus: number;
  requesterPhone?: string;
  rating?: number;
  ratingComment?: string;
  completionConfirmedAt?: string;
  messages: import('./portal').ExternalRequestMessage[];
}

export interface WorkItemDetails {
  id: string;
  number: number;
  reference: string;
  boardId: string;
  boardName: string;
  projectId?: string;
  projectKey?: string;
  teamId?: string;
  teamName?: string;
  stageId?: string;
  stageName?: string;
  workflowStatusId?: string;
  workflowStatusName?: string;
  parentId?: string;
  sprintId?: string;
  sprintName?: string;
  kind: number;
  origin: number;
  title: string;
  subtitle?: string;
  description?: string;
  priority: number;
  responsibleId?: string;
  responsibleName?: string;
  participants: Array<{ userId: string; displayName?: string }>;
  requesterId?: string;
  requesterName?: string;
  requesterEmail?: string;
  startDate?: string;
  dueDate?: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  estimatedHours?: number;
  remainingHours?: number;
  realizedHours: number;
  points?: number;
  taskTypeId?: string;
  taskTypeName?: string;
  tags: Array<{ id: string; name: string; color: string }>;
  acceptanceCriteria?: string;
  isArchived: boolean;
  checklist: Array<{ id: string; text: string; done: boolean; position: number }>;
  subtasks: Array<{ id: string; number: number; title: string; isArchived: boolean; completedAt?: string }>;
  attachmentsCount: number;
  commentsCount: number;
  followerIds: string[];
  isFollowing: boolean;
  links: WorkItemLink[];
  customFields: WorkItemCustomField[];
  externalCommunication?: WorkItemExternalCommunication;
  version: string;
}

export interface SprintCapacity {
  userId: string;
  availableHours: number;
  daysOffHours: number;
}

export interface Sprint {
  id: string;
  projectId?: string;
  teamId: string;
  teamName: string;
  name: string;
  goal?: string;
  startDate: string;
  endDate: string;
  status: number;
  itemCount: number;
  completedItemCount?: number;
  storyPoints: number;
  completedStoryPoints?: number;
  plannedHours?: number;
  remainingHours?: number;
  capacityHours?: number;
  progressPercentage?: number;
  completedAt?: string;
  cancelledAt?: string;
  capacities: SprintCapacity[];
  itemSnapshots?: Array<{
    workItemId: string;
    workItemNumber: number;
    title: string;
    kind: number;
    points?: number;
    estimatedHours?: number;
    wasCompleted: boolean;
    workItemCompletedAt?: string;
    outcome: number;
    destinationSprintId?: string;
  }>;
}

export interface TeamMember {
  userId: string;
  name: string;
  weeklyCapacityHours: number;
  weekHours: number;
}

export interface Team {
  id: string;
  name: string;
  members: TeamMember[];
}

export const kindNames: Record<number, string> = {
  1: 'Épico',
  2: 'Feature',
  3: 'História',
  4: 'Bug',
  5: 'Tarefa',
  6: 'Subtarefa',
  7: 'Melhoria',
  8: 'Débito técnico',
  9: 'Solicitação',
  10: 'Incidente',
};

export const priorityNames: Record<number, string> = {
  0: 'Baixa',
  1: 'Média',
  2: 'Alta',
  3: 'Crítica',
};

export const originNames: Record<number, string> = {
  1: 'Criação interna',
  2: 'Portal externo',
  3: 'Formulário',
  4: 'Integração',
  5: 'Importação',
};

export const linkTypeNames: Record<number, string> = {
  1: 'Depende de',
  2: 'Bloqueia',
  3: 'Relacionado a',
  4: 'Duplicado de',
};

export function getItemDepth(item: BacklogItem, items: BacklogItem[]) {
  const byId = new Map(items.map((current) => [current.id, current]));
  let depth = 0;
  let parentId = item.parentId;
  const visited = new Set<string>();

  while (parentId && !visited.has(parentId)) {
    visited.add(parentId);
    depth += 1;
    parentId = byId.get(parentId)?.parentId;
  }

  return depth;
}
