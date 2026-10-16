import { test, expect } from './fixtures/functional';
import { cleanupFor } from './fixtures/test';
import { withBoardFactory } from './fixtures/boards';
import { withHoursPrivacy, privacyRoles, type HoursPrivacy } from './fixtures/hoursPrivacy';

type Card = { id: string; totalTimeSeconds: number; userTimeSeconds: number };
type Total = { totalSeconds: number; totalHours: number };
type Entry = { userId: string; durationSeconds: number; note: string };
type Dashboard = { kpis: Array<{ key: string; value: number }> };
type Execution = { rows: Array<{ userId: string; hours: number }> };
const reportHours = (role: number) => role === 1 ? 6.5 : role === 2 ? 3 : 1.5;
const query = (f: HoursPrivacy) => `from=${f.date}&to=${f.date}`;
const definition = (f: HoursPrivacy) => ({ columns: ['userId', 'hours'], metrics: [], filters: [],
  descending: false, from: f.date, to: f.date });

for (const { name, role } of privacyRoles) {
  test(`B07 HTTP ${name}: cards, detalhe, subtarefas e totais não contornam a população de horas`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      const taskSeconds = role === 1 || role === 2 ? 7200 : 1800;
      for (const path of [`/api/WorkItems/board/${f.board.id}`, `/api/WorkItems/project/${f.boards.projectId}`, '/api/me/tasks']) {
        const cards = await f.get<Card[]>(path);
        expect(cards.find(c => c.id === f.task.id)).toMatchObject({ totalTimeSeconds: taskSeconds, userTimeSeconds: 1800 });
      }
      expect(await f.get(`/api/WorkItems/${f.task.id}`)).toMatchObject({ realizedHours: taskSeconds / 3600 });
      const children = await f.get<Card[]>(`/api/WorkItems/${f.parent.id}/subitems`);
      expect(children.find(c => c.id === f.child.id)).toMatchObject({ totalTimeSeconds: role === 1 || role === 2 ? 3600 : 1800 });
      const mine = await f.get<{ tasks: Array<{ id: string; userTimeSeconds: number }> }>('/api/me/work');
      expect(mine.tasks.find(t => t.id === f.task.id)).toMatchObject({ userTimeSeconds: 1800 });
      const entries = await f.get<Entry[]>(`/api/TimeEntries/work-item/${f.task.id}`);
      expect(entries.reduce((sum, e) => sum + e.durationSeconds, 0)).toBe(taskSeconds);
      expect(entries.map(e => e.userId).sort()).toEqual((role === 1 || role === 2
        ? [f.actor.id, f.colleague.id] : [f.actor.id]).sort());
      expect(await f.get<Total>(`/api/TimeEntries/work-item/${f.task.id}/total`)).toMatchObject({ totalSeconds: taskSeconds });
      expect(await f.get<Total>(`/api/TimeEntries/board/${f.board.id}/total`)).toMatchObject({ totalHours: role === 1 ? 6.5 : role === 2 ? 3.5 : 1.5 });
      expect(await f.get<Total>(`/api/TimeEntries/user/${f.actor.id}/total`)).toMatchObject({ totalHours: 1.5 });
      const other = await f.api(`/api/TimeEntries/user/${f.colleague.id}/total`);
      expect(other.status()).toBe(role === 1 || role === 2 ? 200 : 403);
      if (role === 1 || role === 2) expect(await other.json()).toMatchObject({ totalHours: 2 });
      expect(await f.get<Total>(`/api/TimeEntries/work-item/${f.other.id}/total`)).toMatchObject({ totalHours: role === 1 ? 3 : 0 });
    });
  });

  test(`B07 HTTP ${name}: relatórios e dashboards aplicam o mesmo recorte`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      const hours = reportHours(role); const period = query(f);
      const project = await f.get<{ project: { realizedHours: number }; entries: Entry[] }>(`/api/reports/projects/${f.boards.projectId}/time?${period}`);
      expect(project.project.realizedHours).toBe(hours);
      expect(project.entries.map(e => e.userId).sort()).toEqual((role === 1
        ? [f.actor.id, f.actor.id, f.actor.id, f.colleague.id, f.colleague.id, f.outsider.id]
        : role === 2 ? [f.actor.id, f.actor.id, f.colleague.id, f.colleague.id] : [f.actor.id, f.actor.id, f.actor.id]).sort());
      for (const path of [`/api/reports/prepared?projectId=${f.boards.projectId}&${period}`,
        `/api/dashboards/manager?projectId=${f.boards.projectId}&${period}`,
        `/api/dashboards/projects/${f.boards.projectId}?${period}`]) {
        const response = await f.api(path); expect(response.status()).toBe(role === 7 ? 403 : 200);
        if (role !== 7) {
          const body = await response.json();
          if (path.includes('/dashboards/projects/')) {
            expect((body as Dashboard).kpis.find(k => k.key === 'tasks')?.value).toBe(role === 1 ? 5 : 3);
            expect((body.items as Array<{ id: string }>).map(item => item.id).sort()).toEqual((role === 1
              ? [f.parent.id, f.task.id, f.child.id, f.other.id, f.personal.id]
              : role === 2 ? [f.parent.id, f.task.id, f.child.id] : [f.task.id, f.child.id, f.personal.id]).sort());
          } else expect(path.includes('/prepared') ? body.hours.realized
            : (body as Dashboard).kpis.find(k => k.key === 'realized')?.value).toBe(hours);
        }
      }
      const personal = await f.get<Dashboard>(`/api/dashboards/collaborator?${period}`);
      expect(personal.kpis.find(k => k.key === 'hours')?.value).toBe(1.5);
      const org = await f.get<{ totalHours: number }>(`/api/reports/hours?teamId=${f.managed}&${period}`);
      expect(org.totalHours).toBe(role === 1 || role === 2 ? 3 : 1);
      expect(await f.get(`/api/reports/hours?teamId=${f.otherTeam}&${period}`)).toMatchObject({ totalHours: role === 1 ? 3 : 0 });
      const preview = await f.api('/api/reports/preview', 'POST', { projectId: f.boards.projectId,
        source: 7, visualization: 1, definition: definition(f) });
      expect(preview.status()).toBe(role === 7 ? 403 : 200);
      if (role !== 7) expect((await preview.json() as Execution).rows.reduce((sum, r) => sum + r.hours, 0)).toBe(hours);
    });
  });

  test(`B07 HTTP ${name}: métricas, empresa e equipes restringem horas antes de somar`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      const hours = reportHours(role);
      const metrics = await f.get<{ hoursByUser: Array<{ hours: number }> }>(`/api/Boards/${f.board.id}/metrics`);
      expect(metrics.hoursByUser.reduce((sum, user) => sum + user.hours, 0)).toBe(hours);
      const company = await f.get<Array<{ id: string; totalHours: number }>>('/api/company/projects');
      expect(company.find(b => b.id === f.board.id)).toMatchObject({ totalHours: hours });
      const teams = await f.get<Array<{ id: string; members: Array<{ userId: string; weekHours: number }> }>>('/api/teams');
      const managed = teams.find(t => t.id === f.managed); expect(managed).toBeDefined();
      expect(managed!.members.find(m => m.userId === f.colleague.id)?.weekHours).toBe(f.inCurrentWeek && (role === 1 || role === 2) ? 2 : 0);
      expect(managed!.members.find(m => m.userId === f.actor.id)?.weekHours).toBe(f.inCurrentWeek ? role === 2 ? 1 : 1.5 : 0);
    });
  });

  test(`B07 HTTP ${name}: relatório salvo e CSV usam o ator, nunca as permissões do autor`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      const created = await f.boards.api('/api/reports/definitions', 'POST', { projectId: f.boards.projectId,
        name: f.boards.name('compartilhado'), source: 7, visualization: 1, definition: definition(f), isShared: true });
      expect(created.status()).toBe(201); const { id } = await created.json();
      cleanupFor(request)(async () => { expect((await f.boards.api(`/api/reports/definitions/${id}`, 'DELETE')).status()).toBe(204); });
      const run = await f.api(`/api/reports/definitions/${id}/run`, 'POST');
      const csv = await f.api(`/api/reports/definitions/${id}/export`);
      expect(run.status()).toBe(role === 7 ? 403 : 200); expect(csv.status()).toBe(role === 7 ? 403 : 200);
      if (role !== 7) {
        const rows = (await run.json() as Execution).rows;
        expect(rows.reduce((sum, r) => sum + r.hours, 0)).toBe(reportHours(role));
        const content = await csv.text(); expect(content).toContain(f.actor.id);
        if (role === 10) { expect(content).not.toContain(f.colleague.id); expect(content).not.toContain(f.outsider.id); }
        if (role === 2) { expect(content).toContain(f.colleague.id); expect(content).not.toContain(f.outsider.id); }
        if (role === 1) { expect(content).toContain(f.colleague.id); expect(content).toContain(f.outsider.id); }
      }
    });
  });

  test(`B07 HTTP ${name}: outro projeto não amplia o escopo de horas por consulta global`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      await withBoardFactory(page, request, async isolated => {
        const person = await isolated.member(); const board = await isolated.board(); const stage = await isolated.stage(board.id);
        const task = await isolated.item(board.id, stage.id, { responsibleId: person.id });
        expect((await isolated.api('/api/TimeEntries/manual', 'POST', { workItemId: task.id,
          startedAt: `${f.date}T00:00:00-03:00`, endedAt: `${f.date}T01:00:00-03:00`, note: isolated.name('reservado') }, person)).status()).toBe(201);
        const company = await f.get<Array<{ id: string; totalHours: number }>>('/api/company/projects');
        if (role === 1 || role === 2) expect(company.find(b => b.id === board.id)).toMatchObject({ totalHours: role === 1 ? 1 : 0 });
        else expect(company.map(b => b.id)).not.toContain(board.id);
        const project = await f.api(`/api/reports/projects/${isolated.projectId}/time?${query(f)}`);
        expect(project.status()).toBe(role === 1 || role === 2 ? 200 : 404);
        if (role === 1 || role === 2) expect(await project.json()).toMatchObject({ project: { realizedHours: role === 1 ? 1 : 0 } });
        expect(await f.get(`/api/reports/hours?userId=${person.id}&${query(f)}`)).toMatchObject({ totalHours: role === 1 ? 1 : 0 });
        const preview = await f.api('/api/reports/preview', 'POST', { source: 7, visualization: 1,
          definition: { ...definition(f), filters: [{ field: 'userId', operator: 'eq', value: person.id }] } });
        expect(preview.status()).toBe(role === 7 ? 403 : 200);
        if (role !== 7) expect((await preview.json() as Execution).rows).toEqual(role === 1 ? [{ userId: person.id, hours: 1 }] : []);
      }, 'H03isolado');
    });
  });
}

