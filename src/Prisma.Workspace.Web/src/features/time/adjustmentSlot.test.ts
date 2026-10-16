import { describe, expect, it } from 'vitest';
import { adjustmentSlot } from './adjustmentSlot';

describe('intervalo livre do ajuste', () => {
  const date = '2026-01-12';
  const now = Date.parse('2026-01-13T12:00:00Z');
  it('dois ajustes ficam adjacentes, sem sobreposição', () => {
    const first = adjustmentSlot(date, 30, [], now);
    const second = adjustmentSlot(date, 30, [first], now);
    expect(first.startedAt).toBe('2026-01-12T12:00:00.000Z');
    expect(second.startedAt).toBe(first.endedAt);
  });
  it('contorna timer atravessando meia-noite e intervalos sobrepostos legados', () => {
    const result = adjustmentSlot(date, 30, [
      { startedAt: '2026-01-11T22:00:00-03:00', endedAt: '2026-01-12T10:00:00-03:00' },
      { startedAt: '2026-01-12T09:00:00-03:00', endedAt: '2026-01-12T11:00:00-03:00' },
    ], now);
    expect(result.startedAt).toBe('2026-01-12T14:00:00.000Z');
  });
  it('antes das 09h usa período passado e nunca o futuro', () => {
    const early = Date.parse('2026-01-12T05:00:00Z');
    expect(adjustmentSlot(date, 30, [], early).endedAt).toBe('2026-01-12T03:30:00.000Z');
    expect(() => adjustmentSlot(date, 180, [], early)).toThrow(/intervalo livre/);
  });
  it('recusa dia cheio e duração inválida', () => {
    expect(() => adjustmentSlot(date, 1, [{ startedAt: `${date}T00:00:00-03:00`, endedAt: '2026-01-13T00:00:00-03:00' }], now)).toThrow(/intervalo livre/);
    expect(() => adjustmentSlot(date, -1, [], now)).toThrow(/positiva/);
  });
});
