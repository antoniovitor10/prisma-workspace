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

function renderDashboard(){
  const client=new QueryClient({defaultOptions:{queries:{retry:false}}});
  return render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><MemoryRouter><SprintDashboard project={project}/></MemoryRouter></ThemeProvider></QueryClientProvider>);
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
