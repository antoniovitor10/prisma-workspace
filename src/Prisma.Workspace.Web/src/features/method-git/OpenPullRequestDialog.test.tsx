import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { ThemeProvider } from 'styled-components';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { darkTheme, theme } from '../../styles/theme';
import { pullRequestApi } from './client';
import { OpenPullRequestButton } from './OpenPullRequestButton';
import type { OpenPullRequestResult, PullRequestSource, PullRequestTask } from './pullRequestTypes';

const task: PullRequestTask = { workItemId: 'item-1', methodTaskId: 'T-0028', title: 'Botão Abrir PR' };
const app: PullRequestSource = { linkId: 'link-1', branch: 'task/T-0028-botao', repositoryFullName: 'qa/metodo', defaultBranch: 'main', mode: 'app' };
const poll: PullRequestSource = { ...app, repositoryFullName: 'detranse/mobile-backend', defaultBranch: 'prod', mode: 'poll' };
const draft = { title: 'Corrige consulta', body: 'Resumo do agente', updatedAtUtc: '2026-11-27T12:00:00Z', actorName: 'Agente' };
const cardUrl = `${window.location.origin}/projects/project-1/backlog?item=item-1`;

function show(source: PullRequestSource | null = app, canEdit = true, onOpened = vi.fn(), dark = false) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<ThemeProvider theme={dark ? darkTheme : theme}><QueryClientProvider client={client}>
    <OpenPullRequestButton projectId="project-1" task={task} source={source} canEdit={canEdit} onOpened={onOpened} />
  </QueryClientProvider></ThemeProvider>);
  return { onOpened };
}
async function openDialog(label = 'Abrir PR da task T-0028') {
  fireEvent.click(screen.getByRole('button', { name: label }));
  const dialog = await screen.findByRole('dialog');
  await within(dialog).findByLabelText('Título do PR');
  return dialog;
}

beforeEach(() => { vi.restoreAllMocks(); });
afterEach(() => cleanup());

describe('OpenPullRequestButton (CA-1, CA-3, CA-8)', () => {
  it('não aparece sem permissão de edição nem sem branch elegível', () => {
    show(app, false);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
    cleanup();
    show(null, true);
    expect(screen.queryByRole('button', { name: /Abrir PR/ })).toBeNull();
  });

  it('modo App mostra "Abrir PR"; modo consulta mostra "Abrir no GitHub"', () => {
    show(app);
    expect(screen.getByRole('button', { name: 'Abrir PR da task T-0028' })).toHaveTextContent('Abrir PR');
    cleanup();
    show(poll);
    expect(screen.getByRole('button', { name: 'Abrir PR no GitHub da task T-0028' })).toHaveTextContent('Abrir no GitHub');
  });
});

