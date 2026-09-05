import { afterEach, describe, expect, it } from 'vitest';
import { api } from './api';

describe('api tenant context', () => {
  afterEach(() => localStorage.clear());

  it('envia a organização ativa em todas as requisições autenticadas', () => {
    api.setToken('header.payload.signature');
    api.setOrganizationId('11111111-1111-4111-8111-111111111111');

    expect(api.headers()).toMatchObject({
      Authorization: 'Bearer header.payload.signature',
      'X-Organization-Id': '11111111-1111-4111-8111-111111111111',
    });
  });
});
