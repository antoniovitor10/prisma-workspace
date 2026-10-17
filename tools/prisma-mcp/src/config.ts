export type PrismaRole = 'agente' | 'humano' | 'sistema';
export interface PrismaConfig {
  url: string;
  token: string;
  project: string;
  board?: string;
  /** Expectativa opcional; a identidade da API é a autoridade. */
  role?: PrismaRole;
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
  const role = env.PRISMA_ROLE?.trim() || undefined;
  if (role !== undefined && !['agente', 'humano', 'sistema'].includes(role)) throw new Error('PRISMA_ROLE deve ser agente, humano ou sistema.');
  const token = env.PRISMA_TOKEN?.trim();
  if (!token || !/^pmt_[A-Za-z0-9_-]{43}$/.test(token)) {
    throw new Error('Configure PRISMA_TOKEN com o token opaco emitido pela API do método. JWT de login e email/senha não são aceitos. Pare e avise.');
  }
  return { url: url.replace(/\/+$/, ''), project, board: env.PRISMA_BOARD?.trim() || undefined,
    role: role as PrismaRole | undefined, token };
}
