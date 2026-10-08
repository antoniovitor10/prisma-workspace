import { useState } from 'react';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { AiConversationControls, type AiChoice } from './AiConversationControls';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const options = { connections: [{ id: 'a', name: 'Conta A', provider: 'OpenAI', type: 'ApiKey', model: 'a-model', isDefault: true }, { id: 'b', name: 'Conta B', provider: 'Gemini', type: 'CliSubscription', model: 'b-model', isDefault: false }] };
function Form() {
  const [choice, setChoice] = useState<AiChoice>({ connectionId: null, model: null });
  return <AiConversationControls tenantId="org" choice={choice} dirty={!!choice.connectionId && !choice.model} disabled={false} onConnection={id => setChoice({ connectionId: id, model: null })} onModel={model => setChoice(v => ({ ...v, model }))}/>;
}
const wrap = () => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><Form/></QueryClientProvider>);
describe('Seleção de assistente por conversa', () => {
  it.each(['unavailable', 'empty', 'authenticationRequired'])('preserva modelo salvo sem afirmar remoção quando o catálogo está %s', async state => {
    vi.spyOn(api, 'request').mockImplementation(async url => url === '/api/ai/options' ? options : { models: [], state, message: 'Esta conexão está indisponível no momento. Tente novamente.' });
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><AiConversationControls tenantId="org" choice={{ connectionId: 'b', model: 'b-model' }} dirty={false} disabled={false} onConnection={vi.fn()} onModel={vi.fn()}/></QueryClientProvider>);
    await screen.findByText('Esta conexão está indisponível no momento. Tente novamente.');
    expect(screen.getByLabelText('Modelo da conversa')).toHaveValue('b-model');
    expect(screen.getByLabelText('Modelo da conversa')).toBeDisabled();
    expect(screen.queryByText(/O modelo salvo não aparece/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Atualizar conexões' })).toBeEnabled();
  });
  it('uma atualização que falha não libera nem acusa remoção com catálogo antigo em cache', async () => {
    let calls = 0;
    vi.spyOn(api, 'request').mockImplementation(async url => {
      if (url === '/api/ai/options') return options;
      if (++calls === 1) return { models: [{ id: 'a-model', name: 'Modelo A' }], state: 'ready' };
      if (calls === 2) throw new Error('Erro de rede');
      return { models: [{ id: 'b-model', name: 'Modelo B' }], state: 'ready' };
    });
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><AiConversationControls tenantId="org" choice={{ connectionId: 'b', model: 'b-model' }} dirty={false} disabled={false} onConnection={vi.fn()} onModel={vi.fn()}/></QueryClientProvider>);
    await screen.findByText(/O modelo salvo não aparece/);
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar conexões' }));
    await screen.findByText('Não foi possível carregar os modelos. Tente atualizar.');
    expect(screen.getByLabelText('Modelo da conversa')).toBeDisabled();
    expect(screen.queryByText(/O modelo salvo não aparece/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar conexões' }));
    await screen.findByRole('option', { name: 'Modelo B — b-model' });
    expect(screen.getByLabelText('Modelo da conversa')).toHaveValue('b-model');
    expect(screen.getByLabelText('Modelo da conversa')).toBeEnabled();
  });
  it('ignora catálogo atrasado de outra conexão e usa somente as opções do usuário comum', async () => {
    let finishA: (value: unknown) => void = () => undefined;
    const request = vi.spyOn(api, 'request').mockImplementation(async url => {
      if (url === '/api/ai/options') return options;
      if (url.includes('/a/models')) return new Promise(resolve => { finishA = resolve; });
      return { models: [{ id: 'b-model', name: 'Modelo B' }], state: 'ready', message: 'Pronto' };
    });
    wrap(); await screen.findByRole('option', { name: 'OpenAI / ChatGPT · Conta A' });
    fireEvent.change(screen.getByLabelText('Provedor da conversa'), { target: { value: 'a' } });
    await waitFor(() => expect(request).toHaveBeenCalledWith('/api/ai/options/a/models?refresh=true', expect.anything()));
    fireEvent.change(screen.getByLabelText('Provedor da conversa'), { target: { value: 'b' } });
    await screen.findByRole('option', { name: 'Modelo B — b-model' });
    finishA({ models: [{ id: 'a-model', name: 'Modelo A' }], state: 'ready' });
    await waitFor(() => expect(screen.getByLabelText('Provedor da conversa')).toHaveValue('b'));
    expect(screen.queryByRole('option', { name: 'Modelo A — a-model' })).not.toBeInTheDocument();
    expect(request.mock.calls.every(([url]) => !url.startsWith('/api/admin/'))).toBe(true);
  });
  it('mostra a falha de catálogo e permite recuperar por atualização explícita', async () => {
    let attempts = 0;
    vi.spyOn(api, 'request').mockImplementation(async url => {
      if (url === '/api/ai/options') return options;
      if (++attempts === 1) throw new Error('Rede indisponível');
      return { models: [{ id: 'b-model', name: 'Modelo B' }], state: 'ready', message: 'Pronto' };
    });
    wrap(); await screen.findByRole('option', { name: 'Google Gemini · Conta B' });
    fireEvent.change(screen.getByLabelText('Provedor da conversa'), { target: { value: 'b' } });
    await screen.findByText(/Não foi possível carregar os modelos/);
    expect(screen.getByLabelText('Modelo da conversa')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar conexões' }));
    await screen.findByRole('option', { name: 'Modelo B — b-model' });
    expect(screen.getByLabelText('Modelo da conversa')).toBeEnabled();
  });
});
