import { describe, expect, it } from 'vitest';
import { resolveUserDisplayLabel, userDisplayLabel } from './userDisplayName';

describe('userDisplayLabel', () => {
  it('prioriza o nome funcional da organização', () => {
    expect(userDisplayLabel({
      id: 'user-12345678',
      displayName: ' Gabriel Tavares ',
      userName: 'gabriel.tavares@detran.se.gov.br',
      email: 'gabriel.tavares@detran.se.gov.br',
    })).toBe('Gabriel Tavares');
  });

  it('nunca usa e-mail ou username em formato de e-mail como rótulo', () => {
    expect(userDisplayLabel({
      id: 'abcdefgh-1234',
      userName: 'gabriel.tavares@detran.se.gov.br',
      email: 'gabriel.tavares@detran.se.gov.br',
    })).toBe('Usuário abcdefgh');
  });

  it('aceita username legado somente quando ele já é um nome não sensível', () => {
    expect(userDisplayLabel({ id: 'user-1', userName: 'Gabriel Tavares', email: 'gabriel@detran.se.gov.br' }))
      .toBe('Gabriel Tavares');
  });

  it('substitui um snapshot antigo de e-mail pelo nome funcional atual', () => {
    const users = [{ id: 'user-12345678', displayName: 'Gabriel Tavares', userName: 'gabriel@detran.se.gov.br', email: 'gabriel@detran.se.gov.br' }];
    expect(resolveUserDisplayLabel(users, 'gabriel@detran.se.gov.br', 'user-12345678')).toBe('Gabriel Tavares');
  });
});
