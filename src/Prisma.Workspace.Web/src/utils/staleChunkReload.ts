/**
 * Depois de um deploy, os arquivos JS da versão anterior deixam de existir no servidor.
 * Quem estava com o Prisma aberto e navega para uma página carregada sob demanda recebe
 * "Failed to fetch dynamically imported module" e a tela fica em branco. O Vite avisa essa
 * falha pelo evento `vite:preloadError`; recarregar busca o index.html novo (servido sem cache)
 * com os arquivos da versão atual. A trava evita laço de recarga se o erro for outro.
 */
export const STALE_CHUNK_RELOAD_KEY = 'prisma_stale_chunk_reload_at';
const RELOAD_GUARD_MS = 10_000;

interface ReloadTarget {
  addEventListener(type: string, listener: (event: Event) => void): void;
  location: { reload(): void };
}

export function installStaleChunkReload(
  target: ReloadTarget = window,
  storage: Pick<Storage, 'getItem' | 'setItem'> | null = safeSessionStorage(),
  now: () => number = Date.now,
): void {
  target.addEventListener('vite:preloadError', event => {
    let last = 0;
    try { last = Number(storage?.getItem(STALE_CHUNK_RELOAD_KEY) ?? 0) || 0; } catch { last = 0; }
    if (now() - last < RELOAD_GUARD_MS) return; // já recarregou agora há pouco: deixa o erro aparecer
    try { storage?.setItem(STALE_CHUNK_RELOAD_KEY, String(now())); } catch { /* sem storage: recarrega mesmo assim */ }
    event.preventDefault();
    target.location.reload();
  });
}

function safeSessionStorage(): Storage | null {
  try { return window.sessionStorage; } catch { return null; }
}
