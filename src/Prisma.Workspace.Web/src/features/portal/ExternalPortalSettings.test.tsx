import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import type { ProjectSummary } from '../../pages/Projects';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { ExternalPortal } from '../../types/portal';
import { ExternalPortalSettings } from './ExternalPortalSettings';

const project:ProjectSummary={
  id:'project-1',key:'TESTE',name:'Projeto Teste',
  boards:[{id:'board-1',name:'Quadro principal'}],teams:[],members:[],
};

const savedPortal:ExternalPortal={
  id:'portal-1',projectId:project.id,boardId:'board-1',projectName:project.name,
  publicSlug:'teste',isEnabled:true,requiresAuthentication:false,accessModes:1,
  publicPath:'/portal/teste',forms:[],
};

function renderSettings(){
  const client=new QueryClient({defaultOptions:{queries:{retry:false},mutations:{retry:false}}});
  return render(<QueryClientProvider client={client}><ThemeProvider theme={theme}><ExternalPortalSettings project={project}/></ThemeProvider></QueryClientProvider>);
}

afterEach(()=>{cleanup();vi.restoreAllMocks();});

describe('ExternalPortalSettings',()=>{
  it('abre somente o endereço persistido e avisa quando há alteração não salva',async()=>{
    vi.spyOn(api,'getExternalPortal').mockResolvedValue(savedPortal);
    vi.spyOn(api,'getExternalForms').mockResolvedValue([]);
    vi.spyOn(api,'getOrganizationMembers').mockResolvedValue([]);
    vi.spyOn(api,'getProjectWorkflow').mockResolvedValue({stages:[]});

    renderSettings();

    const openLink=await screen.findByRole('link',{name:'Abrir portal público'});
    expect(openLink).toHaveAttribute('href',`${window.location.origin}/portal/teste`);
    expect(screen.getByText('Publicado')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Endereço público'),{target:{value:'novo-endereco'}});

    expect(screen.getByRole('link',{name:'Abrir portal público'})).toHaveAttribute('href',`${window.location.origin}/portal/teste`);
    expect(screen.getByText('Alterações não salvas')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Salve as alterações');
  });

  it('não oferece um link inexistente antes da primeira publicação ser salva',async()=>{
    vi.spyOn(api,'getExternalPortal').mockResolvedValue(null);
    vi.spyOn(api,'getOrganizationMembers').mockResolvedValue([]);
    vi.spyOn(api,'getProjectWorkflow').mockResolvedValue({stages:[]});

    renderSettings();
    await waitFor(()=>expect(api.getExternalPortal).toHaveBeenCalledWith(project.id));
    fireEvent.click(screen.getByLabelText('Portal habilitado'));

    expect(screen.queryByRole('link',{name:'Abrir portal público'})).not.toBeInTheDocument();
    expect(screen.getByText('Alterações não salvas')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Salve as alterações');
  });
});
