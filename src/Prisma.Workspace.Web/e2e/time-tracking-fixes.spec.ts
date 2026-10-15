import type { Page } from '@playwright/test';
import { test, expect, navigateWithApi } from './fixtures/functional';
import { withTimeFactory, type TimeFactory, type TimeEntry, type WeeklyTime } from './fixtures/time';

test.use({timezoneId:'America/Sao_Paulo'});
const responseTo=(page:Page,method:string,path:string)=>page.waitForResponse(r=>r.request().method()===method&&new URL(r.url()).pathname.toLowerCase()===path.toLowerCase());
async function board(page:Page,f:TimeFactory,reload=false){
  await navigateWithApi(page,[`/api/WorkItems/board/${f.board.id}`],()=>reload?page.reload({waitUntil:'domcontentloaded'}):page.goto(`/boards/${f.board.id}`,{waitUntil:'domcontentloaded'}));
  await expect(page.getByLabel('Selecionar quadro')).toHaveValue(f.board.id);
}
async function day(page:Page,f:TimeFactory,date:string,reload=false){
  await board(page,f,reload);
  const loaded=responseTo(page,'GET','/api/TimeEntries/my/weekly');
  await page.getByRole('button',{name:'Meu Tempo',exact:true}).click();
  const week:WeeklyTime=await (await loaded).json();
  const index=week.days.findIndex(d=>d.date===date);expect(index).toBeGreaterThanOrEqual(0);
  const entries=responseTo(page,'GET','/api/me/time/entries');
  await page.getByRole('heading',{name:'Meu Tempo',exact:true}).locator('..').getByRole('button',{name:'Ajustar',exact:true}).nth(index).click();
  expect((await entries).status()).toBe(200);
  return page.getByRole('heading',{name:/^Ajustar: /}).locator('..');
}
const today=()=>{
  const today=new Date().toLocaleDateString('en-CA',{timeZone:'America/Sao_Paulo'});
  const date=new Date(`${today}T12:00:00-03:00`);
  // Usar hoje, no início da madrugada, garante passado e pertence à semana visível.
  return date.toLocaleDateString('en-CA',{timeZone:'America/Sao_Paulo'});
};
const total=(f:TimeFactory,id:string)=>f.get<{totalSeconds:number}>(`/api/TimeEntries/work-item/${id}/total`);

test('timer em andamento não dobra após recarregar o card',async({page,request})=>{
  await withTimeFactory(page,request,async f=>{
    const task=await f.task();await board(page,f);
    // Iniciar pela UI do drawer; todos os intervalos pertencem ao usuário da fixture.
    await page.getByRole('heading',{name:new RegExp(task.title)}).click();
    const started=responseTo(page,'POST','/api/TimeEntries/start');
    await page.getByRole('button',{name:'Iniciar cronômetro',exact:true}).click();
    const entry:TimeEntry=await (await started).json();
    await page.getByRole('button',{name:'Fechar modal',exact:true}).click();
    await expect.poll(async()=>Math.floor((Date.now()-Date.parse(entry.startedAt))/1000)).toBeGreaterThanOrEqual(4);
    await navigateWithApi(page,[`/api/WorkItems/board/${f.board.id}`],()=>page.reload({waitUntil:'domcontentloaded'}));
    const card=page.getByRole('heading',{name:new RegExp(task.title)}).locator('..');
    // O timer do card tem texto HH:mm:ss. Comparar ao relógio real, não ao total duplicado.
    const counter=card.getByText(/^\d{2}:\d{2}:\d{2}$/, {exact:true});
    await expect(counter).toBeVisible();
    const text=await counter.innerText();const [h,m,s]=text.split(':').map(Number);
    const seconds=h*3600+m*60+s;
    const real=Math.floor((Date.now()-Date.parse(entry.startedAt))/1000);
    expect(seconds).toBeGreaterThanOrEqual(real-2);expect(seconds).toBeLessThanOrEqual(real+1);
    const stopped=await f.api('/api/TimeEntries/stop','POST',{workItemId:task.id});expect(stopped.status()).toBe(200);
    const closed:TimeEntry=await stopped.json();
    expect((await total(f,task.id)).totalSeconds).toBe(closed.durationSeconds);
  });
});

