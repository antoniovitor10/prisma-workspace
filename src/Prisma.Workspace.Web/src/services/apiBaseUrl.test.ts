import { describe, expect, it } from 'vitest';
import { resolveApiBaseUrl } from './api';

/**
 * Esta trava existe por um defeito real em produção: o bundle publicado chamava
 * `http://localhost:5216`, endereço da máquina de quem abria a página. O Chrome pedia
 * permissão de rede local e o login morria com "Failed to fetch".
 */
describe('base da API', () => {
  it('usa a própria origem em produção quando nada foi configurado', () => {
    expect(resolveApiBaseUrl(undefined, true)).toBe('');
  });

  it('nunca aponta para localhost num build de produção sem configuração', () => {
    expect(resolveApiBaseUrl(undefined, true)).not.toMatch(/localhost|127\.0\.0\.1/);
  });

  it('aponta para a API local em desenvolvimento, onde a SPA roda em outra porta', () => {
    expect(resolveApiBaseUrl(undefined, false)).toBe('http://localhost:5216');
  });

  it('respeita VITE_API_URL nos dois ambientes, para SPA e API em hosts separados', () => {
    expect(resolveApiBaseUrl('https://api.exemplo.test', true)).toBe('https://api.exemplo.test');
    expect(resolveApiBaseUrl('https://api.exemplo.test', false)).toBe('https://api.exemplo.test');
  });

  it('aceita string vazia como escolha explícita de mesma origem', () => {
    expect(resolveApiBaseUrl('', false)).toBe('');
  });
});
