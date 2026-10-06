import { fireEvent, render, screen, waitFor, cleanup } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AiConnectionForm } from './AiConnectionForm';
import { api } from '../../services/api';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });
const wrap = () => render(<AiConnectionForm onSaved={async () => undefined} onCancel={() => undefined}/>);
describe('Configuração guiada da IA', () => {
  it('usa a chave atual ao carregar o catálogo após editar o endereço', async () => {
    const sent: Array<{ provider: string; secret: string }> = [];
    vi.spyOn(api, 'request').mockImplementation(async (_url, init) => {
      const body = JSON.parse(init?.body as string); sent.push(body);
      return { models: body.secret === 'current-key' ? [{ id: 'chat', name: 'Chat' }] : [], state: 'ready', message: 'Pronto' };
    });
    wrap(); fireEvent.change(screen.getByLabelText('Provedor'), { target: { value: 'Custom' } });
    fireEvent.change(screen.getByLabelText('URL base'), { target: { value: 'https://endpoint.invalid/v1' } });
    fireEvent.change(screen.getByLabelText('Chave de API'), { target: { value: 'current-key' } });
    fireEvent.blur(screen.getByLabelText('Chave de API'));
    await screen.findByRole('option', { name: 'Chat — chat' });
    await waitFor(() => expect(sent.filter(x => x.provider === 'Custom')).toHaveLength(2));
    expect(sent.filter(x => x.provider === 'Custom').every(x => x.secret === 'current-key')).toBe(true);
  });
  it('muda métodos e modelos conforme o provedor e descarta a escolha anterior', async () => {
    vi.spyOn(api, 'request').mockImplementation(async (_url, init) => {
      const body = JSON.parse(init?.body as string); return { models: [{ id: body.provider === 'Gemini' ? 'gemini-test' : 'router/model', name: body.provider }], state: 'ready', message: 'Escolha um modelo' };
    });
    wrap(); await screen.findByRole('option', { name: 'OpenRouter — router/model' });
    fireEvent.change(screen.getByLabelText('Modelo'), { target: { value: 'router/model' } });
    fireEvent.change(screen.getByLabelText('Provedor'), { target: { value: 'Gemini' } });
    expect(screen.queryByLabelText('Como conectar')).not.toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /router\/model/ })).not.toBeInTheDocument();
    expect(screen.getByLabelText('Modelo')).toHaveValue('');
    await screen.findByRole('option', { name: 'Gemini — gemini-test' });
    expect(screen.getByRole('button', { name: 'Salvar conexão' })).toBeDisabled();
  });
  it('exige aceite do risco antes de iniciar login oficial e permite cancelar', async () => {
    const request = vi.spyOn(api, 'request').mockImplementation(async url => {
      if (url.endsWith('/status')) return { available: true, authenticated: false, state: 'authenticationRequired', message: 'Entre na conta.' };
      if (url.endsWith('/login')) return { available: true, authenticated: false, sessionId: 'a'.repeat(32), state: 'waiting', url: 'https://auth.openai.com/codex/device', deviceCode: 'TEST-CODE', message: 'Autorize no provedor.' };
      return { models: [], state: 'authenticationRequired', message: 'Autentique primeiro.' };
    });
    wrap(); fireEvent.change(screen.getByLabelText('Provedor'), { target: { value: 'OpenAI' } }); fireEvent.change(screen.getByLabelText('Como conectar'), { target: { value: 'CliSubscription' } });
    expect(screen.getByText(/O provedor pode bloquear ou encerrar sua conta/)).toBeInTheDocument();
    const login = screen.getByRole('button', { name: 'Entrar com ChatGPT' });
    await waitFor(() => expect(request).toHaveBeenCalledWith('/api/admin/ai/cli/OpenAI/status', expect.anything())); expect(login).toBeDisabled();
    fireEvent.click(screen.getByLabelText('Entendo o risco de bloqueio ou perda da conta e quero continuar')); await waitFor(() => expect(login).toBeEnabled()); fireEvent.click(login);
    expect(await screen.findByRole('link', { name: 'Abrir login oficial do provedor' })).toHaveAttribute('href', 'https://auth.openai.com/codex/device');
    expect(screen.getByText('TEST-CODE')).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Salvar conexão' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar login' }));
    await waitFor(() => expect(request).toHaveBeenCalledWith(`/api/admin/ai/cli/OpenAI/login/${'a'.repeat(32)}`, { method: 'DELETE' }));
    expect(screen.queryByText('TEST-CODE')).not.toBeInTheDocument();
  });
  it('não permite que uma resposta atrasada restaure modelos do provedor anterior', async () => {
    let resolveOld: (value: unknown) => void = () => undefined;
    vi.spyOn(api, 'request').mockImplementation(async (_url, init) => {
      const body = JSON.parse(init?.body as string);
      if (body.provider === 'OpenRouter') return new Promise(resolve => { resolveOld = resolve; });
      return { models: [{ id: 'gemini-test', name: 'Gemini' }], state: 'ready', message: 'Pronto' };
    });
    wrap(); await waitFor(() => expect(api.request).toHaveBeenCalled());
    fireEvent.change(screen.getByLabelText('Provedor'), { target: { value: 'Gemini' } });
    await screen.findByRole('option', { name: 'Gemini — gemini-test' });
    resolveOld({ models: [{ id: 'old', name: 'Antigo' }], state: 'ready', message: 'Antigo' });
    await waitFor(() => expect(screen.queryByRole('option', { name: /Antigo/ })).not.toBeInTheDocument());
  });
});
