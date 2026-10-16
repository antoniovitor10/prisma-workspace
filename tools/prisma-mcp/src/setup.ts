import { PrismaApi } from './api.js';
import type { MetodoStatus } from './mapping.js';

export interface ProjetoSetup {
  projectId: string;
  boardId: string;
  stages: Map<MetodoStatus, string>;
  fields: Map<string, string>;
}

export async function ensureProjeto(api: PrismaApi = PrismaApi.fromEnv()): Promise<ProjetoSetup> {
  throw new Error('Implementação em andamento.');
}
