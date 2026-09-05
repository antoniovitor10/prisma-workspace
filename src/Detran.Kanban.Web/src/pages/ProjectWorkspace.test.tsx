import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import type { ProjectSummary } from './Projects';
import { ProjectBoards } from './ProjectWorkspace';

const project: ProjectSummary = {
  id: 'project-1',
  key: 'PORTAL',
  name: 'Portal de serviços',
  boards: [],
  teams: [{ id: 'team-1', name: 'Produtos digitais' }],
};

function ProjectContext() {
  return <Outlet context={{ project }} />;
}

afterEach(() => vi.restoreAllMocks());

describe('ProjectBoards', () => {
  it('cria um quadro já vinculado ao projeto e à equipe escolhida', async () => {
    const createBoard = vi.spyOn(api, 'createBoard').mockResolvedValue('board-new');
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <MemoryRouter initialEntries={['/projects/project-1/boards']}>
            <Routes>
              <Route path="/projects/:projectId" element={<ProjectContext />}>
                <Route path="boards" element={<ProjectBoards />} />
              </Route>
              <Route path="/boards/:boardId" element={<div>Quadro criado</div>} />
            </Routes>
          </MemoryRouter>
        </ThemeProvider>
      </QueryClientProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Novo quadro' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Nome do quadro' }), {
      target: { value: 'Atendimento digital' },
    });
    fireEvent.change(screen.getByRole('combobox', { name: 'Equipe do quadro' }), {
      target: { value: 'team-1' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Criar quadro' }));

    await waitFor(() => expect(createBoard).toHaveBeenCalledWith(
      'Atendimento digital', 'project-1', 'team-1',
    ));
    expect(await screen.findByText('Quadro criado')).toBeInTheDocument();
  });
});
