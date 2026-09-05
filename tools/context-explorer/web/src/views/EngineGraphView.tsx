import { EngineGraph } from '../components/graphs/EngineGraphView'
import { Badge, Kicker, Page, PageHeader, Panel, Split } from '../components/ui'
import type { ExplorerModel, Selection } from '../types/model'

export function EngineGraphView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const graph = model.graphs.engineGraph
  const mapping = graph.mapping as { description?: string; engine_only_states?: Record<string, unknown>; graph_only_nodes?: Record<string, unknown>; notes?: string[] }
  return <Page><PageHeader><div><Kicker>Máquina de estados da execução</Kicker><h1>Grafo do motor</h1><p>Estados técnicos do Motor de Execução. Os nomes não precisam coincidir individualmente com o Grafo do Processo.</p></div><Badge>{graph.states.length} estados · {graph.edges.length} conexões</Badge></PageHeader><Split $aside={340}><EngineGraph model={model} onSelect={item => onSelect(item ? { ...item, _eyebrow: 'Estado do motor' } : null)}/><Panel><h2>Como o mapeamento funciona</h2><p>{mapping.description}</p><h3>Somente no motor</h3><p>{Object.keys(mapping.engine_only_states ?? {}).join(', ') || 'Nenhum'}</p><h3>Somente no processo</h3><p>{Object.keys(mapping.graph_only_nodes ?? {}).join(', ') || 'Nenhum'}</p><h3>Regra importante</h3><p>O Grafo do Processo explica o trabalho; o Grafo do Motor registra a execução.</p></Panel></Split></Page>
}
