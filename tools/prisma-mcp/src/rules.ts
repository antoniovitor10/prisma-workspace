import type { MethodRulesDto } from './api.js';
export type { PrismaRole } from './config.js';
import type { PrismaRole } from './config.js';

export const METHOD_STATUSES = [
  'backlog', 'pronta', 'em-desenvolvimento', 'em-revisao',
  'em-teste', 'concluida', 'bloqueada',
] as const;
export type TaskStatus = typeof METHOD_STATUSES[number];
export function isTaskStatus(value: string): value is TaskStatus {
  return (METHOD_STATUSES as readonly string[]).includes(value);
}
export function parseRole(value: string): PrismaRole {
  if (!['agente', 'humano', 'sistema'].includes(value)) throw new Error('Papel da API inválido. Pare e avise.');
  return value as PrismaRole;
}

/** Redundância local; a API revalida a criação e nunca recebe autoridade deste guard. */
export function assertCreationStatus(role: PrismaRole, status: TaskStatus): void {
  if (!isTaskStatus(status)) throw new Error('Status de criação inválido.');
  if (role === 'sistema' || (role === 'agente' && status !== 'backlog' && status !== 'concluida')
    || (role === 'humano' && status === 'em-desenvolvimento')) {
    throw new Error('Criação recusada: o token não pode conceder autorização humana. Pare e avise.');
  }
}
export function canTransition(rules: MethodRulesDto, from: TaskStatus, to: TaskStatus, canResume = false): boolean {
  if (!isTaskStatus(from) || !isTaskStatus(to)) return false;
  if (from === to) return true;
  if (rules.identity.role === 'sistema') return false;
  if ((from === 'backlog' || from === 'pronta') && to === 'em-desenvolvimento') return false;
  if (from === 'bloqueada' && to === 'em-desenvolvimento' && !canResume) return false;
  return rules.allowedTransitions[from]?.includes(to) === true;
}
export function assertTransition(rules: MethodRulesDto, from: TaskStatus, to: TaskStatus, canResume = false): void {
  if (!canTransition(rules, from, to, canResume)) {
    throw new Error(`Transição recusada pelas regras da API: ${from} -> ${to}. Autorização e liberação de retomada exigem sessão humana na UI. Pare e avise.`);
  }
}
export function getProjectRules(rules: MethodRulesDto) {
  return {
    projeto: rules.identity.projectName, quadro: rules.identity.boardName,
    papel: rules.identity.role, principal: rules.identity.principalId,
    validade: rules.identity.expiresAtUtc, capacidades: rules.identity.capabilities,
    status: [...METHOD_STATUSES], transicoes_permitidas: rules.allowedTransitions,
    mapeamento_status: rules.project.stageMappings,
    autonomia: rules.project.autonomyLevel,
    condicoes: [
      'O papel, o escopo e as permissões são verificados pela API a cada operação.',
      'Tokens de método não concedem autorização humana de início nem liberam retomada.',
      'Retomada depende do bloqueio, principal e ciclo autorizados no backend.',
      '401/403: pare e avise. Não use login humano nem modo local como alternativa.',
    ],
    convencoes: rules.conventions,
  };
}
