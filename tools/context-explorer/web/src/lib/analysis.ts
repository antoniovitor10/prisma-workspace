import type { ExplorerModel, Gap, Spec, Task } from '../types/model'

export type ReviewTone = 'aligned' | 'partial' | 'planned' | 'attention'

export interface ManualReview {
  tone: ReviewTone
  label: string
  summary: string
  evidence: string[]
  closingQuestion?: string
}

export interface SpecAudit {
  spec: Spec
  tasks: Task[]
  gaps: Gap[]
  testGates: string[]
  humanGateTasks: Task[]
  pendingDecisions: string[]
  review: ManualReview
  readiness: number
}

export interface DecisionQuestion {
  id: string
  category: 'escopo' | 'caminho' | 'teste' | 'aprovação' | 'runtime'
  prompt: string
  context: string
  specId?: string
  taskId?: string
  severity: 'high' | 'medium' | 'low'
}

// Baseline de aderência verificada diretamente no código em 2026-08-19.
// Ela não altera o model.json: funciona como uma lente editorial da interface
// sobre as entidades e relações produzidas pelo scanner.
const reviews: Record<string, ManualReview> = {
  'SPEC-PROJECT-STRUCTURE': {
    tone: 'partial',
    label: 'Parcialmente desatualizada',
    summary: 'Os rótulos ainda dizem “Metodologia” e XP não existe, mas o projeto Kanban já não cria sprint automaticamente.',
    evidence: [
      'ProjectMethodology possui apenas Kanban, Scrum, Scrumban e SimpleList.',
      'Projects.tsx e ProjectSettings.tsx ainda usam o rótulo “Metodologia”.',
      'CreateProjectCommandHandler cria o Project e não cria Sprint.'
    ],
    closingQuestion: 'TASK-003 deve ser marcada como já atendida e retirada do trabalho pendente?'
  },
  'SPEC-WORK-ITEMS': {
    tone: 'aligned',
    label: 'Gap confirmado',
    summary: 'Story points continuam visíveis e editáveis sem considerar a metodologia do projeto.',
    evidence: [
      'TaskDetailDrawer renderiza o campo Story points incondicionalmente.',
      'Kanban usa apenas cardSettings.showPoints e não a metodologia.'
    ]
  },
  'SPEC-B-001': {
    tone: 'aligned',
    label: 'Gap confirmado',
    summary: 'O backend já transporta ParentId, mas o frontend remove subtarefas antes de renderizar o backlog.',
    evidence: [
      'BacklogRepository retorna todos os WorkItems ativos do projeto.',
      'BacklogItemDto já inclui ParentId.',
      'BacklogPlanner filtra item.kind !== 6.'
    ],
    closingQuestion: 'A árvore será montada somente no frontend, mantendo a API como lista flat com ParentId?'
  },
  'SPEC-F-009': {
    tone: 'aligned',
    label: 'Gap confirmado',
    summary: 'A criação de vínculo recebe GUID e ainda não há busca por chave humana nem detecção de ciclos.',
    evidence: [
      'CreateWorkItemLinkCommand recebe TargetWorkItemId.',
      'O handler valida existência e duplicidade, mas não percorre o grafo.',
      'Não existe endpoint de autocomplete de WorkItem por chave/título.'
    ]
  },
  'SPEC-S-003': {
    tone: 'aligned',
    label: 'Gap confirmado',
    summary: 'O quadro da sprint é uma visualização por colunas, sem dnd-kit ou persistência de movimento.',
    evidence: [
      'SprintDashboard agrupa itens por etapa e abre detalhes no clique.',
      'A subaba Quadro não registra onDragEnd nem chama moveWorkItem.'
    ]
  },
  'SPEC-WORKFLOW-STATUS': {
    tone: 'planned',
    label: 'Funcionalidade futura',
    summary: 'Workflow por projeto existe; templates globais por organização ainda não existem.',
    evidence: [
      'WorkflowStatus e WorkflowTransition pertencem ao Project.',
      'Não existem OrganizationWorkflowTemplate, endpoints ou telas de template.'
    ],
    closingQuestion: 'O template padrão será obrigatório para toda organização ou o fallback atual deve permanecer permanente?'
  },
  'SPEC-TASK-HISTORY': {
    tone: 'aligned',
    label: 'Gap confirmado',
    summary: 'Comentários e eventos ainda dividem a mesma timeline e StageHistory não registra ator ou motivo.',
    evidence: [
      'TaskDetailDrawer exibe “Comentários internos e histórico” numa seção única.',
      'StageHistory possui apenas WorkItemId, StageId, EnteredAt e LeftAt.',
      'TaskEvent.Payload continua JSON livre e StateGraph não existe.'
    ]
  }
}

const defaultReview: ManualReview = {
  tone: 'attention',
  label: 'Revisão necessária',
  summary: 'O modelo não contém evidência semântica suficiente para afirmar aderência ao código.',
  evidence: []
}

export function sectionBody(spec: Spec, heading: string) {
  return spec.sections.find(section => section.heading.toLowerCase() === heading.toLowerCase())?.body ?? ''
}

