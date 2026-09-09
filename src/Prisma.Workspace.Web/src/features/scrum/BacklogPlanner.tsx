import { zodResolver } from '@hookform/resolvers/zod';
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  AlertTriangle,
  ChevronDown,
  ChevronRight,
  ChevronsUpDown,
  ExternalLink,
  GripVertical,
  Layers3,
  Link2,
  ListFilter,
  Plus,
  RotateCcw,
  Search,
  Sparkles,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useSearchParams } from 'react-router-dom';
import styled from 'styled-components';
import { z } from 'zod';
import { TaskDetailDrawer } from '../../components/TaskDetailDrawer';
import type { ProjectSummary } from '../../pages/Projects';
import { previewBacklog, previewMode, previewSprints } from '../../preview';
import { api } from '../../services/api';
import { kindMeta } from '../workItems/workItemKinds';
import type { BacklogItem, Sprint } from '../../types/scrum';
import { getItemDepth, kindNames, priorityNames } from '../../types/scrum';
import {
  defaultBacklogFilters,
  filterBacklogItems,
  getBacklogGroupLabel,
  getBacklogRelations,
  getEpicId,
  sortBacklogItems,
  type BacklogFilterState,
  type BacklogGroupBy,
} from './BacklogFilters';
import { flattenBacklogHierarchy, includeBacklogAncestors } from './BacklogHierarchy';

const Page = styled.section`
  padding: 20px 28px 40px;
  @media (max-width: 760px) { padding: 16px; }
`;

const Toolbar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 14px;
  margin-bottom: 14px;
  h2 { font-size: 18px; }
  p { margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
`;

const ToolbarActions = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
`;

const SearchBox = styled.label`
  display: flex;
  min-width: 230px;
  min-height: 36px;
  align-items: center;
  gap: 7px;
  padding: 0 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  input {
    min-width: 0;
    flex: 1;
    border: 0;
    outline: 0;
    background: transparent;
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
  }
`;

const Button = styled.button<{ $secondary?: boolean; $danger?: boolean }>`
  display: inline-flex;
  min-height: 34px;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0 11px;
  border: 1px solid ${({ $secondary, $danger, theme }) =>
    $secondary ? theme.color.border : $danger ? theme.color.danger : theme.color.brand};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ $secondary, $danger, theme }) =>
    $secondary ? theme.color.surface : $danger ? theme.color.danger : theme.color.brand};
  color: ${({ $secondary, theme }) => $secondary ? theme.color.text : theme.color.onBrand};
  font-size: 13px;
  font-weight: 800;
  &:disabled { cursor: not-allowed; opacity: .5; }
`;

const QuickAdd = styled.form`
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 8px;
  margin-bottom: 12px;
  padding: 10px;
  border: 1px solid ${({ theme }) => theme.color.accentBlue};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  input {
    min-width: 0;
    min-height: 36px;
    padding: 0 10px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    outline: 0;
    &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; }
  }
  p { grid-column: 1 / -1; color: ${({ theme }) => theme.color.danger}; font-size: 13px; }
`;

const FilterBar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 8px;
  margin-bottom: 12px;
  padding: 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
`;

const FilterTitle = styled.span`
  display: inline-flex;
  height: 34px;
  align-items: center;
  gap: 6px;
  margin-right: 2px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 800;
`;

const SelectField = styled.label`
  display: grid;
  gap: 3px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  select {
    min-width: 112px;
    min-height: 30px;
    padding: 0 8px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13px;
    font-weight: 650;
    text-transform: none;
  }
`;

const BulkBar = styled.div`
  position: sticky;
  z-index: 5;
  top: 8px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  padding: 9px 11px;
  border: 1px solid ${({ theme }) => theme.color.accentBlue};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.md};
  strong { margin-right: auto; color: ${({ theme }) => theme.color.brand}; font-size: 13.5px; }
  select {
    min-height: 32px;
    max-width: 210px;
    padding: 0 8px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13px;
  }
`;

const PlanningGrid = styled.div`
  display: grid;
  grid-template-columns: minmax(620px, 1.45fr) minmax(450px, 1fr);
  gap: 14px;
  align-items: start;
  @media (max-width: 1250px) { grid-template-columns: 1fr; }
`;

const Panel = styled.section<{ $over?: boolean }>`
  min-width: 0;
  overflow: hidden;
  border: 1px solid ${({ $over, theme }) => $over ? theme.color.accentBlue : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
`;

const PanelHeader = styled.header`
  display: flex;
  min-height: 58px;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.neutral[50]};
  h3 { font-size: 15px; }
  p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const HeaderActions = styled.div`
  display: flex;
  align-items: center;
  gap: 7px;
`;

const SelectAll = styled.label`
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
  input { accent-color: ${({ theme }) => theme.color.brand}; }
`;

const Count = styled.span`
  display: inline-flex;
  min-width: 26px;
  height: 24px;
  align-items: center;
  justify-content: center;
  padding: 0 7px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[200]};
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 13px;
  font-weight: 800;
`;

