export interface DisplayUser {
  id: string;
  displayName?: string | null;
  userName?: string | null;
  email?: string | null;
}

export const looksLikeEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);

export function userDisplayLabel(user: DisplayUser): string {
  const displayName = user.displayName?.trim();
  if (displayName) return displayName;

  const userName = user.userName?.trim();
  const email = user.email?.trim();
  if (userName && !looksLikeEmail(userName) && userName.toLocaleLowerCase() !== email?.toLocaleLowerCase()) {
    return userName;
  }

  const normalizedId = user.id.trim();
  return `Usuário ${normalizedId.slice(0, 8) || 'sem-id'}`;
}

export function resolveUserDisplayLabel(
  users: DisplayUser[],
  reference?: string | null,
  fallbackId?: string | null,
): string {
  const normalizedReference = reference?.trim().toLocaleLowerCase();
  const normalizedFallbackId = fallbackId?.trim().toLocaleLowerCase();
  const match = users.find((user) => {
    const aliases = [user.id, user.displayName, user.userName, user.email]
      .filter((value): value is string => Boolean(value?.trim()))
      .map(value => value.trim().toLocaleLowerCase());
    return Boolean(
      (normalizedReference && aliases.includes(normalizedReference))
      || (normalizedFallbackId && aliases.includes(normalizedFallbackId))
    );
  });
  if (match) return userDisplayLabel(match);

  const cleanReference = reference?.trim();
  if (cleanReference && !looksLikeEmail(cleanReference)) return cleanReference;
  return userDisplayLabel({ id: fallbackId?.trim() || cleanReference || 'sem-id' });
}
