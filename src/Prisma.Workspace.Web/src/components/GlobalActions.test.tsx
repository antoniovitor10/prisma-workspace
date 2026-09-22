import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import type { ProjectSummary } from '../pages/Projects';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { QuickCreateDialog } from './GlobalActions';

afterEach(() => vi.restoreAllMocks());

const project: ProjectSummary = {
  id: 'project-1',
  key: 'MOBILE',
  name: 'Aplicativo Mobile do Cidadão',
  defaultBoardId: 'board-1',
  boards: [
    { id: 'board-1', name: 'Backlog Mobile' },
    { id: 'board-2', name: 'Operação e sustentação do aplicativo' },
  ],
  teams: [],
};

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  vi.spyOn(api, 'getProjects').mockResolvedValue([project]);

  render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <QuickCreateDialog open currentProjectId={project.id} onOpenChange={vi.fn()} onCreated={vi.fn()} />
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

describe('QuickCreateDialog', () => {
  // A tarefa pertence a um único quadro (D83): a escolha é exclusiva, não múltipla.
  it('escolhe um único quadro, já no padrão do projeto, e exige título para enviar', async () => {
    renderDialog();

    const boardSelect = await screen.findByRole('combobox', { name: 'Quadro' });
    const submit = screen.getByRole('button', { name: 'Criar item' });

    expect(boardSelect).toHaveValue('board-1');
    expect(screen.queryByRole('checkbox', { name: 'Backlog Mobile' })).toBeNull();
    expect(submit).toBeDisabled();

    fireEvent.change(boardSelect, { target: { value: 'board-2' } });
    expect(boardSelect).toHaveValue('board-2');

    fireEvent.change(screen.getByRole('textbox', { name: 'Título da tarefa' }), { target: { value: 'Revisar login mobile' } });
    expect(submit).toBeEnabled();
  });
});
