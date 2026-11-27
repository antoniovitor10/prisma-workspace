import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it } from 'vitest';
import { theme } from '../../styles/theme';
import { parseFrontmatter } from './frontmatter';
import { headingAnchors, parseInline, parseMarkdown, safeHref, slugify } from './markdown';
import { MarkdownView } from './MarkdownView';

afterEach(cleanup);

describe('leitor de Markdown da ajuda', () => {
  it('gera âncoras sem acento, respeita âncora explícita e numera repetidas', () => {
    const blocks = parseMarkdown('## Configurações do projeto\n\n## Tokens {#emitir}\n\n## Configurações do projeto');
    expect(headingAnchors(blocks)).toEqual(['configuracoes-do-projeto', 'emitir', 'configuracoes-do-projeto-2']);
    expect(slugify('  Por que a integração é pausada?  ')).toBe('por-que-a-integracao-e-pausada');
  });

  it('reconhece listas, citação, tabela e bloco de código', () => {
    const blocks = parseMarkdown([
      '1. primeiro', '2. segundo', '', '- a', '  continua', '', '> **Atenção**: nota', '',
      '| Campo | Valor |', '|---|---|', '| App ID | número |', '', '```', 'npm ci', '```', '', 'Fim.',
    ].join('\n'));
    expect(blocks.map((block) => block.kind)).toEqual(['list', 'list', 'quote', 'table', 'code', 'paragraph']);
    expect(blocks[1]).toMatchObject({ kind: 'list', ordered: false });
    expect(blocks[4]).toMatchObject({ kind: 'code', text: 'npm ci' });
  });

  it('separa negrito, itálico, código, link e imagem em linha', () => {
    expect(parseInline('**forte** e _leve_ com `pmt_` [link](/ajuda) ![tela](./imagens/a.png)').map((node) => node.kind))
      .toEqual(['strong', 'text', 'em', 'text', 'code', 'text', 'link', 'text', 'image']);
    expect(parseInline('nome_com_sublinhado').map((node) => node.kind)).toEqual(['text']);
  });

  it('aceita só destinos seguros nos links', () => {
    expect(safeHref('javascript:alert(1)')).toBeUndefined();
    expect(safeHref('data:text/html,oi')).toBeUndefined();
    expect(safeHref('//exemplo.invalid')).toBeUndefined();
    expect(safeHref('http://exemplo.invalid')).toBeUndefined();
    expect(safeHref('/ajuda/integracoes/tokens#emitir-um-token')).toEqual({ kind: 'internal', to: '/ajuda/integracoes/tokens#emitir-um-token' });
    expect(safeHref('#papeis')).toEqual({ kind: 'anchor', anchor: 'papeis' });
    expect(safeHref('https://docs.github.com')).toEqual({ kind: 'external', href: 'https://docs.github.com' });
  });

  it('desenha HTML do texto como texto, sem criar elementos', () => {
    const blocks = parseMarkdown('<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>\n\n[perigo](javascript:alert(1)) e [ok](/ajuda)\n\n![sem arquivo](https://exemplo.invalid/a.png)');
    const { container } = render(
      <ThemeProvider theme={theme}><MemoryRouter><MarkdownView blocks={blocks} resolveImage={() => undefined} /></MemoryRouter></ThemeProvider>,
    );
    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByText('<script>alert(1)</script>')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'perigo' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'ok' })).toHaveAttribute('href', '/ajuda');
    expect(screen.getByText('[imagem indisponível: sem arquivo]')).toBeInTheDocument();
  });

  it('dá id e foco programático aos títulos', () => {
    render(<ThemeProvider theme={theme}><MemoryRouter><MarkdownView blocks={parseMarkdown('## Emitir um token')} headingShift={1} idPrefix="p-" resolveImage={() => undefined} /></MemoryRouter></ThemeProvider>);
    const heading = screen.getByRole('heading', { level: 3, name: 'Emitir um token' });
    expect(heading).toHaveAttribute('id', 'p-emitir-um-token');
    expect(heading).toHaveAttribute('tabindex', '-1');
  });
});

describe('cabeçalho dos artigos', () => {
  it('lê valores, aspas e listas', () => {
    const { data, body } = parseFrontmatter('---\ntitulo: "Título: com dois pontos"\npaginas:\n  - /home#a\n  - /teams\n---\n\nCorpo');
    expect(data).toEqual({ titulo: 'Título: com dois pontos', paginas: ['/home#a', '/teams'] });
    expect(body.trim()).toBe('Corpo');
  });

  it('recusa cabeçalho ausente ou malformado', () => {
    expect(() => parseFrontmatter('# Sem cabeçalho')).toThrow(/cabeçalho/);
    expect(() => parseFrontmatter('---\nisto não é chave\n---\n')).toThrow(/inválida/);
  });
});
