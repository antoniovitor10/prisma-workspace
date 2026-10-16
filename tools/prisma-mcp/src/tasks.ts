import { PrismaApi, type WorkItemDto, type WorkItemDetailsDto } from './api.js';
import { FIELD_NAMES } from './mapping.js';
import { ensureHuTag, ensureProjeto, type ProjetoSetup } from './setup.js';
import {
  BLOCK_MARKER, STATUS_PREFIX, assertCreationStatus, assertTransition,
  getProjectRules, isTaskStatus, parseRole, type PrismaRole, type TaskStatus,
} from './rules.js';

export interface MethodTask {
  id: string;
  titulo: string;
  status: TaskStatus;
  hu: string | null;
  arquivo: string | null;
  cobre: string[];
  risco: string | null;
  pr: string | null;
  prisma_id: string;
}

export interface CreateTaskInput {
  id: string;
  titulo: string;
  hu: string;
  arquivo: string;
  cobre: string[] | string;
  risco: 'normal' | 'alto';
  status?: TaskStatus;
}

function requireText(value: string, name: string): string {
  if (typeof value !== 'string' || !value.trim()) throw new Error(`${name} é obrigatório.`);
  return value.trim();
}

function requireTaskId(id: string): string {
  if (!/^T-\d{4,}$/.test(id)) throw new Error('ID da task inválido. Use T-0001.');
  return id;
}

/** Comentários comuns não podem se passar por registros de transição. */
function assertOrdinaryText(text: string): void {
  if (text.includes(STATUS_PREFIX) || text.includes(BLOCK_MARKER)) {
    throw new Error('Os marcadores de status e bloqueio são reservados a mover_status.');
  }
}

export class TaskService {
  readonly role: PrismaRole;
  private setupPromise?: Promise<ProjetoSetup>;
  private mutationQueue: Promise<unknown> = Promise.resolve();

  constructor(readonly api: PrismaApi = PrismaApi.fromEnv()) {
    this.role = parseRole(api.config.role);
  }

  regrasDoProjeto() {
    return { projeto: this.api.config.project, quadro: this.api.config.board, ...getProjectRules(this.role) };
  }

  private setup(): Promise<ProjetoSetup> {
    if (!this.setupPromise) {
      this.setupPromise = ensureProjeto(this.api).catch(error => {
        this.setupPromise = undefined;
        throw error;
      });
    }
    return this.setupPromise;
  }

  // Evita duas chamadas concorrentes no mesmo servidor disputarem a chave ou o bloqueio.
  private mutate<T>(operation: () => Promise<T>): Promise<T> {
    const result = this.mutationQueue.then(operation);
    this.mutationQueue = result.catch(() => undefined);
    return result;
  }

  private fieldId(setup: ProjetoSetup, name: string): string {
    const id = setup.fields.get(name);
    if (!id) throw new Error(`Campo personalizado ausente no projeto: ${name}.`);
    return id;
  }

  private field(item: WorkItemDto, setup: ProjetoSetup, name: string): string | null {
    const id = this.fieldId(setup, name);
    return item.customFields.find(value => value.fieldId === id)?.value?.trim() || null;
  }

  private methodId(item: WorkItemDto, setup: ProjetoSetup): string | null {
    return this.field(item, setup, FIELD_NAMES.id);
  }

  private status(item: WorkItemDto, setup: ProjetoSetup): TaskStatus {
    for (const [status, stageId] of setup.stages) {
      if (item.stageId === stageId) return status;
    }
    throw new Error(`A tarefa ${this.methodId(item, setup) ?? item.id} está fora das colunas do método. Confira o quadro no Prisma.`);
  }

  private async items(setup: ProjetoSetup): Promise<WorkItemDto[]> {
    return (await this.api.listWorkItemsByBoard(setup.boardId))
      .filter(item => item.boardId === setup.boardId && item.parentId === null && !item.isArchived);
  }

  private find(id: string, items: WorkItemDto[], setup: ProjetoSetup): WorkItemDto {
    const matches = items.filter(item => this.methodId(item, setup) === id);
    if (!matches.length) throw new Error(`${id} não encontrada no projeto ${this.api.config.project}.`);
    if (matches.length !== 1) throw new Error(`Chave ${id} duplicada no campo ${FIELD_NAMES.id}. Corrija no Prisma antes de continuar.`);
    return matches[0];
  }

