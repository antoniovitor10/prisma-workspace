import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { theme } from '../styles/theme';
import { RichTaskDescriptionEditor } from './RichTaskDescriptionEditor';

afterEach(cleanup);

function renderEditor() {
  const onSave = vi.fn();
  const onOpenAttachments = vi.fn();
  render(
    <ThemeProvider theme={theme}>
      <RichTaskDescriptionEditor
        value="<p>Descrição inicial</p>"
        onSave={onSave}
        onOpenAttachments={onOpenAttachments}
      />
    </ThemeProvider>,
  );
  return { onSave, onOpenAttachments };
}

describe('RichTaskDescriptionEditor', () => {
  it('oferece os controles de formatação essenciais em português', () => {
    renderEditor();

    expect(screen.getByRole('textbox', { name: 'Descrição da tarefa' })).toHaveTextContent('Descrição inicial');
    for (const control of [
      'Negrito', 'Itálico', 'Sublinhado', 'Tachado', 'Realçar', 'Inserir link',
      'Lista numerada', 'Lista com marcadores', 'Checklist', 'Citação',
      'Bloco de código', 'Inserir imagem por endereço', 'Abrir anexos',
    ]) {
      expect(screen.getByRole('button', { name: control })).toBeInTheDocument();
    }
  });

  it('abre anexos e alterna o modo de tela cheia', () => {
    const { onOpenAttachments } = renderEditor();

    fireEvent.click(screen.getByRole('button', { name: 'Abrir anexos' }));
    expect(onOpenAttachments).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole('button', { name: 'Expandir editor' }));
    expect(screen.getByRole('button', { name: 'Sair da tela cheia' })).toBeInTheDocument();

    fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.getByRole('button', { name: 'Expandir editor' })).toBeInTheDocument();
  });
});
