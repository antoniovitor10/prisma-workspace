import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { Teams } from './Teams';

function renderTeams() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry:false } } });
  return render(<QueryClientProvider client={queryClient}><ThemeProvider theme={theme}><Teams/></ThemeProvider></QueryClientProvider>);
}

beforeEach(() => {
  vi.spyOn(api, 'getTeams').mockResolvedValue([]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
  vi.spyOn(api, 'getProjects').mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('feedback de equipes', () => {
  it('mostra o erro de criação como alerta junto do formulário', async () => {
    vi.spyOn(api, 'createTeam').mockRejectedValue(new Error('Sem permissão para criar equipes.'));
    renderTeams();

    fireEvent.change(screen.getByPlaceholderText('Nome da nova equipe'), { target:{ value:'Equipe restrita' } });
    fireEvent.click(screen.getByRole('button', { name:'Criar equipe' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Sem permissão para criar equipes.');
  });
});