  private toTask(item: WorkItemDto, setup: ProjetoSetup): MethodTask {
    const id = this.methodId(item, setup);
    if (!id || !/^T-\d{4,}$/.test(id)) throw new Error('O item não possui ID de task do método.');
    const prefix = `${id} · `;
    return {
      id,
      titulo: item.title.startsWith(prefix) ? item.title.slice(prefix.length) : item.title,
      status: this.status(item, setup),
      hu: this.field(item, setup, FIELD_NAMES.hu),
      arquivo: this.field(item, setup, FIELD_NAMES.arquivo),
      cobre: (this.field(item, setup, FIELD_NAMES.cobre) ?? '').split(',').map(value => value.trim()).filter(Boolean),
      risco: this.field(item, setup, FIELD_NAMES.risco),
      pr: this.field(item, setup, FIELD_NAMES.pr),
      prisma_id: item.id,
    };
  }

  async obterTask(id: string): Promise<MethodTask> {
    requireTaskId(id);
    const setup = await this.setup();
    const items = await this.items(setup);
    return this.toTask(this.find(id, items, setup), setup);
  }

  async listarTasks(status?: TaskStatus): Promise<MethodTask[]> {
    if (status !== undefined && !isTaskStatus(status)) throw new Error('Status inválido. Consulte regras_do_projeto.');
    const setup = await this.setup();
    const items = await this.items(setup);
    const seen = new Set<string>();
    return items.filter(item => /^T-\d{4,}$/.test(this.methodId(item, setup) ?? ''))
      .map(item => {
        const task = this.toTask(item, setup);
        if (seen.has(task.id)) throw new Error(`Chave ${task.id} duplicada no campo ${FIELD_NAMES.id}.`);
        seen.add(task.id);
        return task;
      })
      .filter(task => status === undefined || task.status === status)
      .sort((a, b) => a.id.localeCompare(b.id));
  }

  async proximasTasks(): Promise<MethodTask[]> {
    const setup = await this.setup();
    const authorized = await this.listarTasks('em-desenvolvimento');
    const ready: MethodTask[] = [];
    const details = new Map<string, WorkItemDetailsDto>();
    const load = async (id: string) => {
      let item = details.get(id);
      if (!item) { item = await this.api.getWorkItem(id); details.set(id, item); }
      return item;
    };
    for (const task of authorized) {
      const item = await load(task.prisma_id);
      // DependsOn sai da task; Blocks entra nela. Outros vínculos não são dependências.
      const dependencies = item.links.filter(link =>
        (link.type === 1 && !link.isIncoming) || (link.type === 2 && link.isIncoming));
      let allCompleted = true;
      for (const link of dependencies) {
        const dependency = await load(link.relatedWorkItemId);
        if (dependency.isArchived || dependency.stageId !== setup.stages.get('concluida')) {
          allCompleted = false;
          break;
        }
      }
      // Revalida o status lido no detalhe, sem confiar numa listagem anterior.
      if (item.stageId === setup.stages.get('em-desenvolvimento') && allCompleted) ready.push(task);
    }
    return ready;
  }

  criarTask(input: CreateTaskInput): Promise<MethodTask> {
    return this.mutate(async () => {
      const id = requireTaskId(input.id);
      const title = requireText(input.titulo, 'Título');
      const file = requireText(input.arquivo, 'Arquivo');
      if (!/^HU-\d{4,}$/.test(input.hu)) throw new Error('HU inválida. Use HU-0001.');
      if (input.risco !== 'normal' && input.risco !== 'alto') throw new Error('Risco deve ser normal ou alto.');
      const status = input.status ?? 'backlog';
      assertCreationStatus(this.role, status);
      const setup = await this.setup();
      const items = await this.items(setup);
      if (items.some(item => this.methodId(item, setup) === id)) throw new Error(`${id} já existe. Use obter_task.`);
      const huFieldId = this.fieldId(setup, FIELD_NAMES.hu);
      const huTagId = await ensureHuTag(this.api, input.hu);
      const stageId = setup.stages.get(status);
      if (!stageId) throw new Error(`Coluna ausente para ${status}.`);
      const workItemId = await this.api.createWorkItem({
        boardId: setup.boardId, stageId, parentId: null, title: `${id} · ${title}`, kind: 5,
      });
      const covers = typeof input.cobre === 'string' ? input.cobre : input.cobre.join(', ');
      try {
        await this.api.setCustomFields(workItemId, {
          [this.fieldId(setup, FIELD_NAMES.id)]: id,
          [huFieldId]: input.hu,
          [this.fieldId(setup, FIELD_NAMES.arquivo)]: file,
          [this.fieldId(setup, FIELD_NAMES.cobre)]: covers,
          [this.fieldId(setup, FIELD_NAMES.risco)]: input.risco,
        });
        await this.api.addWorkItemTag(workItemId, huTagId);
      } catch {
        throw new Error(`O card ${workItemId} foi criado, mas a gravação dos campos ou da tag HU falhou. Pare e avise; confira esse card antes de repetir criar_task.`);
      }
      return this.obterTask(id);
    });
  }

