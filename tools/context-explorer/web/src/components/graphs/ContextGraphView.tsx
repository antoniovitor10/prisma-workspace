import { GraphCanvas } from '../GraphCanvas'
import type { ExplorerModel, Selection } from '../../types/model'

export function ContextGraph({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  return <GraphCanvas nodes={model.contextGraph.nodes} edges={model.contextGraph.edges} onSelect={onSelect} direction="LR" />
}
