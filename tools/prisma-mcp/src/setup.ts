import { PrismaApi } from './api.js';
import { FIELD_NAMES, STATUSES, STATUS_MAP, type MetodoStatus } from './mapping.js';

export interface ProjetoSetup {
  projectId: string;
  boardId: string;
  stages: Map<MetodoStatus, string>;
  fields: Map<string, string>;
}

export async function ensureProjeto(api: PrismaApi = PrismaApi.fromEnv()): Promise<ProjetoSetup> {
  await api.login();
  const projects = (await api.listProjects()).filter(x => x.name === api.config.project && !x.isArchived);
  if (projects.length > 1) throw new Error('Mais de um projeto tem o nome PRISMA_PROJECT. Resolva a ambiguidade no Prisma.');
  const projectId = projects[0]?.id ?? await api.createProject(api.config.project);
  const boards = (await api.listBoards()).filter(x => x.projectId === projectId && x.name === api.config.board);
  if (boards.length > 1) throw new Error('Mais de um quadro tem o nome PRISMA_BOARD neste projeto.');
  const boardId = boards[0]?.id ?? await api.createBoard(projectId, api.config.board);
  let columns = await api.listStages(boardId);
  // Um quadro novo recebe três colunas padrão da API. Reaproveitá-las evita
  // exclusão e mantém exatamente as sete colunas do contrato.
  const defaults: Record<string, MetodoStatus> = {
    'A fazer': 'backlog', 'Em andamento': 'em-desenvolvimento', 'Concluído': 'concluida',
  };
  const unknown = columns.filter(x => !STATUSES.some(s => STATUS_MAP[s].name === x.name));
  if (unknown.length && (unknown.some(x => !defaults[x.name]) || (await api.listWorkItemsByBoard(boardId)).length)) {
    throw new Error('O quadro escolhido possui colunas fora do método. Escolha PRISMA_BOARD para um quadro novo e vazio.');
  }
  for (const column of unknown) {
    const target = STATUS_MAP[defaults[column.name]];
    if (columns.some(x => x.name === target.name)) throw new Error(`Coluna duplicada durante a configuração: ${target.name}.`);
    await api.updateStage(column.id, target.name, target.category);
    column.name = target.name;
    column.category = target.category;
  }
  const stages = new Map<MetodoStatus, string>();
  for (const status of STATUSES) {
    const target = STATUS_MAP[status];
    const matches = columns.filter(x => x.name === target.name);
    if (matches.length > 1) throw new Error(`Coluna duplicada: ${target.name}.`);
    const column = matches[0];
    if (column && column.category !== target.category) {
      throw new Error(`A categoria da coluna ${target.name} difere do método. Corrija a configuração no Prisma antes de continuar.`);
    }
    stages.set(status, column?.id ?? await api.createStage(boardId, target.name, target.category));
  }
  columns = await api.listStages(boardId);
  const ordered = columns.sort((a, b) => a.position - b.position).map(x => x.id);
  const expected = [...stages.values()];
  if (ordered.some((id, i) => id !== expected[i])) await api.reorderStages(boardId, expected);

  const definitions = await api.listProjectCustomFields(projectId);
  const fields = new Map<string, string>();
  for (const [index, name] of Object.values(FIELD_NAMES).entries()) {
    const matches = definitions.filter(x => x.name === name);
    if (matches.length > 1) throw new Error(`Campo personalizado duplicado: ${name}.`);
    if (matches[0] && matches[0].type !== 1) throw new Error(`O campo ${name} deve ser do tipo texto.`);
    fields.set(name, matches[0]?.id ?? await api.createProjectCustomField(projectId, { name, type: 1, position: (index + 1) * 100 }));
  }
  return { projectId, boardId, stages, fields };
}