test('B07 HTTP: negação de relatório por projeto prevalece inclusive para administrador', async ({ page, request }) => {
  await withHoursPrivacy(page, request, 1, async f => {
    const grants = new Set<string>();
    cleanupFor(request)(async () => {
      for (const id of grants) expect((await f.boards.api(`/api/organizations/current/permissions/${id}`, 'DELETE')).status()).toBe(204);
    });
    for (const scope of [3, 5]) {
      const added = await f.boards.api('/api/organizations/current/permissions', 'PUT', { userId: f.actor.id,
        scope, scopeId: f.boards.projectId, permission: 10, isAllowed: false });
      expect(added.status()).toBe(200); const { id }: { id: string } = await added.json(); grants.add(id);
      expect((await f.api(`/api/reports/projects/${f.boards.projectId}/time?${query(f)}`)).status()).toBe(403);
      expect((await f.api('/api/reports/preview', 'POST', { projectId: f.boards.projectId,
        source: 7, visualization: 1, definition: definition(f) })).status()).toBe(403);
      expect(await f.get(`/api/reports/hours?userId=${f.actor.id}&${query(f)}`)).toMatchObject({ totalHours: 0 });
      const company = await f.get<Array<{ id: string; totalHours: number }>>('/api/company/projects');
      expect(company.find(b => b.id === f.board.id)).toMatchObject({ totalHours: 0 });
      expect(await f.get(`/api/TimeEntries/work-item/${f.task.id}/total`)).toMatchObject({ totalHours: 0.5 });
      expect((await f.boards.api(`/api/organizations/current/permissions/${id}`, 'DELETE')).status()).toBe(204); grants.delete(id);
    }
  });
});

