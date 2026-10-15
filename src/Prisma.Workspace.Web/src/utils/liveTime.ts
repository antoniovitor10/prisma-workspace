/** Os totais da API já incluem o intervalo aberto até calculatedAt. */
export function liveTimeSeconds(total: number, calculatedAt: string | undefined, startedAt: string | null, now: number) {
  if (!startedAt) return total;
  const start = Date.parse(startedAt);
  // Sem snapshot, preservar o total recebido evita somar duas vezes em dados antigos.
  const snapshot = calculatedAt ? Date.parse(calculatedAt) : now;
  if (!Number.isFinite(start) || !Number.isFinite(snapshot)) return total;
  return total + Math.max(0, Math.floor((now - Math.max(start, snapshot)) / 1000));
}
