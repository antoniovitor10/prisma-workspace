import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { AppThemeProvider } from '../styles/ThemeMode';
import { MinhasTarefas } from './MinhasTarefas';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const dashboard = {
  summary: { assigned: 1, today: 0, thisWeek: 1, overdue: 0, blocked: 0, externalRequests: 0, pendingApprovals: 0, mentions: 0 },
  tasks: [{
    id: 'task-1', number: 1028, title: 'Revisar proposta', boardId: 'board-1', boardName: 'Quadro principal',
    projectKey: 'TEST', stageName: 'Backlog', statusColor: '#94A3B8', priority: 1, kind: 3, origin: 1,
    assigned: true, created: false, following: false, today: false, thisWeek: true, overdue: false,
    blocked: false, externalRequest: false, upcomingDeadline: true, tags: [], userTimeSeconds: 0,
  }],
  comments: [], mentions: [], pendingApprovals: [], upcomingDeadlines: [], importantNotifications: [],
  projects: [], sprints: [], hours: { weekHours: 0, plannedHours: 0, daysWithoutEntry: 0 },
};

describe('Meu trabalho', () => {
  it('abre o detalhe completo ao clicar na linha e volta sem perder a fila', async () => {
    vi.spyOn(api, 'getMyWork').mockResolvedValue(dashboard);
    vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
    vi.spyOn(api, 'getUserId').mockReturnValue('user-1');
    vi.spyOn(api, 'getToken').mockReturnValue(null);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<MemoryRouter><QueryClientProvider client={client}><AppThemeProvider><MinhasTarefas /></AppThemeProvider></QueryClientProvider></MemoryRouter>);

    const task = await screen.findByRole('button', { name: 'Abrir tarefa 1028: Revisar proposta' });
    fireEvent.click(task);

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Revisar proposta' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Fechar modal' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir tarefa 1028: Revisar proposta' })).toBeInTheDocument();
  });
});
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
