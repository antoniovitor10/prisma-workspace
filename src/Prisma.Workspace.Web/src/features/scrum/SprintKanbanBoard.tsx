import {
  DndContext,
  PointerSensor,
  pointerWithin,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { GripVertical, LoaderCircle } from 'lucide-react';
import { useMemo } from 'react';
import type { DragEvent as NativeDragEvent } from 'react';
import styled from 'styled-components';
import { previewMode } from '../../preview';
import { api } from '../../services/api';
import type { BacklogItem, Sprint } from '../../types/scrum';
import { kindNames } from '../../types/scrum';

interface Stage {
  id: string;
  boardId?: string;
  projectId?: string;
  name: string;
  position: number;
  isFinal?: boolean;
}

interface BoardGroup {
  boardId: string;
  boardName: string;
  items: BacklogItem[];
}

export function groupSprintItemsByBoard(items: BacklogItem[]): BoardGroup[] {
  const groups = new Map<string, BoardGroup>();
  for (const item of items) {
    const current = groups.get(item.boardId) ?? { boardId: item.boardId, boardName: item.boardName, items: [] };
    current.items.push(item);
    groups.set(item.boardId, current);
  }
  return [...groups.values()].sort((left, right) => left.boardName.localeCompare(right.boardName, 'pt-BR'));
}

const Section = styled.section`
  margin-top: 12px; padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  > header { display: flex; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
  h3 { font-size: 14px; }
  p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const Columns = styled.div`
  display: grid; grid-auto-columns: minmax(230px, 1fr); grid-auto-flow: column;
  gap: 9px; overflow-x: auto; padding-bottom: 3px;
`;

const Column = styled.div<{ $over: boolean }>`
  min-height: 150px; padding: 9px;
  border: 1px solid ${({ $over, theme }) => $over ? theme.color.brand : 'transparent'};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ $over, theme }) => $over
    ? `color-mix(in srgb, ${theme.color.brand} 7%, ${theme.color.neutral[50]})`
    : theme.color.neutral[50]};
  transition: border-color .15s ease, background .15s ease;
  h4 { display: flex; justify-content: space-between; margin-bottom: 8px; color: ${({ theme }) => theme.color.neutral[700]}; font-size: 13px; text-transform: uppercase; }
`;

const Card = styled.article<{ $dragging: boolean }>`
  display: grid; width: 100%; margin-bottom: 6px; padding: 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  font-size: 13px; font-weight: 650; line-height: 1.4; text-align: left;
  box-shadow: ${({ theme }) => theme.shadow.sm}; opacity: ${({ $dragging }) => $dragging ? .45 : 1}; cursor: grab;
  user-select: none; touch-action: none;
  small { display: block; margin-top: 6px; color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; }
`;

const CardTop = styled.div`display:flex;align-items:flex-start;gap:6px;`;
const DragButton = styled.button`display:grid;width:24px;height:24px;flex:0 0 auto;place-items:center;border-radius:5px;color:${({theme})=>theme.color.textMuted};cursor:grab;&:focus-visible{outline:2px solid ${({theme})=>theme.color.brand};}`;
const CardContent = styled.button`min-width:0;flex:1;color:inherit;text-align:left;font:inherit;strong{display:block;}`;
const MoveField = styled.label`
  display:grid;grid-template-columns:auto minmax(0,1fr);gap:7px;align-items:center;margin-top:9px;
  color:${({theme})=>theme.color.textMuted};font-size:11px;font-weight:700;
  select{min-width:0;min-height:30px;padding:0 7px;border:1px solid ${({theme})=>theme.color.border};border-radius:6px;background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};font-size:12px;}
`;

const Empty = styled.div`
  min-height: 86px; padding: 24px 8px; color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px; text-align: center;
`;

const Notice = styled.p`
  display: flex; align-items: center; gap: 6px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px;
