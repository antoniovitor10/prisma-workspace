import { QueryClient, QueryClientProvider, useMutation } from '@tanstack/react-query';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ArrowRight, Plus } from 'lucide-react';
import { useState } from 'react';
import styled, { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { darkTheme, theme } from '../styles/theme';
import { ActionButton, LOADING_HANDOFF_MS, SLOW_ACTION_MESSAGE, SLOW_ACTION_MS } from './ActionButton';

const Primary = styled.button`
  padding: 0 16px;
  color: ${({ theme }) => theme.color.onBrand};
`;

function deferred() {
  let resolve!: () => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<void>((ok, fail) => { resolve = ok; reject = fail; });
  return { promise, resolve, reject };
}

function renderWithTheme(ui: React.ReactNode, mode = theme) {
  return render(<ThemeProvider theme={mode}>{ui}</ThemeProvider>);
}

const spinnerOf = (button: HTMLElement) => button.querySelector('[data-action-spinner]');

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe('ActionButton', () => {
  it('mostra o spinner ao lado do texto no mesmo clique, com o texto igual (CA-1)', () => {
    const request = deferred();
    renderWithTheme(<Primary as={ActionButton} onClick={() => request.promise}><Plus size={14} />Criar projeto</Primary>);
    const button = screen.getByRole('button', { name: 'Criar projeto' });
    expect(spinnerOf(button)).toBeNull();

    fireEvent.click(button);

    expect(spinnerOf(button)).not.toBeNull();
    expect(button).toHaveTextContent('Criar projeto');
    expect(screen.getByRole('button', { name: 'Criar projeto' })).toBe(button);
    // O spinner toma o lugar do ícone inicial: o ícone fica logo depois dele, escondido pelo CSS.
    expect(spinnerOf(button)?.nextElementSibling?.tagName.toLowerCase()).toBe('svg');
  });

  it('troca só o ícone que vem antes do texto; o ícone final continua visível', () => {
    renderWithTheme(<>
      <ActionButton loading><Plus size={14} />Criar</ActionButton>
      <ActionButton loading>Concluir<ArrowRight size={14} /></ActionButton>
    </>);
    expect(screen.getByRole('button', { name: 'Criar' })).toHaveAttribute('data-leading-icon');
    expect(screen.getByRole('button', { name: 'Concluir' })).not.toHaveAttribute('data-leading-icon');
  });

  it('herda a aparência do botão da tela pelo as', () => {
    renderWithTheme(<Primary as={ActionButton} className="extra">Salvar</Primary>);
    const button = screen.getByRole('button', { name: 'Salvar' });
    expect(button).toHaveClass('extra');
    expect(button.className.split(' ').length).toBeGreaterThan(2);
  });

  it('não chama a ação de novo com cliques repetidos ou Enter no formulário (CA-2)', () => {
    const request = deferred();
    const action = vi.fn(() => request.promise);
    const submit = vi.fn((event: React.FormEvent) => event.preventDefault());
    renderWithTheme(<form onSubmit={submit}><input aria-label="Nome" /><ActionButton type="submit" onClick={action}>Salvar</ActionButton></form>);
    const button = screen.getByRole('button', { name: 'Salvar' });

    fireEvent.click(button);
    fireEvent.click(button);
    fireEvent.click(button);
    // O Enter num campo chega ao botão de envio como clique (envio implícito do formulário);
    // o jsdom não faz o envio implícito, então o clique o representa. O E2E aperta Enter de fato.
    fireEvent.click(button);

    expect(action).toHaveBeenCalledTimes(1);
    expect(submit).toHaveBeenCalledTimes(1);
  });

  it('com loading controlado por uma mutation, o segundo clique não dispara outra requisição (CA-2)', async () => {
    const request = deferred();
    const send = vi.fn(() => request.promise);
    function Screen() {
      const mutation = useMutation({ mutationFn: send });
      return <ActionButton loading={mutation.isPending} onClick={() => mutation.mutate()}>Emitir token</ActionButton>;
    }
    const client = new QueryClient();
    renderWithTheme(<QueryClientProvider client={client}><Screen /></QueryClientProvider>);
    const button = screen.getByRole('button', { name: 'Emitir token' });

    fireEvent.click(button);
    fireEvent.click(button);
    expect(button).toHaveAttribute('aria-busy', 'true');
    await act(async () => { await Promise.resolve(); });
    fireEvent.click(button);

    expect(send).toHaveBeenCalledTimes(1);
    await act(async () => { request.resolve(); await request.promise; });
    await waitFor(() => expect(button).not.toHaveAttribute('aria-busy'));
  });

  it.each([
    ['sucesso', (request: ReturnType<typeof deferred>) => request.resolve()],
    ['erro', (request: ReturnType<typeof deferred>) => request.reject(new Error('falhou'))],
  ])('volta a aceitar clique quando a resposta chega com %s (CA-3)', async (_, finish) => {
    let request = deferred();
    const action = vi.fn(() => request.promise);
    renderWithTheme(<ActionButton onClick={action}>Salvar</ActionButton>);
    const button = screen.getByRole('button', { name: 'Salvar' });

    fireEvent.click(button);
    expect(spinnerOf(button)).not.toBeNull();
    await act(async () => { finish(request); await request.promise.catch(() => undefined); });

    expect(spinnerOf(button)).toBeNull();
    expect(button).not.toHaveAttribute('aria-busy');
    expect(button).not.toHaveAttribute('aria-disabled');
    request = deferred();
    fireEvent.click(button);
    expect(action).toHaveBeenCalledTimes(2);
  });

  it('com loading controlado, sai da espera quando o loading termina (CA-3)', () => {
    function Screen() {
      const [loading, setLoading] = useState(false);
      return <><ActionButton loading={loading}>Salvar</ActionButton><button onClick={() => setLoading(value => !value)}>alternar</button></>;
    }
    renderWithTheme(<Screen />);
    const button = screen.getByRole('button', { name: 'Salvar' });
    fireEvent.click(screen.getByRole('button', { name: 'alternar' }));
    expect(button).toHaveAttribute('aria-busy', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'alternar' }));
    expect(button).not.toHaveAttribute('aria-busy');
    expect(spinnerOf(button)).toBeNull();
  });

  it('se o loading controlado não chega (ex.: validação falhou), o botão volta ao normal', () => {
    vi.useFakeTimers();
    renderWithTheme(<ActionButton loading={false} onClick={() => undefined}>Salvar</ActionButton>);
    const button = screen.getByRole('button', { name: 'Salvar' });
    fireEvent.click(button);
    expect(button).toHaveAttribute('aria-busy', 'true');
    act(() => { vi.advanceTimersByTime(LOADING_HANDOFF_MS); });
    expect(button).not.toHaveAttribute('aria-busy');
  });

  it('mostra "Ainda processando..." depois de 10 s de espera, até a resposta (CA-4)', async () => {
    vi.useFakeTimers();
    const request = deferred();
    renderWithTheme(<ActionButton onClick={() => request.promise}>Gerar relatório</ActionButton>);
    fireEvent.click(screen.getByRole('button', { name: 'Gerar relatório' }));

    act(() => { vi.advanceTimersByTime(SLOW_ACTION_MS - 1); });
    expect(screen.queryByText(SLOW_ACTION_MESSAGE)).toBeNull();
    act(() => { vi.advanceTimersByTime(1); });
    expect(screen.getByText(SLOW_ACTION_MESSAGE)).toBeInTheDocument();

    await act(async () => { request.resolve(); await request.promise; });
    expect(screen.queryByText(SLOW_ACTION_MESSAGE)).toBeNull();
  });

  it('não mostra o aviso de demora quando a resposta chega antes de 10 s (CA-4)', async () => {
    vi.useFakeTimers();
    const request = deferred();
    renderWithTheme(<ActionButton onClick={() => request.promise}>Salvar</ActionButton>);
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }));
    act(() => { vi.advanceTimersByTime(3_000); });
    await act(async () => { request.resolve(); await request.promise; });
    act(() => { vi.advanceTimersByTime(SLOW_ACTION_MS); });
    expect(screen.queryByText(SLOW_ACTION_MESSAGE)).toBeNull();
  });

  it('marca aria-busy, anuncia a ação em andamento e esconde o spinner do leitor de tela (CA-5)', () => {
    const request = deferred();
    renderWithTheme(<ActionButton loadingLabel="Criando organização" onClick={() => request.promise}>Criar organização</ActionButton>);
    const button = screen.getByRole('button', { name: 'Criar organização' });
    const status = document.querySelector('[aria-live="polite"]');
    expect(screen.queryByRole('status')).toBeNull();
    expect(status).toHaveTextContent('');

    fireEvent.click(button);

    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(status).toHaveTextContent('Criando organização');
    expect(spinnerOf(button)).toHaveAttribute('aria-hidden', 'true');
  });

  it('funciona no tema escuro (CA-7)', () => {
    renderWithTheme(<Primary as={ActionButton} loading>Salvar</Primary>, darkTheme);
    const button = screen.getByRole('button', { name: 'Salvar' });
    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(spinnerOf(button)).not.toBeNull();
  });
});
