import { PrismaApi } from './api.js';
import { FIELD_NAMES, STATUSES, type MetodoStatus } from './mapping.js';

export interface ProjetoSetup {
  projectId: string; boardId: string; stages: Map<MetodoStatus, string>; fields: Map<string, string>;
}
/** Resolve somente estrutura provisionada explicitamente pelo administrador na UI. */
export async function ensureProjeto(api: PrismaApi = PrismaApi.fromEnv()): Promise<ProjetoSetup> {
  const { project } = await api.getMethodRules();
  if (!project.enabled) throw new Error('Método desabilitado ou não provisionado. Peça a configuração ao administrador; pare e avise.');
  const stages = new Map<MetodoStatus, string>();
  for (const status of STATUSES) {
    const id = project.stageMappings[status];
    if (!id || [...stages.values()].includes(id)) throw new Error('Mapeamento das colunas do método ausente ou ambíguo. Pare e avise.');
    stages.set(status, id);
  }
  const fields = new Map(Object.entries(project.fields ?? {}));
  fields.set(FIELD_NAMES.id, project.methodIdFieldId);
  fields.set(FIELD_NAMES.hu, project.huFieldId);
  fields.set(FIELD_NAMES.pr, project.prFieldId);
  if (Object.values(FIELD_NAMES).some(name => !fields.get(name))) {
    throw new Error('Campos do método não configurados. Peça a correção ao administrador; pare e avise.');
  }
  return { projectId: project.projectId, boardId: project.boardId, stages, fields };
}
