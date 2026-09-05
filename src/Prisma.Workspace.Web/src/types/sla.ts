export interface SlaRule {
  category?: string;
  priority?: number;
  firstResponseMinutes?: number;
  resolutionMinutes?: number;
  position?: number;
}

export interface ProjectSlaPolicy {
  projectId: string;
  isEnabled: boolean;
  firstResponseMinutes: number;
  resolutionMinutes: number;
  serviceStart: string;
  serviceEnd: string;
  businessDaysMask: number;
  timeZoneId: string;
  pauseWhileWaitingRequester: boolean;
  alertsEnabled: boolean;
  nearDueMinutes: number;
  holidays: string[];
  rules: SlaRule[];
  updatedAt?: string;
}
