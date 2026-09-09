import type { ExternalForm, ExternalRequestTriageInput } from '../types/portal';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5216';
const TOKEN_KEY = 'prisma_workspace_token';
const ORGANIZATION_KEY = 'prisma_workspace_organization';
const LEGACY_TOKEN_KEY = 'detran_kanban_token';
const LEGACY_ORGANIZATION_KEY = 'detran_kanban_organization';
let refreshRequest: Promise<string | null> | null = null;

export type SetupStatus = {
  initialized: boolean;
  setupAvailable: boolean;
};

export type SetupInput = {
  administratorName: string;
  administratorEmail: string;
  administratorPassword: string;
  organizationName: string;
  organizationSlug: string;
};

export class SetupApiError extends Error {
  public readonly status: number;
  public readonly code: string | null;

  constructor(
    status: number,
    code: string | null,
    message: string,
  ) {
    super(message);
    this.name = 'SetupApiError';
    this.status = status;
    this.code = code;
  }
}

async function readSetupResponse<T>(response: Response): Promise<T> {
  if (response.ok) return response.json() as Promise<T>;

  const error = await response.json().catch(() => null) as {
    type?: string;
    code?: string;
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  } | null;
  const validationMessage = error?.errors
    ? Object.values(error.errors).flat().find((value) => typeof value === 'string')
    : undefined;

  throw new SetupApiError(
    response.status,
    error?.type ?? error?.code ?? null,
    error?.detail ?? validationMessage ?? error?.title ?? 'Não foi possível concluir a configuração.',
  );
}

const isJwt = (token: string) => token.split('.').length === 3;

function migrateLegacyKey(currentKey: string, legacyKey: string) {
  const current = localStorage.getItem(currentKey);
  if (current) return current;
  const legacy = localStorage.getItem(legacyKey);
  if (!legacy) return null;
  localStorage.setItem(currentKey, legacy);
  localStorage.removeItem(legacyKey);
  return legacy;
}

