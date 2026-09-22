import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OrganizationStateContext, type OrganizationSummary } from '../organizations/OrganizationState';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { ReportsHub } from './ReportsHub';

vi.mock('../../preview', () => ({ previewMode: false }));

const organizacao: OrganizationSummary = {
  id: 'org-1', name: 'Organização Um', slug: 'org-um', isActive: true,
  locale: 'pt-BR', timeZone: 'America/Recife', weekStartDay: 1, role: 1, isAdministrator: true,
};

const pessoas = [
  { userId: 'user-1', name: 'Ana Souza', isActive: true },
  { userId: 'user-2', name: 'Bruno Lima', isActive: true },
  { userId: 'user-3', name: 'Antigo Colaborador', isActive: false },
];

const relatorioVazio = {
  from: '2026-08-01', to: '2026-08-31',
  tasks: { open: 0, completed: 0, overdue: 0, blocked: 0, total: 0 },
  tasksByStatus: [], tasksByPriority: [], tasksByOrigin: [], tasksByResponsible: [],
  tasksByTeam: [], tasksByProject: [], sprintVelocity: [], workloadByPeriod: [], burndown: [],
  externalRequests: { total: 0, byCategory: [], byRequester: [] },
  hours: { planned: 0, realized: 0, variance: 0 },
};

function renderHub() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <OrganizationStateContext.Provider value={{
          organizations: [organizacao], current: organizacao, switchOrganization: vi.fn(),
        }}>
          <MemoryRouter>
            <ReportsHub />
          </MemoryRouter>
        </OrganizationStateContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(api, 'getOrganizationMembers').mockResolvedValue(pessoas);
  vi.spyOn(api, 'getProjects').mockResolvedValue([]);
  vi.spyOn(api, 'getPreparedReports').mockResolvedValue(relatorioVazio as never);
  vi.spyOn(api, 'getOrganizationHoursReport').mockResolvedValue({ people: [], totalSeconds: 0 } as never);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const seletorPessoa = () => screen.findByRole('combobox', { name: 'Pessoa' });

describe('relatório por pessoa', () => {
  it('oferece as pessoas ativas da organização', async () => {
    renderHub();

    const seletor = await seletorPessoa();
    expect(seletor).toHaveValue('');
    expect(await screen.findByRole('option', { name: 'Ana Souza' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Todas as pessoas' })).toBeInTheDocument();
    // Quem saiu da organização não deve poluir a escolha do gestor.
    expect(screen.queryByRole('option', { name: 'Antigo Colaborador' })).not.toBeInTheDocument();
  });

  it('refaz a consulta filtrando pela pessoa escolhida', async () => {
    renderHub();
    const seletor = await seletorPessoa();
    // O select é controlado: mudar o valor antes das opções chegarem não surte efeito.
    await screen.findByRole('option', { name: 'Bruno Lima' });

    fireEvent.change(seletor, { target: { value: 'user-2' } });

    await waitFor(() => expect(api.getPreparedReports).toHaveBeenCalledWith(
      expect.objectContaining({ userId: 'user-2' })));
  });

  it('sem escolha, não manda pessoa alguma para a API', async () => {
    renderHub();
    await seletorPessoa();

    await waitFor(() => expect(api.getPreparedReports).toHaveBeenCalled());
    const primeiro = (api.getPreparedReports as unknown as { mock: { calls: Array<[{ userId?: string }]> } })
      .mock.calls[0][0];
    expect(primeiro.userId).toBeUndefined();
  });

  it('diz de quem é o relatório no resumo que vai para o PDF', async () => {
    renderHub();
    const seletor = await seletorPessoa();
    await screen.findByRole('option', { name: 'Ana Souza' });

    fireEvent.change(seletor, { target: { value: 'user-1' } });

    // Um relatório de uma pessoa que não diz de quem é serve de pouco depois de impresso.
    // O nome também aparece como <option>, então a asserção olha o resumo inteiro.
    expect(await screen.findByText(/Todos os projetos · Ana Souza ·/)).toBeInTheDocument();
  });
});
