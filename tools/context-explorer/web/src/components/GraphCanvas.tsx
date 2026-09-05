import { useMemo } from 'react'
import { Background, Controls, MarkerType, MiniMap, Position, ReactFlow, type Edge, type Node } from '@xyflow/react'
import dagre from 'dagre'
import '@xyflow/react/dist/style.css'
import { GraphShell } from './ui'
import type { GraphEdge, GraphNode, Selection } from '../types/model'
import { traduzir } from '../lib/i18n'

const colors: Record<string, { border: string; soft: string; label: string }> = {
  domain: { border: '#123a63', soft: '#edf3f7', label: 'Domain' },
  spec: { border: '#1671b8', soft: '#eef7fd', label: 'Spec' },
  task: { border: '#7655a6', soft: '#f5f0fb', label: 'Task' },
  code: { border: '#607185', soft: '#f2f5f7', label: 'Code' },
  test: { border: '#25935a', soft: '#edf9f2', label: 'Test' },
  gate: { border: '#c68c00', soft: '#fff8e5', label: 'Gate' },
  human_gate: { border: '#c68c00', soft: '#fff8e5', label: 'Human gate' },
  human_gate_paused: { border: '#c55245', soft: '#fff1ef', label: 'Paused' },
  decision: { border: '#7655a6', soft: '#f5f0fb', label: 'Decision' },
  terminal: { border: '#25935a', soft: '#edf9f2', label: 'Terminal' },
  failure: { border: '#c55245', soft: '#fff1ef', label: 'Failure' },
  external_failure: { border: '#c68c00', soft: '#fff8e5', label: 'External' },
  blocked: { border: '#c55245', soft: '#fff1ef', label: 'Blocked' },
  actor: { border: '#1671b8', soft: '#eef7fd', label: 'Actor' },
  artifact: { border: '#607185', soft: '#f2f5f7', label: 'Artifact' },
  process: { border: '#7655a6', soft: '#f5f0fb', label: 'Process' },
  validation: { border: '#25935a', soft: '#edf9f2', label: 'Validation' },
  state: { border: '#25935a', soft: '#edf9f2', label: 'State' },
  lateral: { border: '#c68c00', soft: '#fff8e5', label: 'Lateral' }
}

function compactLabel(value: string) {
  if (value.length <= 54) return value
  const parts = value.split('/')
  const slash = parts[parts.length - 1]
  return slash && slash.length <= 54 ? slash : `${value.slice(0, 51)}…`
}

function layoutNodes(items: GraphNode[], edges: GraphEdge[], direction: 'TB' | 'LR') {
  const width = direction === 'LR' ? 228 : 218
  const height = 76
  const graph = new dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}))
  graph.setGraph({ rankdir: direction, nodesep: 38, ranksep: 88, marginx: 28, marginy: 28 })
  items.forEach(item => graph.setNode(item.id, { width, height }))
  edges.forEach(edge => graph.setEdge(edge.from, edge.to))
  dagre.layout(graph)
  return items.map(item => {
    const position = graph.node(item.id) ?? { x: 0, y: 0 }
    const palette = colors[item.type ?? ''] ?? colors.artifact
    const originalLabel = String(item.label ?? item.id)
    const label = traduzir(originalLabel)
    const subtitle = traduzir(item.status ?? item.state ?? item.type ?? 'artifact')
    return {
      id: item.id,
      position: { x: position.x - width / 2, y: position.y - height / 2 },
      data: {
        label: <div style={{ textAlign: 'left' }}><span style={{ display: 'block', marginBottom: 5, color: palette.border, fontSize: 9, fontWeight: 800, letterSpacing: '.09em', textTransform: 'uppercase' }}>{traduzir(palette.label)} · {subtitle}</span><strong title={`${label} (${originalLabel})`} style={{ display: 'block', color: '#183149', fontSize: 11.5, lineHeight: 1.3 }}>{compactLabel(label)}</strong></div>,
        entity: item
      },
      style: { width, minHeight: height, border: `1px solid ${palette.border}`, borderLeftWidth: 4, borderRadius: 11, background: '#fff', padding: '10px 12px', boxShadow: '0 5px 16px rgba(16,47,79,.06)' },
      sourcePosition: direction === 'LR' ? Position.Right : Position.Bottom,
      targetPosition: direction === 'LR' ? Position.Left : Position.Top
    } satisfies Node
  })
}

export function GraphCanvas({ nodes: sourceNodes, edges: sourceEdges, onSelect, direction = 'TB', height = 590, minimap = true }: { nodes: GraphNode[]; edges: GraphEdge[]; onSelect: (entity: Selection) => void; direction?: 'TB' | 'LR'; height?: number; minimap?: boolean }) {
  const nodes = useMemo(() => layoutNodes(sourceNodes, sourceEdges, direction), [sourceNodes, sourceEdges, direction])
  const edges = useMemo<Edge[]>(() => sourceEdges.map((edge, index) => ({
    id: `${edge.from}-${edge.to}-${index}`,
    source: edge.from,
    target: edge.to,
    label: traduzir(edge.label ?? edge.condition ?? edge.relation ?? ''),
    markerEnd: { type: MarkerType.ArrowClosed, width: 15, height: 15 },
    style: { stroke: '#8ca0b2', strokeWidth: 1.3 },
    labelStyle: { fill: '#5f7182', fontSize: 9, fontWeight: 650 },
    labelBgStyle: { fill: '#fbfcfd', fillOpacity: .92 },
    labelBgPadding: [4, 3]
  })), [sourceEdges])

  return <GraphShell $height={height}><ReactFlow nodes={nodes} edges={edges} fitView fitViewOptions={{ padding: .22, maxZoom: 1.05 }} minZoom={0.18} maxZoom={1.8} nodesDraggable={false} onNodeClick={(_, node) => onSelect(node.data.entity as Selection)}><Background color="#dfe6ec" gap={22} size={1} /><Controls showInteractive={false} />{minimap && <MiniMap pannable zoomable nodeStrokeWidth={2} maskColor="rgba(240,244,247,.7)" />}</ReactFlow></GraphShell>
}
