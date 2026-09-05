import { describe, expect, it } from 'vitest';
import type { BacklogItem, Sprint } from '../types/scrum';
import { buildBurndown, buildSprintHistory, buildVelocity, calculateNetCapacity, calculateSprintProgress } from './scrumAnalytics';

const sprint: Sprint = {
  id: 'sprint-1',
  teamId: 'team-1',
  teamName: 'Time',
  name: 'Sprint 1',
  startDate: '2026-07-13',
  endDate: '2026-07-15',
  status: 2,
  itemCount: 2,
  completedItemCount: 1,
  storyPoints: 8,
  completedStoryPoints: 3,
  capacities: [{ userId: 'user-1', availableHours: 24, daysOffHours: 4 }],
};

const items: BacklogItem[] = [
  { id: '1', boardId: 'b', boardName: 'Board', sprintId: sprint.id, kind: 3, title: 'Concluído', priority: 1, points: 3, rank: 1, completedAt: '2026-07-14T16:00:00Z' },
  { id: '2', boardId: 'b', boardName: 'Board', sprintId: sprint.id, kind: 3, title: 'Aberto', priority: 1, points: 5, rank: 2 },
];

describe('scrum analytics', () => {
  it('gera o burndown sem projetar a linha real para datas futuras', () => {
    const result = buildBurndown(sprint, items, new Date('2026-07-14T12:00:00Z'));

    expect(result).toHaveLength(3);
    expect(result[0]).toMatchObject({ ideal: 8, actual: 8 });
    expect(result[1]).toMatchObject({ ideal: 4, actual: 5 });
    expect(result[2].actual).toBeNull();
  });

  it('usa a fotografia da sprint concluída mesmo após devolver itens ao backlog', () => {
    const closed = {
      ...sprint,
      status: 3,
      itemSnapshots: [
        { workItemId: '1', workItemNumber: 1, title: 'Entregue', kind: 3, points: 3, wasCompleted: true, workItemCompletedAt: '2026-07-14T16:00:00Z', outcome: 1 },
        { workItemId: '2', workItemNumber: 2, title: 'Devolvida', kind: 3, points: 5, wasCompleted: false, outcome: 2 },
      ],
    };

    const result = buildBurndown(closed, [], new Date('2026-07-15T12:00:00Z'));

    expect(result[0].actual).toBe(8);
    expect(result.at(-1)?.actual).toBe(5);
  });

  it('calcula progresso e capacidade líquida', () => {
    expect(calculateSprintProgress(sprint)).toBe(50);
    expect(calculateNetCapacity(sprint)).toBe(20);
  });

  it('ordena a velocity pela data de início', () => {
    const older = { ...sprint, id: 'sprint-0', name: 'Sprint 0', startDate: '2026-06-01', storyPoints: 5, completedStoryPoints: 5 };
    expect(buildVelocity([sprint, older]).map((item) => item.name)).toEqual(['Sprint 0', 'Sprint 1']);
  });

  it('monta o histórico apenas com sprints encerradas e calcula a entrega', () => {
    const closed = { ...sprint, status: 3, name: 'Sprint encerrada' };
    const older = {
      ...closed,
      id: 'sprint-0',
      name: 'Sprint anterior',
      startDate: '2026-06-01',
      endDate: '2026-06-14',
      storyPoints: 5,
      completedStoryPoints: 5,
    };

    const result = buildSprintHistory([sprint, older, closed]);

    expect(result.map((item) => item.name)).toEqual(['Sprint encerrada', 'Sprint anterior']);
    expect(result[0]).toMatchObject({ durationDays: 3, completionRate: 38 });
    expect(result[1]).toMatchObject({ durationDays: 14, completionRate: 100 });
  });
});
