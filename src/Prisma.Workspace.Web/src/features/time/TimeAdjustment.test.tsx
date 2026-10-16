import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { theme } from '../../styles/theme';
import { api } from '../../services/api';
import { TimeTaskSearch } from './TimeAdjustment';

afterEach(()=>{cleanup();vi.restoreAllMocks();});
it('mantém a opção ativa visível ao avançar e voltar sem tirar o foco do campo',async()=>{
  const tasks=Array.from({length:6},(_,index)=>({id:`task-${index}`,number:index+1,title:`Tarefa ${index+1}`,boardName:'Quadro',assigned:true}));
  vi.spyOn(api,'getTimeAdjustmentTasks').mockResolvedValue(tasks);
  const changed=vi.fn();
  render(<ThemeProvider theme={theme}><TimeTaskSearch value="" onChange={changed}/></ThemeProvider>);
  const options=await screen.findAllByRole('option');
  const list=screen.getByRole('listbox');
  Object.defineProperty(list,'clientHeight',{configurable:true,value:80});
  vi.spyOn(list,'getBoundingClientRect').mockImplementation(()=>new DOMRect(0,100,200,80));
  options.forEach((option,index)=>vi.spyOn(option,'getBoundingClientRect')
    .mockImplementation(()=>new DOMRect(0,100+index*40-list.scrollTop,200,40)));
  const reveal=options.map(option=>{const scroll=vi.fn();Object.defineProperty(option,'scrollIntoView',{configurable:true,value:scroll});return scroll;});
  const field=screen.getByRole('combobox');act(()=>field.focus());
  for(let index=0;index<5;index++)fireEvent.keyDown(field,{key:'ArrowDown'});
  expect(field).toHaveAttribute('aria-activedescendant','time-task-task-5');
  expect(list.scrollTop).toBe(160);expect(field).toHaveFocus();
  expect(reveal[5]).toHaveBeenLastCalledWith({block:'nearest',inline:'nearest',behavior:'instant'});
  for(let index=0;index<5;index++)fireEvent.keyDown(field,{key:'ArrowUp'});
  expect(field).toHaveAttribute('aria-activedescendant','time-task-task-0');
  expect(list.scrollTop).toBe(0);expect(field).toHaveFocus();
  expect(reveal[0]).toHaveBeenLastCalledWith({block:'nearest',inline:'nearest',behavior:'instant'});
  fireEvent.keyDown(field,{key:'Enter'});
  expect(changed).toHaveBeenLastCalledWith('task-0');
});
it('busca por texto, seleciona com teclado e reabre a lista pelo toque',async()=>{
  vi.spyOn(api,'getTimeAdjustmentTasks').mockResolvedValue([{id:'task',number:123,title:'Minha tarefa',boardName:'Quadro',assigned:true}]);
  const changed=vi.fn();
  render(<ThemeProvider theme={theme}><TimeTaskSearch value="" onChange={changed}/></ThemeProvider>);
  const field=screen.getByRole('combobox',{name:'Buscar tarefa por título ou número'});
  fireEvent.change(field,{target:{value:'Minha'}});
  expect(await screen.findByRole('option')).toHaveTextContent('Minha tarefa');
  fireEvent.keyDown(field,{key:'Enter'});
  expect(changed).toHaveBeenLastCalledWith('task');
  expect(field).toHaveAttribute('aria-expanded','false');
  fireEvent.click(field);
  expect(await screen.findByRole('option')).toHaveTextContent('#123');
  expect(field).toHaveAttribute('aria-expanded','true');
  fireEvent.keyDown(field,{key:'Escape'});
  expect(field).toHaveAttribute('aria-expanded','false');
});
