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

interface StateGraph { nodes: StateNode[]; edges: StateEdge[]; }

// ── Tipos da timeline linearizada ─────────────────────────────────

interface TimelineStep {
  /** Nó da etapa destino desta transição (o estado em que a tarefa "chegou"). */
  node: StateNode;
  /** A transição que trouxe a tarefa até aqui (null para o primeiro estado). */
  edge: StateEdge | null;
}

// ── Helpers ───────────────────────────────────────────────────────

const durationLabel = (seconds: number) => {
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.round(seconds / 60)}min`;
  if (seconds < 86400) return `${(seconds / 3600).toFixed(1)}h`;
  return `${(seconds / 86400).toFixed(1)}d`;
};

const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString('pt-BR', { day: 'numeric', month: 'short', year: 'numeric' });

/** Gera iniciais (até 2 letras) a partir de um nome. */
const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
};

/**
 * Determina a cor indicativa do estado.
 * Segue o espectro Prisma: azul para estados em andamento, verde para concluído/atual.
 */
const dotColor = (node: StateNode, theme: AppTheme) => {
  if (node.isCurrent) return theme.color.primary;
  return theme.color.neutral[400];
};

/**
 * Lineariza o grafo (nós agregados + edges cronológicas) em uma sequência
 * temporal de passos, onde cada passo é um par (estado de chegada, transição).
 * 
 * O primeiro passo não possui transição (é a etapa inicial).
 */
function buildTimeline(graph: StateGraph): TimelineStep[] {
  if (!graph.edges.length) {
    // Tarefa tem apenas um estado (nunca transitou)
    const singleNode = graph.nodes.find(n => n.isCurrent) ?? graph.nodes[0];
    return singleNode ? [{ node: singleNode, edge: null }] : [];
  }

  const nodeMap = new Map(graph.nodes.map(n => [n.id, n]));
  const steps: TimelineStep[] = [];

  // Primeiro estado: source da primeira edge
  const firstEdge = graph.edges[0];
  const firstNode = nodeMap.get(firstEdge.source);
  if (firstNode) {
    steps.push({ node: firstNode, edge: null });
  }

  // Cada edge produz o passo seguinte
  for (const edge of graph.edges) {
    const target = nodeMap.get(edge.target);
    if (target) {
      steps.push({ node: target, edge });
    }
  }

  return steps;
}

// ── Styled Components ─────────────────────────────────────────────

const Track = styled.div`
  overflow-x: auto;
  overflow-y: hidden;
  padding: 8px 0 14px;
  scrollbar-width: thin;
  scrollbar-color: ${({ theme }) => theme.color.neutral[300]} transparent;

  &::-webkit-scrollbar { height: 6px; }
  &::-webkit-scrollbar-thumb {
    background: ${({ theme }) => theme.color.neutral[300]};
    border-radius: 3px;
  }
`;

const Timeline = styled.div`
  display: inline-flex;
  align-items: flex-start;
  gap: 0;
  min-width: 100%;
  padding: 0 4px;
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
  min-width: 150px;
  max-width: 210px;
  padding: 0 4px;
`;

const ArrowLabel = styled.div`
  font-size: 11px;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  line-height: 1.3;
  margin-bottom: 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
`;

const ArrowLine = styled.div`
  display: flex;
  align-items: center;
  width: 100%;
  height: 32px;
`;

const ArrowBar = styled.div`
  flex: 1;
  height: 4px;
  background: ${({ theme }) => theme.color.primary};
  border-radius: 2px;
`;

const ArrowHead = styled.div`
  width: 0;
  height: 0;
  border-top: 8px solid transparent;
  border-bottom: 8px solid transparent;
  border-left: 12px solid ${({ theme }) => theme.color.primary};
  flex-shrink: 0;
`;

const ArrowMeta = styled.div`
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 3px;
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
  color: ${({ theme }) => theme.color.textMuted};
  line-height: 1.3;

  .name { color: ${({ theme }) => theme.color.text}; font-weight: 500; }
  .date { display: block; }
`;

const StateNodeBubble = styled.div<{ $dotColor: string; $isCurrent: boolean }>`
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: ${({ theme, $isCurrent }) =>
    $isCurrent ? theme.color.surface : theme.color.neutral[50]};
  border: ${({ theme, $isCurrent }) =>
    $isCurrent ? `2px solid ${theme.color.primary}` : `1px solid ${theme.color.border}`};
  border-radius: ${({ theme }) => theme.radius.md};
  white-space: nowrap;
  flex-shrink: 0;
  min-height: 40px;
  position: relative;
  top: 14px; /* alinha verticalmente com o centro da seta */
`;

const Dot = styled.div<{ $color: string }>`
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: ${({ $color }) => $color};
  flex-shrink: 0;
`;

const StateName = styled.span<{ $isCurrent: boolean }>`
  font-size: 14px;
  font-weight: ${({ $isCurrent }) => $isCurrent ? 700 : 500};
  color: ${({ theme }) => theme.color.text};
`;

const Empty = styled.div`
  display: grid;
  min-height: 210px;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
`;

// ── Paleta pseudo-aleatória para avatares ─────────────────────────

const avatarColors = ['#7C3AED', '#2563EB', '#06B6D4', '#DB2777', '#F97316', '#10B981', '#E11D48'];

function avatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0;
  return avatarColors[Math.abs(hash) % avatarColors.length];
}

// ── Componente de transição (seta) ────────────────────────────────

function TransitionArrow({ edge, users }: { edge: StateEdge; users: DisplayUser[] }) {
  const actor = resolveUserDisplayLabel(users, edge.actorName, edge.actorId);
  const description = edge.reason
    ? edge.reason
    : `Movido para etapa`;

  return (
    <ArrowSection>
      <ArrowLabel title={description}>{description}</ArrowLabel>
      <ArrowLine>
        <ArrowBar />
        <ArrowHead />
      </ArrowLine>
      <ArrowMeta>
        <Avatar $color={avatarColor(actor)}>{initials(actor)}</Avatar>
        <ActorInfo>
          <span className="name">{actor}</span>
          <span className="date">{formatDate(edge.occurredAt)}</span>
        </ActorInfo>
      </ArrowMeta>
    </ArrowSection>
  );
}

// ── Componente principal ──────────────────────────────────────────

export function TaskStateGraph({ workItemId }: { workItemId: string }) {
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

  const timeline = buildTimeline(query.data);

  return (
    <Track ref={trackRef} aria-label="Grafo de estados da tarefa">
      <Timeline>
        {timeline.map((step, i) => (
          <StepGroup key={`${step.node.id}-${i}`}>
            {/* Seta de transição (não existe no primeiro passo) */}
            {step.edge && <TransitionArrow edge={step.edge} users={users} />}

            {/* Bolha do estado */}
            <StateNodeBubble
              $dotColor={dotColor(step.node, theme)}
              $isCurrent={step.node.isCurrent}
              title={`${step.node.visits} visita(s) · Permanência: ${durationLabel(step.node.totalSeconds)}`}
            >
              <Dot $color={dotColor(step.node, theme)} />
              <StateName $isCurrent={step.node.isCurrent}>
                {step.node.name}
              </StateName>
            </StateNodeBubble>
          </StepGroup>
        ))}
      </Timeline>
    </Track>
  );
}
