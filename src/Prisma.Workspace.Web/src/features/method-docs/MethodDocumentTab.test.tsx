import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiRequestError, api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { MethodDocument, MethodDocumentKind } from '../../types/methodDocuments';
import { MethodDocumentTab } from './MethodDocumentTab';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const HU_CONTENT = '---\nid: HU-0001\ntitulo: "Guardar a HU"\nstatus: aberta\n---\n\n# HU-0001\n\n| CA | Tipo |\n|---|---|\n| CA-1 | interacao |\n';

function doc(overrides: Partial<MethodDocument> = {}): MethodDocument {
  return {
    key: 'HU-0001', kind: 'hu', workItemId: 'story-1', filePath: 'docs/trabalho/HU-0001/hu.md', title: 'Guardar a HU',
    status: 'aberta', version: 3, contentHash: 'abc', updatedByName: 'Ana Lima', updatedVia: 'mcp',
    updatedAtUtc: '2026-11-27T10:00:00Z', content: HU_CONTENT, parentKey: null, locked: false, canEdit: true,
    ...overrides,
  };
}

function renderTab(document: MethodDocument, kind: MethodDocumentKind = document.kind) {
  vi.spyOn(api, 'getMethodDocument').mockResolvedValue(document);
  vi.spyOn(api, 'getMethodDocumentRevisions').mockResolvedValue([
    { version: document.version, contentHash: 'abc', authorName: document.updatedByName, via: document.updatedVia, createdAtUtc: document.updatedAtUtc },
    { version: 1, contentHash: 'old', authorName: 'Bruno', via: 'ui', createdAtUtc: '2026-11-20T09:00:00Z' },
  ]);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ThemeProvider theme={theme}>
        <MethodDocumentTab projectId="project-1" workItemId={document.workItemId} documentKey={document.key} kind={kind} />
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

describe('MethodDocumentTab', () => {
  it('mostra a HU formatada, a faixa de metadados e a versão, sem o YAML cru', async () => {
    renderTab(doc());
    expect(await screen.findByRole('heading', { level: 1, name: 'HU-0001' })).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
    const strip = screen.getByLabelText('Metadados do documento');
    expect(within(strip).getByText('HU-0001')).toBeInTheDocument();
    expect(within(strip).getByText('Aberta')).toBeInTheDocument();
    expect(screen.queryByText(/^---$/)).not.toBeInTheDocument();
    expect(screen.queryByText(/titulo:/)).not.toBeInTheDocument();
    expect(screen.getByText(/Versão 3 · gravada por Ana Lima/)).toHaveTextContent('via MCP');
  });

  it('Escrever, Pré-visualizar e Salvar gravam o texto exato com o cabeçalho intacto e a baseVersion', async () => {
    const save = vi.spyOn(api, 'saveMethodDocument').mockImplementation(async (_project, _key, input) =>
      doc({ content: input.content, version: 4 }));
    renderTab(doc({ key: 'HU-0001/prd', kind: 'prd', content: '# PRD\n\nTexto antigo\n', title: null, status: null }));

    fireEvent.click(await screen.findByRole('button', { name: 'Editar PRD' }));
    const textarea = screen.getByRole('textbox', { name: 'Texto do PRD em markdown' });
    expect(textarea).toHaveValue('# PRD\n\nTexto antigo\n');
    const typed = '# PRD\n\nParágrafo novo <script>x()</script>\n\n| A | B |\n|---|---|\n| 1 | 2 |\n';
    fireEvent.change(textarea, { target: { value: typed } });

    fireEvent.click(screen.getByRole('tab', { name: 'Pré-visualizar' }));
    expect(screen.getByRole('tab', { name: 'Pré-visualizar' })).toHaveAttribute('aria-selected', 'true');
    const preview = screen.getByRole('region', { name: 'Pré-visualização do PRD' });
    expect(within(preview).getByRole('table')).toBeInTheDocument();
    expect(preview.querySelector('script')).toBeNull();

    fireEvent.click(screen.getByRole('tab', { name: 'Escrever' }));
    expect(screen.getByRole('textbox', { name: 'Texto do PRD em markdown' })).toHaveValue(typed);
    fireEvent.click(screen.getByRole('button', { name: 'Salvar PRD' }));

    await waitFor(() => expect(save).toHaveBeenCalledWith('project-1', 'HU-0001/prd', { content: typed, baseVersion: 3 }));
    await waitFor(() => expect(screen.queryByRole('textbox', { name: 'Texto do PRD em markdown' })).not.toBeInTheDocument());
  });

  it('o editor só mexe no corpo: o cabeçalho YAML volta byte a byte', async () => {
    const save = vi.spyOn(api, 'saveMethodDocument').mockImplementation(async (_p, _k, input) => doc({ content: input.content, version: 4 }));
    renderTab(doc());
    fireEvent.click(await screen.findByRole('button', { name: 'Editar documento da HU' }));
    const textarea = screen.getByRole('textbox', { name: 'Texto do documento da HU em markdown' });
    expect((textarea as HTMLTextAreaElement).value).not.toContain('titulo:');
    fireEvent.change(textarea, { target: { value: '\n# HU-0001\n\nnovo\n' } });
    fireEvent.click(screen.getByRole('button', { name: 'Salvar documento da HU' }));
    await waitFor(() => expect(save).toHaveBeenCalledWith('project-1', 'HU-0001', {
      content: '---\nid: HU-0001\ntitulo: "Guardar a HU"\nstatus: aberta\n---\n\n# HU-0001\n\nnovo\n', baseVersion: 3,
    }));
  });

  it('409 de versão mostra a mensagem de recarregar e mantém o texto digitado', async () => {
    vi.spyOn(api, 'saveMethodDocument').mockRejectedValue(
      new ApiRequestError(409, 'method_document_version_conflict', 'O documento mudou.'));
    renderTab(doc({ key: 'HU-0001/prd', kind: 'prd', content: '# PRD\n', title: null, status: null }));
    fireEvent.click(await screen.findByRole('button', { name: 'Editar PRD' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Texto do PRD em markdown' }), { target: { value: 'meu texto' } });
    fireEvent.click(screen.getByRole('button', { name: 'Salvar PRD' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Este documento mudou desde que você abriu. Recarregue para ver a versão nova');
    expect(screen.getByRole('textbox', { name: 'Texto do PRD em markdown' })).toHaveValue('meu texto');
    expect(screen.getByRole('button', { name: 'Recarregar documento' })).toBeInTheDocument();
  });

  it('task em em-desenvolvimento: aviso e nenhum Editar', async () => {
    renderTab(doc({ key: 'T-0001', kind: 'task', title: 'Task travada', status: null, locked: true, canEdit: false, content: '# T-0001\n' }));
    expect(await screen.findByText('O agente está executando esta task; o corpo volta a ser editável quando ela sair de em-desenvolvimento')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: 'Título da task' })).not.toBeInTheDocument();
  });

  it('sem permissão de editar: sem Editar e sem campos de título e status', async () => {
    renderTab(doc({ canEdit: false }));
    expect(await screen.findByRole('heading', { level: 1, name: 'HU-0001' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Editar/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Status da HU' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Status da HU: Aberta')).toBeInTheDocument();
  });

  it('o status do épico oferece só os quatro valores e grava pela rota do cabeçalho', async () => {
    const patch = vi.spyOn(api, 'updateMethodDocumentHeader').mockImplementation(async (_p, _k, input) =>
      doc({ key: 'EPIC-0001', kind: 'epic', status: input.status ?? 'aberta', version: 8 }));
    renderTab(doc({ key: 'EPIC-0001', kind: 'epic', version: 7, content: '---\nid: EPIC-0001\nstatus: aberta\n---\n# Épico\n' }));
    const select = await screen.findByRole('combobox', { name: 'Status do épico' });
    expect(within(select).getAllByRole('option').map(option => (option as HTMLOptionElement).value))
      .toEqual(['aberta', 'planejada', 'concluida', 'cancelada']);
    fireEvent.change(select, { target: { value: 'planejada' } });
    await waitFor(() => expect(patch).toHaveBeenCalledWith('project-1', 'EPIC-0001', { status: 'planejada', baseVersion: 7 }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Status do épico' })).toHaveValue('planejada'));
    expect(await screen.findByText(/Versão 8 · gravada por/)).toBeInTheDocument();
    expect(patch).toHaveBeenCalledTimes(1);
  });

  it('o título da HU grava pelo cabeçalho com Enter', async () => {
    const patch = vi.spyOn(api, 'updateMethodDocumentHeader').mockResolvedValue(doc({ title: 'Novo título', version: 4 }));
    renderTab(doc());
    const title = await screen.findByRole('textbox', { name: 'Título da HU' });
    fireEvent.change(title, { target: { value: '  Novo título  ' } });
    fireEvent.keyDown(title, { key: 'Enter' });
    await waitFor(() => expect(patch).toHaveBeenCalledWith('project-1', 'HU-0001', { title: 'Novo título', baseVersion: 3 }));
    await waitFor(() => expect(screen.getByRole('textbox', { name: 'Título da HU' })).toHaveValue('Novo título'));
    expect(patch).toHaveBeenCalledTimes(1);
  });

  it('lista as versões e abre uma anterior só leitura', async () => {
    const revision = vi.spyOn(api, 'getMethodDocumentRevision').mockResolvedValue({
      version: 1, contentHash: 'old', authorName: 'Bruno', via: 'ui', createdAtUtc: '2026-11-20T09:00:00Z',
      content: '---\nid: HU-0001\n---\n# Texto da versão 1\n',
    });
    renderTab(doc());
    const versions = await screen.findByRole('region', { name: 'Versões do documento' });
    expect(await within(versions).findByText('Versão 3 (atual)')).toBeInTheDocument();
    expect(within(versions).getByText(/Bruno · .* · tela/)).toBeInTheDocument();
    fireEvent.click(within(versions).getByRole('button', { name: 'Ver versão 1' }));
    const viewer = await screen.findByRole('region', { name: 'Texto da versão 1' });
    expect(await within(viewer).findByRole('heading', { name: 'Texto da versão 1' })).toBeInTheDocument();
    expect(within(viewer).queryByRole('textbox')).not.toBeInTheDocument();
    expect(revision).toHaveBeenCalledWith('project-1', 'HU-0001', 1);
  });
});
