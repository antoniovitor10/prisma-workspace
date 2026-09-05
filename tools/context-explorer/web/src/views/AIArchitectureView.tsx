import styled from 'styled-components'
import { AgentsView } from './AgentsView'
import { ArchitectureView } from './ArchitectureView'
import { ContextView } from './ContextView'
import { EngineGraphView } from './EngineGraphView'
import { LoopView } from './LoopView'
import { ProcessGraph } from '../components/graphs/ProcessGraphView'
import { TraceabilityView } from './TraceabilityView'
import { Segmented } from '../components/ui'
import type { ExplorerModel, Selection } from '../types/model'

const Tabs = styled.div`
  position: sticky;
  top: 12px;
  z-index: 20;
  width: fit-content;
  max-width: 100%;
  overflow-x: auto;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgba(16,47,79,.09);
`

const tabItems = [
  ['overview', 'Visão integrada'],
  ['context', 'Contexto'],
  ['workflow', 'Fluxo técnico'],
  ['engine', 'Motor'],
  ['loop', 'Ciclo'],
  ['agents', 'Agentes'],
  ['traceability', 'Rastreabilidade']
] as const

type TabId = typeof tabItems[number][0]

export function AIArchitectureView({ model, onSelect, activeTab, onTabChange }: { model: ExplorerModel; onSelect: (item: Selection) => void; activeTab: string; onTabChange: (tab: string) => void }) {
  const tab: TabId = tabItems.some(([id]) => id === activeTab) ? activeTab as TabId : 'overview'
  return <>
    <Tabs><Segmented role="tablist" aria-label="Áreas da arquitetura de IA">{tabItems.map(([id, label]) => <button key={id} role="tab" aria-selected={tab === id} aria-pressed={tab === id} onClick={() => onTabChange(id)}>{label}</button>)}</Segmented></Tabs>
    <div style={{ marginTop: 18 }}>
      {tab === 'overview' && <ArchitectureView model={model} onSelect={onSelect} />}
      {tab === 'context' && <ContextView model={model} onSelect={onSelect} />}
      {tab === 'workflow' && <><div style={{marginBottom:14}}><strong>Grafo canônico do workflow</strong><p>As 16 etapas e 21 transições lidas diretamente de <code>workflows/feature.yaml</code>.</p></div><ProcessGraph model={model} onSelect={onSelect} /></>}
      {tab === 'engine' && <EngineGraphView model={model} onSelect={onSelect} />}
      {tab === 'loop' && <LoopView model={model} onSelect={onSelect} />}
      {tab === 'agents' && <AgentsView model={model} onSelect={onSelect} />}
      {tab === 'traceability' && <TraceabilityView model={model} onSelect={onSelect} />}
    </div>
  </>
}
