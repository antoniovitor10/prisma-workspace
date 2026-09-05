import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';
import { AutomationManager } from './AutomationManager';

afterEach(()=>{cleanup();vi.restoreAllMocks();});

describe('AutomationManager',()=>{
  it('cria uma regra de movimentação com dados do quadro',async()=>{
    vi.spyOn(api,'getAutomations').mockResolvedValue([]);
    const create=vi.spyOn(api,'createAutomation').mockResolvedValue({});
    render(<ThemeProvider theme={theme}><AutomationManager
      boardId="board-1"
      stages={[{id:'stage-1',label:'A fazer'},{id:'stage-2',label:'Em andamento'}]}
      users={[{id:'user-1',label:'Pessoa'}]}
      tags={[{id:'tag-1',label:'Urgente'}]}
    /></ThemeProvider>);

    fireEvent.click(screen.getByRole('button',{name:'Automações'}));
    await screen.findByRole('dialog');
    fireEvent.change(screen.getByLabelText('Quando entrar em'),{target:{value:'stage-1'}});
    fireEvent.change(screen.getByLabelText('Destino'),{target:{value:'stage-2'}});
    fireEvent.click(screen.getByRole('button',{name:'Criar'}));

    await waitFor(()=>expect(create).toHaveBeenCalledWith('board-1',{
      triggerStageId:'stage-1',actionType:2,actionValue:'stage-2',isActive:true
    }));
  });
});
