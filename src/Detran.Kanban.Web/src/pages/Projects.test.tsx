import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { Projects, type ProjectSummary } from './Projects';

afterEach(() => vi.restoreAllMocks());

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
});
