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
