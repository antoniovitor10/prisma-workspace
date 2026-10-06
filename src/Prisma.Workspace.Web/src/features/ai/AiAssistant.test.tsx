import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from 'styled-components';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AiAnswer, AiAssistant } from './AiAssistant';
import { readAiEvents } from './aiClient';
import { api } from '../../services/api';
import { theme } from '../../styles/theme';

afterEach(() => vi.restoreAllMocks());
const wrap = (child: React.ReactNode) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><ThemeProvider theme={theme}><MemoryRouter>{child}</MemoryRouter></ThemeProvider></QueryClientProvider>);
describe('Assistente Prisma', () => {
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
  it('decodifica eventos SSE separados em vários blocos UTF-8', async () => {
    const bytes = new TextEncoder().encode('event: delta\ndata: {"text":"Olá"}\n\nevent: done\ndata: {}\n\n');
    const stream = new ReadableStream({ start(c) { c.enqueue(bytes.slice(0, 33)); c.enqueue(bytes.slice(33)); c.close(); } });
    const events: unknown[] = []; await readAiEvents(new Response(stream), (event, data) => events.push([event, data]));
    expect(events).toEqual([['delta', { text: 'Olá' }], ['done', {}]]);
  });
});