const SprintSelect = styled.label`
  position: relative;
  display: flex;
  align-items: center;
  select {
    min-height: 32px;
    max-width: 190px;
    appearance: none;
    padding: 0 28px 0 9px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13px;
    font-weight: 700;
  }
  svg { position: absolute; right: 8px; pointer-events: none; color: ${({ theme }) => theme.color.textMuted}; }
`;

const ColumnLegend = styled.div`
  display: grid;
  grid-template-columns: 26px 24px 64px minmax(180px, 1fr) 106px 68px 132px 92px;
  gap: 7px;
  padding: 7px 9px;
  border-bottom: 1px solid ${({ theme }) => theme.color.neutral[100]};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  @media (max-width: 760px) { display: none; }
`;

const ItemList = styled.div`
  display: grid;
  min-height: 250px;
  gap: 5px;
  padding: 8px;
  overflow-x: auto;
`;

const GroupHeader = styled.div`
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 4px 3px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 850;
  letter-spacing: .04em;
  text-transform: uppercase;
`;

const ItemCard = styled.article<{ $dragging: boolean; $depth: number; $orphaned: boolean; $selected: boolean; $blocked: boolean }>`
  position: relative;
  display: grid;
  grid-template-columns: 26px 24px 64px minmax(180px, 1fr) 106px 68px 132px 92px;
  align-items: center;
  gap: 7px;
  min-height: 54px;
  width: ${({ $depth }) => `calc(100% - ${$depth * 30}px)`};
  margin-left: ${({ $depth }) => $depth * 30}px;
  padding: 5px 8px 5px 3px;
  border: 1px solid ${({ $selected, $blocked, theme }) =>
    $selected ? theme.color.accentBlue : $blocked ? '#F3C17A' : theme.color.neutral[100]};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ $selected, theme }) => $selected
    ? `color-mix(in srgb, ${theme.color.accentBlue} 5%, ${theme.color.surface})`
    : theme.color.surface};
  box-shadow: ${({ $dragging, theme }) => $dragging ? theme.shadow.md : 'none'};
  opacity: ${({ $dragging }) => $dragging ? .72 : 1};
  touch-action: none;
  &::before {
    content: '';
    display: ${({ $depth }) => $depth > 0 ? 'block' : 'none'};
    position: absolute;
    top: -6px;
    bottom: 50%;
    left: -19px;
    width: 14px;
    border-left: 2px ${({ $orphaned }) => $orphaned ? 'dashed' : 'solid'} ${({ $orphaned, theme }) => $orphaned ? '#D49A2A' : theme.color.neutral[300]};
    border-bottom: 2px ${({ $orphaned }) => $orphaned ? 'dashed' : 'solid'} ${({ $orphaned, theme }) => $orphaned ? '#D49A2A' : theme.color.neutral[300]};
    border-bottom-left-radius: 8px;
    pointer-events: none;
  }
  &:hover { border-color: ${({ theme }) => theme.color.neutral[300]}; }
  @media (max-width: 760px) {
    grid-template-columns: 26px 24px repeat(3, minmax(76px, 1fr)) 34px;
    width: ${({ $depth }) => `calc(100% - ${$depth * 22}px)`};
    margin-left: ${({ $depth }) => $depth * 22}px;
    > .item-kind { display: none; }
    > .item-title { grid-column: 3 / 6; grid-row: 1; }
    > .item-priority { grid-column: 3; grid-row: 2; }
    > .item-points { grid-column: 4; grid-row: 2; }
    > .item-epic { grid-column: 5; grid-row: 2; }
    > .item-actions { grid-column: 6; grid-row: 1 / 3; }
  }
`;

const DragHandle = styled.button`
  display: grid;
  width: 26px;
  height: 34px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.sm};
  color: ${({ theme }) => theme.color.neutral[400]};
  cursor: grab;
  &:active { cursor: grabbing; }
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.text}; }
`;

const Selection = styled.label`
  display: grid;
  width: 24px;
  height: 32px;
  place-items: center;
  input { width: 14px; height: 14px; accent-color: ${({ theme }) => theme.color.brand}; }
`;

// Cor vem da taxonomia (workItemKinds.ts): cada tipo tem a sua, nao tres para dez.
const Kind = styled.span<{ $kind: number }>`
  color: ${({ $kind }) => kindMeta($kind).color};
  font-size: 11px;
  font-weight: 850;
  text-transform: uppercase;
`;

const ArchivedTag = styled.span`
  display: inline-flex;
  align-items: center;
  padding: 1px 6px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[200]};
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 10.5px;
  font-weight: 800;
  text-transform: uppercase;
`;

const InlineTitle = styled.label`
  min-width: 0;
  display: grid;
  gap: 2px;
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; font-weight: 750; }
  input {
    width: 100%;
    min-width: 0;
    padding: 4px 5px;
    border: 1px solid transparent;
    border-radius: ${({ theme }) => theme.radius.sm};
    background: transparent;
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
    font-weight: 650;
    outline: 0;
    &:hover { border-color: ${({ theme }) => theme.color.neutral[200]}; }
    &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; background: ${({ theme }) => theme.color.surface}; }
  }
`;

