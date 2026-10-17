import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../../services/api';
import { methodApi } from './client';
import type { MethodTask } from './types';

beforeEach(() => {
  vi.spyOn(api, 'getToken').mockReturnValue('ui.jwt.token');
  vi.spyOn(api, 'request').mockResolvedValue(null);
});
afterEach(() => vi.restoreAllMocks());

describe('Cliente da UI do método', () => {
  it('codifica o escopo e não provisiona durante leituras', async () => {
    await methodApi.settings('project/a');
    expect(api.request).toHaveBeenCalledExactlyOnceWith('/api/projects/project%2Fa/method/settings', { method: 'GET' });
  });

  it('emite por sessão UI sem gravar o token retornado na autenticação da SPA', async () => {
    const save = vi.spyOn(api, 'setToken');
    const result = { metadata: { id: 'token-a' }, token: `pmt_${'b'.repeat(43)}` };
    vi.mocked(api.request).mockResolvedValue(result);
    expect(await methodApi.issueToken('project-a', { name: 'Executor', role: 'agente', validDays: 30 })).toEqual(result);
    expect(save).not.toHaveBeenCalled();
    expect(api.request).toHaveBeenCalledWith('/api/projects/project-a/method/tokens', {
      method: 'POST', body: JSON.stringify({ name: 'Executor', role: 'agente', validDays: 30 }),
    });
  });

  it('libera usando GUID da task e IDs de bloqueio/ciclo retornados pelo servidor', async () => {
    const task = { workItemId: 'guid-work-item', id: 'T-0001', cycleId: 'cycle-a', blockingEventId: 'block-a' } as MethodTask;
    await methodApi.approveResume('project-a', task, 'Impedimento resolvido.');
    expect(api.request).toHaveBeenCalledWith('/api/projects/project-a/method/tasks/guid-work-item/resume-approval', {
      method: 'POST', body: JSON.stringify({ blockingEventId: 'block-a', cycleId: 'cycle-a', reason: 'Impedimento resolvido.' }),
    });
  });

  it('redige credenciais em mensagens de erro antes de exibir na UI', async () => {
    const token = `pmt_${'c'.repeat(43)}`;
    vi.mocked(api.request).mockRejectedValue(new Error(`Falha ${token} Bearer ui.jwt.token eyJhbGciOiJIUzI1NiJ9.payload.signature`));
    await expect(methodApi.tokens('project-a')).rejects.toThrow('Falha [removido] Bearer [removido] [removido]');
  });

  it('sessão ausente pede novo login antes de qualquer requisição', async () => {
    vi.mocked(api.getToken).mockReturnValue(null);
    await expect(methodApi.tokens('project-a')).rejects.toThrow('Sua sessão terminou. Entre novamente');
    expect(api.request).not.toHaveBeenCalled();
  });
});
