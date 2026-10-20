import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom';
import { afterEach, expect, it } from 'vitest';
import { readBacklogUrl, writeBacklogUrl } from './BacklogUrlState';
import { useBacklogUrlState } from './useBacklogUrlState';

afterEach(cleanup);
it('restaura filtros, seleção, colapso, sprint e rolagem sem alterar item ou contexto externo', () => {
 const params = new URLSearchParams('item=task-1&context=keep');
 const state = readBacklogUrl(params);
 state.filters = {search: 'Título com acento & espaços', kind: '5', priority:'2', boardId:'board-1', relation:'blocked', includeArchived:true};
 state.groupBy='priority';state.selectedIds=new Set(['task-2','task-1']);state.collapsedIds=new Set(['parent-1']);state.sprintId='sprint-2';state.scrollY=732;
 const encoded=writeBacklogUrl(params,state);
 expect(readBacklogUrl(encoded)).toEqual(state);
 expect(encoded.get('item')).toBe('task-1');expect(encoded.get('context')).toBe('keep');
 expect(encoded.toString()).not.toContain('undefined');
});
it('usa padrões seguros para enum inválido, posição negativa e identificador malformado',()=>{
 const state=readBacklogUrl(new URLSearchParams('bl.kind=500&bl.priority=-1&bl.relation=bad&bl.group=bad&bl.y=-1&bl.selected=bad%20id&bl.selected=ok-id&bl.collapsed=%3Cscript%3E'));
 expect(state.filters.kind).toBe('all');expect(state.filters.priority).toBe('all');expect(state.filters.relation).toBe('all');expect(state.groupBy).toBe('none');expect(state.scrollY).toBe(0);
 expect([...state.selectedIds]).toEqual(['ok-id']);expect(state.collapsedIds.size).toBe(0);
});
it('omite padrões e substitui estado anterior sem duplicar chaves nem manter dados removidos',()=>{
 const original=new URLSearchParams('item=task-1&bl.q=old&bl.selected=old&bl.y=50');
 const next=writeBacklogUrl(original,readBacklogUrl(new URLSearchParams()));
 expect(next.toString()).toBe('item=task-1');expect(original.get('bl.q')).toBe('old');
});
it.each(['NaN','Infinity','-Infinity'])('não restaura rolagem não finita: %s',value=>{
 expect(readBacklogUrl(new URLSearchParams(`bl.y=${value}`)).scrollY).toBe(0);
});
function Harness(){
 const {state,update,updateParams}=useBacklogUrlState();const location=useLocation();const navigate=useNavigate();
 return <>
 <output aria-label="URL">{location.search}</output><output aria-label="Grupo">{state.groupBy}</output><output aria-label="Pesquisa">{state.filters.search}</output>
 <button onClick={()=>{update('filters',current=>({...current,search:'nova'}));update('groupBy','priority');updateParams(params=>{params.set('item','task-new');return params;});}}>Dois campos</button>
 <button onClick={()=>navigate('?bl.q=anterior&bl.group=kind')}>Outra entrada</button><button onClick={()=>navigate(-1)}>Voltar</button><button onClick={()=>navigate(1)}>Avançar</button>
 </>;
}
it('compõe duas alterações no mesmo evento e acompanha voltar e avançar pela URL',()=>{
 render(<MemoryRouter initialEntries={['/?item=task-1']}><Harness/></MemoryRouter>);
 fireEvent.click(screen.getByRole('button',{name:'Dois campos'}));
 expect(screen.getByLabelText('Pesquisa')).toHaveTextContent('nova');expect(screen.getByLabelText('Grupo')).toHaveTextContent('priority');
 expect(screen.getByLabelText('URL')).toHaveTextContent('item=task-new');
 fireEvent.click(screen.getByRole('button',{name:'Outra entrada'}));
 expect(screen.getByLabelText('Pesquisa')).toHaveTextContent('anterior');expect(screen.getByLabelText('Grupo')).toHaveTextContent('kind');
 fireEvent.click(screen.getByRole('button',{name:'Voltar'}));
 expect(screen.getByLabelText('Pesquisa')).toHaveTextContent('nova');expect(screen.getByLabelText('Grupo')).toHaveTextContent('priority');
 fireEvent.click(screen.getByRole('button',{name:'Avançar'}));expect(screen.getByLabelText('Pesquisa')).toHaveTextContent('anterior');
});
