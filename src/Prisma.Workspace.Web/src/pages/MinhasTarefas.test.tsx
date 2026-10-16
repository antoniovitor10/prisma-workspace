import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
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
  it('bloqueia cliques repetidos enquanto iniciar ou parar o timer está pendente', async () => {
    const active = { workItemId: 'task-1', startedAt: new Date().toISOString() };
    vi.spyOn(api, 'getMyWork').mockResolvedValue(dashboard);
    const getActive = vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
    vi.spyOn(api, 'getUserId').mockReturnValue('user-1');
    let finishStart!: (value: Awaited<ReturnType<typeof api.startTimer>>) => void;
    let finishStop!: (value: Awaited<ReturnType<typeof api.stopTimer>>) => void;
    const start = vi.spyOn(api, 'startTimer').mockImplementation(() => new Promise(resolve => { finishStart = resolve; }));
    const stop = vi.spyOn(api, 'stopTimer').mockImplementation(() => new Promise(resolve => { finishStop = resolve; }));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<MemoryRouter><QueryClientProvider client={client}><AppThemeProvider><MinhasTarefas /></AppThemeProvider></QueryClientProvider></MemoryRouter>);
    const startButton = await screen.findByTitle('Iniciar timer');
    fireEvent.click(startButton); fireEvent.click(startButton);
    expect(startButton).toBeDisabled(); expect(start).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    getActive.mockResolvedValue(active);
    await act(async () => { finishStart(active); });
    const stopButton = await screen.findByTitle('Parar timer');
    expect(stopButton).toBeEnabled();
    fireEvent.click(stopButton); fireEvent.click(stopButton);
    expect(stopButton).toBeDisabled(); expect(stop).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    getActive.mockResolvedValue(null);
    await act(async () => { finishStop(active); });
    expect(await screen.findByTitle('Iniciar timer')).toBeEnabled();
  });

  it('libera o timer depois de erro e permite tentar novamente', async () => {
    vi.spyOn(api, 'getMyWork').mockResolvedValue(dashboard);
    vi.spyOn(api, 'getMyActiveTimer').mockResolvedValue(null);
    vi.spyOn(api, 'getUserId').mockReturnValue('user-1');
    const start = vi.spyOn(api, 'startTimer').mockRejectedValue(new Error('Falha temporária do cronômetro.'));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<MemoryRouter><QueryClientProvider client={client}><AppThemeProvider><MinhasTarefas /></AppThemeProvider></QueryClientProvider></MemoryRouter>);
    fireEvent.click(await screen.findByTitle('Iniciar timer'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Falha temporária do cronômetro.');
    const button = screen.getByTitle('Iniciar timer');
    expect(button).toBeEnabled(); fireEvent.click(button);
    await waitFor(() => expect(start).toHaveBeenCalledTimes(2));
  });

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

it('recarga com timer aberto soma o intervalo uma vez às horas encerradas',async()=>{
  const now=Date.now();
  vi.spyOn(Date,'now').mockReturnValue(now);
  const startedAt=new Date(now-(18*3600+28*60)*1000).toISOString();
  vi.spyOn(api,'getMyWork').mockResolvedValue({...dashboard,tasks:[{...dashboard.tasks[0],userTimeSeconds:3600}]});
  vi.spyOn(api,'getMyActiveTimer').mockResolvedValue({workItemId:'task-1',startedAt});
  vi.spyOn(api,'getUserId').mockReturnValue('user-1');
  const client=new QueryClient({defaultOptions:{queries:{retry:false}}});
  const content=<MemoryRouter><QueryClientProvider client={client}><AppThemeProvider><MinhasTarefas/></AppThemeProvider></QueryClientProvider></MemoryRouter>;
  const first=render(content);
  expect(await screen.findByText('19:28:00')).toBeInTheDocument();
  first.unmount();
  render(content);
  expect(await screen.findByText('19:28:00')).toBeInTheDocument();
});
