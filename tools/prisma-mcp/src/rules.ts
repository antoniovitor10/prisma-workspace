export const METHOD_STATUSES = [
  'backlog', 'pronta', 'em-desenvolvimento', 'em-revisao',
  'em-teste', 'concluida', 'bloqueada',
] as const;

export type TaskStatus = typeof METHOD_STATUSES[number];
export type PrismaRole = 'agente' | 'humano';

export const BLOCK_MARKER = '[prisma-mcp:bloqueio]';
export const STATUS_PREFIX = '[status]';

export function isTaskStatus(value: string): value is TaskStatus {
  return (METHOD_STATUSES as readonly string[]).includes(value);
}

export function parseRole(value: string | undefined = process.env.PRISMA_ROLE): PrismaRole {
  const role = value ?? 'agente';
  if (role !== 'agente' && role !== 'humano') {
    throw new Error('PRISMA_ROLE deve ser agente ou humano.');
  }
  return role;
}

/** A autorização humana não pode ser concedida pelo papel agente. */
export function canTransition(
  role: PrismaRole,
  from: TaskStatus,
  to: TaskStatus,
  ownBlock = false,
): boolean {
  if (!isTaskStatus(from) || !isTaskStatus(to)) return false;
  if (role === 'humano') return true;
  if (role !== 'agente' || from === to) return false;
  if (to === 'bloqueada') return from !== 'concluida';
  if (from === 'em-desenvolvimento' && to === 'em-revisao') return true;
  return from === 'bloqueada' && to === 'em-desenvolvimento' && ownBlock;
}

export function assertTransition(
  role: PrismaRole,
  from: TaskStatus,
  to: TaskStatus,
  ownBlock = false,
): void {
  if (canTransition(role, from, to, ownBlock)) return;
  if (role === 'agente' && (from === 'backlog' || from === 'pronta') && to === 'em-desenvolvimento') {
    throw new Error(`Transição recusada: ${from} -> ${to} exige autorização humana. O agente nunca autoriza uma task.`);
  }
  if (role === 'agente' && from === 'bloqueada' && to === 'em-desenvolvimento') {
    throw new Error('Transição recusada: o agente só pode retomar um bloqueio que ele mesmo registrou pelo MCP.');
  }
  throw new Error(`Transição recusada para o papel ${role}: ${from} -> ${to}.`);
}

export function assertCreationStatus(role: PrismaRole, status: TaskStatus): void {
  if (!isTaskStatus(status)) throw new Error('Status de criação inválido.');
  if (role === 'humano' || (role === 'agente' && (status === 'backlog' || status === 'concluida'))) return;
  throw new Error('O agente só pode criar uma task em backlog ou concluida (atalho ou incidente). A autorização para executar exige um humano.');
}

export function getProjectRules(role: PrismaRole = parseRole()) {
  const matrix = Object.fromEntries(METHOD_STATUSES.map(from => [from,
    METHOD_STATUSES.filter(to => from !== to && canTransition(role, from, to, true))]));
  const conventions = [
    'Use branch task/T-XXXX-slug e, quando o repositório usar PR, inclua [T-XXXX] no título.',
    'Nunca escreva status: no frontmatter das tasks. O Prisma é a verdade do status; o conteúdo fica no repositório.',
    'O status da HU continua no frontmatter de hu.md.',
    'Leia regras_do_projeto no início de toda sessão e confirme obter_task antes de executar.',
    'Se o MCP não responde, pare e avise. Não troque para o modo local.',
    'Após abrir o PR, use vincular_pr e comente o relatório de execução.',
    'O agente move em-desenvolvimento para em-revisao apenas no nível 0 (sem push). Nas fases com Git, aguarde os eventos do Git.',
  ];
  return {
    papel: role,
    status: [...METHOD_STATUSES],
    transicoes_permitidas: matrix,
    condicoes: [
      'Agente nunca move backlog/pronta para em-desenvolvimento: isso exige autorização humana.',
      'Agente pode bloquear qualquer status exceto concluida.',
      'Bloqueada -> em-desenvolvimento exige o último bloqueio registrado pelo MCP e de autoria do próprio agente.',
      'Humano pode realizar qualquer transição.',
      'Papel agente é aplicado pelo adaptador nesta fase; a proteção na API está pendente.',
    ],
    criacao_permitida: role === 'humano' ? [...METHOD_STATUSES] : ['backlog', 'concluida'],
    autonomia: 'Fase 1, nível 0 (sem Git): ler, criar tasks, comentar, vincular PR e mover somente conforme a matriz.',
    convencoes: conventions,
  };
}
