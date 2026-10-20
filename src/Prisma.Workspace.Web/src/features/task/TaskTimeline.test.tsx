import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { TaskTimeline } from './TaskTimeline';

vi.mock('./TaskStateGraph', () => ({ TaskStateGraph: () => <div>Grafo complementar preservado</div> }));
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><TaskTimeline workItemId="task" /></ThemeProvider></QueryClientProvider>);
}
const history = {
  usesCurrentColumnNames: true,
  items: [
    { id: '2', kind: 'column_moved', occurredAt: '2026-10-12T12:00:00Z', actorName: 'Ana Alves', fromColumnName: 'Em andamento', toColumnName: 'Testes' },
    { id: '1', kind: 'created_in_column', occurredAt: '2026-10-12T10:00:00Z', actorName: null, toColumnName: 'Em andamento' },
  ],
};

describe('Linha do tempo', () => {
  it('mostra lista textual na ordem da API com autoria neutra e grafo complementar explícito', async () => {
    vi.spyOn(api, 'getTaskTimeline').mockResolvedValue(history);
    show();
    const list = await screen.findByRole('list', { name: 'Movimentações entre colunas' });
    const entries = within(list).getAllByRole('listitem');
    expect(entries[0]).toHaveTextContent('Ana Alves moveu de Em andamento para Testes');
    expect(entries[1]).toHaveTextContent('Tarefa criada na coluna Em andamento');
    expect(entries[1]).toHaveTextContent('Pessoa não registrada');
    expect(entries[0].querySelector('time')).toHaveAttribute('datetime', '2026-10-12T12:00:00Z');
    expect(screen.getByText('Os nomes exibidos refletem os nomes atuais das colunas.')).toBeVisible();
    expect(screen.queryByText(/visitas|permanência|lead time/i)).not.toBeInTheDocument();
    expect(screen.queryByText('Grafo complementar preservado')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Ver grafo de estados' }));
    expect(screen.getByText('Grafo complementar preservado')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Voltar à linha do tempo' }));
    expect(screen.getByRole('list')).toBeInTheDocument();
  });
  it('não inventa criação quando não há histórico persistido', async () => {
    vi.spyOn(api, 'getTaskTimeline').mockResolvedValue({ items: [], usesCurrentColumnNames: true });
    show();
    expect(await screen.findByText('Nenhuma movimentação registrada.')).toBeVisible();
    expect(screen.queryByText(/Tarefa criada/)).not.toBeInTheDocument();
  });
  it('mostra carregamento sem conteúdo inventado', () => {
    vi.spyOn(api, 'getTaskTimeline').mockReturnValue(new Promise(() => {}));
    show();
    expect(screen.getByRole('status')).toHaveTextContent('Carregando movimentações');
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
  it('permite nova tentativa e não expõe erro interno', async () => {
    vi.spyOn(api, 'getTaskTimeline').mockRejectedValueOnce(new Error('INTERNAL_SENTINEL')).mockResolvedValueOnce(history);
    show();
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar a linha do tempo.');
    expect(screen.queryByText('INTERNAL_SENTINEL')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(await screen.findByRole('list')).toBeInTheDocument();
  });
});
