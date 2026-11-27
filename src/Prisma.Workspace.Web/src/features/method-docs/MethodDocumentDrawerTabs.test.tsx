import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { TaskDetailDrawer } from '../../components/TaskDetailDrawer';
import { ApiRequestError, api } from '../../services/api';
import { theme } from '../../styles/theme';
import type { MethodDocumentSummary } from '../../types/methodDocuments';
import type { WorkItemDetails } from '../../types/scrum';
import { MethodStatusTag } from './MethodStatusTag';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

// O detalhe carrega a aba de documento sob demanda (lazy); o primeiro import transforma o
// react-markdown no Vitest. Carregar antes deixa o teste medir só a interação.
beforeAll(async () => { await import('./MethodDocumentTab'); });

const summary = (key: string, kind: MethodDocumentSummary['kind'], workItemId: string): MethodDocumentSummary => ({
  key, kind, workItemId, filePath: null, title: 'Guardar a HU', status: kind === 'hu' ? 'planejada' : null, version: 2,
  contentHash: 'h', updatedByName: 'Ana', updatedVia: 'ui', updatedAtUtc: '2026-11-27T10:00:00Z',
});

function renderDrawer(kind: number, documents: () => Promise<MethodDocumentSummary[]>) {
  const details: WorkItemDetails = {
    id: 'story-1', number: 7, reference: 'MET-7', projectId: 'project-1', projectKey: 'MET',
    boardId: 'board', boardName: 'Método', kind, origin: 1, title: 'HU-0001 · Guardar a HU', priority: 1,
    participants: [], createdAt: '2026-11-20T10:00:00Z', updatedAt: '2026-11-20T10:00:00Z',
    realizedHours: 0, tags: [], isArchived: false, checklist: [], subtasks: [],
    attachmentsCount: 0, commentsCount: 0, followerIds: [], isFollowing: false, links: [], customFields: [], version: 'v1',
  };
  vi.spyOn(api, 'getToken').mockReturnValue('jwt');
  vi.spyOn(api, 'getSetupStatus').mockResolvedValue({ initialized: true, setupAvailable: false, methodEnabled: true });
  vi.spyOn(api, 'getWorkItemDetails').mockResolvedValue(details);
  vi.spyOn(api, 'getProject').mockResolvedValue({ id: 'project-1', methodology: 1, teams: [] });
  vi.spyOn(api, 'getBoards').mockResolvedValue([]);
  vi.spyOn(api, 'getBoardStages').mockResolvedValue([]);
  vi.spyOn(api, 'getAssignableUsers').mockResolvedValue([]);
  vi.spyOn(api, 'getAttachments').mockResolvedValue([]);
  vi.spyOn(api, 'getTaskTypes').mockResolvedValue([]);
  vi.spyOn(api, 'getTags').mockResolvedValue([]);
  vi.spyOn(api, 'getComments').mockResolvedValue([]);
  vi.spyOn(api, 'getTaskEvents').mockResolvedValue([]);
  vi.spyOn(api, 'getProjectSprints').mockResolvedValue([]);
  const list = vi.spyOn(api, 'getMethodDocuments').mockImplementation(documents);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<MemoryRouter><QueryClientProvider client={client}><ThemeProvider theme={theme}>
    <TaskDetailDrawer projectKey="MET" item={{ id: details.id, boardId: 'board', boardName: 'Método', kind, title: details.title, priority: 1, rank: 1 }} onOpenChange={vi.fn()} />
  </ThemeProvider></QueryClientProvider></MemoryRouter>);
  return { list };
}

describe('abas de documento no detalhe', () => {
  it('História com HU e PRD ganha as abas Documento e PRD, e cada uma abre o seu documento', async () => {
    const { list } = renderDrawer(3, async () => [summary('HU-0001', 'hu', 'story-1'), summary('HU-0001/prd', 'prd', 'story-1')]);
    const getDoc = vi.spyOn(api, 'getMethodDocument').mockImplementation(async (_p, key) => ({
      ...summary(key, key.endsWith('/prd') ? 'prd' : 'hu', 'story-1'),
      content: key.endsWith('/prd') ? '# Corpo do PRD\n' : '# Corpo da HU\n', parentKey: null, locked: false, canEdit: true,
    }));
    vi.spyOn(api, 'getMethodDocumentRevisions').mockResolvedValue([]);

    await screen.findByRole('textbox', { name: 'Título' });
    // Três consultas em cadeia (disponibilidade do método, detalhe e documentos) num jsdom frio.
    fireEvent.click(await screen.findByRole('button', { name: 'Documento' }, { timeout: 5000 }));
    expect(list).toHaveBeenCalledWith('project-1', { workItemId: 'story-1' });
    expect(await screen.findByRole('heading', { name: 'Corpo da HU' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Documento' })).toHaveAttribute('aria-current', 'true');
    expect(screen.queryByRole('textbox', { name: 'Título' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'PRD' }));
    expect(await screen.findByRole('heading', { name: 'Corpo do PRD' })).toBeInTheDocument();
    expect(getDoc).toHaveBeenCalledWith('project-1', 'HU-0001/prd');

    fireEvent.click(screen.getByRole('button', { name: 'Descrição' }));
    expect(await screen.findByRole('textbox', { name: 'Título' })).toBeInTheDocument();
  });

  it('item fora do método ou erro na consulta: nenhuma aba nova', async () => {
    const { list } = renderDrawer(5, async () => { throw new ApiRequestError(404, null, 'Não encontrado'); });
    await screen.findByRole('textbox', { name: 'Título' });
    await waitFor(() => expect(list).toHaveBeenCalled(), { timeout: 5000 });
    expect(screen.queryByRole('button', { name: 'Documento' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Corpo da task' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'PRD' })).not.toBeInTheDocument();
  });

  it('Tarefa do método ganha a aba Corpo da task', async () => {
    renderDrawer(5, async () => [summary('T-0001', 'task', 'story-1')]);
    await screen.findByRole('textbox', { name: 'Título' });
    expect(await screen.findByRole('button', { name: 'Corpo da task' }, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Documento' })).not.toBeInTheDocument();
  });
});

describe('MethodStatusTag', () => {
  it('mostra o status da HU com o rótulo acessível e some sem status', () => {
    const { rerender } = render(<ThemeProvider theme={theme}><MethodStatusTag status={{ key: 'HU-0001', kind: 'hu', status: 'concluida' }} /></ThemeProvider>);
    expect(screen.getByText('Status da HU:', { exact: false }).parentElement).toHaveTextContent('Status da HU: Concluída');
    rerender(<ThemeProvider theme={theme}><MethodStatusTag status={undefined} /></ThemeProvider>);
    expect(screen.queryByText(/Status da HU/)).not.toBeInTheDocument();
  });
});
