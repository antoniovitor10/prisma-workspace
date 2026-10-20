import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, expect, it } from 'vitest';
import { ContextBarProvider, ContextBarSlot } from '../layout/ContextBar';
import { theme } from '../styles/theme';
import { ProjectViewSwitcher } from './ProjectWorkspace';

const projectId = 'project-one';
let originalUrl: string;
let originalState: unknown;
beforeEach(() => { originalUrl = window.location.href; originalState = window.history.state; });
afterEach(() => { cleanup(); window.history.replaceState(originalState, '', originalUrl); });
function show(view: 'backlog' | 'boards') {
  window.history.replaceState({ idx: 0 }, '', `/projects/${projectId}/${view}?bl.y=10&outside=old`);
  render(<BrowserRouter><ThemeProvider theme={theme}><ContextBarProvider>
    <ContextBarSlot /><ProjectViewSwitcher projectId={projectId} />
  </ContextBarProvider></ThemeProvider></BrowserRouter>);
}
function latest(view: 'backlog' | 'boards') {
  // O browser já recebeu replaceState, mas a renderização do Router ainda usa a consulta anterior.
  window.history.replaceState(window.history.state, '', `/projects/${projectId}/${view}?bl.y=600&outside=kept`);
}
it.each(['backlog', 'boards'] as const)('leva a consulta atual do browser ao sair de %s antes do próximo render', view => {
  show(view); latest(view);
  fireEvent.click(screen.getByRole('link', { name: view === 'backlog' ? 'Quadro' : 'Backlog' }));
  expect(window.location.pathname).toBe(`/projects/${projectId}/${view === 'backlog' ? 'boards' : 'backlog'}`);
  expect(new URLSearchParams(window.location.search).get('bl.y')).toBe('600');
  expect(new URLSearchParams(window.location.search).get('outside')).toBe('kept');
});
it('atualiza também o destino nativo de um clique modificado sem navegar por conta própria', () => {
  show('backlog'); latest('backlog');
  const link = screen.getByRole('link', { name: 'Quadro' }) as HTMLAnchorElement;
  fireEvent.pointerDown(link, { button: 1 });
  expect(new URL(link.href).searchParams.get('bl.y')).toBe('600');
  expect(window.location.pathname).toBe(`/projects/${projectId}/backlog`);
});
it('preserva a consulta atual ao clicar na visão ativa e mantém a mesma entrada de histórico', () => {
  show('backlog'); latest('backlog');
  const before = window.history.length;
  fireEvent.click(screen.getByRole('link', { name: 'Backlog' }));
  expect(new URLSearchParams(window.location.search).get('bl.y')).toBe('600');
  expect(window.history.length).toBe(before);
});
it('cria uma entrada de retorno quando a URL mudou de visão antes do commit React', () => {
  show('backlog');
  window.history.pushState({ idx: 1 }, '', `/projects/${projectId}/boards?bl.y=600&outside=kept`);
  const before = window.history.length;
  fireEvent.click(screen.getByRole('link', { name: 'Backlog' }));
  expect(window.location.pathname).toBe(`/projects/${projectId}/backlog`);
  expect(new URLSearchParams(window.location.search).get('bl.y')).toBe('600');
  expect(window.history.length).toBe(before + 1);
});
it('mantém a navegação normal quando browser e Router já observam a mesma consulta', () => {
  show('backlog');
  fireEvent.click(screen.getByRole('link', { name: 'Quadro' }));
  expect(window.location.pathname).toBe(`/projects/${projectId}/boards`);
  expect(new URLSearchParams(window.location.search).get('bl.y')).toBe('10');
  expect(new URLSearchParams(window.location.search).get('outside')).toBe('old');
});
