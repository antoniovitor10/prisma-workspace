import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from 'styled-components';
import { afterEach, expect, it, vi } from 'vitest';
import { theme } from '../../styles/theme';
import { api } from '../../services/api';
import { TaskTimePanel } from './TaskTimePanel';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it('permite iniciar nesta tarefa quando existe cronometro em outra', async () => {
  vi.spyOn(api, 'getTimeEntriesByWorkItem').mockResolvedValue([]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
  vi.spyOn(api, 'getRunningTimeEntry').mockResolvedValue({ id: 'timer', workItemId: 'outra', userId: 'user', startedAt: '2026-09-22T10:00:00Z' });
  const start = vi.spyOn(api, 'startTimer').mockResolvedValue(undefined);
  const stop = vi.spyOn(api, 'stopTimer').mockResolvedValue(undefined);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><TaskTimePanel workItemId="atual" realizedHours={0}/></ThemeProvider></QueryClientProvider>);
  await screen.findByText(/encerrado automaticamente/);
  const button = screen.getByRole('button', { name: 'Iniciar cronômetro' });
  expect(button).toBeEnabled();
  fireEvent.click(button);
  await waitFor(() => expect(start).toHaveBeenCalledWith('atual'));
  // A troca pertence ao servidor: nao encerrar o anterior em uma chamada separada.
  expect(stop).not.toHaveBeenCalled();
});

it('resume o tempo por responsavel e deixa cada intervalo no log', async () => {
  vi.spyOn(api, 'getTimeEntriesByWorkItem').mockResolvedValue([
    { id: 'entry-1', workItemId: 'atual', userId: 'user-1', startedAt: '2026-09-22T10:00:00Z', endedAt: '2026-09-22T11:00:00Z', durationSeconds: 3600 },
    { id: 'entry-2', workItemId: 'atual', userId: 'user-1', startedAt: '2026-09-22T12:00:00Z', endedAt: '2026-09-22T12:30:00Z', durationSeconds: 1800 },
    { id: 'entry-3', workItemId: 'atual', userId: 'user-2', startedAt: '2026-09-22T09:00:00Z', endedAt: '2026-09-22T11:00:00Z', durationSeconds: 7200 },
  ]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([
    { id: 'user-1', displayName: 'Barbara Menezes' },
    { id: 'user-2', displayName: 'Vinicius Bispo' },
  ]);
  vi.spyOn(api, 'getRunningTimeEntry').mockResolvedValue(null);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });

  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><TaskTimePanel workItemId="atual" realizedHours={3.5}/></ThemeProvider></QueryClientProvider>);

  const summary = await screen.findByRole('region', { name: 'Tempo geral por responsável' });
  await waitFor(() => expect(summary).toHaveTextContent('Barbara Menezes'));
  expect(summary).toHaveTextContent('1h 30min');
  expect(summary).toHaveTextContent('Vinicius Bispo');
  expect(summary).toHaveTextContent('2h 00min');
  expect(summary).toHaveTextContent('Início:');
  expect(summary).toHaveTextContent('Fim:');

  const log = screen.getByText('Log de apontamentos (3)').closest('details');
  expect(log).not.toHaveAttribute('open');
  fireEvent.click(screen.getByText('Log de apontamentos (3)'));
  expect(log).toHaveAttribute('open');
});
