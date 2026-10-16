import { PrismaApi, type MethodTaskDto } from './api.js';
import { assertCreationStatus, getProjectRules, isTaskStatus, type TaskStatus } from './rules.js';

export interface MethodTask {
  id: string; titulo: string; status: TaskStatus; hu: string | null; arquivo: string | null;
  cobre: string[]; risco: string | null; pr: string | null; prisma_id: string;
}
export interface CreateTaskInput {
  id: string; titulo: string; hu: string; arquivo: string; cobre: string[] | string;
  risco: 'normal' | 'alto'; status?: TaskStatus;
}
function requireText(value: string, name: string): string {
  if (typeof value !== 'string' || !value.trim()) throw new Error(`${name} é obrigatório.`);
  return value.trim();
}
function requireTaskId(id: string): string {
  if (!/^T-\d{4,}$/.test(id)) throw new Error('ID da task inválido. Use T-0001.');
  return id;
}
export function toMethodTask(item: MethodTaskDto): MethodTask {
  requireTaskId(item.id);
  if (!isTaskStatus(item.status)) throw new Error('A API retornou uma task fora dos status do método. Pare e avise.');
  const prefix = `${item.id} · `;
  return { id: item.id, titulo: item.title.startsWith(prefix) ? item.title.slice(prefix.length) : item.title,
    status: item.status, hu: item.hu, arquivo: item.filePath, cobre: item.covers,
    risco: item.risk, pr: item.pr, prisma_id: item.workItemId };
}
export class TaskService {
  constructor(readonly api: PrismaApi = PrismaApi.fromEnv()) {}
  async initialize(): Promise<void> { await this.api.getMethodRules(); }
  async regrasDoProjeto() { return getProjectRules(await this.api.getMethodRules()); }
  async obterTask(id: string): Promise<MethodTask> {
    return toMethodTask(await this.api.getMethodTask(requireTaskId(id)));
  }
  async listarTasks(status?: TaskStatus): Promise<MethodTask[]> {
    if (status !== undefined && !isTaskStatus(status)) throw new Error('Status inválido. Consulte regras_do_projeto.');
    const seen = new Set<string>();
    return (await this.api.listMethodTasks(status)).map(toMethodTask).map(task => {
      if (seen.has(task.id)) throw new Error(`Chave ${task.id} duplicada na API. Pare e avise.`);
      seen.add(task.id); return task;
    }).filter(task => status === undefined || task.status === status).sort((a, b) => a.id.localeCompare(b.id));
  }
  async proximasTasks(): Promise<MethodTask[]> {
    return (await this.api.nextMethodTasks()).filter(task => task.authorized && task.status === 'em-desenvolvimento').map(toMethodTask);
  }
  async criarTask(input: CreateTaskInput): Promise<MethodTask> {
    requireTaskId(input.id);
    if (!/^HU-\d{4,}$/.test(input.hu)) throw new Error('HU inválida. Use HU-0001.');
    if (input.risco !== 'normal' && input.risco !== 'alto') throw new Error('Risco deve ser normal ou alto.');
    const identity = await this.api.initialize();
    const status = input.status ?? 'backlog';
    assertCreationStatus(identity.role, status);
    return toMethodTask(await this.api.createMethodTask({ id: input.id,
      title: requireText(input.titulo, 'Título'), hu: input.hu,
      filePath: requireText(input.arquivo, 'Arquivo'),
      covers: (typeof input.cobre === 'string' ? input.cobre.split(',') : input.cobre).map(x => x.trim()).filter(Boolean),
      risk: input.risco, status }));
  }
  async moverStatus(id: string, newStatus: TaskStatus, reason: string): Promise<MethodTask> {
    requireTaskId(id);
    if (!isTaskStatus(newStatus)) throw new Error('Novo status inválido. Consulte regras_do_projeto.');
    // A operação dedicada verifica matriz, bloqueio e ciclo dentro da transação.
    // Comentários ou estado local não concedem autorização nem liberação.
    return toMethodTask(await this.api.moveMethodTask(id, newStatus, requireText(reason, 'Motivo')));
  }
  async comentar(id: string, text: string) {
    const comment = await this.api.commentMethodTask(requireTaskId(id), requireText(text, 'Texto'));
    return { id, comentario_id: comment.id, texto: comment.text, autor: comment.author, criado_em: comment.createdAtUtc };
  }
  async vincularPr(id: string, url: string): Promise<MethodTask> {
    requireTaskId(id);
    let parsed: URL;
    try { parsed = new URL(requireText(url, 'URL do PR')); } catch { throw new Error('URL do PR inválida.'); }
    if (parsed.protocol !== 'https:' || parsed.username || parsed.password || parsed.search || parsed.hash) {
      throw new Error('URL do PR deve usar HTTPS, sem credenciais, parâmetros ou fragmentos.');
    }
    await this.api.linkMethodPullRequest(id, parsed.href);
    return this.obterTask(id);
  }
}
