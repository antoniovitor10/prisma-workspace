import { createContext, useContext } from 'react';

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  locale: string;
  timeZone: string;
  weekStartDay: number;
  role: number;
  isAdministrator: boolean;
}

export interface OrganizationStateValue {
  organizations: OrganizationSummary[];
  current: OrganizationSummary;
  switchOrganization: (organizationId: string) => void;
}

export const OrganizationStateContext = createContext<OrganizationStateValue | null>(null);

export function useOrganization() {
  const value = useContext(OrganizationStateContext);
  if (!value) throw new Error('useOrganization precisa estar dentro de OrganizationProvider.');
  return value;
}
