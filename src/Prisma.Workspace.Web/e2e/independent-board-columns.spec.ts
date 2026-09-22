import { test, expect } from './fixtures/test';

test('colunas independentes, transferência e exclusão preservam tarefa e histórico', async ({ page, authenticatedGoto, resolveSeedProject }) => {
  const project = await resolveSeedProject();
  await authenticatedGoto('/home');
  const api = async (path: string, method = 'GET', body?: unknown) => page.evaluate(async ({ path, method, body, base }) => {
    const response = await fetch(base + path, { method,
      headers: { Authorization: 'Bearer ' + localStorage.getItem('prisma_workspace_token'),
        'X-Organization-Id': localStorage.getItem('prisma_workspace_organization')!, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body) });
    const text = await response.text();
    let data; try { data = JSON.parse(text); } catch { data = text; }
    return { status: response.status, data };
  }, { path, method, body, base: process.env.E2E_API_URL! });
  const name = 'D89 ' + Date.now();
  const a = await api('/api/boards', 'POST', { name: name + ' A', projectId: project.id });
  expect(a.status).toBe(201);
  const b = await api('/api/boards', 'POST', { name: name + ' B', projectId: project.id, copyStagesFromBoardId: a.data });
  expect(b.status).toBe(201);
  const aStages = (await api('/api/stages/board/' + a.data)).data;
  const bStages = (await api('/api/stages/board/' + b.data)).data;
  expect(aStages).toHaveLength(3); expect(bStages).toHaveLength(3);
  expect(aStages.map((x: {id:string})=>x.id)).not.toEqual(bStages.map((x: {id:string})=>x.id));
  expect((await api('/api/stages/' + aStages[0].id, 'PUT', { name: name + ' coluna A' })).status).toBe(204);
  expect((await api('/api/stages/board/' + b.data)).data[0].name).toBe(bStages[0].name);
  expect((await api('/api/stages/board/' + a.data + '/order', 'PUT', aStages.map((x:{id:string})=>x.id).reverse())).status).toBe(204);
  expect((await api('/api/stages/board/' + a.data + '/order', 'PUT', bStages.map((x:{id:string})=>x.id))).status).toBe(400);
  expect((await api('/api/stages/board/' + b.data)).data.map((x:{id:string})=>x.id)).toEqual(bStages.map((x:{id:string})=>x.id));
  const users = (await api('/api/users/assignable?projectId=' + project.id)).data;
  const created = await api('/api/workitems', 'POST', { boardId:a.data, stageId:aStages[0].id,
    title:name + ' tarefa', responsibleId:users[0].id, priority:1, position:100 });
  expect(created.status).toBe(201);
  expect((await api('/api/workitems/move','POST',{ workItemId:created.data, destinationStageId:bStages[0].id,position:100 })).status).toBe(400);
  expect((await api('/api/boards/' + a.data,'DELETE')).status).toBe(400);
  expect((await api('/api/stages/' + aStages[0].id,'DELETE')).status).toBe(400);
  expect((await api('/api/stages/' + aStages[0].id + '?destinationStageId=' + aStages[1].id,'DELETE')).status).toBe(204);
  expect((await api('/api/workitems/' + created.data)).data.stageId).toBe(aStages[1].id);
  await authenticatedGoto('/boards/' + a.data);
  await expect(page.getByRole('heading', { name: new RegExp(name + ' tarefa') })).toBeVisible();
  await authenticatedGoto('/projects/' + project.id + '/backlog');
  await page.getByRole('button', {name:'Abrir detalhes de ' + name + ' tarefa', exact:true}).click();
  const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
  await expect(dialog).toBeVisible();
  await dialog.getByText('Transferir tarefa para outro quadro', { exact:true }).click();
  await dialog.getByLabel('Quadro de destino', { exact:true }).selectOption(b.data);
  await dialog.getByLabel('Coluna de destino', { exact:true }).selectOption(bStages[0].id);
  await dialog.getByRole('button',{name:'Transferir tarefa',exact:true}).click();
  await expect.poll(async ()=>(await api('/api/workitems/' + created.data)).data.boardId).toBe(b.data);
  expect((await api('/api/boards/' + b.data + '?destinationBoardId=' + a.data + '&destinationStageId=' + aStages[1].id,'DELETE')).status).toBe(204);
  const detail = await api('/api/workitems/' + created.data);
  expect(detail.data.boardId).toBe(a.data); expect(detail.data.stageId).toBe(aStages[1].id);
  const events = await api('/api/workitems/' + created.data + '/events');
  expect(events.status).toBe(200); expect(events.data.length).toBeGreaterThanOrEqual(3);
  await authenticatedGoto('/boards/' + a.data);
  await expect(page.getByRole('heading', { name: new RegExp(name + ' tarefa') })).toBeVisible();
});
