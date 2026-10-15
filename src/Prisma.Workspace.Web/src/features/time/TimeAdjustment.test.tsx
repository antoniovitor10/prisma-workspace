import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { ThemeProvider } from 'styled-components';
import { theme } from '../../styles/theme';
import { api } from '../../services/api';
import { TimeTaskSearch } from './TimeAdjustment';

afterEach(()=>{cleanup();vi.restoreAllMocks();});
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
