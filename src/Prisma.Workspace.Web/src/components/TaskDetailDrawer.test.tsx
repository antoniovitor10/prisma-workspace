import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from 'styled-components';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import type { WorkItemDetails } from '../types/scrum';
import { TaskDetailDrawer } from './TaskDetailDrawer';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('TaskDetailDrawer', () => {
  it('abre o detalhe sem retirar o usuário da visão atual', () => {
    const onOpenChange = vi.fn();
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <TaskDetailDrawer
            projectKey="TESTE"
            item={{ id: 'item-1234', boardId: 'board', boardName: 'Produto', kind: 3, title: 'Validar jornada', priority: 1, rank: 1 }}
            onOpenChange={onOpenChange}
          />
        </ThemeProvider>
      </QueryClientProvider>,
    );

    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Validar jornada' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Fechar modal' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('restaura o valor anterior quando uma edição otimista falha', async () => {
    const details: WorkItemDetails = {
      id: 'item-1234', number: 42, reference: 'TESTE-42', boardId: 'board', boardName: 'Produto',
      stageId: 'stage-1', stageName: 'Desenvolvimento', workflowStatusId: 'status-1', workflowStatusName: 'Em andamento',
      kind: 5, origin: 1, title: 'Título original', priority: 1, participants: [],
      createdAt: '2026-07-20T10:00:00Z', updatedAt: '2026-07-20T10:00:00Z',
      realizedHours: 0, tags: [], isArchived: false, checklist: [], subtasks: [],
      attachmentsCount: 0, commentsCount: 0, followerIds: [], isFollowing: false,
      links: [], customFields: [], version: 'v1',
    };
    vi.spyOn(api, 'getToken').mockReturnValue('jwt');
    vi.spyOn(api, 'getWorkItemDetails').mockResolvedValue(details);
    vi.spyOn(api, 'getStages').mockResolvedValue([{
      id: 'stage-1', name: 'Desenvolvimento', workflowStatusId: 'status-1', statusName: 'Em andamento',
    }]);
    vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
    vi.spyOn(api, 'getAttachments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskTypes').mockResolvedValue([]);
    vi.spyOn(api, 'getTags').mockResolvedValue([]);
    vi.spyOn(api, 'getChecklist').mockResolvedValue([]);
    vi.spyOn(api, 'getComments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskEvents').mockResolvedValue([]);
    vi.spyOn(api, 'updateWorkItem').mockRejectedValue(new Error('Falha simulada'));
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <TaskDetailDrawer
            projectKey="TESTE"
            item={{ id: details.id, boardId: details.boardId, boardName: details.boardName, kind: details.kind, title: details.title, priority: details.priority, rank: 1 }}
            onOpenChange={vi.fn()}
          />
        </ThemeProvider>
      </QueryClientProvider>,
    );

    const title = await screen.findByRole('textbox', { name: 'Título' });
    await waitFor(() => expect(title).toHaveValue('Título original'));
    fireEvent.change(title, { target: { value: 'Título otimista' } });
    expect(title).toHaveValue('Título otimista');
    fireEvent.blur(title);

    await screen.findByText('Falha ao salvar');
    await waitFor(() => expect(title).toHaveValue('Título original'));
  });

  it('usa seis abas e oculta Story Points em projetos Kanban', async () => {
    const details: WorkItemDetails = {
      id: 'kanban-item', number: 9, reference: 'KAN-9', projectId: 'project-1', projectKey: 'KAN',
      boardId: 'board', boardName: 'Operação', kind: 5, origin: 1, title: 'Atender demanda', priority: 1,
      participants: [], createdAt: '2026-08-20T10:00:00Z', updatedAt: '2026-08-20T10:00:00Z',
      realizedHours: 0, points: 8, tags: [], isArchived: false, checklist: [], subtasks: [],
      attachmentsCount: 0, commentsCount: 0, followerIds: [], isFollowing: false,
      links: [], customFields: [], version: 'v1',
    };
    vi.spyOn(api, 'getToken').mockReturnValue('jwt');
    vi.spyOn(api, 'getWorkItemDetails').mockResolvedValue(details);
    vi.spyOn(api, 'getProject').mockResolvedValue({ id: 'project-1', methodology: 1, teams: [] });
    vi.spyOn(api, 'getStages').mockResolvedValue([]);
    vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
    vi.spyOn(api, 'getAttachments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskTypes').mockResolvedValue([]);
    vi.spyOn(api, 'getTags').mockResolvedValue([]);
    vi.spyOn(api, 'getComments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskEvents').mockResolvedValue([]);
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(<MemoryRouter><QueryClientProvider client={queryClient}><ThemeProvider theme={theme}>
      <TaskDetailDrawer projectKey="KAN" item={{ id: details.id, boardId: details.boardId, boardName: details.boardName, kind: 5, title: details.title, priority: 1, rank: 1 }} onOpenChange={vi.fn()} />
    </ThemeProvider></QueryClientProvider></MemoryRouter>);

    await screen.findByRole('textbox', { name: 'Título' });
    await waitFor(() => expect(api.getProject).toHaveBeenCalled());
    expect(screen.queryByText('Story points')).not.toBeInTheDocument();
    for (const tab of ['Descrição', 'Comentários', 'Subtarefas', 'Anexos', 'Histórico', 'Grafo de estados']) {
      expect(screen.getByRole('button', { name: tab })).toBeInTheDocument();
    }
    fireEvent.click(screen.getByRole('button', { name: 'Comentários' }));
    expect(await screen.findByRole('heading', { name: 'Comentários internos' })).toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Título' })).not.toBeInTheDocument();
  });

  it('adiciona vários responsáveis sem substituir os já alocados', async () => {
    const people = [
      { id: 'ana', displayName: 'Ana Silva' },
      { id: 'bruno', displayName: 'Bruno Lima' },
      { id: 'carla', displayName: 'Carla Souza' },
    ];
    const participants = [{ userId: 'ana', displayName: 'Ana Silva' }];
    const details: WorkItemDetails = {
      id: 'multi-item', number: 12, reference: 'KAN-12', projectId: 'project-1', projectKey: 'KAN',
      boardId: 'board', boardName: 'Produto', kind: 5, origin: 1, title: 'Entrega compartilhada', priority: 1,
      responsibleId: 'ana', participants, createdAt: '2026-09-21T10:00:00Z', updatedAt: '2026-09-21T10:00:00Z',
      realizedHours: 0, tags: [], isArchived: false, checklist: [], subtasks: [], attachmentsCount: 0,
      commentsCount: 0, followerIds: [], isFollowing: false, links: [], customFields: [], version: 'v1',
    };
    vi.spyOn(api, 'getToken').mockReturnValue('jwt');
    vi.spyOn(api, 'getWorkItemDetails').mockImplementation(async () => ({ ...details, participants: [...participants] }));
    vi.spyOn(api, 'getProject').mockResolvedValue({ id: 'project-1', methodology: 1, teams: [] });
    vi.spyOn(api, 'getStages').mockResolvedValue([]);
    vi.spyOn(api, 'getAssignableUsers').mockResolvedValue(people);
    vi.spyOn(api, 'getAttachments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskTypes').mockResolvedValue([]);
    vi.spyOn(api, 'getTags').mockResolvedValue([]);
    vi.spyOn(api, 'getComments').mockResolvedValue([]);
    vi.spyOn(api, 'getTaskEvents').mockResolvedValue([]);
    const assignUser = vi.spyOn(api, 'assignUser').mockImplementation(async (_workItemId, userId) => {
      const person = people.find(candidate => candidate.id === userId)!;
      participants.push({ userId: person.id, displayName: person.displayName });
      return undefined;
    });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

    render(<MemoryRouter><QueryClientProvider client={queryClient}><ThemeProvider theme={theme}>
      <TaskDetailDrawer projectKey="KAN" item={{ id: details.id, boardId: details.boardId, boardName: details.boardName, kind: 5, title: details.title, priority: 1, rank: 1 }} onOpenChange={vi.fn()} />
    </ThemeProvider></QueryClientProvider></MemoryRouter>);

    await screen.findByText('Ana Silva', { selector: 'span' });
    const picker = screen.getByRole('combobox', { name: 'Novo responsável' });
    fireEvent.change(picker, { target: { value: 'bruno' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Adicionar responsável' }));
    await waitFor(() => expect(assignUser).toHaveBeenCalledWith('multi-item', 'bruno'));
    expect(await screen.findByRole('button', { name: 'Remover responsável Bruno Lima' })).toBeInTheDocument();

    fireEvent.change(screen.getByRole('combobox', { name: 'Novo responsável' }), { target: { value: 'carla' } });
    fireEvent.submit(screen.getByRole('form', { name: 'Adicionar responsável' }));
    await waitFor(() => expect(assignUser).toHaveBeenCalledWith('multi-item', 'carla'));
    expect(await screen.findByRole('button', { name: 'Remover responsável Carla Souza' })).toBeInTheDocument();
    expect(screen.getByText('Ana Silva', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByText('3 responsáveis')).toBeInTheDocument();
  });
});
