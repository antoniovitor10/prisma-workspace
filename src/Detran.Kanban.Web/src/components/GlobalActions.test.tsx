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
  it('exibe quadros como checkboxes compactos e mantém o envio desabilitado sem título', async () => {
    renderDialog();

    const defaultBoard = await screen.findByRole('checkbox', { name: 'Backlog Mobile' });
    const longBoard = screen.getByRole('checkbox', { name: 'Operação e sustentação do aplicativo' });
    const submit = screen.getByRole('button', { name: 'Criar item' });

    expect(defaultBoard).toBeChecked();
    expect(longBoard).not.toBeChecked();
    expect(defaultBoard).toHaveStyle({ width: '16px', height: '16px', minHeight: '16px' });
    expect(submit).toBeDisabled();

    fireEvent.change(screen.getByRole('textbox', { name: 'Título da tarefa' }), { target: { value: 'Revisar login mobile' } });
    expect(submit).toBeEnabled();
  });
});