for (const role of [10, 1]) {
  test(`B07 HTTP papel ${role}: negações de tarefa e equipe também restringem consultas globais`, async ({ page, request }) => {
    await withHoursPrivacy(page, request, role, async f => {
      const grants = new Set<string>();
      cleanupFor(request)(async () => {
        for (const id of grants) expect((await f.boards.api(`/api/organizations/current/permissions/${id}`, 'DELETE')).status()).toBe(204);
      });
      for (const [scope, scopeId, expected] of [[4, f.task.id, role === 1 ? 4.5 : 1], [2, f.managed, role === 1 ? 3.5 : 0.5]] as const) {
        const added = await f.boards.api('/api/organizations/current/permissions', 'PUT', { userId: f.actor.id,
          scope, scopeId, permission: 10, isAllowed: false });
        expect(added.status()).toBe(200); const { id }: { id: string } = await added.json(); grants.add(id);
        const metrics = await f.get<{ hoursByUser: Array<{ hours: number }> }>(`/api/Boards/${f.board.id}/metrics`);
        expect(metrics.hoursByUser.reduce((sum, row) => sum + row.hours, 0)).toBe(expected);
        const report = await f.get<{ project: { realizedHours: number } }>(`/api/reports/projects/${f.boards.projectId}/time?${query(f)}`);
        expect(report.project.realizedHours).toBe(expected);
        expect(await f.get(`/api/TimeEntries/work-item/${f.task.id}/total`)).toMatchObject({ totalHours: 0.5 });
        expect((await f.boards.api(`/api/organizations/current/permissions/${id}`, 'DELETE')).status()).toBe(204); grants.delete(id);
      }
    });
  });
}
