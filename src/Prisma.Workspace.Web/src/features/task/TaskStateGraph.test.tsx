import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { TaskStateGraph } from './TaskStateGraph';
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
it('preserva o grafo complementar sem expor permanência por etapa no tooltip', async () => {
  vi.spyOn(api, 'getTaskStateGraph').mockResolvedValue({ nodes: [{ id: 'stage', name: 'Em andamento', visits: 1, totalSeconds: 90061, isCurrent: true }], edges: [] });
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><TaskStateGraph workItemId="task" createdAt="2026-10-20T10:00:00Z" createdByName="Ana Alves" /></ThemeProvider></QueryClientProvider>);
  expect(await screen.findByText('Em andamento')).toBeVisible();
  expect(screen.getByTitle('Em andamento · 1 visita(s) (estado atual)')).toBeVisible();
  expect(screen.queryByTitle(/Permanência|90061|1\.0d/)).not.toBeInTheDocument();
  expect(screen.getByText('Ana Alves')).toBeVisible();
});
