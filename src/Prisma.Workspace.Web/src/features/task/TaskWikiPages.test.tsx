import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { TaskWikiPages } from './TaskWikiPages';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('páginas da Wiki vinculadas à tarefa', () => {
  it('oferece uma ação explícita para visualizar a página relacionada', async () => {
    vi.spyOn(api, 'getTaskWikiPages').mockResolvedValue([{
      pageId: 'page-1', title: 'Processo de compra',
    }]);
    const onNavigate = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <MemoryRouter initialEntries={['/projects/project-1/backlog']}>
        <QueryClientProvider client={client}>
          <ThemeProvider theme={theme}>
            <Routes>
              <Route path="/projects/:projectId/backlog" element={<TaskWikiPages projectId="project-1" workItemId="task-42" onNavigate={onNavigate} />} />
              <Route path="/projects/:projectId/wiki/:pageId" element={<div>Página Wiki aberta</div>} />
            </Routes>
          </ThemeProvider>
        </QueryClientProvider>
      </MemoryRouter>,
    );

    const open = await screen.findByRole('button', { name: 'Visualizar página da Wiki Processo de compra' });
    expect(open).toHaveTextContent('Visualizar');
    fireEvent.click(open);

    expect(onNavigate).toHaveBeenCalledOnce();
    expect(await screen.findByText('Página Wiki aberta')).toBeInTheDocument();
  });
});
