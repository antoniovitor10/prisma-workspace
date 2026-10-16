export type PrismaRole = 'agente' | 'humano';
export interface PrismaConfig {
  url: string;
  email?: string;
  password?: string;
  token?: string;
  project: string;
  board: string;
  role: PrismaRole;
}

export function readConfig(env: NodeJS.ProcessEnv = process.env): PrismaConfig {
  const url = env.PRISMA_URL?.trim();
  const project = env.PRISMA_PROJECT?.trim();
  if (!url || !project) throw new Error('Configure PRISMA_URL e PRISMA_PROJECT.');
  let parsed: URL;
  try { parsed = new URL(url); } catch { throw new Error('PRISMA_URL inválida.'); }
  if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password || parsed.search || parsed.hash) {
    throw new Error('PRISMA_URL deve ser uma URL HTTP(S), sem credenciais, parâmetros ou fragmentos.');
  }
  const role = env.PRISMA_ROLE ?? 'agente';
  if (role !== 'agente' && role !== 'humano') throw new Error('PRISMA_ROLE deve ser agente ou humano.');
  if (!env.PRISMA_TOKEN && (!env.PRISMA_EMAIL || !env.PRISMA_PASSWORD)) {
    throw new Error('Configure PRISMA_TOKEN ou PRISMA_EMAIL e PRISMA_PASSWORD.');
  }
  return { url: url.replace(/\/+$/, ''), project, board: env.PRISMA_BOARD?.trim() || project,
    role, email: env.PRISMA_EMAIL, password: env.PRISMA_PASSWORD, token: env.PRISMA_TOKEN };
}
