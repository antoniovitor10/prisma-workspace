/** Mantém a posição sem permitir que um frame antigo reverta uma navegação. */
export function subscribeBacklogScroll(ownerUrl: string, persist: (position: number) => void) {
  let frame = 0;
  const belongsToOwner = () => window.location.pathname + window.location.search === ownerUrl;
  const save = () => {
    if (!belongsToOwner()) return;
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(() => {
      if (!belongsToOwner()) return;
      persist(Math.max(0, Math.round(window.scrollY)));
    });
  };
  window.addEventListener('scroll', save, { passive: true });
  return () => { window.removeEventListener('scroll', save); cancelAnimationFrame(frame); };
}
