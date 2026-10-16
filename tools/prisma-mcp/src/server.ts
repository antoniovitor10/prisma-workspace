#!/usr/bin/env node
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { z } from 'zod';
import { METHOD_STATUSES } from './rules.js';
import { TaskService, type MethodTask } from './tasks.js';

const taskId = z.string().regex(/^T-\d{4,}$/, 'Use o ID do método, como T-0001.');
const status = z.enum(METHOD_STATUSES);
const text = z.string().trim().min(1, 'Informe um texto não vazio.');

function taskSummary(task: MethodTask): string {
  return `${task.id} · ${task.titulo} · ${task.status}\nArquivo: ${task.arquivo ?? 'não informado'} · HU: ${task.hu ?? 'não informada'} · PR: ${task.pr ?? 'não vinculado'}`;
}

function listSummary(tasks: MethodTask[], empty: string): string {
  return tasks.length ? tasks.map(task => `${task.id} · ${task.titulo} · ${task.status}`).join('\n') : empty;
}

/** Não registra credenciais nem deixa texto de erro transportá-las ao cliente. */
function safeError(error: unknown, service: TaskService): string {
  let message = error instanceof Error ? error.message : 'Falha ao executar a operação no Prisma. Pare e avise.';
  for (const secret of [service.api.config.token]) {
    if (secret) message = message.split(secret).join('[removido]');
  }
  return message.replace(/Bearer\s+\S+/gi, 'Bearer [removido]');
}

export function createMcpServer(service: TaskService = new TaskService()): McpServer {
  const server = new McpServer({ name: 'prisma-metodo', version: '0.1.0' }, {
    instructions: 'Leia regras_do_projeto no início da sessão. O Prisma é a verdade do status. Se o MCP não responder, pare e avise.',
  });
  const execute = async (operation: () => Promise<{ summary: string; data: Record<string, unknown> }>): Promise<CallToolResult> => {
    try {
      const { summary, data } = await operation();
      return { content: [{ type: 'text', text: `${summary}\n\n${JSON.stringify(data)}` }], structuredContent: data };
    } catch (error) {
      const message = safeError(error, service);
      return { isError: true, content: [{ type: 'text', text: message }], structuredContent: { erro: message } };
    }
  };
  const readOnly = { readOnlyHint: true, destructiveHint: false, openWorldHint: false };
  const write = { readOnlyHint: false, destructiveHint: false, openWorldHint: false };

  server.registerTool('regras_do_projeto', {
    description: 'Leia no início da sessão: papel atual, matriz de status, autonomia e convenções do método.',
    inputSchema: {}, annotations: readOnly,
  }, () => execute(async () => {
    const rules = await service.regrasDoProjeto();
    return {
      summary: `Projeto: ${rules.projeto} · Papel: ${rules.papel}\n${rules.condicoes.join('\n')}\n${rules.convencoes.join('\n')}`,
      data: rules,
    };
  }));

  server.registerTool('obter_task', {
    description: 'Consulta uma task pela chave T-XXXX no campo ID do metodo: status, título, arquivo, HU e PR.',
    inputSchema: { id: taskId }, annotations: readOnly,
  }, ({ id }) => execute(async () => {
    const task = await service.obterTask(id);
    return { summary: taskSummary(task), data: { task } };
  }));

  server.registerTool('listar_tasks', {
    description: 'Lista tasks do projeto configurado; permite filtrar pelo status do método.',
    inputSchema: { status: status.optional() }, annotations: readOnly,
  }, ({ status }) => execute(async () => {
    const tasks = await service.listarTasks(status);
    return { summary: listSummary(tasks, 'Nenhuma task encontrada.'), data: { tasks, total: tasks.length } };
  }));

  server.registerTool('proximas_tasks', {
    description: 'Lista tasks autorizadas (em-desenvolvimento) cujas dependências estão todas concluídas.',
    inputSchema: {}, annotations: readOnly,
  }, () => execute(async () => {
    const tasks = await service.proximasTasks();
    return { summary: listSummary(tasks, 'Nenhuma task autorizada com todas as dependências concluídas.'), data: { tasks, total: tasks.length } };
  }));

  server.registerTool('criar_task', {
    description: 'Cria uma task de topo no quadro, com HU em campo personalizado e tag de mesmo nome. Agente pode criar em backlog (padrão) ou concluida (atalho/incidente).',
    inputSchema: {
      id: taskId, titulo: text, hu: z.string().regex(/^HU-\d{4,}$/, 'Use HU-0001.'),
      arquivo: text, cobre: z.union([z.array(text), text]), risco: z.enum(['normal', 'alto']),
      status: status.optional(),
    }, annotations: write,
  }, input => execute(async () => {
    const task = await service.criarTask(input);
    return { summary: `Task criada.\n${taskSummary(task)}`, data: { task } };
  }));

  server.registerTool('mover_status', {
    description: 'Move conforme a matriz do papel e registra motivo no histórico. A API verifica autorização e liberação humana de retomada por principal e ciclo.',
    inputSchema: { id: taskId, novo_status: status, motivo: text }, annotations: write,
  }, ({ id, novo_status, motivo }) => execute(async () => {
    const task = await service.moverStatus(id, novo_status, motivo);
    return { summary: `Status confirmado.\n${taskSummary(task)}`, data: { task } };
  }));

  server.registerTool('comentar', {
    description: 'Registra progresso, relatório de execução, bloqueios ou handoff na task.',
    inputSchema: { id: taskId, texto: text }, annotations: write,
  }, ({ id, texto }) => execute(async () => {
    const comment = await service.comentar(id, texto);
    return { summary: `Comentário registrado em ${id}: ${comment.texto}`, data: { comentario: comment } };
  }));

  server.registerTool('vincular_pr', {
    description: 'Vincula o PR no backend e grava campo e comentário na mesma transação.',
    inputSchema: { id: taskId, url: z.string().url('Informe uma URL válida para o PR.') }, annotations: write,
  }, ({ id, url }) => execute(async () => {
    const task = await service.vincularPr(id, url);
    return { summary: `PR vinculado.\n${taskSummary(task)}`, data: { task } };
  }));
  return server;
}

export async function startServer(): Promise<void> {
  const service = new TaskService();
  await service.initialize();
  const server = createMcpServer(service);
  const transport = new StdioServerTransport();
  for (const signal of ['SIGINT', 'SIGTERM'] as const) {
    process.once(signal, () => { void server.close().finally(() => process.exit(0)); });
  }
  await server.connect(transport);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  startServer().catch(() => {
    process.stderr.write('Não foi possível iniciar o MCP Prisma. Confira as variáveis PRISMA_URL, PRISMA_PROJECT e as credenciais; pare e avise.\n');
    process.exitCode = 1;
  });
}
