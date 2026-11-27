import { describe, expect, it } from 'vitest';
import { bodyWithCardLine, hasTaskPrefix } from './pullRequestEligibility';

describe('texto do PR', () => {
  it('prefixo e linha do card', () => {
    expect(hasTaskPrefix('[T-0028] Título', 'T-0028')).toBe(true);
    expect(hasTaskPrefix('Título', 'T-0028')).toBe(false);
    expect(bodyWithCardLine('Corpo\n', 'https://x/c')).toBe('Corpo\n\nTask no Prisma: https://x/c');
    expect(bodyWithCardLine('Corpo\n\nTask no Prisma: https://x/c', 'https://x/c')).toBe('Corpo\n\nTask no Prisma: https://x/c');
    expect(bodyWithCardLine('', 'https://x/c')).toBe('Task no Prisma: https://x/c');
  });
});
