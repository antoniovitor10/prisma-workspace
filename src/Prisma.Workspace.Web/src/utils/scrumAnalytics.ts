import type { BacklogItem, Sprint } from '../types/scrum';

export interface BurndownPoint {
  date: string;
  label: string;
  ideal: number;
  actual: number | null;
}

export interface SprintHistoryEntry {
  id: string;
  name: string;
  teamName: string;
  goal?: string;
  startDate: string;
  endDate: string;
  durationDays: number;
  itemCount: number;
  completedItemCount: number;
  plannedPoints: number;
  deliveredPoints: number;
  completionRate: number;
}

const parseDate = (value: string) => new Date(`${value.slice(0, 10)}T00:00:00.000Z`);

const dateKey = (value: Date) => value.toISOString().slice(0, 10);

const round = (value: number) => Math.round(value * 10) / 10;

export function buildBurndown(
  sprint: Sprint,
  items: BacklogItem[],
  today = new Date(),
): BurndownPoint[] {
  const sprintItems = sprint.itemSnapshots?.length
    ? sprint.itemSnapshots.map((item) => ({
        kind: item.kind,
        points: item.points,
        estimatedHours: item.estimatedHours,
        remainingHours: undefined,
        completedAt: item.workItemCompletedAt,
      }))
    : items.filter((item) => item.sprintId === sprint.id);
  const start = parseDate(sprint.startDate);
  const end = parseDate(sprint.endDate);
  const totalDays = Math.max(1, Math.floor((end.getTime() - start.getTime()) / 86_400_000) + 1);
  const pointItems = sprintItems.filter((item) => item.kind === 3 || item.kind === 4);
  const usesPoints = pointItems.some((item) => item.points != null);
  const scopedItems = usesPoints ? pointItems : sprintItems;
  const effort = (item: { points?: number; estimatedHours?: number; remainingHours?: number }) => usesPoints
    ? item.points ?? 0
    : item.estimatedHours ?? item.remainingHours ?? 0;
  const total = scopedItems.reduce((sum, item) => sum + effort(item), 0);
  const todayKey = dateKey(today);

  return Array.from({ length: totalDays }, (_, index) => {
    const current = new Date(start);
    current.setUTCDate(start.getUTCDate() + index);
    const currentKey = dateKey(current);
    const completed = scopedItems
      .filter((item) => item.completedAt && item.completedAt.slice(0, 10) <= currentKey)
      .reduce((sum, item) => sum + effort(item), 0);

    return {
      date: currentKey,
      label: new Intl.DateTimeFormat('pt-BR', {
        day: '2-digit',
        month: 'short',
        timeZone: 'UTC',
      }).format(current),
      ideal: round(total * (1 - index / Math.max(1, totalDays - 1))),
      actual: currentKey <= todayKey ? round(Math.max(0, total - completed)) : null,
    };
  });
}

export function buildVelocity(sprints: Sprint[]) {
  return [...sprints]
    .sort((a, b) => a.startDate.localeCompare(b.startDate))
    .slice(-6)
    .map((sprint) => ({
      name: sprint.name,
      planned: sprint.storyPoints,
      delivered: sprint.completedStoryPoints ?? (sprint.status === 3 ? sprint.storyPoints : 0),
    }));
}

export function calculateSprintProgress(sprint: Sprint) {
  if (sprint.itemCount === 0) return 0;
  return Math.round(((sprint.completedItemCount ?? 0) / sprint.itemCount) * 100);
}

export function calculateNetCapacity(sprint: Sprint) {
  return sprint.capacities.reduce(
    (sum, capacity) => sum + Math.max(0, capacity.availableHours - capacity.daysOffHours),
    0,
  );
}

export function buildSprintHistory(sprints: Sprint[]): SprintHistoryEntry[] {
  return sprints
    .filter((sprint) => sprint.status === 3 || sprint.status === 4)
    .map((sprint) => {
      const start = parseDate(sprint.startDate);
      const end = parseDate(sprint.endDate);
      const completedItemCount = sprint.completedItemCount ?? 0;
      const deliveredPoints = sprint.completedStoryPoints ?? 0;
      const completionRate = sprint.storyPoints > 0
        ? Math.round((deliveredPoints / sprint.storyPoints) * 100)
        : sprint.itemCount > 0
          ? Math.round((completedItemCount / sprint.itemCount) * 100)
          : 0;

      return {
        id: sprint.id,
        name: sprint.name,
        teamName: sprint.teamName,
        goal: sprint.goal,
        startDate: sprint.startDate,
        endDate: sprint.endDate,
        durationDays: Math.max(1, Math.floor((end.getTime() - start.getTime()) / 86_400_000) + 1),
        itemCount: sprint.itemCount,
        completedItemCount,
        plannedPoints: sprint.storyPoints,
        deliveredPoints,
        completionRate,
      };
    })
    .sort((left, right) => right.endDate.localeCompare(left.endDate));
}
