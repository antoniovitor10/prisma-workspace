import {
  AlertTriangle,
  BookOpenCheck,
  CheckCircle2,
  ListTree,
  MoveRight,
} from 'lucide-react';
import { useMemo } from 'react';
import styled from 'styled-components';
import type { BacklogItem, Sprint } from '../../types/scrum';
import { kindNames, priorityNames } from '../../types/scrum';

const Panel = styled.section`
  min-width: 0;
  margin-top: 12px;
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
`;

const Header = styled.header`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;

  h3 { display: flex; align-items: center; gap: 7px; font-size: 14px; }
  p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const Action = styled.button`
  display: inline-flex;
  min-height: 34px;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 13px;
  font-weight: 750;
  &:hover { background: ${({ theme }) => theme.color.neutral[50]}; }
`;

const Locked = styled.span`
  display: inline-flex;
  min-height: 32px;
  align-items: center;
  padding: 0 10px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[100]};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 750;
`;

const TypeStrip = styled.div`
  display: grid;
  grid-template-columns: repeat(4, minmax(110px, 1fr));
  gap: 8px;
  margin-bottom: 12px;
  @media (max-width: 680px) { grid-template-columns: repeat(2, 1fr); }
`;

const TypeCard = styled.div`
  padding: 10px 11px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.neutral[50]};
  small, strong { display: block; }
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; font-weight: 800; text-transform: uppercase; }
  strong { margin-top: 2px; color: ${({ theme }) => theme.color.text}; font-size: 15px; }
`;

const Table = styled.div`
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: minmax(260px, 2fr) minmax(100px, .8fr) minmax(116px, .8fr) 90px;
  align-items: center;
  @media (max-width: 760px) {
    grid-template-columns: minmax(220px, 1fr) 90px;
    > *:nth-child(2), > *:nth-child(3) { display: none; }
  }
`;

const TableHead = styled(Grid)`
  min-height: 34px;
  padding: 0 10px;
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
  font-weight: 800;
  letter-spacing: .04em;
  text-transform: uppercase;
`;

const ItemRow = styled(Grid).attrs({ as: 'button' })`
  width: 100%;
  min-height: 56px;
  padding: 0 10px;
  border-top: 1px solid ${({ theme }) => theme.color.neutral[100]};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  text-align: left;
  &:hover { background: ${({ theme }) => theme.color.neutral[50]}; }
  &:focus-visible { position: relative; z-index: 1; outline: 2px solid ${({ theme }) => theme.color.brand}; outline-offset: -2px; }
`;

const ItemTitle = styled.div<{ $depth: number }>`
  min-width: 0;
  padding: 8px 8px 8px ${({ $depth }) => 8 + Math.min($depth, 3) * 18}px;
  strong { display: block; overflow: hidden; font-size: 13px; text-overflow: ellipsis; white-space: nowrap; }
  small { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; }
`;

const Meta = styled.span`
  display: flex;
  align-items: center;
  gap: 5px;
  min-width: 0;
  padding: 8px;
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 12px;
`;

const Badge = styled.span<{ $positive?: boolean; $warning?: boolean }>`
  display: inline-flex;
  align-items: center;
  gap: 4px;
  width: fit-content;
  padding: 3px 6px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ $positive, $warning, theme }) => $positive
    ? `color-mix(in srgb, ${theme.color.success} 10%, ${theme.color.surface})`
    : $warning
      ? `color-mix(in srgb, ${theme.color.warning} 12%, ${theme.color.surface})`
      : theme.color.neutral[100]};
  color: ${({ $positive, $warning, theme }) => $positive
    ? theme.color.success
    : $warning
      ? theme.color.warning
      : theme.color.neutral[700]};
  font-size: 11px;
  font-weight: 750;
`;

const Empty = styled.div`
  padding: 36px 16px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  text-align: center;
