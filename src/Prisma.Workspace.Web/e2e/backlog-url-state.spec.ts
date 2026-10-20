import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {test,expect} from './fixtures/test';
import {withBoardFactory} from './fixtures/boards';
test('TASK213 seleção, colapso e rolagem sobrevivem a reload e ida ao quadro',async({page,request,authenticatedGoto},info)=>{
 await page.setViewportSize(info.project.name.includes('mobile')?{width:390,height:844}:{width:1440,height:900});
 await authenticatedGoto('/projects');
 await withBoardFactory(page,request,async f=>{
  const board=await f.board();const stages=await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`);
  const parent=await f.item(board.id,stages[0].id,{kind:5});const child=await f.item(board.id,stages[0].id,{parentId:parent.id,kind:6});
  for(let index=0;index<20;index++)await f.item(board.id,stages[0].id);
  const params=new URLSearchParams({'bl.selected':parent.id,'bl.collapsed':parent.id});
  await page.addInitScript(()=>{
   const events:Array<{kind:string;route:string;index:number|null;t:number}>=[];
   const record=(kind:string)=>{const end=location.pathname.split('/').at(-1)??'';events.push({kind,route:['boards','backlog'].includes(end)?end:'other',index:Number.isInteger(history.state?.idx)?history.state.idx:null,t:Math.round(performance.now())});};
   const push=history.pushState.bind(history);const replace=history.replaceState.bind(history);
   history.pushState=(...args:Parameters<History['pushState']>)=>{push(...args);record('push');};
   history.replaceState=(...args:Parameters<History['replaceState']>)=>{replace(...args);record('replace');};
   addEventListener('popstate',()=>record('pop'));
   (window as Window & {oficinaHistoryEvidence?:unknown}).oficinaHistoryEvidence=events;
  });
  await page.goto(`/projects/${f.projectId}/backlog?${params}`);
  await expect(page.getByRole('textbox',{name:`Título de ${parent.title}`,exact:true})).toBeVisible();
  await expect(page.getByRole('checkbox',{name:`Selecionar ${parent.title}`,exact:true})).toBeChecked();
  await expect(page.getByRole('textbox',{name:`Título de ${child.title}`,exact:true})).toHaveCount(0);
  await page.reload();
  await expect(page.getByRole('checkbox',{name:`Selecionar ${parent.title}`,exact:true})).toBeChecked();
  await expect(page.getByRole('textbox',{name:`Título de ${child.title}`,exact:true})).toHaveCount(0);
  await page.evaluate(()=>window.scrollTo(0,600));
  await expect.poll(()=>page.evaluate(()=>window.scrollY)).toBeGreaterThan(250);
  await expect.poll(()=>Number(new URL(page.url()).searchParams.get('bl.y'))).toBeGreaterThan(250);
  const previousY=await page.evaluate(()=>window.scrollY);
  const switcher=page.locator('[aria-label="Alternar visão do projeto"]');
  const boardLink=switcher.getByRole('link',{name:'Quadro',exact:true});
  await boardLink.click();await expect(page).toHaveURL(new RegExp(`/projects/${f.projectId}/boards`));
  await switcher.getByRole('link',{name:'Backlog',exact:true}).click();
  await expect(page.getByRole('checkbox',{name:`Selecionar ${parent.title}`,exact:true})).toBeChecked();
  await expect.poll(()=>new URL(page.url()).searchParams.get('bl.collapsed')).toBe(parent.id);
  await expect.poll(()=>page.evaluate(()=>window.scrollY)).toBe(previousY);
  await expect(page.getByRole('textbox',{name:`Título de ${child.title}`,exact:true})).toHaveCount(0);
  try{
   await page.goBack();await expect(page).toHaveURL(new RegExp(`/projects/${f.projectId}/boards`));
   await page.goForward();await expect(page.getByRole('checkbox',{name:`Selecionar ${parent.title}`,exact:true})).toBeChecked();
  }finally{
   console.info('OFICINA_HISTORY_EVIDENCE',JSON.stringify(await page.evaluate(()=>(window as Window & {oficinaHistoryEvidence?:unknown}).oficinaHistoryEvidence)));
  }
  const dir=resolve('../../.runlogs/oficina/screenshots');mkdirSync(dir,{recursive:true});
  await page.screenshot({path:`${dir}/backlog-url-${info.project.name}.png`,scale:'css'});
 },'Oficina URL');
});

test('TASK213 filtros e item aberto são restaurados por URL direta',async({page,request,authenticatedGoto},info)=>{
 await page.setViewportSize(info.project.name.includes('mobile')?{width:390,height:844}:{width:1440,height:900});
 await authenticatedGoto('/projects');
 await withBoardFactory(page,request,async f=>{
  const board=await f.board();const stages=await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`);
  const item=await f.item(board.id,stages[0].id,{kind:5,priority:2});
  const params=new URLSearchParams({'bl.q':item.title,'bl.kind':'5','bl.priority':'2','bl.board':board.id,'bl.group':'priority'});
  await page.goto(`/projects/${f.projectId}/backlog?${params}`);
  await expect(page.getByRole('textbox',{name:'Pesquisar backlog',exact:true})).toHaveValue(item.title);
  await expect(page.getByRole('textbox',{name:`Título de ${item.title}`,exact:true})).toHaveValue(item.title);
  params.set('item',item.id);
  await page.goto(`/projects/${f.projectId}/backlog?${params}`);
  await expect(page.getByRole('heading',{name:item.title,exact:true})).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading',{name:item.title,exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Fechar modal',exact:true}).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByRole('textbox',{name:'Pesquisar backlog',exact:true})).toHaveValue(item.title);
  await expect(page.getByRole('textbox',{name:`Título de ${item.title}`,exact:true})).toHaveValue(item.title);
  for(const key of ['bl.kind','bl.priority','bl.board','bl.group'])expect(new URL(page.url()).searchParams.get(key)).toBe(params.get(key));
 },'Oficina URL filtros');
});
