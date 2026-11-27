import { cleanup, render, screen, within } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it } from 'vitest';
import { darkTheme, theme } from '../../styles/theme';
import { splitFrontmatter } from './frontmatter';
import { buildMethodDocumentTabs } from './labels';
import { MarkdownView } from './MarkdownView';

afterEach(cleanup);

const renderView = (content: string, current = theme) =>
  render(<ThemeProvider theme={current}><MarkdownView content={content} aria-label="Documento" /></ThemeProvider>);

describe('MarkdownView', () => {
  it('não executa HTML cru nem ativa link javascript:', () => {
    const { container } = renderView([
      '# Título',
      '',
      '<script>window.__xss = true</script>',
      '',
      '<img src="x" onerror="window.__xss = true">',
      '',
      'Texto com <b onclick="window.__xss = true">negrito cru</b>.',
      '',
      '[clique aqui](javascript:window.__xss=true)',
      '',
      '[seguro](https://example.test/docs)',
    ].join('\n'));

    expect(container.querySelector('script')).toBeNull();
    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('b')).toBeNull();
    expect(container.querySelector('[onerror],[onclick]')).toBeNull();
    expect(container.innerHTML).not.toContain('javascript:');
    expect((window as Window & { __xss?: boolean }).__xss).toBeUndefined();
    expect(screen.getByText('clique aqui').closest('a')).toBeNull();
    expect(screen.queryByRole('link', { name: 'clique aqui' })).not.toBeInTheDocument();
    const safe = screen.getByRole('link', { name: 'seguro' });
    expect(safe).toHaveAttribute('href', 'https://example.test/docs');
    expect(safe).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });

  it('formata títulos, listas de tarefas e tabelas GFM nos temas claro e escuro', () => {
    for (const current of [theme, darkTheme]) {
      renderView([
        '## Critérios de aceite',
        '',
        '- [x] feito',
        '- [ ] pendente',
        '',
        '| CA | Tipo |',
        '|---|---|',
        '| CA-1 | interacao |',
      ].join('\n'), current);

      expect(screen.getByRole('heading', { level: 2, name: 'Critérios de aceite' })).toBeInTheDocument();
      const table = screen.getByRole('table');
      expect(within(table).getAllByRole('columnheader').map(cell => cell.textContent)).toEqual(['CA', 'Tipo']);
      expect(within(table).getByRole('cell', { name: 'CA-1' })).toBeInTheDocument();
      const boxes = screen.getAllByRole('checkbox');
      expect(boxes).toHaveLength(2);
      expect(boxes[0]).toBeChecked();
      expect(boxes[0]).toBeDisabled();
      cleanup();
    }
  });
});

describe('splitFrontmatter', () => {
  it('separa o cabeçalho sem mudar um byte e lê os pares para a faixa', () => {
    const content = '﻿---\nid: HU-0001\ntitulo: "Guardar a HU"\nstatus: planejada\ncobre: [CA-1, CA-2]\nskills:\n  - a\n  - b\n---\n\n# HU\r\ncorpo\n';
    const split = splitFrontmatter(content);
    expect(split.head + split.body).toBe(content);
    expect(split.body).toBe('\n# HU\r\ncorpo\n');
    expect(split.entries).toEqual([
      { key: 'id', value: 'HU-0001' },
      { key: 'titulo', value: 'Guardar a HU' },
      { key: 'status', value: 'planejada' },
      { key: 'cobre', value: 'CA-1, CA-2' },
      { key: 'skills', value: 'a, b' },
    ]);
  });

  it('documento sem cabeçalho fica inteiro no corpo', () => {
    expect(splitFrontmatter('# PRD\n\ntexto')).toEqual({ head: '', body: '# PRD\n\ntexto', entries: [] });
    expect(splitFrontmatter('---\nsem fim')).toEqual({ head: '', body: '---\nsem fim', entries: [] });
  });
});

describe('buildMethodDocumentTabs', () => {
  const summary = (key: string, kind: 'epic' | 'hu' | 'prd' | 'task') => ({
    key, kind, workItemId: 'w', filePath: null, title: null, status: null, version: 1, contentHash: 'h',
    updatedByName: 'Ana', updatedVia: 'ui' as const, updatedAtUtc: '2026-11-27T10:00:00Z',
  });

  it('mostra as abas conforme o documento do item', () => {
    expect(buildMethodDocumentTabs([summary('EPIC-0001', 'epic')]).map(tab => tab.label)).toEqual(['Documento']);
    expect(buildMethodDocumentTabs([summary('HU-0001', 'hu')]).map(tab => tab.label)).toEqual(['Documento']);
    expect(buildMethodDocumentTabs([summary('HU-0001/prd', 'prd'), summary('HU-0001', 'hu')]).map(tab => [tab.label, tab.key]))
      .toEqual([['Documento', 'HU-0001'], ['PRD', 'HU-0001/prd']]);
    expect(buildMethodDocumentTabs([summary('T-0001', 'task')]).map(tab => tab.label)).toEqual(['Corpo da task']);
    expect(buildMethodDocumentTabs([])).toEqual([]);
    expect(buildMethodDocumentTabs(undefined)).toEqual([]);
  });
});