`;

interface SprintBacklogPanelProps {
  sprint: Sprint;
  items: BacklogItem[];
  allItems: BacklogItem[];
  projectKey: string;
  onOpenItem: (item: BacklogItem) => void;
  onPlan: () => void;
  showPoints?: boolean;
}

function orderHierarchically(items: BacklogItem[]) {
  const root = '__root__';
  const itemIds = new Set(items.map((item) => item.id));
  const children = new Map<string, BacklogItem[]>();
  const byRank = (left: BacklogItem, right: BacklogItem) => left.rank - right.rank || left.title.localeCompare(right.title);

  items.forEach((item) => {
    const parent = item.parentId && itemIds.has(item.parentId) ? item.parentId : root;
    children.set(parent, [...(children.get(parent) ?? []), item]);
  });

  children.forEach((group) => group.sort(byRank));
  const result: Array<{ item: BacklogItem; depth: number }> = [];
  const visited = new Set<string>();
  const append = (parentId: string, depth: number) => {
    (children.get(parentId) ?? []).forEach((item) => {
      if (visited.has(item.id)) return;
      visited.add(item.id);
      result.push({ item, depth });
      append(item.id, depth + 1);
    });
  };

  append(root, 0);
  items.filter((item) => !visited.has(item.id)).sort(byRank).forEach((item) => result.push({ item, depth: 0 }));
  return result;
}

export default function SprintBacklogPanel({
  sprint,
  items,
  allItems,
  projectKey,
  onOpenItem,
  onPlan,
  showPoints = true,
}: SprintBacklogPanelProps) {
  const orderedItems = useMemo(() => orderHierarchically(items), [items]);
  const allItemsById = useMemo(() => new Map(allItems.map((item) => [item.id, item])), [allItems]);
  const typeCounts = [
    { kind: 1, label: 'Épicos' },
    { kind: 3, label: 'Histórias' },
    { kind: 4, label: 'Bugs' },
    { kind: 5, label: 'Tarefas' },
  ];
  const points = items
    .filter((item) => item.kind === 3 || item.kind === 4)
    .reduce((sum, item) => sum + (item.points ?? 0), 0);

  return (
    <Panel>
      <Header>
        <div>
          <h3><ListTree size={15} />Sprint Backlog</h3>
          <p>{items.length} itens{showPoints?` · ${points} pontos`:''} · hierarquia e critérios de aceite no mesmo plano.</p>
        </div>
        {sprint.status >= 3
          ? <Locked>Escopo preservado no histórico</Locked>
          : <Action onClick={onPlan}><MoveRight size={13} />Planejar Product Backlog</Action>}
      </Header>

      <TypeStrip>
        {typeCounts.map((type) => (
          <TypeCard key={type.kind}><small>{type.label}</small><strong>{items.filter((item) => item.kind === type.kind).length}</strong></TypeCard>
        ))}
      </TypeStrip>

      <Table>
        <TableHead><span>Item</span><span>Status</span><span>Critérios de aceite</span><span>Estimativa</span></TableHead>
        {orderedItems.map(({ item, depth }) => {
          const parent = item.parentId ? allItemsById.get(item.parentId) : undefined;
          const hasCriteria = Boolean(item.acceptanceCriteria?.trim());
          return (
            <ItemRow key={item.id} onClick={() => onOpenItem(item)} aria-label={`Abrir ${item.title}`}>
              <ItemTitle $depth={depth}>
                <strong>{item.title}</strong>
                <small>
                  <span>{item.number ? `${projectKey}-${item.number}` : kindNames[item.kind]}</span>
                  <span>{kindNames[item.kind]} · {priorityNames[item.priority]}</span>
                  {parent && <span>↳ {parent.title}</span>}
                  {item.isBlocked && <Badge $warning><AlertTriangle size={9} />Bloqueado</Badge>}
                </small>
              </ItemTitle>
              <Meta>{item.completedAt ? <Badge $positive><CheckCircle2 size={9} />Concluído</Badge> : <Badge>{item.stageName || 'A fazer'}</Badge>}</Meta>
              <Meta>
                {item.kind === 3 || item.kind === 4
                  ? hasCriteria
                    ? <Badge $positive><BookOpenCheck size={9} />Definidos</Badge>
                    : <Badge $warning>Pendentes</Badge>
                  : <span>—</span>}
              </Meta>
              <Meta>{showPoints&&item.points != null ? `${item.points} pts` : item.remainingHours != null ? `${item.remainingHours}h` : '—'}</Meta>
            </ItemRow>
          );
        })}
        {orderedItems.length === 0 && <Empty>Nenhum item foi planejado para esta sprint.</Empty>}
      </Table>
    </Panel>
  );
}
