import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { DependencyAutocomplete } from './DependencyAutocomplete';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function renderAutocomplete(onSelect = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ThemeProvider theme={theme}>
        <DependencyAutocomplete projectId="project-1" excludeWorkItemId="current" onSelect={onSelect} />
      </ThemeProvider>
    </QueryClientProvider>,
  );
  return onSelect;
}

describe('DependencyAutocomplete', () => {
  it('só pesquisa após dois caracteres e seleciona pela referência humana', async () => {
    const search = vi.spyOn(api, 'searchWorkItems').mockResolvedValue([{
      id: 'target', projectId: 'project-2', projectKey: 'DET', number: 42,
      title: 'Emitir documento', status: 'Em andamento',
    }]);
    const onSelect = renderAutocomplete();
    const input = screen.getByRole('combobox', { name: 'Buscar tarefa relacionada' });

    fireEvent.change(input, { target: { value: 'D' } });
    await new Promise(resolve => setTimeout(resolve, 300));
    expect(search).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'DET-4' } });
    await screen.findByRole('button', { name: /DET-42.*Emitir documento/ });
    fireEvent.click(screen.getByRole('button', { name: /DET-42.*Emitir documento/ }));

    expect(search).toHaveBeenCalledWith('project-1', 'DET-4');
    expect(onSelect).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'target' }));
    expect(input).toHaveValue('DET-42 · Emitir documento');
  });

  it('apresenta o detalhe do ProblemDetails convertido pelo cliente', async () => {
    vi.spyOn(api, 'searchWorkItems').mockRejectedValue(new Error('Acesso negado ao projeto.'));
    renderAutocomplete();
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'DET' } });
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Acesso negado ao projeto.'));
  });
});
