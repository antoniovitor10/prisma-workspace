import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OrganizationStateContext, type OrganizationSummary } from '../features/organizations/OrganizationState';
import { api } from '../services/api';
import { theme } from '../styles/theme';
import { ProjectSettings } from './ProjectSettings';
import { projectMemberSuccessMessage, translateProjectHistoryKind } from './projectSettingsLabels';
import type { ProjectSummary } from './Projects';

vi.mock('../features/workflow/ProjectWorkflowSettings', () => ({
  ProjectWorkflowSettings: () => <section><h2>Fluxo do projeto</h2></section>,
}));
vi.mock('../features/portal/ExternalPortalSettings', () => ({
  ExternalPortalSettings: () => <section><h2>Portal do cidadão</h2></section>,
}));

const project: ProjectSummary = {
  id: 'project-1',
  key: 'KANBAN',
  name: 'Projeto Kanban',
  ownerId: 'user-1',
  methodology: 1,
  boards: [],
  teams: [],
  members: [{ userId: 'user-1', role: 5 }, { userId: 'user-2', role: 2 }, { userId: 'user-3', role: 1 }],
};

const organization: OrganizationSummary = {
  id: 'organization-1',
  name: 'Organização de teste',
  slug: 'organizacao-de-teste',
  isActive: true,
  locale: 'pt-BR',
  timeZone: 'America/Sao_Paulo',
  weekStartDay: 1,
  role: 1,
  isAdministrator: true,
};

function ProjectContext() {
  return <Outlet context={{ project }} />;
}

function renderSettings(rota = '/projects/project-1/settings') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <OrganizationStateContext.Provider value={{
          organizations: [organization],
          current: organization,
          switchOrganization: vi.fn(),
        }}>
          <MemoryRouter initialEntries={[rota]}>
            <Routes>
              <Route path="/projects/:projectId" element={<ProjectContext />}>
                <Route path="settings" element={<ProjectSettings />} />
              </Route>
            </Routes>
          </MemoryRouter>
        </OrganizationStateContext.Provider>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.spyOn(api, 'getOrganizationMembers').mockResolvedValue([
    { userId:'user-1', name:'Responsável Teste', role:6, isActive:true },
    { userId:'user-2', name:'Pessoa Teste', role:6, isActive:true },
    { userId:'user-3', name:'Gestora Teste', role:2, isActive:true },
  ]);
  vi.spyOn(api, 'getTags').mockResolvedValue([]);
  vi.spyOn(api, 'getTeams').mockResolvedValue([]);
  vi.spyOn(api, 'getProjectHistory').mockResolvedValue([]);
  vi.spyOn(api, 'saveProjectCustomField').mockRejectedValue(new Error('Sem permissão para alterar campos.'));
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe('configurações do projeto por categoria', () => {
  it('traduz eventos conhecidos e mantém fallback legível sem alterar o valor armazenado', () => {
    expect(translateProjectHistoryKind('archived')).toBe('Projeto arquivado');
    expect(translateProjectHistoryKind('member_added')).toBe('Membro adicionado');
    expect(translateProjectHistoryKind('member_updated')).toBe('Membro atualizado');
    expect(translateProjectHistoryKind('custom_field_saved')).toBe('Campo personalizado salvo');
    expect(translateProjectHistoryKind('custom_field_disabled')).toBe('Campo personalizado desativado');
    expect(translateProjectHistoryKind('new_unknown_event')).toBe('New Unknown Event');
    expect(translateProjectHistoryKind('')).toBe('Alteração administrativa');
  });

  it('diferencia a confirmação de inclusão da atualização de papel', () => {
    expect(projectMemberSuccessMessage(false)).toBe('Membro adicionado ao projeto.');
    expect(projectMemberSuccessMessage(true)).toBe('Papel do membro atualizado.');
  });

  it('mostra erro de ação e limpa o alerta após nova tentativa bem-sucedida', async () => {
    renderSettings('/projects/project-1/settings?secao=classificacao');
    fireEvent.change(screen.getByPlaceholderText('Nome do campo'), { target: { value: 'Código interno' } });
    fireEvent.click(screen.getByRole('button', { name: 'Criar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Sem permissão para alterar campos.');

    vi.mocked(api.saveProjectCustomField).mockResolvedValueOnce({ id: 'field-1' } as never);
    fireEvent.change(screen.getByPlaceholderText('Nome do campo'), { target: { value: 'Código interno' } });
    fireEvent.click(screen.getByRole('button', { name: 'Criar' }));
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('abre em Geral e mostra apenas as seções dessa categoria', () => {
    renderSettings();

    expect(screen.getByRole('navigation', { name: 'Categorias de configuração' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dados do projeto' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ciclo de vida' })).toBeInTheDocument();
    // O que fazia a tela virar um rolo unico: todas as secoes juntas, sempre.
    expect(screen.queryByRole('heading', { name: 'Membros e papéis' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Campos personalizados' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Histórico do projeto' })).not.toBeInTheDocument();
  });

  it('troca de categoria ao escolher no menu', () => {
    renderSettings();

    fireEvent.click(screen.getByRole('button', { name: 'Pessoas e equipes' }));

    expect(screen.getByRole('heading', { name: 'Membros e papéis' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Equipes' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Dados do projeto' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pessoas e equipes' })).toHaveAttribute('aria-current', 'true');
  });

  it('não oferece papel ineficaz ao responsável e persiste o papel de membro comum', async () => {
    const setProjectMember=vi.spyOn(api,'setProjectMember').mockResolvedValue(undefined);
    renderSettings('/projects/project-1/settings?secao=pessoas');

    expect(await screen.findByText('Responsável Teste')).toBeInTheDocument();
    expect(screen.queryByRole('combobox',{name:'Papel de Responsável Teste'})).not.toBeInTheDocument();
    expect(screen.getByTitle('O responsável principal sempre administra o projeto')).toHaveTextContent('Administrador');
    expect(screen.queryByRole('combobox',{name:'Papel de Gestora Teste'})).not.toBeInTheDocument();
    expect(screen.getByTitle('O perfil Gestor da organização concede administração do projeto')).toHaveTextContent('pela organização');

    fireEvent.change(screen.getByRole('combobox',{name:'Papel de Pessoa Teste'}),{target:{value:'1'}});

    await waitFor(()=>expect(setProjectMember).toHaveBeenCalledWith('project-1','user-2',1));
    expect(await screen.findByRole('status')).toHaveTextContent('Papel do membro atualizado.');
  });

  it('respeita a categoria vinda na URL', () => {
    renderSettings('/projects/project-1/settings?secao=historico');

    expect(screen.getByRole('heading', { name: 'Histórico do projeto' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Dados do projeto' })).not.toBeInTheDocument();
  });

  it('cai em Geral quando a URL traz categoria desconhecida', () => {
    renderSettings('/projects/project-1/settings?secao=inexistente');

    expect(screen.getByRole('heading', { name: 'Dados do projeto' })).toBeInTheDocument();
  });

  it('mantém fluxo de trabalho e portal externo em categorias próprias', () => {
    renderSettings('/projects/project-1/settings?secao=fluxo');
    expect(screen.getByRole('heading', { name: 'Colunas dos quadros' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Portal do cidadão' })).not.toBeInTheDocument();

    cleanup();

    renderSettings('/projects/project-1/settings?secao=portal');
    expect(screen.getByRole('heading', { name: 'Portal do cidadão' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Fluxo do projeto' })).not.toBeInTheDocument();
  });
});
