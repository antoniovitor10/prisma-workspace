import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { ReportBuilder } from './ReportBuilder';

function renderBuilder() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ThemeProvider theme={theme}>
        <ReportBuilder />
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
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
  vi.restoreAllMocks();
});

describe('editor de filtros e métricas do relatório', () => {
  it('mantém todos os controles e a remoção dentro do grupo do filtro', async () => {
    renderBuilder();
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
    renderBuilder();
    await screen.findByRole('option', { name: 'Tarefas' });

    fireEvent.click(screen.getAllByRole('button', { name: 'Adicionar' })[1]);

    const metric = screen.getByRole('group', { name: 'Métrica 1' });
    expect(within(metric).getByRole('button', { name: 'Remover métrica' })).toBeInTheDocument();
  });
});