const InlineField = styled.label`
  display: grid;
  gap: 2px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
  font-weight: 700;
  select, input {
    width: 100%;
    min-width: 0;
    min-height: 29px;
    padding: 0 6px;
    border: 1px solid ${({ theme }) => theme.color.neutral[200]};
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 12px;
  }
`;

const ItemActions = styled.div`
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 4px;
`;

const RelationBadge = styled.span<{ $warning?: boolean; $blocking?: boolean }>`
  display: inline-flex;
  height: 24px;
  align-items: center;
  gap: 3px;
  padding: 0 5px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ $warning, $blocking }) => $warning ? '#FEF3C7' : $blocking ? '#E0F2FE' : '#F1F5F9'};
  color: ${({ $warning, $blocking, theme }) => $warning ? '#9A5B00' : $blocking ? '#0369A1' : theme.color.textMuted};
  font-size: 11px;
  font-weight: 800;
`;

const OpenButton = styled.button`
  display: grid;
  width: 26px;
  height: 26px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.sm};
  color: ${({ theme }) => theme.color.textMuted};
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; color: ${({ theme }) => theme.color.brand}; }
`;

const Empty = styled.div`
  display: grid;
  min-height: 210px;
  place-items: center;
  padding: 24px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13.5px;
  line-height: 1.6;
  text-align: center;
`;

const ErrorBanner = styled.div`
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border-left: 3px solid ${({ theme }) => theme.color.danger};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.danger} 6%, ${theme.color.surface})`};
  color: ${({ theme }) => theme.color.danger};
  font-size: 13px;
  font-weight: 700;
