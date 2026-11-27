import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AiAnswer, AiAssistant } from './AiAssistant';
import { readAiEvents } from './aiClient';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const wrap = (child: React.ReactNode) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><ThemeProvider theme={theme}><MemoryRouter>{child}</MemoryRouter></ThemeProvider></QueryClientProvider>);
describe('Assistente Prisma', () => {
  it('reconcilia PATCH recusado e reutiliza a conversa criada, mantendo-a no histórico', async () => {
    let savedModel: string | null = null; let patchCalls = 0;
    const request = vi.spyOn(api, 'request').mockImplementation(async (url, init) => {
      if (url === '/api/ai/status') return { enabled: true, remainingTokens: null };
      if (url === '/api/ai/options') return { connections: [{ id: 'a', name: 'Conta A', provider: 'Custom', model: 'model-a' }] };
      if (url.includes('/models')) return { models: [{ id: 'model-a', name: 'model-a' }], state: 'ready' };
      if (url === '/api/ai/conversations' && init?.method === 'POST') return { id: 'new', title: 'Conversa criada' };
      if (url === '/api/ai/conversations') return request.mock.calls.some(([, opts]) => opts?.method === 'POST') ? [{ id: 'new', title: 'Conversa criada' }] : [];
      if (url.endsWith('/selection')) {
        if (++patchCalls === 1) throw new Error('A conexão mudou.');
        savedModel = 'model-a'; return { selectedConnectionId: 'a', selectedModel: savedModel };
      }
      return { selectedConnectionId: savedModel ? 'a' : null, selectedModel: savedModel, messages: [] };
    });
    wrap(<AiAssistant/>); fireEvent.click(await screen.findByRole('button', { name: 'Abrir assistente Prisma' }));
    await screen.findByRole('option', { name: 'Endpoint personalizado · Conta A' });
    fireEvent.change(screen.getByLabelText('Provedor da conversa'), { target: { value: 'a' } });
    await screen.findByRole('option', { name: 'model-a' });
    fireEvent.change(screen.getByLabelText('Modelo da conversa'), { target: { value: 'model-a' } });
    await screen.findByText('A conexão mudou.');
    await waitFor(() => expect(screen.getByLabelText('Provedor da conversa')).toHaveValue(''));
    expect(screen.getByLabelText('Conversa')).toHaveValue('new');
    expect(screen.getByRole('option', { name: 'Conversa criada' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Provedor da conversa'), { target: { value: 'a' } });
    await screen.findByRole('option', { name: 'model-a' });
    fireEvent.change(screen.getByLabelText('Modelo da conversa'), { target: { value: 'model-a' } });
    await waitFor(() => expect(patchCalls).toBe(2));
    await waitFor(() => expect(screen.getByLabelText('Modelo da conversa')).toHaveValue('model-a'));
    expect(request.mock.calls.filter(([url, init]) => url === '/api/ai/conversations' && init?.method === 'POST')).toHaveLength(1);
  });
  it.each(['O modelo não está mais disponível.', 'Não foi possível consultar os modelos desta conexão agora. Tente novamente'])('preserva seleção, rascunho e histórico quando o envio recebe 409: %s', async detail => {
    vi.spyOn(api, 'request').mockImplementation(async url => {
      if (url === '/api/ai/status') return { enabled: true, remainingTokens: null };
      if (url === '/api/ai/options') return { connections: [{ id: 'a', name: 'Conta A', provider: 'Custom', model: 'model-a' }] };
      if (url.includes('/models')) return { models: [{ id: 'model-a', name: 'model-a' }], state: 'ready' };
      if (url === '/api/ai/conversations') return [{ id: 'existing', title: 'Histórico' }];
      return { messages: [{ role: 'assistant', content: 'Resposta anterior' }], selectedConnectionId: 'a', selectedModel: 'model-a' };
    });
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ detail }), { status: 409 }));
    wrap(<AiAssistant/>); fireEvent.click(await screen.findByRole('button', { name: 'Abrir assistente Prisma' }));
    await screen.findByRole('option', { name: 'Histórico' });
    fireEvent.change(screen.getByLabelText('Conversa'), { target: { value: 'existing' } });
    await screen.findByText('Resposta anterior');
    await screen.findByRole('option', { name: 'model-a' });
    fireEvent.change(screen.getByLabelText('Pergunte ao workspace'), { target: { value: '  Meu texto preservado  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar pergunta' }));
    await screen.findByText(detail);
    expect(screen.getByLabelText('Pergunte ao workspace')).toHaveValue('  Meu texto preservado  ');
    expect(screen.getByText('Resposta anterior')).toBeInTheDocument();
    expect(screen.getByLabelText('Provedor da conversa')).toHaveValue('a');
    expect(screen.getByLabelText('Modelo da conversa')).toHaveValue('model-a');
    expect(document.querySelectorAll('article')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Enviar pergunta' })).toBeEnabled();
  });
  it('não exibe o botão quando a IA está desabilitada ou sem conexão ativa', async () => {
    const request = vi.spyOn(api, 'request').mockResolvedValue({ enabled: false }); wrap(<AiAssistant/>);
    await waitFor(() => expect(request).toHaveBeenCalled()); expect(screen.queryByRole('button', { name: 'Abrir assistente Prisma' })).not.toBeInTheDocument();
  });
  it('abre, foca a pergunta e fecha com Escape devolvendo o foco', async () => {
    vi.spyOn(api, 'request').mockImplementation(async url => url === '/api/ai/status' ? { enabled: true, remainingTokens: null } : []);
    wrap(<AiAssistant/>); const launcher = await screen.findByRole('button', { name: 'Abrir assistente Prisma' }); fireEvent.click(launcher);
    await waitFor(() => expect(screen.getByLabelText('Pergunte ao workspace')).toHaveFocus()); fireEvent.keyDown(window, { key: 'Escape' });
    expect(screen.queryByRole('complementary', { name: 'Assistente Prisma' })).not.toBeInTheDocument(); expect(launcher).toHaveFocus();
  });
  it('transforma somente referências autorizadas em links internos', () => {
    wrap(<AiAnswer text="Veja [T:1001] e [T:9999]" sources={[{ reference: 'T:1001', title: 'Permitida', path: '/projects/p/backlog?item=t' }]}/>);
    expect(screen.getByRole('link', { name: '[T:1001]' })).toHaveAttribute('href', '/projects/p/backlog?item=t'); expect(screen.queryByRole('link', { name: '[T:9999]' })).not.toBeInTheDocument();
  });
  it('transforma a referência da ajuda em "Abrir na ajuda" só quando o artigo existe (CA-4)', () => {
    wrap(<AiAnswer text="Pause na seção Integrações. [A:integracoes/integracoes#ativar-ou-pausar] Veja também [A:integracoes/inventado]"/>);
    expect(screen.getByRole('link', { name: 'Abrir na ajuda: Integrações do projeto' })).toHaveAttribute('href', '/ajuda/integracoes/integracoes#ativar-ou-pausar');
    expect(screen.getByText(/\[artigo de ajuda indisponível\]/)).toBeInTheDocument();
  });
  it('pergunta de uso envia o artigo da página atual; pergunta sobre dados não envia (CA-4)', async () => {
    vi.spyOn(api, 'request').mockImplementation(async (url, init) => {
      if (url === '/api/ai/status') return { enabled: true, remainingTokens: null };
      if (url === '/api/ai/options') return { connections: [] };
      if (url === '/api/ai/conversations' && init?.method === 'POST') return { id: 'c1', title: 'Nova' };
      if (url === '/api/ai/conversations') return [];
      return { messages: [] };
    });
    const sse = () => new Response('event: delta\ndata: {"text":"Use Pausar. [A:integracoes/integracoes#ativar-ou-pausar]"}\n\nevent: done\ndata: {}\n\n', { status: 200 });
    const fetch = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => sse());
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><ThemeProvider theme={theme}>
      <MemoryRouter initialEntries={['/projects/11111111-1111-4111-8111-111111111111/settings?secao=metodo']}><AiAssistant/></MemoryRouter></ThemeProvider></QueryClientProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Abrir assistente Prisma' }));
    fireEvent.change(screen.getByLabelText('Pergunte ao workspace'), { target: { value: 'Como faço para pausar a integração?' } });
    fireEvent.click(screen.getByRole('button', { name: 'Enviar pergunta' }));
    expect(await screen.findByRole('link', { name: 'Abrir na ajuda: Integrações do projeto' })).toHaveAttribute('href', '/ajuda/integracoes/integracoes#ativar-ou-pausar');
    const body = JSON.parse(String(fetch.mock.calls[0][1]?.body));
    expect(body.help.page).toBe('/projects/11111111-1111-4111-8111-111111111111/settings');
    expect(body.help.articles[0]).toMatchObject({ ref: 'integracoes/integracoes', title: 'Integrações do projeto', updatedAt: '2026-11-27' });
    expect(body.help.articles[0].content).toContain('(âncora: ativar-ou-pausar)');
    fireEvent.change(screen.getByLabelText('Pergunte ao workspace'), { target: { value: 'Quais são minhas tarefas?' } });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Enviar pergunta' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Enviar pergunta' }));
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
    expect(JSON.parse(String(fetch.mock.calls[1][1]?.body)).help).toBeUndefined();
  });
  it('decodifica eventos SSE separados em vários blocos UTF-8', async () => {
    const bytes = new TextEncoder().encode('event: delta\ndata: {"text":"Olá"}\n\nevent: done\ndata: {}\n\n');
    const stream = new ReadableStream({ start(c) { c.enqueue(bytes.slice(0, 33)); c.enqueue(bytes.slice(33)); c.close(); } });
    const events: unknown[] = []; await readAiEvents(new Response(stream), (event, data) => events.push([event, data]));
    expect(events).toEqual([['delta', { text: 'Olá' }], ['done', {}]]);
  });
});
