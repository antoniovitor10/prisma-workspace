import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { TaskFeed } from './TaskFeed';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
it('separa movimentações da linha do tempo e preserva alterações no histórico geral', async () => {
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([{ id: 'actor', displayName: 'Ana Alves' }]);
  const getEvents = vi.spyOn(api, 'getTaskEvents').mockResolvedValue([
    { id: 'move', actorId: 'actor', kind: 'stage_changed', payload: '{"from":"Origem","to":"Destino"}', createdAt: '2026-10-20T10:00:00Z' },
    { id: 'update', actorId: 'actor', kind: 'updated', payload: '{}', createdAt: '2026-10-20T11:00:00Z' },
  ]);
  render(<ThemeProvider theme={theme}><TaskFeed workItemId="task" mode="history" /></ThemeProvider>);
  expect(await screen.findByText('Ana Alves atualizou os dados da tarefa')).toBeVisible();
  expect(screen.queryByText(/moveu|Origem|Destino/)).not.toBeInTheDocument();
  expect(getEvents).toHaveBeenCalledWith('task');
});