`;

type InlinePatch = Partial<Pick<BacklogItem, 'title' | 'priority' | 'points'>> & {
  epicId?: string | null;
  updateEpic?: boolean;
};

interface SortableWorkItemProps {
  item: BacklogItem;
  allItems: BacklogItem[];
  epics: BacklogItem[];
  depth: number;
  hierarchyEnabled: boolean;
  orphaned: boolean;
  selected: boolean;
  saving: boolean;
  hasChildren: boolean;
  collapsed: boolean;
  contextOnly: boolean;
  showPoints: boolean;
  onToggleCollapsed: (itemId: string) => void;
  onToggle: (itemId: string) => void;
  onOpen: (item: BacklogItem) => void;
  onCommit: (item: BacklogItem, patch: InlinePatch) => Promise<void>;
}

function SortableWorkItem({
  item,
  allItems,
  epics,
  depth,
  hierarchyEnabled,
  orphaned,
  selected,
  saving,
  hasChildren,
  collapsed,
  contextOnly,
  showPoints,
  onToggleCollapsed,
  onToggle,
  onOpen,
  onCommit,
}: SortableWorkItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.id });
  const [title, setTitle] = useState(item.title);
  const [points, setPoints] = useState(item.points?.toString() ?? '');
  const relations = getBacklogRelations(item);
  const epicId = getEpicId(item, allItems) ?? '';
  const dependencyTitle = relations.dependencies
    .map((link) => `#${link.relatedNumber} ${link.relatedTitle}${link.isOpen ? '' : ' (concluída)'}`)
    .join('\n');
  const blockingTitle = relations.blocking
    .map((link) => `#${link.relatedNumber} ${link.relatedTitle}`)
    .join('\n');
  const visualDepth = depth > 0 ? depth : orphaned ? 1 : 0;

  useEffect(() => setTitle(item.title), [item.title]);
  useEffect(() => setPoints(item.points?.toString() ?? ''), [item.points]);

  const commitTitle = () => {
    const normalized = title.trim();
    if (!normalized) { setTitle(item.title); return; }
    if (normalized !== item.title) void onCommit(item, { title: normalized });
  };
  const commitPoints = () => {
    const parsed = Number(points);
    if (points.trim() !== '' && !Number.isFinite(parsed)) { setPoints(item.points?.toString() ?? ''); return; }
    const next = points.trim() === '' ? undefined : Math.max(0, Math.round(parsed));
    if (next !== item.points) void onCommit(item, { points: next });
  };

  return (
    <ItemCard
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      $dragging={isDragging}
      $depth={visualDepth}
      $orphaned={orphaned}
      role={hierarchyEnabled ? 'treeitem' : undefined}
      aria-level={hierarchyEnabled ? visualDepth + 1 : undefined}
      aria-expanded={hierarchyEnabled && hasChildren ? !collapsed : undefined}
      data-testid="backlog-item"
      data-work-item-id={item.id}
      data-parent-id={item.parentId ?? ''}
      data-hierarchy-depth={visualDepth}
      data-hierarchy-state={orphaned ? 'orphan' : visualDepth > 0 ? 'child' : 'root'}
      $selected={selected}
      $blocked={relations.blocked}
    >
      <DragHandle {...attributes} {...listeners} aria-label={`Mover ${item.title}`} title="Arrastar item">
        <GripVertical size={14} />
      </DragHandle>
      <Selection title="Selecionar item">
        <input type="checkbox" checked={selected} onChange={() => onToggle(item.id)} aria-label={`Selecionar ${item.title}`} />
      </Selection>
      <Kind className="item-kind" $kind={item.kind} title={kindMeta(item.kind).description}>
        {kindMeta(item.kind).label}
        {item.isArchived && <> <ArchivedTag>Arquivada</ArchivedTag></>}
      </Kind>
      <InlineTitle className="item-title">
        <small>#{item.number ?? '—'} · {saving ? 'Salvando...' : item.boardName}</small>
        <input
          aria-label={`Título de ${item.title}`}
          value={title}
          maxLength={500}
          onChange={(event) => setTitle(event.target.value)}
          onBlur={commitTitle}
          onKeyDown={(event) => {
            if (event.key === 'Enter') event.currentTarget.blur();
            if (event.key === 'Escape') { setTitle(item.title); event.currentTarget.blur(); }
          }}
        />
      </InlineTitle>
      <InlineField className="inline-field item-priority">
        <span>Prioridade</span>
        <select
          aria-label={`Prioridade de ${item.title}`}
          value={item.priority}
          onChange={(event) => void onCommit(item, { priority: Number(event.target.value) })}
        >
          {[0, 1, 2, 3].map((priority) => <option key={priority} value={priority}>{priorityNames[priority]}</option>)}
        </select>
      </InlineField>
      {showPoints && <InlineField className="inline-field item-points">
        <span>Pontos</span>
        <input
          aria-label={`Story points de ${item.title}`}
          type="number"
          min="0"
          step="1"
          value={points}
          onChange={(event) => setPoints(event.target.value)}
          onBlur={commitPoints}
          onKeyDown={(event) => { if (event.key === 'Enter') event.currentTarget.blur(); }}
        />
      </InlineField>}
      <InlineField className="inline-field item-epic">
        <span>Épico</span>
        <select
          aria-label={`Épico de ${item.title}`}
          value={item.kind === 1 ? item.id : epicId}
          disabled={item.kind === 1}
          onChange={(event) => void onCommit(item, {
            epicId: event.target.value || null,
            updateEpic: true,
          })}
        >
          {item.kind === 1
            ? <option value={item.id}>Épico raiz</option>
            : <><option value="">Sem épico</option>{epics.map((epic) => <option key={epic.id} value={epic.id}>{epic.title}</option>)}</>}
        </select>
      </InlineField>
      <ItemActions className="item-actions">
        {orphaned && <RelationBadge $warning title="Subtarefa sem item pai disponível">sem pai</RelationBadge>}
        {contextOnly && <RelationBadge title="Ancestral exibido para contextualizar o resultado">contexto</RelationBadge>}
        {hasChildren && (
          <OpenButton
            type="button"
            data-testid="backlog-toggle-children"
            aria-label={`${collapsed ? 'Expandir' : 'Recolher'} ${item.title}`}
            title={collapsed ? 'Expandir subtarefas' : 'Recolher subtarefas'}
            onClick={() => onToggleCollapsed(item.id)}
          >
            {collapsed ? <ChevronRight size={13} /> : <ChevronDown size={13} />}
          </OpenButton>
        )}
        {relations.blocked && (
          <RelationBadge $warning title={dependencyTitle || 'Dependência pendente'}>
            <AlertTriangle size={11} />{relations.dependencies.length || 1}
          </RelationBadge>
        )}
        {!relations.blocked && relations.dependencies.length > 0 && (
          <RelationBadge title={dependencyTitle}><Link2 size={10} />{relations.dependencies.length}</RelationBadge>
        )}
        {relations.blocking.length > 0 && (
          <RelationBadge $blocking title={`Bloqueia:\n${blockingTitle}`}>
            <Layers3 size={10} />{relations.blocking.length}
          </RelationBadge>
        )}
        <OpenButton onClick={() => onOpen(item)} title="Abrir detalhes" aria-label={`Abrir detalhes de ${item.title}`}>
          <ExternalLink size={13} />
        </OpenButton>
      </ItemActions>
    </ItemCard>
  );
}

interface DroppablePanelProps {
  id: string;
  title: string;
  subtitle: string;
  items: BacklogItem[];
  allItems: BacklogItem[];
  epics: BacklogItem[];
  groupBy: BacklogGroupBy;
  selectedIds: Set<string>;
  savingIds: Set<string>;
  headerAction?: React.ReactNode;
  emptyText: string;
  hierarchy?: boolean;
  onToggle: (itemId: string) => void;
  onToggleAll: (itemIds: string[]) => void;
  onOpen: (item: BacklogItem) => void;
  onCommit: (item: BacklogItem, patch: InlinePatch) => Promise<void>;
  collapsedIds: Set<string>;
  onToggleCollapsed: (itemId: string) => void;
  matchedIds: Set<string>;
  showPoints: boolean;
}