`;

function SprintCard({ item, stages, disabled, showPoints, onOpen, onMove }: {
  item: BacklogItem;
  stages: Stage[];
  disabled: boolean;
  showPoints: boolean;
  onOpen: (item: BacklogItem) => void;
  onMove: (item: BacklogItem, stage: Stage) => void;
}) {
  const draggable = useDraggable({ id: `sprint-item:${item.id}`, disabled, data: { item } });
  return (
    <Card
      ref={draggable.setNodeRef}
      style={{ transform: draggable.transform ? `translate3d(${draggable.transform.x}px, ${draggable.transform.y}px, 0)` : undefined }}
      $dragging={draggable.isDragging}
      aria-label={`Abrir ${item.title}`}
      data-testid="sprint-card"
      data-work-item-id={item.id}
      draggable={!disabled}
      onDragStart={(event: NativeDragEvent<HTMLElement>) => {
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('application/x-work-item-id', item.id);
        event.dataTransfer.setData('text/plain', item.id);
      }}
    >
      <CardTop>
        <DragButton type="button" aria-label={`Arrastar ${item.title}`} disabled={disabled}
          {...draggable.listeners} {...draggable.attributes}>
          <GripVertical aria-hidden size={14} />
        </DragButton>
        <CardContent type="button" onClick={() => onOpen(item)}>
          <strong>{item.title}</strong>
        </CardContent>
      </CardTop>
      <small>{kindNames[item.kind]} · {showPoints&&item.points != null ? `${item.points} pts` : item.remainingHours != null ? `${item.remainingHours}h` : 'sem estimativa'}</small>
      <MoveField>
        <span>Mover para</span>
        <select aria-label={`Mover ${item.title} para etapa`} value={item.stageId ?? ''} disabled={disabled}
          onChange={(event) => {
            const stage = stages.find((candidate) => candidate.id === event.target.value);
            if (stage) onMove(item, stage);
          }}>
          {stages.map((stage) => <option key={stage.id} value={stage.id}>{stage.name}</option>)}
        </select>
      </MoveField>
    </Card>
  );
}

function SprintColumn({ boardId, stage, stages, items, disabled, showPoints, onOpen, onNativeDrop }: {
  boardId: string;
  stage: Stage;
  stages: Stage[];
  items: BacklogItem[];
  disabled: boolean;
  showPoints: boolean;
  onOpen: (item: BacklogItem) => void;
  onNativeDrop: (workItemId: string, stage: Stage) => void;
}) {
  const droppable = useDroppable({ id: `sprint-stage:${boardId}:${stage.id}`, disabled, data: { boardId, stage } });
  return (
    <Column
      ref={droppable.setNodeRef}
      $over={droppable.isOver}
      data-testid="sprint-stage"
      data-stage-id={stage.id}
      onDragOver={(event) => { if (!disabled) { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; } }}
      onDrop={(event) => {
        if (disabled) return;
        event.preventDefault();
        const workItemId = event.dataTransfer.getData('application/x-work-item-id') || event.dataTransfer.getData('text/plain');
        if (workItemId) onNativeDrop(workItemId, stage);
      }}
    >
      <h4><span>{stage.name}</span><span>{items.length}</span></h4>
      {items.sort((left, right) => (left.position ?? 0) - (right.position ?? 0)).map((item) => (
        <SprintCard key={item.id} item={item} stages={stages} disabled={disabled} showPoints={showPoints}
          onOpen={onOpen} onMove={(entry, targetStage) => onNativeDrop(entry.id, targetStage)} />
      ))}
      {items.length === 0 && <Empty>Solte um item aqui</Empty>}
    </Column>
  );
}

interface SprintKanbanBoardProps {
  projectId: string;
  sprint: Sprint;
  items: BacklogItem[];
  onOpenItem: (item: BacklogItem) => void;
  onError: (message: string) => void;
  showPoints?: boolean;
}

export default function SprintKanbanBoard({ projectId, sprint, items, onOpenItem, onError, showPoints = true }: SprintKanbanBoardProps) {
  const queryClient = useQueryClient();
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const boards = useMemo(() => groupSprintItemsByBoard(items), [items]);
  const stagesQuery = useQuery({
    queryKey: ['stages', projectId],
    queryFn: () => api.getStages(projectId) as Promise<Stage[]>,
    retry: false,
    staleTime: 30_000,
    enabled: Boolean(projectId),
  });
  const stages = stagesQuery.data ?? [];
  const readOnly = sprint.status >= 3;

  const moveMutation = useMutation({
    mutationFn: async ({ item, stage, position }: { item: BacklogItem; stage: Stage; position: number }) => {
      if (!previewMode) await api.moveWorkItem(item.id, stage.id, position);
    },
    onMutate: async ({ item, stage, position }) => {
      onError('');
      await queryClient.cancelQueries({ queryKey: ['project-backlog', projectId] });
      const previous = queryClient.getQueryData<BacklogItem[]>(['project-backlog', projectId]);
      queryClient.setQueryData<BacklogItem[]>(['project-backlog', projectId], (current = []) => current.map((candidate) =>
        candidate.id === item.id ? { ...candidate, stageId: stage.id, stageName: stage.name, position } : candidate));
      return { previous };
    },
    onError: (caught, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(['project-backlog', projectId], context.previous);
      onError(caught instanceof Error ? caught.message : 'Não foi possível mover o item. A alteração foi desfeita.');
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['project-backlog', projectId] }),
  });

  const moveItemToStage = (item: BacklogItem | undefined, stage: Stage | undefined, targetBoardId?: string) => {
    if (readOnly || moveMutation.isPending) return;
    if (!item || !stage || (targetBoardId && targetBoardId !== item.boardId)) {
      onError('Itens só podem ser movidos entre etapas do mesmo quadro.');
      return;
    }
    if (item.stageId === stage.id) return;
    const destinationItems = items.filter((candidate) => candidate.boardId === item.boardId && candidate.stageId === stage.id);
    const position = Math.max(0, ...destinationItems.map((candidate) => candidate.position ?? 0)) + 1000;
    moveMutation.mutate({ item, stage, position });
  };

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over) return;
    const item = active.data.current?.item as BacklogItem | undefined;
    const target = over.data.current as { boardId?: string; stage?: Stage } | undefined;
    moveItemToStage(item, target?.stage, target?.boardId);
  };

  if (boards.length === 0) return <Section><Empty>Nenhum item planejado nesta sprint.</Empty></Section>;

  return (
    <DndContext sensors={sensors} collisionDetection={pointerWithin} onDragEnd={onDragEnd}>
      {boards.map((board) => {
        const loading = stagesQuery.isLoading;
        const boardStages = stages.filter(stage => stage.boardId === board.boardId);
        return (
          <Section key={board.boardId} aria-label={`Quadro ${board.boardName}`}>
            <header>
              <div><h3>{board.boardName}</h3><p>{board.items.length} itens · fluxo do projeto</p></div>
              {readOnly && <Notice>Somente leitura: sprint encerrada</Notice>}
              {moveMutation.isPending && <Notice><LoaderCircle aria-hidden size={13} />Salvando movimento...</Notice>}
            </header>
            {loading ? <Empty>Carregando etapas...</Empty> : (
              <Columns>
                {[...boardStages].sort((left, right) => left.position - right.position).map((stage) => (
                  <SprintColumn key={stage.id} boardId={board.boardId} stage={stage} stages={boardStages}
                    items={board.items.filter((item) => item.stageId === stage.id)}
                    disabled={readOnly || moveMutation.isPending} showPoints={showPoints} onOpen={onOpenItem}
                    onNativeDrop={(workItemId, targetStage) => moveItemToStage(
                      items.find(item => item.id === workItemId), targetStage, board.boardId)} />
                ))}
                {boardStages.length === 0 && <Empty>Este projeto ainda não possui etapas.</Empty>}
              </Columns>
            )}
          </Section>
        );
      })}
    </DndContext>
  );
}
