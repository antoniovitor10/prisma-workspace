import { useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import styled, { useTheme } from 'styled-components';
import { api } from '../../services/api';
import { resolveUserDisplayLabel, type DisplayUser } from '../../utils/userDisplayName';
import type { AppTheme } from '../../styles/theme';

// ── Tipos do backend ──────────────────────────────────────────────

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

interface StateGraph {
  nodes: StateNode[];
  edges: StateEdge[];
}

// ── Tipos da timeline linearizada ─────────────────────────────────

interface TimelineStep {
  /** Nó da etapa em que a tarefa chegou neste passo. */
  node: StateNode;
  /** Transição que levou a tarefa a este estado. */
  edge: {
    occurredAt: string;
    actorId?: string;
    actorName?: string;
    label: string;
  };
}

// ── Helpers ───────────────────────────────────────────────────────

const durationLabel = (seconds: number) => {
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}min`;
  if (seconds < 86400) return `${(seconds / 3600).toFixed(1)}h`;
  return `${(seconds / 86400).toFixed(1)}d`;
};

const formatDate = (iso: string) => {
  try {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleDateString('pt-BR', { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
};

/** Iniciais (até 2 letras) a partir de um nome. */
const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
};

/**
 * Cores dos indicadores de estado (dots), alinhadas ao Azure DevOps / espectro Prisma.
 */
function getStateDotColor(name: string, theme: AppTheme): string {
  const lower = name.toLowerCase();
  if (lower.includes('conclu') || lower.includes('done') || lower.includes('finaliz')) {
    return theme.color.success; // '#10B981'
  }
  if (lower.includes('andamento') || lower.includes('progress') || lower.includes('fazendo') || lower.includes('desenvolv')) {
    return theme.color.primary; // '#2563EB'
  }
  if (lower.includes('ready') || lower.includes('pront') || lower.includes('aguardando') || lower.includes('espera')) {
    return '#EAB308'; // Amber / Yellow
  }
  if (lower.includes('revis') || lower.includes('review') || lower.includes('valid') || lower.includes('test') || lower.includes('qa') || lower.includes('homolog')) {
    return theme.color.accentViolet; // '#7C3AED'
  }
  if (lower.includes('cancel') || lower.includes('imped') || lower.includes('bloque')) {
    return theme.color.danger; // '#E11D48'
  }
  return theme.color.neutral[400]; // '#94A3B8'
}

/**
 * Lineariza o grafo em passos cronológicos.
 * Todo estado possui uma transição de entrada à esquerda (padrão Azure DevOps).
 * O primeiro estado recebe a transição de criação ("Novo item de trabalho").
 */
function buildTimeline(
  graph: StateGraph,
  createdAt?: string,
  createdByName?: string
): TimelineStep[] {
  if (!graph.nodes.length) return [];

  const nodeMap = new Map(graph.nodes.map(n => [n.id, n]));
  const steps: TimelineStep[] = [];

  // Primeiro nó (estado inicial da tarefa)
  const firstNode = graph.edges.length > 0
    ? (nodeMap.get(graph.edges[0].source) ?? graph.nodes[0])
    : (graph.nodes.find(n => n.isCurrent) ?? graph.nodes[0]);

  if (firstNode) {
    const creationTime = createdAt || (graph.edges.length > 0 ? graph.edges[0].occurredAt : new Date().toISOString());
    steps.push({
      node: firstNode,
      edge: {
        occurredAt: creationTime,
        actorName: createdByName || (graph.edges.length > 0 ? graph.edges[0].actorName : undefined),
        label: 'Novo item de trabalho',
      },
    });
  }

  // Transições subsequentes entre estados
  for (const edge of graph.edges) {
    const target = nodeMap.get(edge.target);
    if (target) {
      steps.push({
        node: target,
        edge: {
          occurredAt: edge.occurredAt,
          actorId: edge.actorId,
          actorName: edge.actorName,
          label: edge.reason?.trim() || `Movido para ${target.name}`,
        },
      });
    }
  }

  return steps;
}

// ── Styled Components ─────────────────────────────────────────────

const Track = styled.div`
  overflow-x: auto;
  overflow-y: hidden;
  padding: 12px 4px 16px;
  scrollbar-width: thin;
  scrollbar-color: ${({ theme }) => theme.color.neutral[300]} transparent;

  &::-webkit-scrollbar {
    height: 6px;
  }
  &::-webkit-scrollbar-thumb {
    background: ${({ theme }) => theme.color.neutral[300]};
    border-radius: 3px;
  }
`;

const Timeline = styled.div`
  display: inline-flex;
  align-items: flex-start;
  min-width: 100%;
`;

const StepGroup = styled.div`
  display: flex;
  align-items: flex-start;
  flex-shrink: 0;
`;

const ArrowSection = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 175px;
  flex-shrink: 0;
  padding: 0 2px;
`;

const ArrowLabel = styled.div`
  font-size: 11px;
  font-weight: 500;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  height: 16px;
  line-height: 16px;
  margin-bottom: 6px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  width: 100%;
`;

const ArrowLine = styled.div`
  display: flex;
  align-items: center;
  width: 100%;
  height: 36px;
`;

const ArrowBar = styled.div`
  flex: 1;
  height: 3px;
  background: ${({ theme }) => theme.color.primary};
`;

const ArrowHead = styled.div`
  width: 0;
  height: 0;
  border-top: 6px solid transparent;
  border-bottom: 6px solid transparent;
  border-left: 10px solid ${({ theme }) => theme.color.primary};
  flex-shrink: 0;
`;

const ArrowMeta = styled.div`
  display: flex;
  align-items: center;
  gap: 7px;
  margin-top: 6px;
  width: 100%;
  padding-left: 6px;
`;

const Avatar = styled.div<{ $color: string }>`
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: ${({ $color }) => $color};
  color: #fff;
  font-size: 9px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
`;

const ActorInfo = styled.div`
  font-size: 11px;
  line-height: 1.25;
  overflow: hidden;

  .name {
    display: block;
    color: ${({ theme }) => theme.color.text};
    font-weight: 500;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .date {
    display: block;
    color: ${({ theme }) => theme.color.textMuted};
    font-size: 10px;
    white-space: nowrap;
  }
`;

const NodeColumn = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  flex-shrink: 0;
`;

const NodeTopSpacer = styled.div`
  height: 22px; /* Espaço para coincidir com a altura do ArrowLabel + margem */
`;

const StateNodeBubble = styled.div<{ $isCurrent: boolean }>`
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 36px;
  padding: 0 14px;
  background: ${({ theme, $isCurrent }) =>
    $isCurrent
      ? theme.mode === 'dark' ? '#1e293b' : '#eff6ff'
      : theme.color.surface};
  border: ${({ theme, $isCurrent }) =>
    $isCurrent
      ? `2px solid ${theme.color.primary}`
      : `1px solid ${theme.color.border}`};
  border-radius: ${({ theme }) => theme.radius.sm};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  white-space: nowrap;
  flex-shrink: 0;
`;

const Dot = styled.div<{ $color: string }>`
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: ${({ $color }) => $color};
  flex-shrink: 0;
`;

const StateName = styled.span<{ $isCurrent: boolean }>`
  font-size: 13px;
  font-weight: ${({ $isCurrent }) => ($isCurrent ? 700 : 500)};
  color: ${({ theme }) => theme.color.text};
`;

const Empty = styled.div`
  display: grid;
  min-height: 200px;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
`;

// ── Paleta de cores para avatares ─────────────────────────────────

const avatarColors = ['#7C3AED', '#2563EB', '#06B6D4', '#DB2777', '#F97316', '#10B981', '#E11D48'];

function avatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
  return avatarColors[Math.abs(hash) % avatarColors.length];
}

// ── Componente de transição (seta) ────────────────────────────────

function TransitionArrow({
  edge,
  users,
}: {
  edge: TimelineStep['edge'];
  users: DisplayUser[];
}) {
  const actor = resolveUserDisplayLabel(users, edge.actorName, edge.actorId);

  return (
    <ArrowSection>
      <ArrowLabel title={edge.label}>{edge.label}</ArrowLabel>
      <ArrowLine>
        <ArrowBar />
        <ArrowHead />
      </ArrowLine>
      <ArrowMeta>
        <Avatar $color={avatarColor(actor)}>{initials(actor)}</Avatar>
        <ActorInfo>
          <span className="name" title={actor}>{actor}</span>
          <span className="date">{formatDate(edge.occurredAt)}</span>
        </ActorInfo>
      </ArrowMeta>
    </ArrowSection>
  );
}

// ── Componente principal ──────────────────────────────────────────

export interface TaskStateGraphProps {
  workItemId: string;
  createdAt?: string;
  createdByName?: string;
}

export function TaskStateGraph({ workItemId, createdAt, createdByName }: TaskStateGraphProps) {
  const theme = useTheme() as AppTheme;
  const trackRef = useRef<HTMLDivElement>(null);

  const query = useQuery<StateGraph>({
    queryKey: ['task-state-graph', workItemId],
    queryFn: () => api.getTaskStateGraph(workItemId) as Promise<StateGraph>,
  });
  const usersQuery = useQuery<DisplayUser[]>({
    queryKey: ['assignable-users'],
    queryFn: () => api.getAssignableUsers() as Promise<DisplayUser[]>,
  });
  const users = usersQuery.data ?? [];

  if (query.isLoading) return <Empty>Carregando caminho da tarefa...</Empty>;
  if (query.error) return <Empty>Não foi possível carregar o caminho da tarefa.</Empty>;
  if (!query.data?.nodes.length) return <Empty>A tarefa ainda não possui transições registradas.</Empty>;

  const timeline = buildTimeline(query.data, createdAt, createdByName);

  return (
    <Track ref={trackRef} aria-label="Grafo de estados da tarefa">
      <Timeline>
        {timeline.map((step, i) => (
          <StepGroup key={`${step.node.id}-${i}`}>
            {/* Seta de transição que entra neste estado */}
            <TransitionArrow edge={step.edge} users={users} />

            {/* Coluna do estado: espaçador superior + bolha do estado */}
            <NodeColumn>
              <NodeTopSpacer />
              <StateNodeBubble
                $isCurrent={step.node.isCurrent}
                title={`${step.node.name} · ${step.node.visits} visita(s) · Permanência: ${durationLabel(step.node.totalSeconds)}${step.node.isCurrent ? ' (estado atual)' : ''}`}
              >
                <Dot $color={getStateDotColor(step.node.name, theme)} />
                <StateName $isCurrent={step.node.isCurrent}>
                  {step.node.name}
                </StateName>
              </StateNodeBubble>
            </NodeColumn>
          </StepGroup>
        ))}
      </Timeline>
    </Track>
  );
}
