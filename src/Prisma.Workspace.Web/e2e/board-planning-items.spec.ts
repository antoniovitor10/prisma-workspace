import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';
import { withBoardFactory, type BoardFactory, type BoardStage } from './fixtures/boards';

/**
 * T-0022 (HU-0008, CA-4 e CA-13): em todos os quadros, inclusive os sem método, Épico,
 * Funcionalidade e História deixam de ser card; a Tarefa filha da História vira card; a
 * Subtarefa continua dentro do card da Tarefa; o "Novo card" não oferece os tipos de planejamento.
 */

const Kind = { Epic: 1, Feature: 2, UserStory: 3, Bug: 4, Task: 5, Subtask: 6 } as const;
const screenshots = resolve('../../.runlogs/oficina/screenshots');

type Created = { id: string; title: string };
type Scenario = {
  boardId: string; stages: BoardStage[];
  epic: Created; feature: Created; story: Created; storyTask: Created;
  topTask: Created; subtask: Created; bug: Created;
};

async function createItem(factory: BoardFactory, boardId: string, stageId: string, label: string,
  kind: number, extra: Record<string, unknown> = {}): Promise<Created> {
  const title = factory.name(label);
  const response = await factory.api('/api/WorkItems', 'POST', {
    boardId, stageId, title, kind, responsibleId: factory.actorId, priority: 1, position: 100, ...extra,
  });
  expect(response.status(), `POST /api/WorkItems (${label})`).toBe(201);
  return { id: await response.json(), title };
}

/** Itens do CA-4 criados pela API num projeto sem método. */
async function createScenario(factory: BoardFactory): Promise<Scenario> {
  const board = await factory.board();
  const stages = await factory.get<BoardStage[]>(`/api/Stages/board/${board.id}`);
  const first = stages[0].id;
  // Vencimento antigo põe os itens da pessoa no topo de Minhas tarefas, mesmo com muitos itens no seed.
  const mine = { dueDate: '2020-01-02' };
  const epic = await createItem(factory, board.id, first, 'epico', Kind.Epic);
  const feature = await createItem(factory, board.id, first, 'funcionalidade', Kind.Feature, { parentId: epic.id });
  const story = await createItem(factory, board.id, first, 'historia', Kind.UserStory, mine);
  const storyTask = await createItem(factory, board.id, first, 'tarefa-da-historia', Kind.Task, { parentId: story.id, ...mine });
  const topTask = await createItem(factory, board.id, first, 'tarefa-topo', Kind.Task);
  const subtask = await createItem(factory, board.id, first, 'subtarefa', Kind.Subtask, { parentId: topTask.id });
  const bug = await createItem(factory, board.id, first, 'bug-topo', Kind.Bug);
  return { boardId: board.id, stages, epic, feature, story, storyTask, topTask, subtask, bug };
}

const cardCheckbox = (page: Page, title: string) =>
  page.getByRole('checkbox', { name: new RegExp(`^Selecionar #\\d* ${title}$`) });

async function setViewport(page: Page, projectName: string) {
  await page.setViewportSize(projectName.includes('mobile') ? { width: 390, height: 844 } : { width: 1440, height: 900 });
}

