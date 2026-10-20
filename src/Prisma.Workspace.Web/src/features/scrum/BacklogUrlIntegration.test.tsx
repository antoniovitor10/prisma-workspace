import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { ProjectSummary } from '../../pages/Projects';
import { BacklogPlanner } from './BacklogPlanner';
vi.mock('../../components/TaskDetailDrawer',()=>({TaskDetailDrawer:({item}:{item?:{title:string}})=><output aria-label="Detalhe aberto">{item?.title}</output>}));
const project:ProjectSummary={id:'project-1',key:'P',name:'Projeto',ownerId:'user-1',methodology:2,boards:[],teams:[],members:[]};
const items=[
 {id:'parent-1',title:'História principal',kind:3,boardId:'board-1',boardName:'Quadro',rank:1,priority:2},
 {id:'child-1',title:'Tarefa filha',kind:5,parentId:'parent-1',boardId:'board-1',boardName:'Quadro',rank:2,priority:1},
];
function Location(){return <output aria-label="Endereço">{useLocation().search}</output>;}
function show(search:string){
 const client=new QueryClient({defaultOptions:{queries:{retry:false}}});
 render(<MemoryRouter initialEntries={[`/projects/project-1/backlog${search}`]}><ThemeProvider theme={theme}><QueryClientProvider client={client}><Location/><BacklogPlanner project={project}/></QueryClientProvider></ThemeProvider></MemoryRouter>);
}
beforeEach(()=>{localStorage.clear();vi.spyOn(api,'getProjectBacklog').mockResolvedValue(items);vi.spyOn(api,'getProjectSprints').mockResolvedValue([]);});
afterEach(()=>{cleanup();vi.restoreAllMocks();localStorage.clear();});
it('restaura filtro e detalhe pela URL, preservando ancestrais do resultado',async()=>{
 show('?bl.q=Tarefa&bl.kind=5&bl.board=board-1&item=child-1');
 await screen.findByRole('textbox',{name:'Título de Tarefa filha'});
 expect(screen.getByRole('textbox',{name:'Pesquisar backlog'})).toHaveValue('Tarefa');
 expect(screen.getByRole('textbox',{name:'Título de História principal'})).toBeInTheDocument();
 expect(screen.getByLabelText('Detalhe aberto')).toHaveTextContent('Tarefa filha');
 fireEvent.change(screen.getByRole('textbox',{name:'Pesquisar backlog'}),{target:{value:'principal'}});
 await waitFor(()=>expect(screen.getByLabelText('Endereço')).toHaveTextContent('bl.q=principal'));
 expect(screen.getByLabelText('Endereço')).toHaveTextContent('item=child-1');
});
it('restaura colapso e seleção válida, sem selecionar identificador de outro projeto',async()=>{
 show('?bl.collapsed=parent-1&bl.selected=parent-1&bl.selected=foreign-id');
 const parent=await screen.findByRole('textbox',{name:'Título de História principal'});
 expect(parent).toBeInTheDocument();expect(screen.queryByRole('textbox',{name:'Título de Tarefa filha'})).not.toBeInTheDocument();
 expect(screen.getByRole('checkbox',{name:'Selecionar História principal'})).toBeChecked();
 expect(screen.getByText('1 item(ns) selecionado(s)')).toBeInTheDocument();
 fireEvent.click(screen.getByTestId('backlog-toggle-children'));
 expect(await screen.findByRole('textbox',{name:'Título de Tarefa filha'})).toBeInTheDocument();
 expect(screen.getByLabelText('Endereço')).not.toHaveTextContent('bl.collapsed');
});
