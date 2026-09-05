import { useMemo, useState } from 'react'
import { GraphCanvas } from '../components/GraphCanvas'
import { Badge, FilterRow, Kicker, Page, PageHeader, Panel } from '../components/ui'
import { activeSpecs } from '../lib/insights'
import type { ExplorerModel, GraphEdge, GraphNode, Selection } from '../types/model'

export function TraceabilityView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const specs = activeSpecs(model)
  const [specId, setSpecId] = useState(specs[0]?.id ?? '')
  const graph = useMemo(() => {
    const nodes: GraphNode[] = []
    const edges: GraphEdge[] = []
    const spec = model.specs.find(item => item.id === specId)
    const stories = model.userStories.filter(story => story.specId === specId)
    const feedbackTasks = model.storyTasks.filter(task => task.specId === specId)
    if (spec) nodes.push({ ...spec, id: `spec:${spec.id}`, type: 'spec', label: spec.id, _eyebrow: 'Especificação', _summary: spec.title })
    stories.forEach(story => {
      nodes.push({ ...story, id: `story:${story.id}`, type: 'artifact', label: story.id, _eyebrow: 'História de usuário', _summary: story.narrative })
      edges.push({ from: `story:${story.id}`, to: `spec:${story.specId}`, relation: 'origina' })
    })
    model.tasks.filter(task => task.specId === specId).forEach(task => {
      const requirementId = `requirement:${task.id}`
      nodes.push({ id: requirementId, type: 'artifact', label: task.requirement, requirement: task.requirement, task: task.id, _eyebrow: 'Requisito' })
      nodes.push({ ...task, id: `task:${task.id}`, type: 'task', label: task.id, _summary: task.requirement, _eyebrow: 'Tarefa' })
      edges.push({ from: `spec:${task.specId}`, to: requirementId, relation: 'define' }, { from: requirementId, to: `task:${task.id}`, relation: 'executado por' })
      task.gates.forEach((gate, index) => {
        const isTest = /test|e2e/i.test(gate)
        const id = `${isTest ? 'test' : 'gate'}:${task.id}:${index}`
        nodes.push({ id, type: isTest ? 'test' : 'gate', label: gate, task: task.id, spec: specId, _eyebrow: isTest ? 'Teste exigido' : 'Gate exigido', _summary: 'Obrigação declarada pela tarefa; esta entidade não representa um resultado de execução.' })
        edges.push({ from: `task:${task.id}`, to: id, relation: isTest ? 'deve ser testado por' : 'deve passar por' })
      })
    })
    if (stories.length) {
      nodes.push({ id: `homologation:${specId}`, type: 'validation', label: 'Homologação', _eyebrow: 'Conferência humana', _summary: 'PO confere o produto pelas histórias; não aprova cada história como gate.' })
      stories.forEach(story => edges.push({ from: `story:${story.id}`, to: `homologation:${specId}`, relation: 'homologada em' }))
    }
    feedbackTasks.forEach(task => {
      nodes.push({ ...task, id: `feedback:${task.id}`, type: 'task', label: task.id, _eyebrow: 'Tarefa da homologação', _summary: task.requirement })
      edges.push({ from: `homologation:${specId}`, to: `feedback:${task.id}`, relation: 'gera' })
    })
    return { nodes, edges }
  }, [model, specId])
  return <Page><PageHeader><div><Kicker>Cadeia de evidências exigidas</Kicker><h1>Rastreabilidade</h1><p>História → especificação → requisito → tarefa → teste/gate → homologação → tarefa de correção. Testes e gates mostrados são obrigações declaradas, não resultados de CI.</p></div><Badge>{graph.nodes.length} entidades</Badge></PageHeader><FilterRow><label htmlFor="trace-spec">Especificação</label><select id="trace-spec" value={specId} onChange={event => setSpecId(event.target.value)}>{specs.map(spec => <option key={spec.id}>{spec.id}</option>)}</select></FilterRow><Panel><GraphCanvas nodes={graph.nodes} edges={graph.edges} onSelect={onSelect} direction="LR" height={700}/></Panel></Page>
}
