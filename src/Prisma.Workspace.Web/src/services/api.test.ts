import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

describe('api tenant context', () => {
  afterEach(() => { vi.restoreAllMocks(); localStorage.clear(); });

  it('envia a organização ativa em todas as requisições autenticadas', () => {
    api.setToken('header.payload.signature');
    api.setOrganizationId('11111111-1111-4111-8111-111111111111');

    expect(api.headers()).toMatchObject({
      Authorization: 'Bearer header.payload.signature',
      'X-Organization-Id': '11111111-1111-4111-8111-111111111111',
    });
  });

  it('consulta responsáveis elegíveis no projeto informado', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('[]', { status: 200 }));
    await api.getAssignableUsers('project/with spaces');
    expect(fetchMock.mock.calls[0]?.[0]).toContain('/api/Users/assignable?projectId=project%2Fwith%20spaces');
  });
});

describe('api installation setup', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('envia o token exclusivamente no header dedicado', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(
      JSON.stringify({ initialized: true }),
      { status: 201, headers: { 'Content-Type': 'application/json' } },
    ));

    await api.completeSetup('codigo-secreto', {
      administratorName: 'Ana Silva',
      administratorEmail: 'ana@example.com',
      administratorPassword: 'SenhaForte#123',
      organizationName: 'Equipe Prisma',
      organizationSlug: 'equipe-prisma',
    });

    const [url, request] = fetchMock.mock.calls[0];
    expect(url).toBe(`${api.baseUrl}/api/setup`);
    expect(new Headers(request?.headers).get('X-Prisma-Setup-Token')).toBe('codigo-secreto');
    expect(request?.body).not.toContain('codigo-secreto');
    expect(localStorage.getItem('codigo-secreto')).toBeNull();
  });

  it('lê o código estável no campo type do ProblemDetails', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(
      JSON.stringify({
        type: 'setup_unavailable',
        title: 'Configuração indisponível',
        detail: 'O setup não está disponível.',
      }),
      { status: 403, headers: { 'Content-Type': 'application/problem+json' } },
    ));

    await expect(api.completeSetup('codigo-secreto', {
      administratorName: 'Ana Silva',
      administratorEmail: 'ana@example.com',
      administratorPassword: 'SenhaForte#123',
      organizationName: 'Equipe Prisma',
      organizationSlug: 'equipe-prisma',
    })).rejects.toMatchObject({
      status: 403,
      code: 'setup_unavailable',
    });
  });
});