function DroppablePanel({
  id,
  title,
  subtitle,
  items,
  allItems,
  epics,
  groupBy,
  selectedIds,
  savingIds,
  headerAction,
  emptyText,
  hierarchy,
  onToggle,
  onToggleAll,
  onOpen,
  onCommit,
  collapsedIds,
  onToggleCollapsed,
  matchedIds,
  showPoints,
}: DroppablePanelProps) {
  const { isOver, setNodeRef } = useDroppable({ id });
  const allSelected = items.length > 0 && items.every((item) => selectedIds.has(item.id));

  return (
    <Panel $over={isOver}>
      <PanelHeader>
        <div><h3>{title}</h3><p>{subtitle}</p></div>
        <HeaderActions>
          <SelectAll><input type="checkbox" checked={allSelected} disabled={items.length === 0} onChange={() => onToggleAll(items.map((item) => item.id))} />Todos</SelectAll>
          {headerAction}
          <Count>{items.length}</Count>
        </HeaderActions>
      </PanelHeader>
      <ColumnLegend><span /><span /><span>Tipo</span><span>Item</span><span>Prioridade</span><span>{showPoints?'Pontos':''}</span><span>Épico</span><span>Relações</span></ColumnLegend>
      <SortableContext items={items.map((item) => item.id)} strategy={verticalListSortingStrategy}>
        <ItemList ref={setNodeRef} role={hierarchy && groupBy === 'none' ? 'tree' : undefined} data-testid={`${id}-panel`}>
          {items.length === 0 ? <Empty>{emptyText}</Empty> : items.map((item, index) => {
            const group = getBacklogGroupLabel(item, allItems, groupBy);
            const previousGroup = index > 0 ? getBacklogGroupLabel(items[index - 1], allItems, groupBy) : undefined;
            const hierarchyEnabled = Boolean(hierarchy && groupBy === 'none');
            const depth = hierarchyEnabled ? getItemDepth(item, allItems) : 0;
            const orphaned = hierarchyEnabled && item.kind === 6
              && (!item.parentId || !allItems.some(candidate => candidate.id === item.parentId));
            return (
              <div key={item.id}>
                {groupBy !== 'none' && group !== previousGroup && <GroupHeader><Layers3 size={11} />{group}</GroupHeader>}
                <SortableWorkItem
                  item={item}
                  allItems={allItems}
                  epics={epics}
                  depth={depth}
                  hierarchyEnabled={hierarchyEnabled}
                  orphaned={orphaned}
                  selected={selectedIds.has(item.id)}
                  saving={savingIds.has(item.id)}
                  hasChildren={Boolean(hierarchy && groupBy === 'none'
                    && allItems.some(candidate => candidate.parentId === item.id))}
                  collapsed={collapsedIds.has(item.id)}
                  contextOnly={!matchedIds.has(item.id)}
                  showPoints={showPoints}
                  onToggleCollapsed={onToggleCollapsed}
                  onToggle={onToggle}
                  onOpen={onOpen}
                  onCommit={onCommit}
                />
              </div>
            );
          })}
        </ItemList>
      </SortableContext>
    </Panel>
  );
}

const quickSchema = z.object({ title: z.string().trim().min(1, 'Digite um título.').max(500) });
type QuickForm = z.infer<typeof quickSchema>;

interface BacklogPlannerProps { project: ProjectSummary; }