function bullets(value: string) {
  return value
    .split(/\r?\n/)
    .map(line => line.replace(/^\s*(?:[-*]|\d+\.)\s*/, '').trim())
    .filter(line => line && !/^nenhum[.!]?$/i.test(line))
}

function gapsForSpec(model: ExplorerModel, spec: Spec, tasks: Task[]) {
  const taskIds = new Set(tasks.map(task => task.id))
  const affected = new Set(tasks.flatMap(task => task.affected_areas))
  const context = model.context.specContexts.find(item => item.specId === spec.id)
  const declared = new Set([...(context?.minimum ?? []), ...(context?.expanded ?? [])].map(item => item.path))
  return model.gaps.filter(gap => taskIds.has(gap.subject) || Boolean(gap.path && (affected.has(gap.path) || declared.has(gap.path))))
}

export function buildSpecAudits(model: ExplorerModel): SpecAudit[] {
  return model.specs.map(spec => {
    const tasks = model.tasks.filter(task => task.specId === spec.id)
    const gaps = gapsForSpec(model, spec, tasks)
    const testGates = [...new Set(tasks.flatMap(task => task.gates).filter(gate => gate.includes('test')))]
    const humanGateTasks = tasks.filter(task => /sim|yes|true/i.test(task.human_gate))
    const pendingDecisions = bullets(sectionBody(spec, 'Pending Decisions'))
    const readiness = Math.max(0, Math.min(100,
      25
      + (tasks.length > 0 ? 20 : 0)
      + (testGates.length > 0 ? 20 : 0)
      + (pendingDecisions.length === 0 ? 20 : 0)
      + (gaps.length === 0 ? 15 : 0)))
    return { spec, tasks, gaps, testGates, humanGateTasks, pendingDecisions, review: reviews[spec.id] ?? defaultReview, readiness }
  })
}

export function buildDecisionQuestions(model: ExplorerModel): DecisionQuestion[] {
  const audits = buildSpecAudits(model)
  const questions: DecisionQuestion[] = []

  for (const audit of audits) {
    audit.pendingDecisions.forEach((decision, index) => questions.push({
      id: `${audit.spec.id}:scope:${index}`,
      category: 'escopo',
      prompt: decision.endsWith('?') ? decision : `${decision}?`,
      context: `Decisão pendente declarada em ${audit.spec.path}.`,
      specId: audit.spec.id,
      severity: 'high'
    }))

    const missingPaths = audit.gaps.filter(gap => gap.type === 'broken_path')
    if (missingPaths.length > 0) questions.push({
      id: `${audit.spec.id}:paths`,
      category: 'caminho',
      prompt: `Os ${missingPaths.length} caminhos ausentes são artefatos novos ou referências que precisam ser corrigidas?`,
      context: `O scanner não distingue ausência planejada de path obsoleto. Exemplos: ${missingPaths.slice(0, 2).map(gap => gap.subject).join(' · ')}.`,
      specId: audit.spec.id,
      severity: 'medium'
    })

    const missingTests = audit.gaps.filter(gap => gap.type === 'missing_test')
    if (missingTests.length > 0) questions.push({
      id: `${audit.spec.id}:tests`,
      category: 'teste',
      prompt: `Qual teste automatizado comprova o fechamento de ${audit.spec.id}?`,
      context: `${missingTests.length} alerta(s) de cobertura estão ligados às tarefas desta especificação.`,
      specId: audit.spec.id,
      severity: 'medium'
    })

    if (audit.review.closingQuestion) questions.push({
      id: `${audit.spec.id}:review`,
      category: 'escopo',
      prompt: audit.review.closingQuestion,
      context: `Pergunta gerada pela revisão de aderência atual ao código.`,
      specId: audit.spec.id,
      severity: audit.review.tone === 'partial' ? 'high' : 'medium'
    })

    for (const task of audit.humanGateTasks) questions.push({
      id: `${task.id}:approval`,
      category: 'aprovação',
      prompt: `Qual evidência e qual aprovador liberam o human gate de ${task.id}?`,
      context: task.human_gate,
      specId: audit.spec.id,
      taskId: task.id,
      severity: 'high'
    })
  }

  if ((model.artifactsSummary.actualContext as number) === 0) questions.push({
    id: 'runtime:actual-context',
    category: 'runtime',
    prompt: 'Qual tarefa deve ser executada primeiro para produzir um contexto efetivo comparável ao contexto candidato?',
    context: 'O model.json atual não encontrou um envelope de tarefa ativo em .agent-state.',
    severity: 'low'
  })

  if (!model.readiness.omniroute.configExists) questions.push({
    id: 'runtime:omniroute',
    category: 'runtime',
    prompt: 'OmniRoute continuará apenas em Observer Mode nesta versão?',
    context: 'A configuração .omniroute.json não existe; o painel não deve inventar mapeamentos.',
    severity: 'low'
  })

  return questions
}

export function gapGroup(gap: Gap) {
  if (gap.type === 'broken_path') return 'Referências e artefatos'
  if (gap.type.includes('test') || gap.type.includes('gate')) return 'Cobertura e validação'
  if (gap.type === 'verification_note') return 'Verificações manuais'
  return 'Consistência do modelo'
}
