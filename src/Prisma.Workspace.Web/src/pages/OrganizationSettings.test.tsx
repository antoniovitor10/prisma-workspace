import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { OrganizationSettings } from './OrganizationSettings';

afterEach(() => vi.restoreAllMocks());

const organization: OrganizationSummary = {
  id: 'organization-1',
  name: 'Prisma Demo',
  slug: 'prisma-demo',
  isActive: true,
  locale: 'pt-BR',
  timeZone: 'America/Sao_Paulo',
  weekStartDay: 1,
  role: 2,
  isAdministrator: false,
};

describe('OrganizationSettings', () => {
  it('permite ao gestor de membros editar o nome funcional sem perder o e-mail', async () => {
    vi.spyOn(api, 'getCurrentOrganization').mockResolvedValue(organization);
    vi.spyOn(api, 'getOrganizationAccess').mockResolvedValue({
      role: 2,
      allowedPermissions: [1, 13],
    });
    vi.spyOn(api, 'getOrganizationMembers').mockResolvedValue([{
      userId: 'user-1',
      name: 'Maria Antiga',
      displayName: 'Maria Antiga',
      email: 'maria@empresa.com',
      userName: 'maria.legado',
      role: 6,
      isActive: true,
      joinedAt: '2026-01-01T00:00:00Z',
    }]);
    const updateMember = vi.spyOn(api, 'updateOrganizationMember').mockResolvedValue(undefined);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <OrganizationStateContext.Provider value={{
            organizations: [organization],
            current: organization,
            switchOrganization: vi.fn(),
          }}>
            <OrganizationSettings />
          </OrganizationStateContext.Provider>
        </ThemeProvider>
      </QueryClientProvider>,
    );

    const nameInput = await screen.findByRole('textbox', { name: 'Nome de exibição de Maria Antiga' });
    expect(screen.getByText('maria@empresa.com')).toBeInTheDocument();
    fireEvent.change(nameInput, { target: { value: '  Maria da Silva  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Salvar nome de Maria Antiga' }));

    await waitFor(() => expect(updateMember).toHaveBeenCalledWith('user-1', {
      role: 6,
      isActive: true,
      displayName: 'Maria da Silva',
    }));
  });
});
