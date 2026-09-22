/**
 * Taxonomia dos tipos de item de trabalho.
 *
 * Fonte única de rótulo, cor, sigla, descrição e hierarquia. Antes disso os tipos tinham
 * nome mas só três cores para dez valores, e nada explicava quando usar cada um nem o que
 * podia ser filho de quê — a regra existia apenas no domínio, em
 * WorkItemHierarchyRules.cs, invisível para quem usa o produto.
 *
 * Os números espelham o enum WorkItemKind do domínio e não podem ser reordenados.
 */

export const WorkItemKind = {
  Epic: 1,
  Feature: 2,
  UserStory: 3,
  Bug: 4,
  Task: 5,
  Subtask: 6,
  Improvement: 7,
  TechnicalDebt: 8,
  Request: 9,
  Incident: 10,
} as const;

export type WorkItemKindValue = (typeof WorkItemKind)[keyof typeof WorkItemKind];

export interface WorkItemKindMeta {
  /** Nome completo, usado em seletores e no detalhe. */
  label: string;
  /** Sigla curta para o cartão do Kanban, onde o espaço é escasso. */
  short: string;
  /** Cor de identidade do tipo. Famílias distintas, tons distintos. */
  color: string;
  /** Quando usar. Aparece como dica no seletor de tipo. */
  description: string;
  /** Nível na hierarquia de planejamento: 0 é o mais alto. */
  level: number;
}

export const workItemKinds: Record<number, WorkItemKindMeta> = {
  // ── Planejamento: o que se quer alcançar ───────────────────────────────
  [WorkItemKind.Epic]: {
    label: 'Épico', short: 'EPI', color: '#7A5CC6', level: 0,
    description: 'Objetivo grande, de vários meses. Agrupa features e histórias.',
  },
  [WorkItemKind.Feature]: {
    label: 'Feature', short: 'FEA', color: '#5B7CFA', level: 1,
    description: 'Capacidade entregável do produto. Vive dentro de um épico.',
  },
  [WorkItemKind.UserStory]: {
    label: 'História', short: 'HIS', color: '#0EA5A5', level: 2,
    description: 'Necessidade de quem usa, escrita do ponto de vista dela. Unidade de valor da sprint.',
  },

  // ── Execução: o que se faz ─────────────────────────────────────────────
  [WorkItemKind.Task]: {
    label: 'Tarefa', short: 'TAR', color: '#2563EB', level: 3,
    description: 'Trabalho concreto para entregar uma história.',
  },
  [WorkItemKind.Subtask]: {
    label: 'Subtarefa', short: 'SUB', color: '#64748B', level: 4,
    description: 'Passo dentro de uma tarefa. O menor nível da hierarquia.',
  },

  // ── Correção e manutenção ──────────────────────────────────────────────
  [WorkItemKind.Bug]: {
    label: 'Bug', short: 'BUG', color: '#D92D20', level: 2,
    description: 'Comportamento errado no que já foi entregue.',
  },
  [WorkItemKind.Incident]: {
    label: 'Incidente', short: 'INC', color: '#B42318', level: 2,
    description: 'Falha em produção que exige resposta imediata.',
  },
  [WorkItemKind.Improvement]: {
    label: 'Melhoria', short: 'MEL', color: '#059669', level: 2,
    description: 'Ajuste em algo que já funciona, para funcionar melhor.',
  },
  [WorkItemKind.TechnicalDebt]: {
    label: 'Débito técnico', short: 'DÉB', color: '#B45309', level: 2,
    description: 'Correção interna que não muda o comportamento, mas sustenta o ritmo futuro.',
  },

  // ── Entrada externa ────────────────────────────────────────────────────
  [WorkItemKind.Request]: {
    label: 'Solicitação', short: 'SOL', color: '#E8833A', level: 2,
    description: 'Pedido vindo do portal externo. Sempre tem solicitante identificado.',
  },
};

/** Rótulos por número, mantido para quem já consumia `kindNames`. */
export const kindNames: Record<number, string> = Object.fromEntries(
  Object.entries(workItemKinds).map(([id, meta]) => [Number(id), meta.label]),
);

export const kindMeta = (kind: number): WorkItemKindMeta =>
  workItemKinds[kind] ?? {
    label: 'Item', short: 'ITE', color: '#64748B', level: 3,
    description: 'Tipo não reconhecido.',
  };

/**
 * Espelha WorkItemHierarchyRules.cs do domínio. A API é a autoridade — isto existe para
 * a interface poder oferecer só o que será aceito, em vez de deixar a pessoa escolher e
 * receber erro depois.
 */
const hierarquia: ReadonlyArray<readonly [number, number]> = [
  [WorkItemKind.Epic, WorkItemKind.Feature],
  [WorkItemKind.Epic, WorkItemKind.UserStory],
  [WorkItemKind.Feature, WorkItemKind.UserStory],
  [WorkItemKind.UserStory, WorkItemKind.Task],
  [WorkItemKind.Task, WorkItemKind.Subtask],
];

export const canBeChildOf = (parent: number, child: number): boolean =>
  hierarquia.some(([p, c]) => p === parent && c === child);

/** Tipos que podem ser filhos deste. Vazio significa que o tipo não aceita filhos. */
export const allowedChildKinds = (parent: number): number[] =>
  hierarquia.filter(([p]) => p === parent).map(([, c]) => c);

/** Ordem de exibição em seletores: planejamento, execução, correção, entrada externa. */
export const kindDisplayOrder: number[] = [
  WorkItemKind.Epic, WorkItemKind.Feature, WorkItemKind.UserStory,
  WorkItemKind.Task, WorkItemKind.Subtask,
  WorkItemKind.Bug, WorkItemKind.Incident, WorkItemKind.Improvement, WorkItemKind.TechnicalDebt,
  WorkItemKind.Request,
];
