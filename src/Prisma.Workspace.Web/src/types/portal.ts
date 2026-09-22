export interface ExternalPortal {
  id: string;
  projectId: string;
  boardId: string;
  projectName: string;
  publicSlug: string;
  isEnabled: boolean;
  requiresAuthentication: boolean;
  accessModes: number;
  publicPath: string;
  forms: ExternalFormSummary[];
}

export interface ExternalFormSummary {
  id: string;
  publicSlug: string;
  title: string;
  description?: string;
  isEnabled: boolean;
  isDefault: boolean;
  publicPath: string;
}

export interface ExternalFormField {
  key: string;
  label: string;
  type: number;
  kind: number;
  isRequired: boolean;
  position: number;
  placeholder?: string;
  helpText?: string;
  options?: string[];
  validationPattern?: string;
  minLength?: number;
  maxLength?: number;
  conditionalFieldKey?: string;
  conditionalValue?: string;
  projectCustomFieldId?: string;
}

export interface ExternalFormAssignmentRule {
  fieldKey: string;
  operator: number;
  expectedValue?: string;
  responsibleId?: string;
  teamId?: string;
  priority?: number;
  stageId?: string;
  position: number;
}

export interface ExternalForm {
  id: string;
  projectId: string;
  externalPortalId: string;
  publicSlug: string;
  title: string;
  description?: string;
  category?: string;
  confirmationMessage?: string;
  isEnabled: boolean;
  isDefault: boolean;
  defaultPriority: number;
  initialStageId?: string;
  defaultTeamId?: string;
  defaultResponsibleId?: string;
  maxFiles: number;
  maxFileSizeBytes: number;
  allowedExtensions: string;
  allowedMimeTypes: string;
  minimumCompletionSeconds: number;
  fields: ExternalFormField[];
  assignmentRules: ExternalFormAssignmentRule[];
  publicPath: string;
}

export interface ExternalRequestMessage {
  id: string;
  authorType: number;
  authorName: string;
  content: string;
  createdAt: string;
}

export interface ExternalRequestAttachment {
  id: string;
  fileName: string;
  fileSize?: number;
  mimeType?: string;
  createdAt: string;
}

export interface ExternalRequestTriageEvent {
  id: string;
  action: number;
  actorName: string;
  description: string;
  dataJson?: string;
  createdAt: string;
}

export interface ExternalRequest {
  id: string;
  protocol: string;
  projectId: string;
  projectKey: string;
  projectName: string;
  boardId: string;
  workItemId: string;
  workItemNumber: number;
  title: string;
  description?: string;
  status: string;
  priority: number;
  requesterName: string;
  requesterEmail: string;
  requesterPhone?: string;
  category?: string;
  relatedService?: string;
  externalFormId?: string;
  externalFormTitle?: string;
  triageStatus: number;
  responsibleId?: string;
  teamId?: string;
  stageId?: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  rating?: number;
  ratingComment?: string;
  completionConfirmedAt?: string;
  submittedValues: Record<string, string | undefined>;
  triageEvents: ExternalRequestTriageEvent[];
  messages: ExternalRequestMessage[];
  attachments: ExternalRequestAttachment[];
}

export interface PublicExternalRequest {
  protocol: string;
  title: string;
  description?: string;
  status: string;
  category?: string;
  relatedService?: string;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
  rating?: number;
  ratingComment?: string;
  completionConfirmedAt?: string;
  messages: ExternalRequestMessage[];
  attachments: ExternalRequestAttachment[];
}

export interface CreatedExternalRequest {
  protocol: string;
  accessKey: string;
  trackingPath: string;
  workItemId: string;
  confirmationMessage?: string;
  confirmationDelivered: boolean;
}

export interface ExternalRequestTriageInput {
  action: number;
  reason?: string;
  message?: string;
  category?: string;
  priority?: number;
  responsibleId?: string;
  teamId?: string;
  projectId?: string;
  boardId?: string;
  stageId?: string;
  relatedWorkItemId?: string;
  relatedWorkItemNumber?: number;
}
