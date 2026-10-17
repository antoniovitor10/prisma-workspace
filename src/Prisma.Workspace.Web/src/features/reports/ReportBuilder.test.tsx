import { notifyManager, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { ReportBuilder } from './ReportBuilder';

let client: QueryClient | undefined;
async function renderBuilder() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client = queryClient;
  // Aguarda a consulta mockada e suas notificações antes da busca custosa por role.
  await act(async () => {
    render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <ReportBuilder />
        </ThemeProvider>
      </QueryClientProvider>,
    );
  });
}

beforeEach(() => {
  // Entrega as notificações dos mocks dentro do act inicial, antes de consultar os controles carregados.
  notifyManager.setScheduler(queueMicrotask);
  notifyManager.setNotifyFunction(callback => act(callback));
  vi.spyOn(api, 'getReportCatalog').mockResolvedValue({
    sources: [{
      source: 1,
      name: 'Tarefas',
      fields: [
        { key: 'number', label: 'Número', type: 'number', numeric: true },
        { key: 'title', label: 'Título', type: 'string', numeric: false },
      ],
    }],
    filterOperators: ['eq', 'between'],
    metrics: [1, 2],
    visualizations: [1],
  });
  vi.spyOn(api, 'getSavedReports').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  client?.clear();
  vi.restoreAllMocks();
  notifyManager.setScheduler(callback => setTimeout(callback, 0));
  notifyManager.setNotifyFunction(callback => callback());
});

describe('editor de filtros e métricas do relatório', () => {
  it('mantém todos os controles e a remoção dentro do grupo do filtro', async () => {
    await renderBuilder();
    await screen.findByRole('option', { name: 'Tarefas' });

    fireEvent.click(screen.getAllByRole('button', { name: 'Adicionar' })[0]);

    const filter = screen.getByRole('group', { name: 'Filtro 1' });
    expect(within(filter).getByRole('combobox', { name: 'Campo do filtro' })).toBeInTheDocument();
    expect(within(filter).getByRole('combobox', { name: 'Operador' })).toBeInTheDocument();
    expect(within(filter).getByRole('textbox', { name: 'Valor do filtro' })).toBeInTheDocument();
    expect(within(filter).getByRole('button', { name: 'Remover filtro' })).toBeInTheDocument();

    fireEvent.change(within(filter).getByRole('combobox', { name: 'Operador' }), {
      target: { value: 'between' },
    });
    expect(within(filter).getByRole('textbox', { name: 'Valor final do filtro' })).toBeInTheDocument();

    fireEvent.click(within(filter).getByRole('button', { name: 'Remover filtro' }));
    expect(screen.queryByRole('group', { name: 'Filtro 1' })).not.toBeInTheDocument();
  });

  it('agrupa a lixeira com a métrica correspondente', async () => {
    await renderBuilder();
    await screen.findByRole('option', { name: 'Tarefas' });

    fireEvent.click(screen.getAllByRole('button', { name: 'Adicionar' })[1]);

    const metric = screen.getByRole('group', { name: 'Métrica 1' });
    expect(within(metric).getByRole('button', { name: 'Remover métrica' })).toBeInTheDocument();
  });
});
