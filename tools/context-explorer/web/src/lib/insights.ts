import type { ExplorerModel, Section, Spec, Task } from '../types/model'

export interface DeclaredGap {
  id: string
  specId: string
  specTitle: string
  specPath: string
  heading: string
  detail: string
}

export interface ValidationCheck {
  id: string
  specId: string
  specTitle: string
  specPath: string
  heading: string
  description: string
}

const gapHeading = /(gap|lacuna|diverg)/i
const manualHeading = /(homologa|manual validation|acceptance criteria|crit[eé]rios? de aceit|testes e evid[eê]ncias)/i

export function activeSpecs(model: ExplorerModel) {
  return model.specs.filter(spec => spec.status.toLowerCase() !== 'superseded')
}

export function historicalSpecs(model: ExplorerModel) {
  return model.specs.filter(spec => spec.status.toLowerCase() === 'superseded')
}

export function openTasks(model: ExplorerModel) {
  return model.tasks.filter(task => ['pending', 'in_progress'].includes(task.status.toLowerCase()))
}

export function tasksForSpec(model: ExplorerModel, specId: string) {
  return model.tasks.filter(task => task.specId === specId)
}

export function userStoriesForSpec(model: ExplorerModel, specId: string) {
  return (model.userStories ?? []).filter(story => story.specId === specId)
}

export function taskTestGates(tasks: Task[]) {
  return [...new Set(tasks.flatMap(task => task.gates).filter(gate => /test|build|lint|e2e/i.test(gate)))]
}

export function specSummary(spec: Spec) {
  const preferred = ['objetivo', 'objective', 'propósito', 'contexto', 'context']
  const section = preferred
    .map(name => spec.sections.find(item => item.heading.toLowerCase() === name))
    .find(Boolean)
  return firstMeaningfulParagraph(section?.body ?? spec.body) || 'Contrato canônico do módulo.'
}

export function sectionItems(section: Section) {
  const lines = section.body
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(line => line && !/^#{1,6}\s/.test(line) && !/^\|?\s*:?-{3,}/.test(line))

  const bullets = lines
    .filter(line => /^[-*+]\s+|^\d+[.)]\s+|^- \[[ xX]\]\s+/.test(line))
    .map(cleanMarkdown)
    .filter(isUseful)

  if (bullets.length > 0) return bullets
  const paragraph = firstMeaningfulParagraph(section.body)
  return paragraph ? [paragraph] : []
}

export function declaredGaps(model: ExplorerModel): DeclaredGap[] {
  return activeSpecs(model).flatMap(spec => spec.sections
    .filter(section => gapHeading.test(section.heading) || /^G-[A-Z0-9-]+/.test(section.heading))
    .flatMap((section, index) => sectionItems(section).map((detail, itemIndex) => ({
      id: `${spec.id}:gap:${index}:${itemIndex}`,
      specId: spec.id,
      specTitle: spec.title,
      specPath: spec.path,
      heading: section.heading,
      detail
    }))))
}

export function validationChecks(model: ExplorerModel): ValidationCheck[] {
  return activeSpecs(model).flatMap(spec => spec.sections
    .filter(section => manualHeading.test(section.heading))
    .flatMap((section, index) => sectionItems(section).map((description, itemIndex) => ({
      id: `${spec.id}:validation:${index}:${itemIndex}`,
      specId: spec.id,
      specTitle: spec.title,
      specPath: spec.path,
      heading: section.heading,
      description
    }))))
}

export function statusLabel(value: string) {
  const labels: Record<string, string> = {
    approved: 'aprovada',
    superseded: 'substituída',
    review: 'em revisão',
    draft: 'rascunho',
    pending: 'pendente',
    in_progress: 'em andamento',
    completed: 'concluída',
    cancelled: 'cancelada',
    pending_analysis: 'aguardando análise da IA',
    ready: 'pronta para execução',
    archived: 'arquivada'
  }
  return labels[value.toLowerCase()] ?? value
}

function firstMeaningfulParagraph(value: string) {
  return value
    .split(/\r?\n\s*\r?\n/)
    .map(cleanMarkdown)
    .find(isUseful) ?? ''
}

function cleanMarkdown(value: string) {
  return value
    .replace(/^[-*+]\s+/, '')
    .replace(/^\d+[.)]\s+/, '')
    .replace(/^- \[[ xX]\]\s+/, '')
    .replace(/\[(.*?)\]\([^)]*\)/g, '$1')
    .replace(/[`*_>#]/g, '')
    .replace(/\s+/g, ' ')
    .trim()
}

function isUseful(value: string) {
  return value.length > 8 && !/^nenhum[.!]?$/i.test(value) && !/^n\/a$/i.test(value)
}
