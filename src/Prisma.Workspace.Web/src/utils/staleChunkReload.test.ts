import { describe, expect, it, vi } from 'vitest';
import { installStaleChunkReload, STALE_CHUNK_RELOAD_KEY } from './staleChunkReload';

function setup(start = 1_000_000) {
  const target = new EventTarget() as EventTarget & { location: { reload: () => void } };
  const reload = vi.fn();
  target.location = { reload };
  const values = new Map<string, string>();
  const storage = { getItem: (k: string) => values.get(k) ?? null, setItem: (k: string, v: string) => { values.set(k, v); } };
  let clock = start;
  installStaleChunkReload(target, storage, () => clock);
  const fire = () => { const event = new Event('vite:preloadError', { cancelable: true }); target.dispatchEvent(event); return event; };
  return { reload, values, fire, advance: (ms: number) => { clock += ms; } };
}

describe('installStaleChunkReload', () => {
  it('recarrega a página quando um pedaço da versão anterior não existe mais', () => {
    const { reload, fire, values } = setup();
    const event = fire();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(event.defaultPrevented).toBe(true);
    expect(values.get(STALE_CHUNK_RELOAD_KEY)).toBe('1000000');
  });

  it('não entra em laço: uma segunda falha logo depois da recarga deixa o erro aparecer', () => {
    const { reload, fire, advance } = setup();
    fire();
    advance(3_000);
    const second = fire();
    expect(reload).toHaveBeenCalledTimes(1);
    expect(second.defaultPrevented).toBe(false);
  });

  it('volta a recarregar num deploy seguinte', () => {
    const { reload, fire, advance } = setup();
    fire();
    advance(60_000);
    fire();
    expect(reload).toHaveBeenCalledTimes(2);
  });
});