export const api = {
  baseUrl: API_BASE_URL,
  getToken: () => {
    const token = migrateLegacyKey(TOKEN_KEY, LEGACY_TOKEN_KEY);

    if (token && !isJwt(token)) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(LEGACY_TOKEN_KEY);
      return null;
    }

    return token;
  },
  getUserId: (): string | null => {
    const token = api.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      return payload.sub
        ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
        ?? payload.nameid
        ?? null;
    } catch { return null; }
  },
  setToken: (token: string) => {
    if (!isJwt(token)) {
      throw new Error('Token de autenticação inválido.');
    }

    localStorage.setItem(TOKEN_KEY, token);
    localStorage.removeItem(LEGACY_TOKEN_KEY);
  },
  clearToken: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(LEGACY_TOKEN_KEY);
  },
  getOrganizationId: () => migrateLegacyKey(ORGANIZATION_KEY, LEGACY_ORGANIZATION_KEY),
  setOrganizationId: (organizationId: string) => {
    localStorage.setItem(ORGANIZATION_KEY, organizationId);
    localStorage.removeItem(LEGACY_ORGANIZATION_KEY);
  },
  clearOrganizationId: () => {
    localStorage.removeItem(ORGANIZATION_KEY);
    localStorage.removeItem(LEGACY_ORGANIZATION_KEY);
  },

  headers: (includeContentType = true) => {
    const token = api.getToken();
    return {
      ...(includeContentType ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
      ...(api.getOrganizationId() ? { 'X-Organization-Id': api.getOrganizationId()! } : {})
    };
  },

  async request(endpoint: string, options: RequestInit = {}, allowRefresh = true): Promise<any> {
    const url = `${API_BASE_URL}${endpoint}`;
    const hasFormDataBody = options.body instanceof FormData;
    const response = await fetch(url, {
      ...options,
      credentials: 'include',
      headers: {
        ...api.headers(!hasFormDataBody),
        ...options.headers
      }
    });

    if (response.status === 401 && allowRefresh && api.getToken()
        && !['/api/auth/login', '/api/auth/register', '/api/auth/refresh'].includes(endpoint)) {
      refreshRequest ??= fetch(`${API_BASE_URL}/api/auth/refresh`, {
        method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }
      }).then(async refreshResponse => {
        if (!refreshResponse.ok) return null;
        const data = await refreshResponse.json();
        if (!data?.accessToken) return null;
        api.setToken(data.accessToken);
        return data.accessToken as string;
      }).finally(() => { refreshRequest = null; });
      if (await refreshRequest) return api.request(endpoint, options, false);
    }

    if (response.status === 401) {
      api.clearToken();
      window.dispatchEvent(new Event('auth-change'));
      throw new Error('Não autorizado. Faça login novamente.');
    }

    if (!response.ok) {
      const errorText = await response.text();
      let errorJson;
      try {
        errorJson = JSON.parse(errorText);
      } catch {
        // não é json
      }
      const validationMessage = errorJson?.errors
        ? (Object.values(errorJson.errors).flat().find(value => typeof value === 'string') as string | undefined)
        : undefined;
      throw new Error(errorJson?.detail || validationMessage || errorJson?.title || 'Erro na requisição.');
    }

    if (response.status === 204) return null;
    return response.json();
  },

  async getSetupStatus(): Promise<SetupStatus> {
    const response = await fetch(`${API_BASE_URL}/api/setup/status`, {
      credentials: 'include',
      headers: { 'Accept': 'application/json' },
    });
    return readSetupResponse<SetupStatus>(response);
  },

  async completeSetup(token: string, input: SetupInput): Promise<{ initialized: true }> {
    const response = await fetch(`${API_BASE_URL}/api/setup`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
        'X-Prisma-Setup-Token': token,
      },
      body: JSON.stringify(input),
    });
    return readSetupResponse<{ initialized: true }>(response);
  },

  // Auth endpoints (Identity API)
  async register(email: string, passwordHash: string) {
    return this.request('/api/auth/register', {
      method: 'POST', body: JSON.stringify({ email, password: passwordHash })
    });
  },

  async confirmEmail(userId: string, token: string) {
    return this.request('/api/auth/confirm-email', {
      method: 'POST', body: JSON.stringify({ userId, token })
    });
  },

  async forgotPassword(email: string) {
    return this.request('/api/auth/forgot-password', {
      method: 'POST', body: JSON.stringify({ email })
    });
  },

  async resetPassword(userId: string, token: string, newPassword: string) {
    return this.request('/api/auth/reset-password', {
      method: 'POST', body: JSON.stringify({ userId, token, newPassword })
    });
  },

  async login(email: string, passwordHash: string) {
    const data = await this.request('/api/auth/login', {
      method: 'POST', body: JSON.stringify({ email, password: passwordHash })
    });
    if (data?.accessToken) {
      api.setToken(data.accessToken);
      window.dispatchEvent(new Event('auth-change'));
    }
    return data;
  },

  async logout() {
    try { await this.request('/api/auth/logout', { method: 'POST' }, false); }
    finally {
      api.clearToken();
      api.clearOrganizationId();
      window.dispatchEvent(new Event('auth-change'));
    }
  },

  async globalSearch(query: string, limit = 6) {
    return this.request(`/api/search?q=${encodeURIComponent(query)}&limit=${limit}`);
  },

  async searchWorkItems(projectId: string, query: string, limit = 12) {
    const parameters = new URLSearchParams({ projectId, query, limit: String(limit) });
    return this.request(`/api/WorkItems/search?${parameters.toString()}`);
  },

  async getNotifications(unreadOnly = false, page = 1, pageSize = 30) {
    return this.request(`/api/notifications?unreadOnly=${unreadOnly}&page=${page}&pageSize=${pageSize}`);
  },

  async markNotificationRead(id: string) {
    return this.request(`/api/notifications/${id}/read`, { method: 'PUT' });
  },

  async markAllNotificationsRead() {
    return this.request('/api/notifications/read-all', { method: 'PUT' });
  },

  async getNotificationPreferences() {
    return this.request('/api/notifications/preferences');
  },

  async setNotificationPreference(type: number, inAppEnabled: boolean, emailEnabled: boolean) {
    return this.request(`/api/notifications/preferences/${type}`, {
      method: 'PUT', body: JSON.stringify({ inAppEnabled, emailEnabled })
    });
  },

  async getAuditLogs(query = '') {
    return this.request(`/api/audit${query ? `?${query}` : ''}`);
  },

  // Organizações, membros e acesso
  async getOrganizations() {
    return this.request('/api/organizations');
  },
  async createOrganization(data: { name: string; slug?: string }) {
    return this.request('/api/organizations', { method: 'POST', body: JSON.stringify(data) });
  },
  async getCurrentOrganization() {
    return this.request('/api/organizations/current');
  },
  async updateCurrentOrganization(data: {
    name: string; locale: string; timeZone: string; weekStartDay: number;
  }) {
    return this.request('/api/organizations/current', { method: 'PUT', body: JSON.stringify(data) });
  },
  async getOrganizationAccess() {
    return this.request('/api/organizations/current/access');
  },
  async getOrganizationMembers() {
    return this.request('/api/organizations/current/members');
  },
  async updateOrganizationMember(userId: string, data: { role: number; isActive: boolean; displayName?: string | null }) {
    return this.request(`/api/organizations/current/members/${encodeURIComponent(userId)}`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },
  async inviteOrganizationMember(data: { email: string; role: number; expiresInDays?: number }) {
    return this.request('/api/organizations/current/invitations', {
      method: 'POST', body: JSON.stringify(data)
    });
  },
  async acceptOrganizationInvitation(token: string) {
    return this.request('/api/organizations/invitations/accept', {
      method: 'POST', body: JSON.stringify({ token })
    });
  },
  async getPermissionGrants() {
    return this.request('/api/organizations/current/permissions');
  },
  async setPermissionGrant(data: {
    userId: string; scope: number; scopeId?: string; permission: number; isAllowed: boolean;
  }) {
    return this.request('/api/organizations/current/permissions', {
      method: 'PUT', body: JSON.stringify(data)
    });
  },
  async deletePermissionGrant(grantId: string) {
    return this.request(`/api/organizations/current/permissions/${grantId}`, { method: 'DELETE' });
  },

  // Boards
  async getBoards() {
    return this.request('/api/Boards');
  },

  async getBoardById(id: string) {
    return this.request(`/api/Boards/${id}`);
  },

  async createBoard(name: string, projectId?: string, teamId?: string) {
    return this.request('/api/Boards', {
      method: 'POST',
      body: JSON.stringify({ name, projectId, teamId })
    });
  },

  // Projects and Scrum
  async getProjects(includeArchived = false) {
    return this.request(`/api/projects${includeArchived ? '?includeArchived=true' : ''}`);
  },

  async getProject(id: string) {
    return this.request(`/api/projects/${id}`);
  },

  async createProject(data: {
    key: string; name: string; description?: string; nature: number; workType: number; methodology?: number;
    startDate?: string; dueDate?: string;
  }) {
    return this.request('/api/projects', { method: 'POST', body: JSON.stringify(data) });
  },

  async updateProjectDetails(projectId: string, data: {
    name: string; description?: string | null; ownerId: string;
    startDate?: string | null; dueDate?: string | null; status: number;
    methodology: number; nature: number; workType: number; settingsJson?: string | null; tagIds: string[];
  }) {
    return this.request(`/api/projects/${projectId}`, { method: 'PUT', body: JSON.stringify(data) });
  },

  async archiveProject(projectId: string) {
    return this.request(`/api/projects/${projectId}/archive`, { method: 'POST' });
  },

  async reactivateProject(projectId: string) {
    return this.request(`/api/projects/${projectId}/reactivate`, { method: 'POST' });
  },

  async removeProjectMember(projectId: string, userId: string) {
    return this.request(`/api/projects/${projectId}/members/${encodeURIComponent(userId)}`, { method: 'DELETE' });
  },

  async setProjectMember(projectId: string, userId: string, role: number) {
    return this.request(`/api/projects/${projectId}/members/${encodeURIComponent(userId)}`, {
      method: 'PUT', body: JSON.stringify({ role })
    });
  },

  async getProjectHistory(projectId: string) {
    return this.request(`/api/projects/${projectId}/history`);
  },

  async saveProjectCustomField(projectId: string, fieldId: string | null, data: {
    name: string; type: number; isRequired: boolean; optionsJson?: string | null; position: number;
  }) {
    return this.request(`/api/projects/${projectId}/custom-fields${fieldId ? `/${fieldId}` : ''}`, {
      method: fieldId ? 'PUT' : 'POST', body: JSON.stringify(data)
    });
  },

  async deleteProjectCustomField(projectId: string, fieldId: string) {
    return this.request(`/api/projects/${projectId}/custom-fields/${fieldId}`, { method: 'DELETE' });
  },

  async getProjectBacklog(projectId: string) {
    return this.request(`/api/projects/${projectId}/backlog`);
  },

  async reorderBacklog(projectId: string, orderedIds: string[]) {
    return this.request(`/api/projects/${projectId}/backlog/order`, {
      method: 'PUT', body: JSON.stringify({ orderedIds })
    });
  },

  async planSprint(projectId: string, sprintId: string | null, workItemIds: string[]) {
    return this.request(`/api/projects/${projectId}/backlog/sprint`, {
      method: 'PUT', body: JSON.stringify({ sprintId, workItemIds })
    });
  },

  async updateBacklogItem(projectId: string, workItemId: string, data: {
    title: string;
    priority: number;
    points?: number | null;
    epicId?: string | null;
    updateEpic?: boolean;
  }) {
    return this.request(`/api/projects/${projectId}/backlog/items/${workItemId}`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },

  async getProjectSprints(projectId: string) {
    return this.request(`/api/projects/${projectId}/sprints`);
  },

  async createSprint(projectId: string, data: {
      teamId?: string; name: string; goal?: string; startDate: string; endDate: string;
    }) {
    return this.request(`/api/projects/${projectId}/sprints`, {
      method: 'POST',
      body: JSON.stringify({ ...data, teamId: data.teamId || null }),
    });
  },

  async updateSprint(sprintId: string, data: {
    name: string; goal?: string; startDate: string; endDate: string;
  }) {
    return this.request(`/api/sprints/${sprintId}`, { method: 'PUT', body: JSON.stringify(data) });
  },

  async changeSprintStatus(
    sprintId: string,
    status: number,
    incompleteItemsAction = 1,
    targetSprintId?: string | null,
  ) {
    return this.request(`/api/sprints/${sprintId}/status`, {
      method: 'PUT', body: JSON.stringify({ status, incompleteItemsAction, targetSprintId })
    });
  },

  async setSprintCapacity(sprintId: string, memberId: string, availableHours: number, daysOffHours: number) {
    return this.request(`/api/sprints/${sprintId}/capacity/${memberId}`, {
      method: 'PUT', body: JSON.stringify({ availableHours, daysOffHours })
    });
  },

  async getExternalPortal(projectId: string) {
    return this.request(`/api/projects/${projectId}/external-portal`);
  },

  async saveExternalPortal(projectId: string, data: {
    boardId: string;
    publicSlug: string;
    isEnabled: boolean;
    requiresAuthentication: boolean;
    accessModes: number;
  }) {
    return this.request(`/api/projects/${projectId}/external-portal`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },

  async createExternalPortalInvitation(projectId: string, email: string, expiresInDays = 7) {
    return this.request(`/api/projects/${projectId}/external-portal/invitations`, {
      method: 'POST', body: JSON.stringify({ email, expiresInDays })
    });
  },

  async getExternalForms(projectId: string): Promise<ExternalForm[]> {
    return this.request(`/api/projects/${projectId}/external-portal/forms`);
  },

  async saveExternalForm(projectId: string, data: Omit<ExternalForm, 'id' | 'projectId' | 'externalPortalId' | 'publicPath'>, formId?: string) {
    return this.request(`/api/projects/${projectId}/external-portal/forms${formId ? `/${formId}` : ''}`, {
      method: formId ? 'PUT' : 'POST', body: JSON.stringify(data)
    });
  },

  async getExternalRequests() {
    return this.request('/api/external-requests');
  },

  async replyToExternalRequest(protocol: string, content: string) {
    return this.request(`/api/external-requests/${encodeURIComponent(protocol)}/replies`, {
      method: 'POST', body: JSON.stringify({ content })
    });
  },

  async triageExternalRequest(protocol: string, data: ExternalRequestTriageInput) {
    return this.request(`/api/external-requests/${encodeURIComponent(protocol)}/triage`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async getPublicPortal(slug: string) {
    return this.request(`/api/public/portals/${encodeURIComponent(slug)}`);
  },

  async getPublicExternalForm(portalSlug: string, formSlug: string): Promise<ExternalForm> {
    return this.request(`/api/public/portals/${encodeURIComponent(portalSlug)}/forms/${encodeURIComponent(formSlug)}`);
  },

  async submitExternalForm(portalSlug: string, formSlug: string, data: {
    values: Record<string, string | undefined>;
    startedAt: string;
    website?: string;
    invitationToken?: string;
    verificationCode?: string;
  }, files: File[]) {
    const body = new FormData();
    body.append('payload', JSON.stringify(data));
    files.forEach((file) => body.append('files', file));
    return this.request(`/api/public/portals/${encodeURIComponent(portalSlug)}/forms/${encodeURIComponent(formSlug)}/submissions`, {
      method: 'POST', body
    });
  },

  async requestPortalVerificationCode(slug: string, email: string) {
    return this.request(`/api/public/portals/${encodeURIComponent(slug)}/verification-codes`, {
      method: 'POST', body: JSON.stringify({ email })
    });
  },

  async createExternalRequest(slug: string, data: {
    title: string;
    description?: string;
    requesterName: string;
    requesterEmail: string;
    invitationToken?: string;
    verificationCode?: string;
  }) {
    return this.request(`/api/public/portals/${encodeURIComponent(slug)}/requests`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async trackExternalRequest(protocol: string, accessKey: string) {
    return this.request(`/api/public/requests/${encodeURIComponent(protocol)}?key=${encodeURIComponent(accessKey)}`);
  },

  async addExternalRequestReply(protocol: string, accessKey: string, content: string) {
    return this.request(`/api/public/requests/${encodeURIComponent(protocol)}/replies`, {
      method: 'POST', body: JSON.stringify({ accessKey, content })
    });
  },

  async uploadExternalRequestAttachment(protocol: string, accessKey: string, file: File) {
    const form = new FormData();
    form.append('accessKey', accessKey);
    form.append('file', file);
    return this.request(`/api/public/requests/${encodeURIComponent(protocol)}/attachments`, {
      method: 'POST', body: form
    });
  },

  externalAttachmentUrl(protocol: string, accessKey: string, attachmentId: string) {
    return `${API_BASE_URL}/api/public/requests/${encodeURIComponent(protocol)}/attachments/${attachmentId}?key=${encodeURIComponent(accessKey)}`;
  },

  async rateExternalRequest(protocol: string, accessKey: string, rating: number, comment?: string) {
    return this.request(`/api/public/requests/${encodeURIComponent(protocol)}/rating`, {
      method: 'POST', body: JSON.stringify({ accessKey, rating, comment })
    });
  },

  async confirmExternalRequestCompletion(protocol: string, accessKey: string) {
    return this.request(`/api/public/requests/${encodeURIComponent(protocol)}/confirm-completion`, {
      method: 'POST', body: JSON.stringify({ accessKey })
    });
  },

  async getSavedFilters(boardId: string) {
    return this.request(`/api/boards/${boardId}/saved-filters`);
  },

  async createSavedFilter(boardId: string, name: string, filter: object) {
    return this.request(`/api/boards/${boardId}/saved-filters`, {
      method: 'POST', body: JSON.stringify({ name, filterJson: JSON.stringify(filter) })
    });
  },

  async deleteSavedFilter(boardId: string, filterId: string) {
    return this.request(`/api/boards/${boardId}/saved-filters/${filterId}`, { method: 'DELETE' });
  },

  async bulkWorkItems(boardId: string, data: {
    workItemIds: string[]; action: number; targetValue?: string; priority?: number;
  }) {
    return this.request(`/api/boards/${boardId}/work-items/bulk`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async getAutomations(boardId: string) {
    return this.request(`/api/boards/${boardId}/automations`);
  },

  async createAutomation(boardId: string, data: {
    triggerStageId: string; actionType: number; actionValue: string; isActive?: boolean;
  }) {
    return this.request(`/api/boards/${boardId}/automations`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async updateAutomation(boardId: string, automationId: string, data: {
    triggerStageId: string; actionType: number; actionValue: string; isActive: boolean;
  }) {
    return this.request(`/api/boards/${boardId}/automations/${automationId}`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },

  async deleteAutomation(boardId: string, automationId: string) {
    return this.request(`/api/boards/${boardId}/automations/${automationId}`, { method: 'DELETE' });
  },

  async getBoardLeadTime(boardId: string) {
    return this.request(`/api/Boards/${boardId}/lead-time`);
  },

  // Stages
  async getStages(boardId: string) {
    return this.request(`/api/Stages/board/${boardId}`);
  },

  async createStage(
    boardId: string, name: string, position: number,
    options?: { workflowStatusId?: string; category?: number; color?: string }
  ) {
    return this.request('/api/Stages', {
      method: 'POST',
      body: JSON.stringify({ boardId, name, position, ...options })
    });
  },

  async getProjectWorkflow(projectId: string) {
    return this.request(`/api/projects/${projectId}/workflow`);
  },

  async getOrganizationWorkflowTemplates(organizationId: string) {
    return this.request(`/api/organizations/${organizationId}/workflow-templates`);
  },

  async createOrganizationWorkflowTemplate(organizationId: string, data: object) {
    return this.request(`/api/organizations/${organizationId}/workflow-templates`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async disableOrganizationWorkflowTemplate(organizationId: string, templateId: string) {
    return this.request(`/api/organizations/${organizationId}/workflow-templates/${templateId}`, { method: 'DELETE' });
  },

  async setProjectWorkflowInheritance(projectId: string, mode: number, workflowTemplateId?: string | null) {
    return this.request(`/api/projects/${projectId}/workflow/inheritance`, {
      method: 'PUT', body: JSON.stringify({ mode, workflowTemplateId: workflowTemplateId || null })
    });
  },

  async createWorkflowStatus(projectId: string, data: {
    name: string; color: string; position: number; category: number;
    isInitial: boolean; isFinal: boolean;
  }) {
    return this.request(`/api/projects/${projectId}/workflow/statuses`, {
      method: 'POST', body: JSON.stringify(data)
    });
  },

  async updateWorkflowStatus(projectId: string, statusId: string, data: {
    name: string; color: string; position: number; category: number;
    isInitial: boolean; isFinal: boolean;
  }) {
    return this.request(`/api/projects/${projectId}/workflow/statuses/${statusId}`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },

  async deleteWorkflowStatus(projectId: string, statusId: string) {
    return this.request(`/api/projects/${projectId}/workflow/statuses/${statusId}`, { method: 'DELETE' });
  },

  async reorderWorkflowStatuses(projectId: string, statusIds: string[]) {
    return this.request(`/api/projects/${projectId}/workflow/status-order`, {
      method: 'PUT', body: JSON.stringify({ statusIds })
    });
  },

  async replaceWorkflowTransitions(
    projectId: string, transitions: { sourceStatusId: string; targetStatusId: string }[]
  ) {
    return this.request(`/api/projects/${projectId}/workflow/transitions`, {
      method: 'PUT', body: JSON.stringify({ transitions })
    });
  },

  async updateWorkflowStage(projectId: string, stageId: string, data: {
    name: string; position: number; workflowStatusId: string;
  }) {
    return this.request(`/api/projects/${projectId}/workflow/stages/${stageId}`, {
      method: 'PUT', body: JSON.stringify(data)
    });
  },

  async deleteWorkflowStage(projectId: string, stageId: string) {
    return this.request(`/api/projects/${projectId}/workflow/stages/${stageId}`, { method: 'DELETE' });
  },

  async updateBoardCardSettings(projectId: string, boardId: string, settings: object) {
    return this.request(`/api/projects/${projectId}/workflow/boards/${boardId}/card-settings`, {
      method: 'PUT', body: JSON.stringify({ settingsJson: JSON.stringify(settings) })
    });
  },

  // WorkItems
  async getWorkItems(boardId: string) {
    return this.request(`/api/WorkItems/board/${boardId}`);
  },

  async createWorkItem(data: {
    boardId: string;
    boardIds?: string[];
    projectId?: string;
    stageId?: string;
    parentId?: string;
    title: string;
    subtitle?: string;
    description?: string;
    priority: number;
    estimatedHours?: number;
    dueDate?: string;
    position: number;
  }) {
    return this.request('/api/WorkItems', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  },

  async deleteBoard(id: string, destinationBoardId?: string) {
    const query = destinationBoardId ? `?destinationBoardId=${encodeURIComponent(destinationBoardId)}` : '';
    return this.request(`/api/Boards/${id}${query}`, { method: 'DELETE' });
  },

  async reorderStages(boardId: string, orderedStageIds: string[]) {
    return this.request(`/api/Stages/board/${boardId}/order`, {
      method: 'PUT',
      body: JSON.stringify(orderedStageIds)
    });
  },

  async moveWorkItem(workItemId: string, destinationStageId: string | null, position: number) {
    return this.request('/api/WorkItems/move', {
      method: 'POST',
      body: JSON.stringify({ workItemId, destinationStageId, position })
    });
  },

  async getSubItems(parentId: string) {
    return this.request(`/api/WorkItems/${parentId}/subitems`);
  },

  async getAssignableUsers() {
    return this.request('/api/Users/assignable');
  },

  async getAssignees(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/assignees`);
  },

  async assignUser(workItemId: string, userId: string) {
    return this.request(`/api/WorkItems/${workItemId}/assignees`, {
      method: 'POST',
      body: JSON.stringify({ userId })
    });
  },

  async removeAssignee(workItemId: string, userId: string) {
    return this.request(`/api/WorkItems/${workItemId}/assignees/${userId}`, {
      method: 'DELETE'
    });
  },

  async getAttachments(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/attachments`);
  },

  async uploadAttachment(workItemId: string, file: File) {
    const formData = new FormData();
    formData.append('file', file);

    return this.request(`/api/WorkItems/${workItemId}/attachments`, {
      method: 'POST',
      body: formData
    });
  },

  async deleteAttachment(workItemId: string, attachmentId: string) {
    return this.request(`/api/WorkItems/${workItemId}/attachments/${attachmentId}`, {
      method: 'DELETE',
    });
  },

  async downloadAttachment(workItemId: string, attachmentId: string) {
    const response = await fetch(`${API_BASE_URL}/api/WorkItems/${workItemId}/attachments/${attachmentId}/download`, {
      headers: api.headers(false)
    });

    if (response.status === 401) {
      api.clearToken();
      window.dispatchEvent(new Event('auth-change'));
      throw new Error('Nao autorizado. Faca login novamente.');
    }

    if (!response.ok) {
      throw new Error('Erro ao baixar anexo.');
    }

    return response.blob();
  },

  async getRunningTimeEntry() {
    const response = await fetch(`${API_BASE_URL}/api/TimeEntries/running`, {
      headers: api.headers(false)
    });

    if (response.status === 204) return null;

    if (response.status === 401) {
      api.clearToken();
      window.dispatchEvent(new Event('auth-change'));
      throw new Error('Nao autorizado. Faca login novamente.');
    }

    if (!response.ok) {
      throw new Error('Erro ao buscar timer em andamento.');
    }

    return response.json();
  },

  async getTimeEntriesByWorkItem(workItemId: string) {
    return this.request(`/api/TimeEntries/work-item/${workItemId}`);
  },

  async startTimer(workItemId: string) {
    return this.request('/api/TimeEntries/start', {
      method: 'POST',
      body: JSON.stringify({ workItemId })
    });
  },

  async stopTimer(workItemId?: string) {
    return this.request('/api/TimeEntries/stop', {
      method: 'POST',
      body: JSON.stringify({ workItemId })
    });
  },

  async createManualTimeEntry(data: {
    workItemId: string;
    startedAt: string;
    endedAt: string;
    note?: string;
  }) {
    return this.request('/api/TimeEntries/manual', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  },

  async getWorkItemTimeTotal(workItemId: string) {
    return this.request(`/api/TimeEntries/work-item/${workItemId}/total`);
  },

  async getBoardTimeTotal(boardId: string) {
    return this.request(`/api/TimeEntries/board/${boardId}/total`);
  },

  async getMyWeeklyTime(weekStart?: string) {
    const query = weekStart ? `?weekStart=${weekStart}` : '';
    return this.request(`/api/TimeEntries/my/weekly${query}`);
  },

  async getProjectTimeReport(projectId: string, filters: {from?:string;to?:string;teamId?:string;userId?:string} = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key,value]) => { if (value) query.set(key,value); });
    return this.request(`/api/reports/projects/${projectId}/time${query.size?`?${query.toString()}`:''}`);
  },

  async getWikiTree(projectId: string, includeTrash = false) {
    return this.request(`/api/projects/${projectId}/wiki/tree${includeTrash ? '?includeTrash=true' : ''}`);
  },
  async getWikiPage(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}`);
  },
  async createWikiPage(projectId: string, parentPageId: string | null, title: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages`, { method: 'POST', body: JSON.stringify({ parentPageId, title }) });
  },
  async renameWikiPage(projectId: string, pageId: string, title: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/title`, { method: 'PUT', body: JSON.stringify({ title }) });
  },
  async saveWikiContent(projectId: string, pageId: string, contentHtml: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/content`, { method: 'PUT', body: JSON.stringify({ contentHtml }) });
  },
  async moveWikiPage(projectId: string, pageId: string, parentPageId: string | null, position: number) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/move`, { method: 'PUT', body: JSON.stringify({ parentPageId, position }) });
  },
  async acquireWikiLock(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/lock`, { method: 'POST' });
  },
  async releaseWikiLock(projectId: string, pageId: string, force = false) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/lock${force ? '?force=true' : ''}`, { method: 'DELETE' });
  },
  async deleteWikiPage(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}`, { method: 'DELETE' });
  },
  async restoreWikiPage(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/restore`, { method: 'POST' });
  },

  async getWikiHistory(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/history`);
  },
  async getWikiRevision(projectId: string, pageId: string, revisionId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/revisions/${revisionId}`);
  },
  async revertWikiPage(projectId: string, pageId: string, revisionId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/revisions/${revisionId}/revert`, { method: 'POST' });
  },
  async getWikiAttachments(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/attachments`);
  },
  async uploadWikiAttachment(projectId: string, pageId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/attachments`, { method: 'POST', body: form });
  },
  async deleteWikiAttachment(projectId: string, pageId: string, attachmentId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/attachments/${attachmentId}`, { method: 'DELETE' });
  },
  async downloadWikiAttachment(projectId: string, pageId: string, attachmentId: string): Promise<Blob> {
    const response = await fetch(`${API_BASE_URL}/api/projects/${projectId}/wiki/pages/${pageId}/attachments/${attachmentId}/download`, { headers: api.headers(false) });
    if (!response.ok) throw new Error('Não foi possível baixar o anexo.');
    return response.blob();
  },

  async getWikiPageLinks(projectId: string, pageId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/links`);
  },
  async linkWikiTask(projectId: string, pageId: string, workItemNumber: number) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/links`, { method: 'POST', body: JSON.stringify({ workItemNumber }) });
  },
  async unlinkWikiTask(projectId: string, pageId: string, workItemId: string) {
    return this.request(`/api/projects/${projectId}/wiki/pages/${pageId}/links/${workItemId}`, { method: 'DELETE' });
  },
  async getTaskWikiPages(projectId: string, workItemId: string) {
    return this.request(`/api/projects/${projectId}/wiki/task-links/${workItemId}`);
  },

  async getOrganizationHoursReport(filters: {from?:string;to?:string;teamId?:string;userId?:string} = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key,value]) => { if (value) query.set(key,value); });
    return this.request(`/api/reports/hours${query.size?`?${query.toString()}`:''}`);
  },

  async getProjectCustomFieldReport(projectId: string, filters: {fieldId?:string;value?:string} = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key,value]) => { if (value) query.set(key,value); });
    return this.request(`/api/reports/projects/${projectId}/custom-fields${query.size?`?${query.toString()}`:''}`);
  },

  async getPreparedReports(filters: {
    projectId?:string; teamId?:string; userId?:string; status?:string;
    priority?:number; from?:string; to?:string;
  } = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== '') query.set(key, String(value));
    });
    return this.request(`/api/reports/prepared${query.size ? `?${query.toString()}` : ''}`);
  },

  async getReportCatalog(projectId?: string) {
    return this.request(`/api/reports/catalog${projectId ? `?projectId=${projectId}` : ''}`);
  },

  async getSavedReports(projectId?: string) {
    return this.request(`/api/reports/definitions${projectId ? `?projectId=${projectId}` : ''}`);
  },

  async saveReport(data: unknown, id?: string) {
    return this.request(`/api/reports/definitions${id ? `/${id}` : ''}`, {
      method: id ? 'PUT' : 'POST',
      body: JSON.stringify(data)
    });
  },

  async duplicateReport(id: string) {
    return this.request(`/api/reports/definitions/${id}/duplicate`, { method: 'POST' });
  },

  async deleteReport(id: string) {
    return this.request(`/api/reports/definitions/${id}`, { method: 'DELETE' });
  },

  async runSavedReport(id: string) {
    return this.request(`/api/reports/definitions/${id}/run`, { method: 'POST' });
  },

  async previewReport(data: unknown) {
    return this.request('/api/reports/preview', {
      method: 'POST',
      body: JSON.stringify(data)
    });
  },

  async exportReport(id: string) {
    const response = await fetch(`${API_BASE_URL}/api/reports/definitions/${id}/export`, {
      headers: api.headers(false)
    });
    if (!response.ok) throw new Error('Não foi possível exportar o relatório.');
    const disposition = response.headers.get('content-disposition') ?? '';
    const fileName = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i)?.[1]
      ?? 'relatorio.csv';
    return { blob: await response.blob(), fileName: decodeURIComponent(fileName) };
  },

  async getCollaboratorDashboard(filters: {from?:string;to?:string} = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => { if (value) query.set(key, value); });
    return this.request(`/api/dashboards/collaborator${query.size ? `?${query.toString()}` : ''}`);
  },

  async getManagerDashboard(filters: {
    projectId?:string; teamId?:string; userId?:string; status?:string;
    priority?:number; from?:string; to?:string;
  } = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== '') query.set(key, String(value));
    });
    return this.request(`/api/dashboards/manager${query.size ? `?${query.toString()}` : ''}`);
  },

  async getProjectDashboard(projectId: string, filters: {
    teamId?:string; userId?:string; status?:string; priority?:number; from?:string; to?:string;
  } = {}) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== '') query.set(key, String(value));
    });
    return this.request(`/api/dashboards/projects/${projectId}${query.size ? `?${query.toString()}` : ''}`);
  },

  // Area pessoal ("Eu")
  async getMyTasks() {
    return this.request('/api/me/tasks');
  },

  async updateMyTaskPriorities(workItemIds: string[]) {
    return this.request('/api/me/tasks/priorities', {
      method: 'PUT',
      body: JSON.stringify({ workItemIds })
    });
  },

  async getMyActiveTimer() {
    const response = await fetch(`${API_BASE_URL}/api/me/active-timer`, {
      headers: api.headers(false)
    });
    if (response.status === 204) return null;
    if (response.status === 401) {
      api.clearToken();
      window.dispatchEvent(new Event('auth-change'));
      throw new Error('Nao autorizado. Faca login novamente.');
    }
    if (!response.ok) throw new Error('Erro ao buscar timer ativo.');
    return response.json();
  },

  // Feed da tarefa (comentarios + eventos de sistema)
  async getComments(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/comments`);
  },

  async addComment(workItemId: string, text: string, mentionedUserIds: string[] = []) {
    return this.request(`/api/WorkItems/${workItemId}/comments`, {
      method: 'POST',
      body: JSON.stringify({ text, mentionedUserIds })
    });
  },

  async getTaskEvents(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/events`);
  },

  async getTaskStateGraph(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/state-graph`);
  },

  // ─── Fase 2: Tipos, Tags, Pontos ───────────────────────────────
  async getTaskTypes() {
    return this.request('/api/task-types');
  },
  async createTaskType(name: string, color: string) {
    return this.request('/api/task-types', { method: 'POST', body: JSON.stringify({ name, color }) });
  },
  async deleteTaskType(id: string) {
    return this.request(`/api/task-types/${id}`, { method: 'DELETE' });
  },
  async getTags() {
    return this.request('/api/tags');
  },
  async createTag(name: string, color: string) {
    return this.request('/api/tags', { method: 'POST', body: JSON.stringify({ name, color }) });
  },
  async deleteTag(id: string) {
    return this.request(`/api/tags/${id}`, { method: 'DELETE' });
  },
  async setDescription(workItemId: string, description: string | null) {
    return this.request(`/api/WorkItems/${workItemId}/description`, {
      method: 'PUT',
      body: JSON.stringify({ description })
    });
  },
  async setTaxonomy(workItemId: string, taskTypeId: string | null, points: number | null, tagIds: string[]) {
    return this.request(`/api/WorkItems/${workItemId}/taxonomy`, {
      method: 'PUT',
      body: JSON.stringify({ taskTypeId, points, tagIds })
    });
  },

  // ─── Checklist ─────────────────────────────────────────────────
  async getChecklist(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/checklist`);
  },
  async addChecklistItem(workItemId: string, text: string) {
    return this.request(`/api/WorkItems/${workItemId}/checklist`, { method: 'POST', body: JSON.stringify({ text }) });
  },
  async toggleChecklistItem(workItemId: string, itemId: string, done: boolean) {
    return this.request(`/api/WorkItems/${workItemId}/checklist/${itemId}`, { method: 'PUT', body: JSON.stringify({ done }) });
  },
  async deleteChecklistItem(workItemId: string, itemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/checklist/${itemId}`, { method: 'DELETE' });
  },

  // ─── Clientes / Empresa ────────────────────────────────────────
  async getClients() {
    return this.request('/api/clients');
  },
  async createClient(name: string) {
    return this.request('/api/clients', { method: 'POST', body: JSON.stringify({ name }) });
  },
  async deleteClient(id: string) {
    return this.request(`/api/clients/${id}`, { method: 'DELETE' });
  },
  async getCompanyProjects() {
    return this.request('/api/company/projects');
  },
  async updateProject(boardId: string, clientId: string | null, description: string | null) {
    return this.request(`/api/company/projects/${boardId}`, { method: 'PUT', body: JSON.stringify({ clientId, description }) });
  },

  // ─── Meu Tempo: justificativas + ajuste por tarefa ─────────────
  async getDailyByTask(date: string) {
    return this.request(`/api/me/time/daily-by-task?date=${date}`);
  },
  async getJustifications(date: string) {
    return this.request(`/api/me/time/justifications?date=${date}`);
  },
  async addJustification(date: string, reason: string, hours: number) {
    return this.request('/api/me/time/justifications', { method: 'POST', body: JSON.stringify({ date, reason, hours }) });
  },
  async deleteJustification(id: string) {
    return this.request(`/api/me/time/justifications/${id}`, { method: 'DELETE' });
  },

  // ─── Fase 3: Aprovações ────────────────────────────────────────
  async getApprovals(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/approvals`);
  },
  async requestApproval(workItemId: string, approverId: string) {
    return this.request(`/api/WorkItems/${workItemId}/approvals`, { method: 'POST', body: JSON.stringify({ approverId }) });
  },
  async decideApproval(id: string, status: 1 | 2, note?: string) {
    return this.request(`/api/approvals/${id}`, { method: 'PUT', body: JSON.stringify({ status, note }) });
  },
  async getMyApprovals() {
    return this.request('/api/me/approvals');
  },

  // ─── Fase 3: Equipes ───────────────────────────────────────────
  async getTeams() {
    return this.request('/api/teams');
  },
  async createTeam(name: string, defaultWeeklyCapacityHours = 40) {
    return this.request('/api/teams', {
      method: 'POST', body: JSON.stringify({ name, defaultWeeklyCapacityHours })
    });
  },

  async getMyWork() {
    return this.request('/api/me/work');
  },

  async getWorkItemDetails(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}`);
  },

  async updateWorkItem(workItemId: string, data: {
    title: string; description?: string | null; kind: number; stageId?: string | null;
    priority: number; responsibleId?: string | null; teamId?: string | null; origin: number;
    requesterId?: string | null; requesterName?: string | null; requesterEmail?: string | null;
    startDate?: string | null; dueDate?: string | null; estimatedHours?: number | null;
    remainingHours?: number | null; points?: number | null; acceptanceCriteria?: string | null;
  }) {
    return this.request(`/api/WorkItems/${workItemId}`, { method: 'PUT', body: JSON.stringify(data) });
  },

  async archiveWorkItem(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}`, { method: 'DELETE' });
  },

  async reactivateWorkItem(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/reactivate`, { method: 'POST' });
  },

  async duplicateWorkItem(workItemId: string) {
    return this.request(`/api/WorkItems/${workItemId}/duplicate`, { method: 'POST' });
  },

  async addWorkItemLink(workItemId: string, targetWorkItemId: string, type: number) {
    return this.request(`/api/WorkItems/${workItemId}/links`, {
      method: 'POST', body: JSON.stringify({ targetWorkItemId, type })
    });
  },

  async removeWorkItemLink(workItemId: string, linkId: string) {
    return this.request(`/api/WorkItems/${workItemId}/links/${linkId}`, { method: 'DELETE' });
  },

  async setWorkItemFollowing(workItemId: string, following: boolean) {
    return this.request(`/api/WorkItems/${workItemId}/following`, {
      method: 'PUT', body: JSON.stringify({ following })
    });
  },

  async setWorkItemCustomFields(workItemId: string, values: Record<string, string | null>) {
    return this.request(`/api/WorkItems/${workItemId}/custom-fields`, {
      method: 'PUT', body: JSON.stringify({ values })
    });
  },
  async updateTeam(teamId: string, data: {
    name: string; leaderId?: string | null; defaultWeeklyCapacityHours: number;
  }) {
    return this.request(`/api/teams/${teamId}`, { method: 'PUT', body: JSON.stringify(data) });
  },
  async setTeamActive(teamId: string, isActive: boolean) {
    return this.request(`/api/teams/${teamId}/active`, {
      method: 'PUT', body: JSON.stringify({ isActive })
    });
  },
  async deleteTeam(id: string) {
    return this.request(`/api/teams/${id}`, { method: 'DELETE' });
  },
  async addTeamMember(teamId: string, userId: string, weeklyCapacityHours?: number) {
    return this.request(`/api/teams/${teamId}/members`, { method: 'POST', body: JSON.stringify({ userId, weeklyCapacityHours }) });
  },
  async removeTeamMember(teamId: string, userId: string) {
    return this.request(`/api/teams/${teamId}/members/${userId}`, { method: 'DELETE' });
  },
  async updateTeamMemberCapacity(teamId: string, userId: string, weeklyCapacityHours: number) {
    return this.request(`/api/teams/${teamId}/members/${userId}/capacity`, {
      method: 'PUT', body: JSON.stringify({ weeklyCapacityHours })
    });
  },
  async addProjectTeam(projectId: string, teamId: string) {
    return this.request(`/api/projects/${projectId}/teams/${teamId}`, { method: 'POST' });
  },
  async removeProjectTeam(projectId: string, teamId: string) {
    return this.request(`/api/projects/${projectId}/teams/${teamId}`, { method: 'DELETE' });
  }
};