describe('OpenPullRequestDialog (CA-4, CA-6, CA-7, CA-10)', () => {
  it('com rascunho: título e corpo do agente, base cadastrada, caixa desmarcada, prévia da linha e dica do prefixo', async () => {
    const getDraft = vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    show();
    const dialog = await openDialog();
    expect(getDraft).toHaveBeenCalledWith('project-1', 'T-0028');
    expect(within(dialog).getByLabelText('Título do PR')).toHaveValue('Corrige consulta');
    expect(within(dialog).getByLabelText('Corpo do PR')).toHaveValue('Resumo do agente');
    expect(within(dialog).getByRole('checkbox', { name: 'Abrir como rascunho' })).not.toBeChecked();
    expect(within(dialog).getByText('main')).toBeInTheDocument();
    expect(within(dialog).getByText('task/T-0028-botao')).toBeInTheDocument();
    expect(within(dialog).getByText(`Task no Prisma: ${cardUrl}`)).toBeInTheDocument();
    expect(within(dialog).getByText('O Prisma põe [T-0028] no início do título ao abrir o PR.')).toBeInTheDocument();
    // Título que já começa com o ID não mostra a dica.
    fireEvent.change(within(dialog).getByLabelText('Título do PR'), { target: { value: '[T-0028] Corrige consulta' } });
    expect(within(dialog).queryByText('O Prisma põe [T-0028] no início do título ao abrir o PR.')).toBeNull();
  });

  it('sem rascunho (404): "[T-XXXX] título da task" e o link do card', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockRejectedValue(new Error('A task ainda não tem rascunho de PR.'));
    show();
    const dialog = await openDialog();
    expect(within(dialog).getByLabelText('Título do PR')).toHaveValue('[T-0028] Botão Abrir PR');
    expect(within(dialog).getByLabelText('Corpo do PR')).toHaveValue(`Task no Prisma: ${cardUrl}`);
    expect(within(dialog).queryByText('Não consegui carregar o rascunho; usei o texto padrão.')).toBeNull();
  });

  it('o texto digitado e a caixa vão no POST; Confirmar fica desabilitado durante o envio; resultado e onOpened', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    let finish: (value: OpenPullRequestResult) => void = () => {};
    const open = vi.spyOn(pullRequestApi, 'open').mockReturnValue(new Promise(resolve => { finish = resolve; }));
    const { onOpened } = show();
    const dialog = await openDialog();
    fireEvent.change(within(dialog).getByLabelText('Título do PR'), { target: { value: 'Título editado' } });
    fireEvent.change(within(dialog).getByLabelText('Corpo do PR'), { target: { value: 'Corpo editado' } });
    fireEvent.click(within(dialog).getByRole('checkbox', { name: 'Abrir como rascunho' }));
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrir PR' }));
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Abrindo PR…' })).toBeDisabled());
    expect(open).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalledWith('project-1', 'T-0028', { linkId: 'link-1', title: 'Título editado', body: 'Corpo editado', draft: true });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrindo PR…' }));
    expect(open).toHaveBeenCalledTimes(1);
    finish({ outcome: 'opened', number: 7, url: 'https://github.com/qa/metodo/pull/7', moved: true, message: 'PR #7 aberto; o card foi para Em revisão.' });
    const status = await within(dialog).findByRole('status');
    expect(status).toHaveTextContent('PR #7 aberto; o card foi para Em revisão.');
    expect(within(status).getByRole('link', { name: 'Ver PR #7 no GitHub' })).toHaveAttribute('href', 'https://github.com/qa/metodo/pull/7');
    expect(onOpened).toHaveBeenCalledWith(expect.objectContaining({ number: 7, moved: true }));
  });

  it('PR que já existia e card que não andou mostram a mensagem da API', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    vi.spyOn(pullRequestApi, 'open').mockResolvedValue({ outcome: 'linked_existing', number: 41, url: 'https://github.com/qa/metodo/pull/41', moved: false,
      message: 'Já havia um PR, vinculei o #41; o card ficou em Pronta.' });
    show();
    const dialog = await openDialog();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrir PR' }));
    expect(await within(dialog).findByRole('status')).toHaveTextContent('Já havia um PR, vinculei o #41; o card ficou em Pronta.');
  });

  it.each([
    'A App ainda não tem permissão para abrir PR neste repositório. Aceite a nova permissão da instalação no GitHub ou avise o administrador.',
    'Não há diferença entre task/T-0028-botao e main: nada para abrir no PR.',
    'O GitHub limitou as chamadas. Tente de novo depois de 10:30 (horário de Brasília).',
  ])('erro da API em role="alert" mantém o texto: %s', async message => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    const onOpened = vi.fn();
    vi.spyOn(pullRequestApi, 'open').mockRejectedValue(new Error(message));
    show(app, true, onOpened);
    const dialog = await openDialog();
    fireEvent.change(within(dialog).getByLabelText('Corpo do PR'), { target: { value: 'Texto que não pode sumir' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrir PR' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(message);
    expect(within(dialog).getByLabelText('Corpo do PR')).toHaveValue('Texto que não pode sumir');
    expect(within(dialog).getByRole('button', { name: 'Abrir PR' })).toBeEnabled();
    expect(onOpened).not.toHaveBeenCalled();
  });

  it('Esc fecha o modal', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    show();
    const dialog = await openDialog();
    fireEvent.keyDown(dialog, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('tema escuro renderiza o modal', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    show(app, true, vi.fn(), true);
    const dialog = await openDialog();
    expect(within(dialog).getByRole('heading', { name: 'Abrir PR da task T-0028' })).toBeInTheDocument();
  });
});

describe('modo consulta: Abrir no GitHub (CA-9)', () => {
  it('chama o compare-url, abre a URL numa aba nova e não chama o POST open; sem caixa de rascunho', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    const compare = vi.spyOn(pullRequestApi, 'compareUrl').mockResolvedValue({ url: 'https://github.com/detranse/mobile-backend/compare/prod...task/T-0028-botao?quick_pull=1', truncated: false });
    const open = vi.spyOn(pullRequestApi, 'open');
    const windowOpen = vi.spyOn(window, 'open').mockReturnValue(null);
    show(poll);
    const dialog = await openDialog('Abrir PR no GitHub da task T-0028');
    expect(within(dialog).queryByRole('checkbox', { name: 'Abrir como rascunho' })).toBeNull();
    expect(within(dialog).getByText('prod')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Título do PR')).toHaveValue('Corrige consulta');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrir no GitHub' }));
    await waitFor(() => expect(windowOpen).toHaveBeenCalledWith('https://github.com/detranse/mobile-backend/compare/prod...task/T-0028-botao?quick_pull=1', '_blank', 'noopener'));
    expect(compare).toHaveBeenCalledWith('project-1', 'T-0028', { linkId: 'link-1', title: 'Corrige consulta', body: 'Resumo do agente' });
    expect(open).not.toHaveBeenCalled();
    expect(await within(dialog).findByRole('status')).toHaveTextContent('Página do GitHub aberta numa aba nova.');
  });

  it('corpo cortado: avisa e "Copiar corpo" copia o corpo inteiro com a linha do link', async () => {
    vi.spyOn(pullRequestApi, 'draft').mockResolvedValue(draft);
    vi.spyOn(pullRequestApi, 'compareUrl').mockResolvedValue({ url: 'https://github.com/x/y/compare/prod...b?quick_pull=1', truncated: true });
    vi.spyOn(window, 'open').mockReturnValue(null);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    show(poll);
    const dialog = await openDialog('Abrir PR no GitHub da task T-0028');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Abrir no GitHub' }));
    expect(await within(dialog).findByText('O corpo é longo demais para o link; ele foi cortado. Copie o corpo completo e cole no GitHub.')).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Copiar corpo' }));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(`Resumo do agente\n\nTask no Prisma: ${cardUrl}`));
    expect(await within(dialog).findByRole('button', { name: 'Corpo copiado' })).toBeInTheDocument();
  });
});
