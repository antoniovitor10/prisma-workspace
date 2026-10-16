interface Period { startedAt: string; endedAt?: string | null; }

/** Escolhe um intervalo livre no dia, em Brasília, sem criar horas futuras. */
export function adjustmentSlot(date: string, minutes: number, entries: Period[], now = Date.now()) {
  if (!Number.isFinite(minutes) || minutes <= 0) throw new Error('Informe uma duração positiva.');
  const dayStart = Date.parse(`${date}T00:00:00-03:00`);
  const dayEnd = Math.min(dayStart + 24 * 3600_000, now);
  const duration = minutes * 60_000;
  const occupied = entries.map(e => [Date.parse(e.startedAt), e.endedAt ? Date.parse(e.endedAt) : now])
    .filter(([start, end]) => end > dayStart && start < dayEnd).sort((a, b) => a[0] - b[0]);
  const find = (initial: number) => {
    let candidate = initial;
    for (const [start, end] of occupied) {
      if (end <= candidate) continue;
      if (candidate + duration <= start) break;
      candidate = Math.max(candidate, end);
    }
    return candidate + duration <= dayEnd ? candidate : null;
  };
  // Mantém a preferência anterior pelas 09h, mas nunca reutiliza um período ocupado.
  const start = find(dayStart + 9 * 3600_000) ?? find(dayStart);
  if (start === null) throw new Error('Não há um intervalo livre com essa duração neste dia até agora.');
  return { startedAt: new Date(start).toISOString(), endedAt: new Date(start + duration).toISOString() };
}