export function BacklogPlanner({ project }: BacklogPlannerProps) {
  const queryClient = useQueryClient();
  const showPoints = project.methodology !== 1;
  const [searchParams, setSearchParams] = useSearchParams();
  const [items, setItems] = useState<BacklogItem[]>([]);
  const [selectedSprintId, setSelectedSprintId] = useState('');
  const [filters, setFilters] = useState<BacklogFilterState>(defaultBacklogFilters);
  const [groupBy, setGroupBy] = useState<BacklogGroupBy>('none');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [savingIds, setSavingIds] = useState<Set<string>>(new Set());
  const [collapsedIds, setCollapsedIds] = useState<Set<string>>(new Set());
  const [showQuickAdd, setShowQuickAdd] = useState(false);
  const [error, setError] = useState('');
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  // A chave inclui includeArchived: arquivadas vêm em outra consulta, e sem isso o
  // cache devolveria a lista sem elas ao ligar o filtro.
  const backlogQuery = useQuery<BacklogItem[]>({
    queryKey: ['project-backlog', project.id, filters.includeArchived],
    retry: false,
    queryFn: async () => {
      try { return await api.getProjectBacklog(project.id, filters.includeArchived); }
      catch { return previewMode ? previewBacklog : []; }
    },
  });
  const sprintQuery = useQuery<Sprint[]>({
    queryKey: ['project-sprints', project.id],
    retry: false,
    queryFn: async () => {
      try { return await api.getProjectSprints(project.id); }
      catch { return previewMode ? previewSprints : []; }
    },
  });

  useEffect(() => { if (backlogQuery.data) setItems(backlogQuery.data); }, [backlogQuery.data]);
  useEffect(() => {
    if (selectedSprintId || !sprintQuery.data?.length) return;
    setSelectedSprintId(sprintQuery.data.find((sprint) => sprint.status === 2)?.id ?? sprintQuery.data[0].id);
  }, [selectedSprintId, sprintQuery.data]);

  const selectedSprint = sprintQuery.data?.find((sprint) => sprint.id === selectedSprintId);
  const epics = useMemo(() => items.filter((item) => item.kind === 1).sort((left, right) => left.rank - right.rank), [items]);
  const boards = useMemo(() => Array.from(new Map(items.map((item) => [item.boardId, item.boardName])).entries()), [items]);
  const filteredItems = useMemo(() => filterBacklogItems(items, filters), [items, filters]);
  const contextualItems = useMemo(
    () => includeBacklogAncestors(filteredItems, items),
    [filteredItems, items],
  );
  const matchedIds = useMemo(() => new Set(filteredItems.map(item => item.id)), [filteredItems]);
  const filterActive = filters.search.trim().length > 0
    || filters.kind !== 'all'
    || filters.priority !== 'all'
    || filters.boardId !== 'all'
    || filters.relation !== 'all'
    || filters.includeArchived;
  const contextualBacklogItems = useMemo(
    () => contextualItems.filter(item => !item.sprintId),
    [contextualItems],
  );
  const contextualSprintItems = useMemo(
    () => contextualItems.filter(item => item.sprintId === selectedSprintId),
    [contextualItems, selectedSprintId],
  );
  const backlogHierarchy = useMemo(
    () => flattenBacklogHierarchy(
      contextualBacklogItems,
      items,
      filterActive ? new Set<string>() : collapsedIds,
    ),
    [collapsedIds, contextualBacklogItems, filterActive, items],
  );
  const backlogItems = useMemo(
    () => groupBy === 'none'
      ? backlogHierarchy.items
      : sortBacklogItems(contextualBacklogItems, items, groupBy),
    [backlogHierarchy.items, contextualBacklogItems, groupBy, items],
  );
  const sprintItems = useMemo(
    () => sortBacklogItems(contextualSprintItems, items, groupBy),
    [contextualSprintItems, groupBy, items],
  );
  const selectedItems = items.filter((item) => selectedIds.has(item.id));
  const selectedItem = items.find((item) => item.id === searchParams.get('item')) ?? null;
  const activeFilterCount = [filters.kind, filters.priority, filters.boardId]
    .filter((value) => value !== 'all').length + (filters.relation === 'all' ? 0 : 1) + (filters.search ? 1 : 0) + (filters.includeArchived ? 1 : 0);

  const openItem = (item: BacklogItem) => {
    const next = new URLSearchParams(searchParams);
    next.set('item', item.id);
    setSearchParams(next, { replace: true });
  };
  const closeItem = () => {
    const next = new URLSearchParams(searchParams);
    next.delete('item');
    setSearchParams(next, { replace: true });
    void queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] });
  };
  const toggleItem = (itemId: string) => setSelectedIds((current) => {
    const next = new Set(current);
    if (next.has(itemId)) next.delete(itemId); else next.add(itemId);
    return next;
  });
  const toggleAll = (itemIds: string[]) => setSelectedIds((current) => {
    const next = new Set(current);
    const allSelected = itemIds.length > 0 && itemIds.every((id) => next.has(id));
    itemIds.forEach((id) => allSelected ? next.delete(id) : next.add(id));
    return next;
  });
  const toggleCollapsed = (itemId: string) => setCollapsedIds(current => {
    const next = new Set(current);
    if (next.has(itemId)) next.delete(itemId); else next.add(itemId);
    return next;
  });

  const commitItem = async (item: BacklogItem, patch: InlinePatch) => {
    const updated: BacklogItem = {
      ...item,
      ...patch,
      parentId: patch.updateEpic ? patch.epicId ?? undefined : item.parentId,
    };
    setError('');
    setItems((current) => current.map((candidate) => candidate.id === item.id ? updated : candidate));
    setSavingIds((current) => new Set(current).add(item.id));
    try {
      if (!previewMode) {
        await api.updateBacklogItem(project.id, item.id, {
          title: updated.title,
          priority: updated.priority,
          points: updated.points ?? null,
          epicId: patch.updateEpic ? patch.epicId ?? null : null,
          updateEpic: patch.updateEpic ?? false,
        });
        await queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] });
      }
    } catch (caught) {
      setItems((current) => current.map((candidate) => candidate.id === item.id ? item : candidate));
      setError(caught instanceof Error ? caught.message : 'Não foi possível atualizar o item.');
    } finally {
      setSavingIds((current) => {
        const next = new Set(current);
        next.delete(item.id);
        return next;
      });
    }
  };

  const planItems = async (workItemIds: string[], sprintId: string | null) => {
    if (workItemIds.length === 0) return;
    if (sprintId === '' || (sprintId && !selectedSprint)) {
      setError('Selecione uma sprint válida para planejar os itens.');
      return;
    }
    const previous = items;
    setError('');
    setItems((current) => current.map((item) => workItemIds.includes(item.id)
      ? { ...item, sprintId: sprintId ?? undefined }
      : item));
    try {
      if (!previewMode) await api.planSprint(project.id, sprintId, workItemIds);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] }),
        queryClient.invalidateQueries({ queryKey: ['project-sprints', project.id] }),
      ]);
    } catch (caught) {
      setItems(previous);
      setError(caught instanceof Error ? caught.message : 'Não foi possível atualizar o planejamento.');
    }
  };

  const handleDragEnd = async ({ active, over }: DragEndEvent) => {
    if (!over) return;
    const activeItem = items.find((item) => item.id === active.id);
    if (!activeItem) return;
    const overItem = items.find((item) => item.id === over.id);
    const targetSprintId = over.id === 'sprint-drop'
      ? selectedSprintId
      : over.id === 'backlog-drop'
        ? undefined
        : overItem?.sprintId;
    const movingIds = selectedIds.has(activeItem.id)
      ? selectedItems.map((item) => item.id)
      : [activeItem.id];

    if ((activeItem.sprintId ?? undefined) !== (targetSprintId ?? undefined)) {
      await planItems(movingIds, targetSprintId ?? null);
      return;
    }

    if (!activeItem.sprintId && overItem && !overItem.sprintId) {
      if (groupBy !== 'none') {
        setError('Desative o agrupamento para priorizar por arraste.');
        return;
      }
      const previous = items;
      const ordered = items.filter((item) => !item.sprintId).sort((left, right) => left.rank - right.rank);
      const oldIndex = ordered.findIndex((item) => item.id === active.id);
      const newIndex = ordered.findIndex((item) => item.id === over.id);
      if (oldIndex < 0 || newIndex < 0 || oldIndex === newIndex) return;
      const reordered = arrayMove(ordered, oldIndex, newIndex)
        .map((item, index) => ({ ...item, rank: (index + 1) * 1000 }));
      const byId = new Map(reordered.map((item) => [item.id, item]));
      setItems((current) => current.map((item) => byId.get(item.id) ?? item));
      try {
        if (!previewMode) await api.reorderBacklog(project.id, reordered.map((item) => item.id));
        await queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] });
      } catch (caught) {
        setItems(previous);
        setError(caught instanceof Error ? caught.message : 'Não foi possível reordenar o backlog.');
      }
    }
  };

  const { register, handleSubmit, reset, formState: { errors } } = useForm<QuickForm>({
    resolver: zodResolver(quickSchema),
    defaultValues: { title: '' },
  });
  const createItem = async ({ title }: QuickForm) => {
    const board = project.boards[0];
    if (!board) { setError('Crie um quadro antes de adicionar itens.'); return; }
    const nextRank = Math.max(0, ...items.map((item) => item.rank || 0)) + 1000;
    try {
      const id = previewMode ? `preview-${Date.now()}` : await api.createWorkItem({
        boardId: board.id,
        title,
        priority: 1,
        position: nextRank,
      });
      if (previewMode) setItems((current) => [...current, {
        id,
        boardId: board.id,
        boardName: board.name,
        kind: 5,
        title,
        priority: 1,
        rank: nextRank,
        createdAt: new Date().toISOString(),
        links: [],
        isBlocked: false,
      }]);
      else await queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] });
      reset();
      setShowQuickAdd(false);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Não foi possível criar o item.');
    }
  };

  const backlogPoints = backlogItems.reduce((sum, item) => sum + (item.points ?? 0), 0);

  return (
    <Page>
      <Toolbar>
        <div><h2>Planejamento do backlog</h2><p>{backlogItems.length} itens visíveis{showPoints?` · ${backlogPoints} pontos não planejados`:''}</p></div>
        <ToolbarActions>
          <SearchBox><Search size={14} /><input aria-label="Pesquisar backlog" placeholder="Número, título, quadro ou solicitante" value={filters.search} onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))} /></SearchBox>
          <Button onClick={() => setShowQuickAdd((current) => !current)}><Plus size={14} />Novo item</Button>
        </ToolbarActions>
      </Toolbar>

      {showQuickAdd && (
        <QuickAdd onSubmit={handleSubmit(createItem)}>
          <input autoFocus aria-label="Título do novo item" placeholder="Digite somente o título e pressione Enter" {...register('title')} />
          <Button type="submit"><Sparkles size={14} />Adicionar</Button>
          {errors.title && <p>{errors.title.message}</p>}
        </QuickAdd>
      )}

      <FilterBar>
        <FilterTitle><ListFilter size={13} />Filtros {activeFilterCount > 0 && `(${activeFilterCount})`}</FilterTitle>
        <SelectField>Tipo<select value={filters.kind} onChange={(event) => setFilters((current) => ({ ...current, kind: event.target.value }))}><option value="all">Todos</option>{Object.entries(kindNames).map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></SelectField>
        <SelectField>Prioridade<select value={filters.priority} onChange={(event) => setFilters((current) => ({ ...current, priority: event.target.value }))}><option value="all">Todas</option>{[0, 1, 2, 3].map((priority) => <option key={priority} value={priority}>{priorityNames[priority]}</option>)}</select></SelectField>
        <SelectField>Quadro<select value={filters.boardId} onChange={(event) => setFilters((current) => ({ ...current, boardId: event.target.value }))}><option value="all">Todos</option>{boards.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></SelectField>
        <SelectField>Arquivadas<select aria-label="Mostrar tarefas arquivadas" value={filters.includeArchived ? 'yes' : 'no'} onChange={(event) => setFilters((current) => ({ ...current, includeArchived: event.target.value === 'yes' }))}><option value="no">Ocultar</option><option value="yes">Mostrar</option></select></SelectField>
        <SelectField>Relações<select value={filters.relation} onChange={(event) => setFilters((current) => ({ ...current, relation: event.target.value as BacklogFilterState['relation'] }))}><option value="all">Todas</option><option value="blocked">Bloqueados</option><option value="dependencies">Com dependências</option><option value="unparented">Sem épico</option></select></SelectField>
        <SelectField>Agrupar por<select value={groupBy} onChange={(event) => setGroupBy(event.target.value as BacklogGroupBy)}><option value="none">Sem agrupamento</option><option value="epic">Épico</option><option value="kind">Tipo</option><option value="priority">Prioridade</option><option value="board">Quadro</option></select></SelectField>
        <Button $secondary onClick={() => { setFilters(defaultBacklogFilters); setGroupBy('none'); }} disabled={activeFilterCount === 0 && groupBy === 'none'}><RotateCcw size={12} />Limpar</Button>
      </FilterBar>

      {selectedIds.size > 0 && (
        <BulkBar>
          <strong>{selectedIds.size} item(ns) selecionado(s)</strong>
          <select aria-label="Sprint para ação em massa" value={selectedSprintId} onChange={(event) => setSelectedSprintId(event.target.value)}>
            {(sprintQuery.data ?? []).filter((sprint) => sprint.status < 3).map((sprint) => <option key={sprint.id} value={sprint.id}>{sprint.name}</option>)}
          </select>
          <Button onClick={() => void planItems(selectedItems.map((item) => item.id), selectedSprintId)} disabled={!selectedSprintId}>Mover para sprint</Button>
          <Button $secondary onClick={() => void planItems(selectedItems.map((item) => item.id), null)}>Retornar ao backlog</Button>
          <Button $secondary onClick={() => setSelectedIds(new Set())} aria-label="Limpar seleção"><X size={13} />Limpar</Button>
        </BulkBar>
      )}

      {error && <ErrorBanner role="alert"><AlertCircle size={15} />{error}</ErrorBanner>}
      {backlogHierarchy.errors.length > 0 && (
        <ErrorBanner role="alert"><AlertCircle size={15} />{backlogHierarchy.errors[0]}</ErrorBanner>
      )}

      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
        <PlanningGrid>
          <DroppablePanel
            id="backlog-drop"
            title="Product backlog"
            subtitle={groupBy === 'none' ? 'Arraste para priorizar ou planejar na sprint' : 'Agrupado; desative o agrupamento para priorizar'}
            items={backlogItems}
            allItems={items}
            epics={epics}
            groupBy={groupBy}
            selectedIds={selectedIds}
            savingIds={savingIds}
            emptyText="Nenhum item corresponde aos filtros. Crie uma demanda ou limpe os filtros."
            hierarchy
            onToggle={toggleItem}
            onToggleAll={toggleAll}
            onOpen={openItem}
            onCommit={commitItem}
            collapsedIds={filterActive ? new Set<string>() : collapsedIds}
            onToggleCollapsed={toggleCollapsed}
            matchedIds={matchedIds}
            showPoints={showPoints}
          />
          <DroppablePanel
            id="sprint-drop"
            title={selectedSprint?.name ?? 'Sprint'}
            subtitle={selectedSprint?.goal || 'Selecione uma sprint para planejar'}
            items={sprintItems}
            allItems={items}
            epics={epics}
            groupBy={groupBy}
            selectedIds={selectedIds}
            savingIds={savingIds}
            emptyText="Arraste ou selecione itens do backlog para planejar esta sprint."
            onToggle={toggleItem}
            onToggleAll={toggleAll}
            onOpen={openItem}
            onCommit={commitItem}
            collapsedIds={new Set<string>()}
            onToggleCollapsed={toggleCollapsed}
            matchedIds={matchedIds}
            showPoints={showPoints}
            headerAction={(
              <SprintSelect>
                <select aria-label="Sprint de planejamento" value={selectedSprintId} onChange={(event) => setSelectedSprintId(event.target.value)}>
                  {(sprintQuery.data ?? []).filter((sprint) => sprint.status < 3).map((sprint) => <option key={sprint.id} value={sprint.id}>{sprint.name}</option>)}
                </select>
                <ChevronDown size={12} />
              </SprintSelect>
            )}
          />
        </PlanningGrid>
      </DndContext>

      {backlogQuery.isLoading && <Empty><ChevronsUpDown size={20} />Carregando backlog...</Empty>}
      <TaskDetailDrawer
        item={selectedItem}
        projectKey={project.key}
        sprintName={sprintQuery.data?.find((sprint) => sprint.id === selectedItem?.sprintId)?.name}
        onOpenChange={(open) => { if (!open) closeItem(); }}
      />
    </Page>
  );
}
