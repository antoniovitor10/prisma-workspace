import { ProcessGraph } from '../components/graphs/ProcessGraphView'
import { Badge, Grid, Kicker, Page, PageHeader, Panel } from '../components/ui'
import type { ExplorerModel, Selection } from '../types/model'

export function ProcessGraphView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const nodes = model.graphs.processGraph.nodes.length
  const edges = model.graphs.processGraph.edges.length
  return <Page>
    <PageHeader><div><Kicker>Contrato conceitual</Kicker><h1>Grafo do processo</h1><p>O contrato humano do processo, da criação da especificação à conclusão. Ele descreve intenção e governança, não estados internos da execução.</p></div><Badge>{nodes} nós · {edges} conexões</Badge></PageHeader>
    <ProcessGraph model={model} onSelect={item => onSelect(item ? { ...item, _eyebrow: 'Nó do processo', _summary: String(item.description ?? '') } : null)}/>
    <Grid $columns={3}><Panel><h3>Antes de implementar</h3><p>Especificação em rascunho → revisão → G-SPEC → entrada da tarefa.</p></Panel><Panel><h3>Durante a execução</h3><p>Montagem do contexto → implementação → verificações, com retorno quando falha.</p></Panel><Panel><h3>Controle humano</h3><p>Aprovações humanas pausam o fluxo; nenhum agente pode autoaprovar.</p></Panel></Grid>
  </Page>
}
