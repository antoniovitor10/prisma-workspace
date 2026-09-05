import { Background, Controls, MarkerType, Position, ReactFlow, type Edge, type Node } from '@xyflow/react'
import styled from 'styled-components'
import { traduzir } from '../../lib/i18n'
import type { ExplorerModel, Selection } from '../../types/model'

type Transition = { from: string; to: string; label: string }

const Shell = styled.section`border:1px solid #d7e0e8;border-radius:16px;background:#fff;overflow:hidden;box-shadow:0 8px 34px rgba(16,47,79,.055);`
const Toolbar = styled.div`display:flex;align-items:center;justify-content:space-between;gap:14px;padding:14px 16px;border-bottom:1px solid #dce4ea;background:#f8fafb;strong{color:#183149}small{color:#718094}`
const Explanation = styled.div`display:flex;flex-wrap:wrap;gap:8px;padding:11px 16px;border-bottom:1px solid #e3e9ee;background:#fff;color:#53677a;font-size:12px;.arrow{color:#1671b8;font-weight:800}.item{display:inline-flex;align-items:center;gap:8px}`
const SectionTitle = styled.div`padding:14px 16px 4px;border-top:1px solid #dce4ea;background:#fff;h3{margin:0 0 4px;color:#183149;font-size:15px}p{margin:0;color:#718094;font-size:12px}`

const primaryStates = ['idle','spec-draft','spec-review','context-loading','implementing','gating','pr-created','human-review','merged']
const corrections: Transition[] = [
  { from: 'spec-draft', to: 'spec-draft', label: 'continuar editando' },
  { from: 'spec-review', to: 'spec-draft', label: 'pedir ajustes na especificação' },
  { from: 'gating', to: 'implementing', label: 'corrigir o código' },
  { from: 'human-review', to: 'implementing', label: 'aplicar ajustes da revisão' },
  { from: 'blocked', to: 'implementing', label: 'desbloquear e retomar' },
  { from: 'blocked', to: 'spec-draft', label: 'redefinir a especificação' }
]
const failures: Transition[] = [
  { from: 'implementing', to: 'blocked', label: 'depende de decisão ou fator externo' },
  { from: 'implementing', to: 'failed', label: 'falha irrecuperável' },
  { from: 'gating', to: 'blocked', label: 'verificação exige intervenção' },
  { from: 'gating', to: 'failed', label: 'verificação falhou definitivamente' },
  { from: 'human-review', to: 'blocked', label: 'aprovação pendente' },
  { from: 'human-review', to: 'failed', label: 'revisão rejeitada' },
  { from: 'blocked', to: 'failed', label: 'encerrar como falha' }
]

function palette(state: string) {
  if (state === 'merged') return { border: '#238755', background: '#eef9f3' }
  if (state === 'failed') return { border: '#bf4b40', background: '#fff1ef' }
  if (state === 'blocked') return { border: '#c78d00', background: '#fff8e7' }
  if (state === 'gating' || state === 'human-review') return { border: '#7655a6', background: '#f6f1fb' }
  return { border: '#287ba9', background: '#f0f7fb' }
}

function stateNode(id: string, state: string, x: number, y: number, sourcePosition = Position.Right, targetPosition = Position.Left): Node {
  const colors = palette(state)
  return {
    id,
    position: { x, y },
    sourcePosition,
    targetPosition,
    data: { state, label: <div><small style={{display:'block',marginBottom:5,color:colors.border,fontSize:9,fontWeight:800,letterSpacing:'.08em'}}>ESTADO</small><strong style={{fontSize:12,color:'#183149'}}>{traduzir(state)}</strong></div> },
    style: { width: 188, minHeight: 66, padding: '10px 13px', border: `1px solid ${colors.border}`, borderLeftWidth: 5, borderRadius: 11, background: colors.background, textAlign: 'left', boxShadow: '0 4px 13px rgba(16,47,79,.06)' }
  }
}

