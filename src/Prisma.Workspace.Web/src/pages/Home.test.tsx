import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrganizationStateContext, type OrganizationStateValue } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { AppThemeProvider } from '../styles/ThemeMode';
import { Home } from './Home';

const organization: OrganizationStateValue['organizations'][number] = {
  id: 'org-1', name: 'Prisma Labs', slug: 'prisma-labs', isActive: true,
  locale: 'pt-BR', timeZone: 'America/Sao_Paulo', weekStartDay: 1, role: 1, isAdministrator: true,
};

afterEach(() => vi.restoreAllMocks());

describe('Home', () => {
  it('resume prioridades reais e oferece continuidade para o trabalho e projetos', async () => {
    vi.spyOn(api, 'getMyWork').mockResolvedValue({
      summary: { assigned: 8, today: 3, thisWeek: 6, overdue: 2, blocked: 1, pendingApprovals: 0 },
      tasks: [{ id: 'task-1', number: 42, title: 'Revisar jornada inicial', boardId: 'board-1', boardName: 'Produto', projectKey: 'PRISMA', stageName: 'Em andamento', priority: 3, today: true, overdue: true, blocked: false }],
      importantNotifications: [],
    } as never);
    vi.spyOn(api, 'getProjects').mockResolvedValue([{ id: 'project-1', key: 'PRISMA', name: 'Prisma WorkSpace', boards: [{ id: 'board-1', name: 'Produto' }], teams: [], updatedAt: '2026-09-03' }]);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const state: OrganizationStateValue = { organizations: [organization], current: organization, switchOrganization: vi.fn() };

    render(<MemoryRouter initialEntries={['/home']}><QueryClientProvider client={queryClient}><AppThemeProvider><OrganizationStateContext.Provider value={state}><Routes><Route path="/home" element={<Home/>}/><Route path="/me/tasks" element={<div>Fila pessoal</div>}/></Routes></OrganizationStateContext.Provider></AppThemeProvider></QueryClientProvider></MemoryRouter>);

    expect(await screen.findByRole('heading', { name: 'O trabalho que importa, claro desde o primeiro olhar.' })).toBeInTheDocument();
    expect(await screen.findByText(/Revisar jornada inicial/)).toBeInTheDocument();
    expect(await screen.findByText('Prisma WorkSpace')).toBeInTheDocument();
    expect(await screen.findByText('8')).toBeInTheDocument();
    expect(await screen.findByText('2')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Ver meu trabalho/ }));
    expect(await screen.findByText('Fila pessoal')).toBeInTheDocument();
  });
});
