import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { useState } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PageHelpButton } from '../features/help/PageHelpButton';
import { HelpButton } from '../features/help/HelpPanel';
import { theme } from '../styles/theme';
import { HelpCenter } from './HelpCenter';

afterEach(() => { cleanup(); vi.useRealTimers(); });

const renderAt = (path: string) => render(
  <ThemeProvider theme={theme}>
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/ajuda" element={<HelpCenter />} />
        <Route path="/ajuda/:subject/:slug" element={<HelpCenter />} />
      </Routes>
    </MemoryRouter>
  </ThemeProvider>,
);

describe('central de ajuda', () => {
  it('lista os artigos por assunto (CA-1)', () => {
    renderAt('/ajuda');
    expect(screen.getByRole('heading', { level: 1, name: 'Central de ajuda' })).toBeInTheDocument();
    const list = screen.getByRole('navigation', { name: 'Artigos por assunto' });
    for (const subject of ['Organização, projetos e equipes', 'Quadro, backlog e sprints', 'Integrações', 'Repositórios e Git']) {
      expect(within(list).getByRole('heading', { name: subject })).toBeInTheDocument();
    }
    expect(within(list).getByRole('link', { name: 'Tokens de agente, humano e sistema' })).toHaveAttribute('href', '/ajuda/integracoes/tokens');
  });

  it('busca por palavras e anuncia o resultado (CA-1)', () => {
    renderAt('/ajuda');
    fireEvent.change(screen.getByRole('searchbox', { name: 'Buscar na ajuda' }), { target: { value: 'installation id' } });
    expect(screen.getByRole('status')).toHaveTextContent(/artigo\(s\) encontrado\(s\)/);
    const results = screen.getByRole('list', { name: 'Resultados da busca' });
    expect(within(results).getAllByRole('link')[0]).toHaveTextContent('Conectar um repositório do GitHub');
    fireEvent.change(screen.getByRole('searchbox', { name: 'Buscar na ajuda' }), { target: { value: 'palavrainexistente' } });
    expect(screen.getByRole('status')).toHaveTextContent('Nenhum artigo encontrado.');
  });

  it('mostra título, conteúdo e "Atualizado em" do artigo (CA-1)', () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-12-01T12:00:00'));
    renderAt('/ajuda/integracoes/tokens');
    const article = screen.getByRole('article', { name: 'Tokens de agente, humano e sistema' });
    expect(within(article).getByText(/Atualizado em/)).toHaveTextContent('Atualizado em 27/11/2026');
    expect(within(article).getByRole('heading', { name: 'Emitir um token' })).toHaveAttribute('id', 'emitir-um-token');
    expect(within(article).queryByRole('note')).not.toBeInTheDocument();
  });

  it('avisa quando a revisão do artigo venceu (CA-9)', () => {
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2028-01-01T12:00:00'));
    renderAt('/ajuda/integracoes/tokens');
    expect(screen.getByRole('note')).toHaveTextContent('Este artigo pode estar desatualizado');
  });

  it('leva o foco à seção da âncora (CA-3, CA-11)', async () => {
    renderAt('/ajuda/integracoes/integracoes#pausada-recusa-token');
    const heading = screen.getByRole('heading', { name: 'Por que a integração pausada recusa todo token' });
    await waitFor(() => expect(heading).toHaveFocus());
  });

  it('informa quando o artigo não existe', () => {
    renderAt('/ajuda/integracoes/nao-existe');
    expect(screen.getByRole('heading', { name: 'Artigo não encontrado' })).toBeInTheDocument();
  });
});

function FakeSettings() {
  const [draft, setDraft] = useState('');
  return <><PageHelpButton /><label>Rascunho<input value={draft} onChange={(event) => setDraft(event.target.value)} /></label></>;
}

describe('botão "?" da página', () => {
  const renderPage = (path: string) => render(
    <ThemeProvider theme={theme}>
      <MemoryRouter initialEntries={[path]}>
        <Routes><Route path="*" element={<FakeSettings />} /></Routes>
      </MemoryRouter>
    </ThemeProvider>,
  );

  it('abre o artigo da página na seção certa e fecha sem perder o que foi digitado (CA-3)', async () => {
    renderPage('/projects/abc/settings?secao=metodo');
    fireEvent.change(screen.getByLabelText('Rascunho'), { target: { value: 'meu texto' } });
    fireEvent.click(screen.getByRole('button', { name: 'Ajuda desta página' }));
    const dialog = await screen.findByRole('dialog', { name: 'Integrações do projeto' });
    const section = within(dialog).getByRole('heading', { name: 'Ativar ou pausar' });
    await waitFor(() => expect(section).toHaveFocus());
    expect(within(dialog).getByRole('link', { name: 'Abrir na central de ajuda' })).toHaveAttribute('href', '/ajuda/integracoes/integracoes#ativar-ou-pausar');
    fireEvent.keyDown(dialog, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByLabelText('Rascunho')).toHaveValue('meu texto');
    expect(screen.getByRole('button', { name: 'Ajuda desta página' })).toHaveFocus();
  });

  it('sem artigo da página, leva à central de ajuda', () => {
    renderPage('/reports');
    expect(screen.getByRole('link', { name: 'Central de ajuda' })).toHaveAttribute('href', '/ajuda');
  });

  it('o "?" de seção abre o artigo do alvo registrado (CA-8)', async () => {
    render(<ThemeProvider theme={theme}><MemoryRouter><HelpButton target="integrationRepositories" label="Ajuda sobre repositórios GitHub" /></MemoryRouter></ThemeProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Ajuda sobre repositórios GitHub' }));
    const dialog = await screen.findByRole('dialog', { name: 'Conectar um repositório do GitHub' });
    await waitFor(() => expect(within(dialog).getByRole('heading', { name: 'Conectar o repositório' })).toHaveFocus());
  });
});