test('editar lançamento no ajuste persiste horários, observação e atualiza dia e semana',async({page,request})=>{
  await withTimeFactory(page,request,async f=>{
    const task=await f.task();const date=today();
    const entry=await f.manual(task.id,`${date}T00:00:00-03:00`,`${date}T00:02:00-03:00`);
    const modal=await day(page,f,date);
    const row=modal.getByRole('article',{name:`Lançamento ${task.title}`,exact:true});
    await row.getByRole('button',{name:'Editar lançamento',exact:true}).click();
    await row.getByLabel('Fim (Brasília)',{exact:true}).fill(`${date}T00:03`);
    await row.getByRole('textbox',{name:'Observação',exact:true}).fill('Correção própria T7');
    const saved=responseTo(page,'PUT',`/api/timeentries/${entry.id}`);
    const daily=responseTo(page,'GET','/api/me/time/daily-by-task');const weekly=responseTo(page,'GET','/api/TimeEntries/my/weekly');
    await row.getByRole('button',{name:'Salvar lançamento',exact:true}).click();
    expect((await saved).status()).toBe(200);
    expect(await (await daily).json()).toEqual([expect.objectContaining({workItemId:task.id,seconds:180})]);
    const week:WeeklyTime=await (await weekly).json();expect(week.days.find(d=>d.date===date)?.totalSeconds).toBe(180);
    await expect(modal.getByText('0h03',{exact:true})).toBeVisible();
    await expect(row).toContainText('0h 03min 00s');await expect(row).toContainText('Correção própria T7');
    expect(await f.get<TimeEntry[]>(`/api/TimeEntries/work-item/${task.id}`)).toEqual([expect.objectContaining({id:entry.id,durationSeconds:180,note:'Correção própria T7'})]);
    const reopened=await day(page,f,date,true);
    await expect(reopened.getByRole('article',{name:`Lançamento ${task.title}`})).toContainText('0h 03min 00s');
  });
});

test('excluir lançamento exige confirmação, cancelar preserva e confirmar atualiza os totais',async({page,request})=>{
  await withTimeFactory(page,request,async f=>{
    const task=await f.task();const date=today();
    const entry=await f.manual(task.id,`${date}T00:00:00-03:00`,`${date}T00:02:00-03:00`);
    const modal=await day(page,f,date);const row=modal.getByRole('article',{name:`Lançamento ${task.title}`,exact:true});
    await row.getByRole('button',{name:'Excluir lançamento',exact:true}).click();
    await expect(row.getByRole('alertdialog',{name:'Confirmar exclusão do lançamento'})).toBeVisible();
    await row.getByRole('button',{name:'Cancelar exclusão',exact:true}).click();
    expect((await total(f,task.id)).totalSeconds).toBe(120);
    await row.getByRole('button',{name:'Excluir lançamento',exact:true}).click();
    const removed=responseTo(page,'DELETE',`/api/timeentries/${entry.id}`);
    const daily=responseTo(page,'GET','/api/me/time/daily-by-task');const weekly=responseTo(page,'GET','/api/TimeEntries/my/weekly');
    await row.getByRole('button',{name:'Confirmar exclusão',exact:true}).click();
    expect((await removed).status()).toBe(204);expect(await (await daily).json()).toEqual([]);
    const week:WeeklyTime=await (await weekly).json();expect(week.days.find(d=>d.date===date)?.totalSeconds).toBe(0);
    await expect(row).toHaveCount(0);expect((await total(f,task.id)).totalSeconds).toBe(0);
    expect(await f.get<TimeEntry[]>(`/api/TimeEntries/work-item/${task.id}`)).toEqual([]);
  });
});

test('buscar tarefa pelo título e número no ajuste funciona com teclado',async({page,request})=>{
  await withTimeFactory(page,request,async f=>{
    const task=await f.task();const archived=await f.task();
    expect((await f.boards.api(`/api/WorkItems/${archived.id}`,'DELETE')).status()).toBe(204);
    const unassigned=await f.boards.item(f.board.id,f.stage.id);
    const available=await f.get<Array<{id:string;assigned:boolean}>>('/api/me/time/tasks');
    expect(available.findIndex(t=>t.id===task.id)).toBeLessThan(available.findIndex(t=>t.id===unassigned.id));
    expect(available.map(t=>t.id)).not.toContain(archived.id);
    const date=today();const modal=await day(page,f,date);
    const field=modal.getByRole('combobox',{name:'Buscar tarefa por título ou número'});
    await field.fill(archived.title);
    await expect(modal.getByText('Nenhuma tarefa encontrada.',{exact:true})).toBeVisible();
    await field.fill(task.title);
    const option=modal.getByRole('option').filter({hasText:task.title});await expect(option).toHaveCount(1);
    await expect(option).toContainText('Atribuída a você');
    await field.press('Enter');await expect(modal.getByRole('status',{name:''}).filter({hasText:'Tarefa selecionada.'})).toBeVisible();
    const item=await f.boards.get<{number:number}>(`/api/WorkItems/${task.id}`);
    await field.press('ArrowDown');await field.fill(`#${item.number}`);await expect(option).toHaveCount(1);
    await field.press('ArrowDown');await field.press('Enter');
    await expect(modal.getByRole('button',{name:'Adicionar',exact:true})).toBeDisabled();
    await modal.getByPlaceholder('Minutos a adicionar').fill('1');
    const created=responseTo(page,'POST','/api/TimeEntries/manual');
    await modal.getByRole('button',{name:'Adicionar',exact:true}).click();expect((await created).status()).toBe(201);
    expect((await total(f,task.id)).totalSeconds).toBe(60);
  });
});