test('quadro sem método mostra só os cards de execução, no Kanban, na sprint e em Minhas tarefas',
  async ({ page, request, authenticatedGoto }, info) => {
    test.setTimeout(180_000);
    await setViewport(page, info.project.name);
    await authenticatedGoto('/projects');
    await withBoardFactory(page, request, async factory => {
      const s = await createScenario(factory);

      await authenticatedGoto(`/boards/${s.boardId}`);
      for (const card of [s.storyTask, s.topTask, s.bug]) await expect(cardCheckbox(page, card.title)).toHaveCount(1);
      for (const hidden of [s.story, s.epic, s.feature, s.subtask]) await expect(cardCheckbox(page, hidden.title)).toHaveCount(0);
      mkdirSync(screenshots, { recursive: true });
      await page.screenshot({ path: `${screenshots}/T-0022-board-${info.project.name}.png`, scale: 'css' });

      // A Subtarefa continua dentro do card da Tarefa de topo: a gaveta da Tarefa lista a Subtarefa.
      await page.getByRole('button', { name: 'Lista', exact: true }).click();
      await page.getByRole('row').filter({ hasText: s.topTask.title }).click();
      const topDrawer = page.getByRole('dialog', { name: s.topTask.title });
      await expect(topDrawer).toBeVisible();
      await topDrawer.getByRole('button', { name: 'Subtarefas', exact: true }).click();
      await expect(topDrawer.getByRole('button', { name: `Abrir detalhes da subtarefa ${s.subtask.title}` })).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(topDrawer).toBeHidden();

      // O card da Tarefa filha da História abre a gaveta dela.
      await page.getByRole('row').filter({ hasText: s.storyTask.title }).click();
      const drawer = page.getByRole('dialog', { name: s.storyTask.title });
      await expect(drawer).toBeVisible();
      await page.keyboard.press('Escape');
      await expect(drawer).toBeHidden();
      for (const hidden of [s.story, s.epic, s.feature, s.subtask])
        await expect(page.getByRole('row').filter({ hasText: hidden.title })).toHaveCount(0);

      // Quadro da sprint: mesma regra sobre os itens planejados na sprint.
      const today = new Date();
      const iso = (days: number) => new Date(today.getTime() + days * 86_400_000).toISOString().slice(0, 10);
      const sprint = await factory.api(`/api/projects/${factory.projectId}/sprints`, 'POST', {
        name: factory.name('sprint'), startDate: iso(-1), endDate: iso(13),
      });
      expect(sprint.status(), 'POST sprint').toBeLessThan(300);
      const sprintId: string = await sprint.json();
      const planned = await factory.api(`/api/projects/${factory.projectId}/backlog/sprint`, 'PUT', {
        sprintId, workItemIds: [s.epic, s.feature, s.story, s.storyTask, s.topTask, s.subtask, s.bug].map(item => item.id),
      });
      expect(planned.status(), 'PUT backlog/sprint').toBeLessThan(300);
      await authenticatedGoto(`/projects/${factory.projectId}/sprints`);
      await page.getByRole('tab', { name: 'Quadro' }).click();
      for (const card of [s.storyTask, s.topTask, s.bug])
        await expect(page.getByRole('button', { name: `Arrastar ${card.title}`, exact: true })).toHaveCount(1);
      for (const hidden of [s.story, s.epic, s.feature, s.subtask])
        await expect(page.getByRole('button', { name: `Arrastar ${hidden.title}`, exact: true })).toHaveCount(0);
      await page.screenshot({ path: `${screenshots}/T-0022-sprint-${info.project.name}.png`, scale: 'css' });

      // Minhas tarefas: a Tarefa filha da História entra e a História atribuída continua.
      // O painel pessoal é pesado (até 500 itens): espera a resposta real antes de procurar os itens.
      const myWork = page.waitForResponse(response => new URL(response.url()).pathname === '/api/me/work'
        && response.request().method() === 'GET');
      await authenticatedGoto('/me/tasks');
      expect((await myWork).status(), 'GET /api/me/work').toBe(200);
      await page.getByRole('textbox', { name: 'Pesquisar meu trabalho' }).fill(factory.batch);
      await expect(page.getByRole('button', { name: new RegExp(`^Abrir tarefa \\d+: ${s.storyTask.title}$`) })).toBeVisible();
      await expect(page.getByRole('button', { name: new RegExp(`^Abrir tarefa \\d+: ${s.story.title}$`) })).toBeVisible();
    }, 'T0022');
  });

test('Novo card do quadro não oferece Épico, Funcionalidade nem História, nos temas claro e escuro',
  async ({ page, request, authenticatedGoto }, info) => {
    await setViewport(page, info.project.name);
    await authenticatedGoto('/projects');
    await withBoardFactory(page, request, async factory => {
      const board = await factory.board();
      const stages = await factory.get<BoardStage[]>(`/api/Stages/board/${board.id}`);
      const story = await createItem(factory, board.id, stages[0].id, 'historia', Kind.UserStory);
      const storyTask = await createItem(factory, board.id, stages[0].id, 'tarefa-da-historia', Kind.Task, { parentId: story.id });
      await authenticatedGoto(`/boards/${board.id}`);
      mkdirSync(screenshots, { recursive: true });

      // Começa no claro, qualquer que seja a preferência salva.
      const toDark = page.getByRole('button', { name: 'Ativar modo escuro' });
      const toLight = page.getByRole('button', { name: 'Ativar modo claro' });
      await expect(toDark.or(toLight)).toBeVisible();
      if (await toLight.isVisible()) await toLight.click();
      await expect(toDark).toBeVisible();

      for (const mode of ['claro', 'escuro'] as const) {
        if (mode === 'escuro') {
          await toDark.click();
          await expect(toLight).toBeVisible();
        }
        await expect(cardCheckbox(page, storyTask.title)).toHaveCount(1);
        await page.getByRole('button', { name: 'Novo Card', exact: true }).first().click();
        const kind = page.getByRole('combobox', { name: 'Tipo do Card' });
        await expect(kind).toBeVisible();
        await expect(kind.getByRole('option')).toHaveText(['Tarefa', 'Bug']);
        for (const planning of ['Épico', 'Feature', 'História'])
          await expect(kind.getByRole('option', { name: planning, exact: true })).toHaveCount(0);
        await expect(kind).toHaveAccessibleDescription('Épicos e Histórias são criados no backlog');
        await expect(page.getByRole('note').filter({ hasText: 'Épicos e Histórias são criados no backlog' })).toBeVisible();
        await page.screenshot({ path: `${screenshots}/T-0022-novo-card-${mode}-${info.project.name}.png`, scale: 'css' });
        await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
        await expect(kind).toBeHidden();
      }
      await toLight.click();
      await expect(toDark).toBeVisible();
    }, 'T0022');
  });
