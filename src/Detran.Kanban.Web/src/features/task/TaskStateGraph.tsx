import { useMemo, useState } from 'react';
import { Background, Controls, MarkerType, ReactFlow, type Edge, type Node } from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useQuery } from '@tanstack/react-query';
import styled from 'styled-components';
import { api } from '../../services/api';
import { resolveUserDisplayLabel, type DisplayUser } from '../../utils/userDisplayName';

interface StateNode {
  id: string;
  name: string;
  visits: number;
  totalSeconds: number;
  isCurrent: boolean;
}

interface StateEdge {
  id: string;
  source: string;
  target: string;
  occurredAt: string;
  actorId?: string;
  actorName?: string;
  reason?: string;
}

interface StateGraph { nodes: StateNode[]; edges: StateEdge[]; }

const Canvas = styled.div`
  height: 390px;
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.neutral[50]};
`;

const Inspector = styled.div`
  margin-top: 10px;
  padding: 11px 13px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  line-height: 1.5;
  strong { color: ${({ theme }) => theme.color.text}; }
`;

const Empty = styled.div`
  display: grid;
  min-height: 210px;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
`;

const durationLabel = (seconds: number) => {
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}min`;
  if (seconds < 86400) return `${(seconds / 3600).toFixed(1)}h`;
  return `${(seconds / 86400).toFixed(1)}d`;
};

export function TaskStateGraph({ workItemId }: { workItemId: string }) {
  const [selected, setSelected] = useState<StateNode | StateEdge | null>(null);
  const query = useQuery<StateGraph>({
    queryKey: ['task-state-graph', workItemId],
    queryFn: () => api.getTaskStateGraph(workItemId) as Promise<StateGraph>,
  });
  const usersQuery = useQuery<DisplayUser[]>({
    queryKey: ['assignable-users'],
    queryFn: () => api.getAssignableUsers() as Promise<DisplayUser[]>,
  });
  const users = usersQuery.data ?? [];

  const flow = useMemo(() => {
    const source = query.data;
    if (!source) return { nodes: [] as Node[], edges: [] as Edge[] };
    const nodes: Node[] = source.nodes.map((node, index) => ({
      id: node.id,
      position: { x: (index % 2) * 290, y: Math.floor(index / 2) * 130 },
      data: { label: `${node.name}\n${node.visits} visita(s) · ${durationLabel(node.totalSeconds)}` },
      style: {
        width: 230,
        whiteSpace: 'pre-line',
        border: `2px solid ${node.isCurrent ? '#16834f' : '#1671b9'}`,
        borderRadius: 12,
        background: node.isCurrent ? '#edf9f2' : '#fff',
        color: '#142a3f',
        fontWeight: 700,
        padding: 12,
      },
    }));
    const edges: Edge[] = source.edges.map(edge => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      label: edge.reason || resolveUserDisplayLabel(users, edge.actorName, edge.actorId),
      markerEnd: { type: MarkerType.ArrowClosed },
      style: { strokeWidth: 2, stroke: '#66839e' },
      labelStyle: { fill: '#425d75', fontSize: 11 },
    }));
    return { nodes, edges };
  }, [query.data, users]);

  if (query.isLoading) return <Empty>Carregando caminho da tarefa...</Empty>;
  if (query.error) return <Empty>Nao foi possivel carregar o caminho da tarefa.</Empty>;
  if (!query.data?.nodes.length) return <Empty>A tarefa ainda nao possui transicoes registradas.</Empty>;

  const selectedEdge = selected && 'source' in selected ? selected as StateEdge : null;
  const selectedNode = selected && 'visits' in selected ? selected as StateNode : null;
  return <>
    <Canvas aria-label="Grafo de estados da tarefa">
      <ReactFlow
        nodes={flow.nodes}
        edges={flow.edges}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable
        onNodeClick={(_, node) => setSelected(query.data!.nodes.find(item => item.id === node.id) ?? null)}
        onEdgeClick={(_, edge) => setSelected(query.data!.edges.find(item => item.id === edge.id) ?? null)}
      >
        <Background gap={18} size={1} />
        <Controls showInteractive={false} />
      </ReactFlow>
    </Canvas>
    <Inspector>
      {selectedNode && <><strong>{selectedNode.name}</strong><br />{selectedNode.visits} visita(s), permanencia total de {durationLabel(selectedNode.totalSeconds)}{selectedNode.isCurrent ? ' · etapa atual' : ''}.</>}
      {selectedEdge && <><strong>Transicao em {new Date(selectedEdge.occurredAt).toLocaleString('pt-BR')}</strong><br />Responsavel: {resolveUserDisplayLabel(users, selectedEdge.actorName, selectedEdge.actorId)}{selectedEdge.reason ? ` · Motivo: ${selectedEdge.reason}` : ' · sem motivo informado'}.</>}
      {!selected && <>Clique em uma etapa ou transicao para ver quem executou, quando ocorreu e o motivo registrado.</>}
    </Inspector>
  </>;
}
