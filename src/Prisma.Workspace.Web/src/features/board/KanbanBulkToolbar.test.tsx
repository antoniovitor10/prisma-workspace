import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { theme } from '../../styles/theme';
import { BULK_ACTION } from './KanbanBulkActions';
import { KanbanBulkToolbar } from './KanbanBulkToolbar';

const options = [{id:'one',label:'Opção 1'}];

afterEach(cleanup);

function renderToolbar(onApply = vi.fn().mockResolvedValue(undefined)) {
  const onToggleVisible = vi.fn();
  render(<ThemeProvider theme={theme}><KanbanBulkToolbar
    selectedCount={2}
    visibleCount={3}
    allVisibleSelected={false}
    stages={options}
    users={options}
    tags={options}
    sprints={options}
    busy={false}
    feedback={null}
    onToggleVisible={onToggleVisible}
    onClear={vi.fn()}
    onApply={onApply}
  /></ThemeProvider>);
  return {onApply,onToggleVisible};
}

describe('KanbanBulkToolbar',()=>{
  it('solicita a seleção de todas as tarefas visíveis',()=>{
    const {onToggleVisible}=renderToolbar();
    fireEvent.click(screen.getByRole('checkbox',{name:'Selecionar tarefas visíveis'}));
    expect(onToggleVisible).toHaveBeenCalledOnce();
  });

  it('envia ação, destino e seleção ao quadro',async()=>{
    const {onApply}=renderToolbar();
    fireEvent.change(screen.getByLabelText('Destino da ação'),{target:{value:'one'}});
    fireEvent.click(screen.getByRole('button',{name:'Aplicar'}));
    await waitFor(()=>expect(onApply).toHaveBeenCalledWith(BULK_ACTION.move,'one',undefined));
  });

  it('envia prioridade sem destino textual',async()=>{
    const {onApply}=renderToolbar();
    fireEvent.change(screen.getByLabelText('Ação em massa'),{target:{value:String(BULK_ACTION.priority)}});
    fireEvent.change(screen.getByLabelText('Prioridade'),{target:{value:'3'}});
    fireEvent.click(screen.getByRole('button',{name:'Aplicar'}));
    await waitFor(()=>expect(onApply).toHaveBeenCalledWith(BULK_ACTION.priority,undefined,3));
  });
});
