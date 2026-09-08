import { lazy, Suspense, useState, useEffect } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import styled from 'styled-components';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AppThemeProvider } from './styles/ThemeMode';
import { GlobalStyle } from './styles/global';
import { Auth } from './pages/Auth';
import { Setup } from './pages/Setup';
import { AppShell } from './layout/AppShell';
import { api } from './services/api';
import { previewMode } from './preview';
import { OrganizationProvider } from './features/organizations/OrganizationContext';
import { useOrganization } from './features/organizations/OrganizationState';

const Kanban = lazy(() => import('./pages/Kanban').then((module) => ({ default: module.Kanban })));
const MinhasTarefas = lazy(() => import('./pages/MinhasTarefas').then((module) => ({ default: module.MinhasTarefas })));
const Company = lazy(() => import('./pages/Company').then((module) => ({ default: module.Company })));
const Teams = lazy(() => import('./pages/Teams').then((module) => ({ default: module.Teams })));
const Projects = lazy(() => import('./pages/Projects').then((module) => ({ default: module.Projects })));
const Home = lazy(() => import('./pages/Home').then((module) => ({ default: module.Home })));
const Requests = lazy(() => import('./pages/Requests').then((module) => ({ default: module.Requests })));
const Reports = lazy(() => import('./pages/Reports').then((module) => ({ default: module.Reports })));
const ProjectWorkspace = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectWorkspace })));
const ProjectBacklog = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectBacklog })));
const ProjectItems = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectItems })));
const ProjectSprints = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectSprints })));
const ProjectBoards = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectBoards })));
const ProjectReports = lazy(() => import('./pages/ProjectWorkspace').then((module) => ({ default: module.ProjectReports })));
const ProjectSettings = lazy(() => import('./pages/ProjectSettings').then((module) => ({ default: module.ProjectSettings })));
const ProjectWiki = lazy(() => import('./pages/ProjectWiki').then((module) => ({ default: module.ProjectWiki })));
const OrganizationSettings = lazy(() => import('./pages/OrganizationSettings').then((module) => ({ default: module.OrganizationSettings })));
const PublicPortal = lazy(() => import('./pages/PublicPortal').then((module) => ({ default: module.PublicPortal })));

function OrganizationStart() {
  const { current } = useOrganization();
  return <Navigate to={current.role === 8 ? '/requests' : '/home'} replace />;
}

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 15_000, refetchOnWindowFocus: false } },
});

const RouteLoading = styled.div`
  display: grid;
  min-height: 240px;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
`;

const routerBasename = (import.meta.env.BASE_URL || '/').replace(/\/$/, '') || undefined;

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(previewMode || !!api.getToken());
  const [isSetupRoute, setIsSetupRoute] = useState(
    window.location.pathname === '/setup' || window.location.pathname === '/setup/',
  );
  const isPublicPortal = window.location.pathname.startsWith('/portal/');

  const leaveSetup = (completed: boolean) => {
    window.history.replaceState(
      completed ? { setupCompleted: true } : {},
      '',
      '/login',
    );
    setIsSetupRoute(false);
  };

  // Guarda o convite mesmo antes do login: quem é novo cai na tela de acesso,
  // e o token precisa sobreviver ao cadastro/login para ser aceito depois.
  useEffect(() => {
    const invite = new URLSearchParams(window.location.search).get('invite');
    if (invite && !isPublicPortal) localStorage.setItem('pendingInvite', invite);
  }, [isPublicPortal]);

  useEffect(() => {
    const handleAuthChange = () => {
      setIsAuthenticated(previewMode || !!api.getToken());
    };

    window.addEventListener('auth-change', handleAuthChange);
    return () => {
      window.removeEventListener('auth-change', handleAuthChange);
    };
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <AppThemeProvider>
        <GlobalStyle />
        {isSetupRoute ? (
          <BrowserRouter basename={routerBasename}><Routes><Route path="/setup" element={<Setup onExit={leaveSetup} />} /><Route path="*" element={<Navigate to="/setup" replace />} /></Routes></BrowserRouter>
        ) : isPublicPortal ? (
          <BrowserRouter basename={routerBasename}><Suspense fallback={<RouteLoading>Carregando portal...</RouteLoading>}><Routes><Route path="/portal/:slug/*" element={<PublicPortal />} /></Routes></Suspense></BrowserRouter>
        ) : isAuthenticated ? (
          <OrganizationProvider><BrowserRouter basename={routerBasename}>
            <Suspense fallback={<RouteLoading>Carregando visão...</RouteLoading>}>
              <Routes>
                <Route element={<AppShell />}>
                  <Route path="/" element={<OrganizationStart />} />
                  <Route path="/home" element={<Home />} />
                  <Route path="/projects" element={<Projects />} />
                  <Route path="/projects/:projectId" element={<ProjectWorkspace />}>
                    <Route index element={<Navigate to="backlog" replace />} />
                    <Route path="items" element={<ProjectItems />} />
                    <Route path="backlog" element={<ProjectBacklog />} />
                    <Route path="sprints" element={<ProjectSprints />} />
                    <Route path="boards" element={<ProjectBoards />} />
                    <Route path="reports" element={<ProjectReports />} />
                    <Route path="wiki" element={<ProjectWiki />} />
                    <Route path="wiki/:pageId" element={<ProjectWiki />} />
                    <Route path="settings" element={<ProjectSettings />} />
                  </Route>
                  <Route path="/boards/:boardId" element={<Kanban />} />
                  <Route path="/company" element={<Company />} />
                  <Route path="/teams" element={<Teams />} />
                  <Route path="/requests" element={<Requests />} />
                  <Route path="/me/tasks" element={<MinhasTarefas />} />
                  <Route path="/reports" element={<Reports />} />
                  <Route path="/settings" element={<OrganizationSettings />} />
                  <Route path="*" element={<Navigate to="/projects" replace />} />
                </Route>
              </Routes>
            </Suspense>
          </BrowserRouter></OrganizationProvider>
        ) : (
          <Auth />
        )}
      </AppThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
