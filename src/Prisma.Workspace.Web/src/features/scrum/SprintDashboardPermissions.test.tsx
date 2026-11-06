import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProjectSummary } from '../../pages/Projects';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { SprintDashboard } from './SprintDashboard';

const project:ProjectSummary={
  id:'project-1',key:'PRD',name:'Produto',methodology:2,
  boards:[],teams:[],members:[],
};

function renderDashboard(methodology = project.methodology){
  const displayProject = {...project,methodology};
  const client=new QueryClient({defaultOptions:{queries:{retry:false}}});
  return render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><MemoryRouter><SprintDashboard project={displayProject}/></MemoryRouter></ThemeProvider></QueryClientProvider>);
}

beforeEach(()=>{
  vi.spyOn(api,'getProjectSprints').mockResolvedValue([]);
  vi.spyOn(api,'getProjectBacklog').mockResolvedValue([]);
  vi.spyOn(api,'getTeams').mockResolvedValue([]);
});

afterEach(()=>{cleanup();vi.restoreAllMocks();});

describe('permissão para criar sprint',()=>{
  it('oculta Nova sprint quando Gerenciar sprint está negada',async()=>{
    vi.spyOn(api,'getProjectAccess').mockResolvedValue({canManageSprint:false});
    renderDashboard();
    expect(await screen.findByRole('heading',{name:'Sprints'})).toBeInTheDocument();
    expect(screen.queryByRole('button',{name:'Nova sprint'})).not.toBeInTheDocument();
  });

  it('exibe Nova sprint quando Gerenciar sprint está permitida',async()=>{
    vi.spyOn(api,'getProjectAccess').mockResolvedValue({canManageSprint:true});
    renderDashboard();
    expect(await screen.findByRole('button',{name:'Nova sprint'})).toBeInTheDocument();
  });
});

it.each([1,2,3,4])('mantém pontos da sprint para valor interno %s',async methodology=>{
 vi.spyOn(api,'getProjectAccess').mockResolvedValue({canManageSprint:true});
 vi.mocked(api.getProjectSprints).mockResolvedValue([{id:'sprint-1',teamId:'team-1',teamName:'Equipe',name:'Sprint igual',goal:'Entrega',startDate:'2026-10-01',endDate:'2026-10-30',status:2,itemCount:0,completedItemCount:0,storyPoints:8,completedStoryPoints:3,capacities:[]}]);
 renderDashboard(methodology);
 expect(await screen.findByText('Pontos entregues',{exact:true})).toBeInTheDocument();
});
