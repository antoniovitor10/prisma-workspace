import { GraphCanvas } from '../GraphCanvas'
import type { ExplorerModel, Selection } from '../../types/model'

export function CorrectionLoopGraph({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const pass = { id: 'PASS', type: 'state', label: 'Aprovado / Revisão', description: 'Validação aprovada e encaminhada para revisão.' }
  const edges = [...model.graphs.loopGraph.edges, { from: 'gating', to: 'PASS', label: 'Aprovado' }]
  return <GraphCanvas nodes={[...model.graphs.loopGraph.nodes, pass]} edges={edges} onSelect={onSelect} direction="LR" height={450} minimap={false} />
}