function primaryGraph() {
  const positions: Record<string, [number, number, Position, Position]> = {
    idle: [0, 0, Position.Right, Position.Left],
    'spec-draft': [245, 0, Position.Right, Position.Left],
    'spec-review': [490, 0, Position.Bottom, Position.Left],
    'context-loading': [490, 155, Position.Left, Position.Top],
    implementing: [245, 155, Position.Left, Position.Right],
    gating: [0, 155, Position.Bottom, Position.Right],
    'pr-created': [0, 310, Position.Right, Position.Top],
    'human-review': [245, 310, Position.Right, Position.Left],
    merged: [490, 310, Position.Right, Position.Left]
  }
  const nodes = primaryStates.map(state => { const [x,y,source,target] = positions[state]; return stateNode(state,state,x,y,source,target) })
  const edges = primaryStates.slice(0,-1).map((state,index): Edge => ({ id:`main-${state}`, source:state, target:primaryStates[index+1], markerEnd:{type:MarkerType.ArrowClosed}, style:{stroke:'#287ba9',strokeWidth:2.2}, animated:index === 3 || index === 4 }))
  return { nodes, edges }
}

function rowsGraph(transitions: Transition[], tone: 'correction' | 'failure') {
  const nodes: Node[] = []
  const edges: Edge[] = []
  transitions.forEach((transition,index) => {
    const y = index * 100
    const source = `${index}:source`
    const target = `${index}:target`
    nodes.push(stateNode(source,transition.from,0,y), stateNode(target,transition.to,430,y))
    edges.push({ id:`row-${index}`, source, target, label:transition.label, markerEnd:{type:MarkerType.ArrowClosed}, style:{stroke:tone === 'failure' ? '#bf4b40' : '#7655a6',strokeWidth:2}, labelStyle:{fill:'#40566a',fontSize:11,fontWeight:700}, labelBgStyle:{fill:'#fff',fillOpacity:.96}, labelBgPadding:[6,4] })
  })
  return { nodes, edges }
}

export function EngineGraph({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const principal = primaryGraph()
  const correctionGraph = rowsGraph(corrections,'correction')
  const failureGraph = rowsGraph(failures,'failure')
  const selectState = (_: unknown,node:Node) => { const state=String(node.data.state); onSelect({id:state,title:traduzir(state),state,_eyebrow:'Estado do motor',_summary:`Estado técnico canônico: ${state}`}) }
  return <Shell>
    <Toolbar><strong>Fluxo completo do motor</strong><small>{model.graphs.engineGraph.edges.length} transições organizadas em três faixas</small></Toolbar>
    <Explanation>{primaryStates.map((state,index) => <span className="item" key={state}>{index > 0 && <span className="arrow">→</span>}<span>{traduzir(state)}</span></span>)}</Explanation>
    <div style={{height:500}}><ReactFlow nodes={principal.nodes} edges={principal.edges} fitView fitViewOptions={{padding:.18,maxZoom:1.15}} minZoom={.35} maxZoom={1.7} nodesDraggable={false} onNodeClick={selectState}><Background color="#dfe6ec" gap={22} size={1}/><Controls showInteractive={false}/></ReactFlow></div>
    <SectionTitle><h3>Retornos e correções</h3><p>Cada linha mostra de onde a execução volta e qual é a ação esperada.</p></SectionTitle>
    <div style={{height:620}}><ReactFlow nodes={correctionGraph.nodes} edges={correctionGraph.edges} fitView fitViewOptions={{padding:.18,maxZoom:1.15}} minZoom={.35} maxZoom={1.7} nodesDraggable={false} onNodeClick={selectState}><Background color="#dfe6ec" gap={22} size={1}/><Controls showInteractive={false}/></ReactFlow></div>
    <SectionTitle><h3>Bloqueios e falhas</h3><p>Cada linha explica como uma execução deixa o caminho normal.</p></SectionTitle>
    <div style={{height:700}}><ReactFlow nodes={failureGraph.nodes} edges={failureGraph.edges} fitView fitViewOptions={{padding:.18,maxZoom:1.15}} minZoom={.35} maxZoom={1.7} nodesDraggable={false} onNodeClick={selectState}><Background color="#dfe6ec" gap={22} size={1}/><Controls showInteractive={false}/></ReactFlow></div>
  </Shell>
}
