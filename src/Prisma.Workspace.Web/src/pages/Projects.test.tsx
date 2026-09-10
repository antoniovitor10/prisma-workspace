import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { Projects, type ProjectSummary } from './Projects';

afterEach(() => {
  // Sem desmontar, a tela do teste anterior fica no documento e as buscas passam a
  // encontrar dois "Novo projeto".
  cleanup();
  vi.restoreAllMocks();
});

const organization = (id: string, name: string): OrganizationSummary => ({
  id,
  name,
  slug: id,
  isActive: true,
  locale: 'pt-BR',
  timeZone: 'America/Sao_Paulo',
  weekStartDay: 1,
  role: 1,
  isAdministrator: true,
});

const project = (id: string, name: string): ProjectSummary => ({
  id,
  key: id.toUpperCase(),
  name,
  boards: [],
  teams: [],
});

function view(current: OrganizationSummary, organizations: OrganizationSummary[]) {
  return (
    <MemoryRouter initialEntries={['/projects']}>
      <ThemeProvider theme={theme}>
        <OrganizationStateContext.Provider value={{ organizations, current, switchOrganization: vi.fn() }}>
          <Projects />
        </OrganizationStateContext.Provider>
      </ThemeProvider>
    </MemoryRouter>
  );
}

describe('Projects', () => {
  it('recarrega os projetos quando a organização ativa muda', async () => {
    const first = organization('org-a', 'Organização A');
    const second = organization('org-b', 'Organização B');
    const getProjects = vi.spyOn(api, 'getProjects')
      .mockResolvedValueOnce([project('a', 'Projeto da organização A')])
      .mockResolvedValueOnce([project('b', 'Projeto da organização B')]);

    const rendered = render(view(first, [first, second]));
    expect(await screen.findByText('Projeto da organização A')).toBeInTheDocument();

    rendered.rerender(view(second, [first, second]));

    expect(await screen.findByText('Projeto da organização B')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Projeto da organização A')).not.toBeInTheDocument());
    expect(getProjects).toHaveBeenCalledTimes(2);
  });

  it('cria projeto sem prazo e sem data de inicio, e diz que sao opcionais', async () => {
    const org = organization('org-a', 'Organização A');
    vi.spyOn(api, 'getProjects').mockResolvedValue([]);
    const createProject = vi.spyOn(api, 'createProject').mockResolvedValue({ id: 'novo' } as never);

    render(view(org, [org]));

    fireEvent.click(await screen.findByRole('button', { name: 'Novo projeto' }));
    fireEvent.change(screen.getByPlaceholderText('Nome do projeto'), { target: { value: 'Sem prazo' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Natureza' }), { target: { value: '1' } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Tipo de Trabalho' }), { target: { value: '1' } });

    // O que o PO pediu: dar para criar sem definir prazo. Ja funcionava; o que faltava era
    // a tela dizer isso, porque ao lado de dois selects obrigatorios o campo parecia exigido.
    expect(screen.getByLabelText('Prazo')).toHaveValue('');
    expect(screen.getAllByText('opcional')).toHaveLength(2);

    fireEvent.click(screen.getByRole('button', { name: 'Criar projeto' }));

    await waitFor(() => expect(createProject).toHaveBeenCalled());
    const enviado = createProject.mock.calls[0][0] as { dueDate?: string; startDate?: string };
    expect(enviado.dueDate).toBeUndefined();
    expect(enviado.startDate).toBeUndefined();
  });
});