  moverStatus(id: string, newStatus: TaskStatus, reason: string): Promise<MethodTask> {
    return this.mutate(async () => {
      requireTaskId(id);
      if (!isTaskStatus(newStatus)) throw new Error('Novo status inválido. Consulte regras_do_projeto.');
      const motive = requireText(reason, 'Motivo');
      assertOrdinaryText(motive);
      const setup = await this.setup();
      const items = await this.items(setup);
      const item = this.find(id, items, setup);
      const from = this.status(item, setup);
      if (from === newStatus) return this.toTask(item, setup);
      let ownBlock = false;
      if (this.role === 'agente' && from === 'bloqueada' && newStatus === 'em-desenvolvimento') {
        await this.api.login();
        const comments = await this.api.listComments(item.id);
        const graph = await this.api.getStateGraph(item.id);
        const lastMove = graph.edges.at(-1);
        // O marcador referencia a entrada atual do histórico: bloqueios antigos não autorizam uma nova volta.
        const latest = comments.filter(comment => comment.text.startsWith(`${STATUS_PREFIX} `)).at(-1);
        ownBlock = Boolean(latest && this.api.userId && latest.userId === this.api.userId
          && latest.text.startsWith(`${STATUS_PREFIX} em-desenvolvimento -> bloqueada · agente · mcp · `)
          && lastMove && lastMove.target === item.stageId && lastMove.actorId === this.api.userId
          && latest.text.endsWith(`\n${BLOCK_MARKER} ${lastMove.id}`));
      }
      assertTransition(this.role, from, newStatus, ownBlock);
      const stageId = setup.stages.get(newStatus);
      if (!stageId) throw new Error(`Coluna ausente para ${newStatus}.`);
      await this.api.moveWorkItem(item.id, stageId);
      try {
        let marker = '';
        if (this.role === 'agente' && newStatus === 'bloqueada') {
          const lastMove = (await this.api.getStateGraph(item.id)).edges.at(-1);
          if (!lastMove || lastMove.target !== stageId || lastMove.actorId !== this.api.userId) {
            throw new Error('Não foi possível confirmar a autoria da movimentação no histórico.');
          }
          marker = `\n${BLOCK_MARKER} ${lastMove.id}`;
        }
        await this.api.addComment(item.id, `${STATUS_PREFIX} ${from} -> ${newStatus} · ${this.role} · mcp · ${motive}${marker}`);
      } catch {
        throw new Error(`O status de ${id} mudou para ${newStatus}, mas o comentário de histórico falhou. Pare e avise; confira a tarefa no Prisma antes de continuar.`);
      }
      return this.obterTask(id);
    });
  }

  comentar(id: string, text: string) {
    return this.mutate(async () => {
      const task = await this.obterTask(id);
      const content = requireText(text, 'Texto');
      assertOrdinaryText(content);
      const comment = await this.api.addComment(task.prisma_id, content);
      return { id, comentario_id: comment.id, texto: comment.text, autor: comment.displayName, criado_em: comment.createdAt };
    });
  }

  vincularPr(id: string, url: string): Promise<MethodTask> {
    return this.mutate(async () => {
      const link = requireText(url, 'URL do PR');
      let parsed: URL;
      try { parsed = new URL(link); } catch { throw new Error('URL do PR inválida.'); }
      if (!['https:', 'http:'].includes(parsed.protocol) || parsed.username || parsed.password) {
        throw new Error('URL do PR deve usar HTTP(S), sem credenciais.');
      }
      assertOrdinaryText(link);
      const task = await this.obterTask(id);
      const setup = await this.setup();
      await this.api.setCustomFields(task.prisma_id, { [this.fieldId(setup, FIELD_NAMES.pr)]: link });
      try {
        await this.api.addComment(task.prisma_id, `[pr] ${link} · ${this.role} · mcp`);
      } catch {
        throw new Error(`O PR foi vinculado a ${id}, mas o comentário falhou. Pare e avise; confira a tarefa no Prisma.`);
      }
      return this.obterTask(id);
    });
  }
}
