import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import type {Page,TestInfo} from '@playwright/test';
import {test,expect} from './fixtures/test';
import {withBoardFactory} from './fixtures/boards';

test.beforeEach(async({page},info)=>{await page.setViewportSize(info.project.name.includes('mobile')?{width:390,height:844}:{width:1440,height:900});});
const ids=(page:Page)=>page.locator('[data-stage-id]').evaluateAll(elements=>elements.map(element=>(element as HTMLElement).dataset.stageId));
async function capture(page:Page,info:TestInfo,label:string){
 const directory=resolve('../../.runlogs/oficina/screenshots');mkdirSync(directory,{recursive:true});
 await page.screenshot({path:`${directory}/columns-${label}-${info.project.name}.png`,scale:'css'});
}
async function drag(page:Page,fromId:string,toId:string,mobile:boolean,cancel=false){
 const source=page.locator(`[data-stage-drag-handle="${fromId}"]`);
 await source.scrollIntoViewIfNeeded();
 const a=await source.boundingBox();const b=await page.locator(`[data-stage-id="${toId}"]`).boundingBox();
 expect(a).not.toBeNull();expect(b).not.toBeNull();
 const width=page.viewportSize()!.width;
 const start={x:Math.min(a!.x+a!.width/2,width-8),y:a!.y+a!.height/2};
 const end={x:Math.min(b!.x+b!.width/2,width-8),y:start.y};
 if(mobile){
  const cdp=await page.context().newCDPSession(page);
  try{
   await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{...start,id:1}]});
   for(let i=1;i<=8;i++)await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:start.x+(end.x-start.x)*i/8,y:start.y,id:1}]});
   await cdp.send('Input.dispatchTouchEvent',{type:cancel?'touchCancel':'touchEnd',touchPoints:[]});
  }finally{await cdp.detach();}
 }else{
  await page.mouse.move(start.x,start.y);await page.mouse.down();
  await page.mouse.move(end.x,end.y,{steps:8});
  if(cancel)await page.mouse.move(5,80,{steps:3});
  await page.mouse.up();
 }
}

test('TASK124 arraste persiste a ordem, preserva cartões e mantém alternativa de teclado',async({page,request,authenticatedGoto},info)=>{
 await authenticatedGoto('/projects');
 await withBoardFactory(page,request,async f=>{
  const board=await f.board();const stages=await f.get<Array<{id:string;name:string;category:number}>>(`/api/Stages/board/${board.id}`);expect(stages.length).toBeGreaterThanOrEqual(2);
  const item=await f.item(board.id,stages[0].id);const original=await f.get(`/api/WorkItems/${item.id}`);
  const originalIds=stages.map(stage=>stage.id);const reordered=[originalIds[1],originalIds[0],...originalIds.slice(2)];
  await page.goto(`/boards/${board.id}`);await expect.poll(()=>ids(page)).toEqual(originalIds);
  await drag(page,stages[0].id,stages[1].id,info.project.name.includes('mobile'));
  await expect.poll(async()=>(await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`)).map(stage=>stage.id)).toEqual(reordered);
  await page.reload();await expect.poll(()=>ids(page)).toEqual(reordered);
  expect(await f.get(`/api/WorkItems/${item.id}`)).toEqual(original);
  await page.locator('[data-stage-id]').first().scrollIntoViewIfNeeded();await capture(page,info,'persisted');
  const move=page.getByRole('button',{name:`Mover coluna ${stages[0].name} para a esquerda`,exact:true});
  await move.focus();await move.press('Enter');
  await expect.poll(async()=>(await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`)).map(stage=>stage.id)).toEqual(originalIds);
 },'Oficina colunas');
});

test('TASK124 cancelamento não salva e erro do servidor restaura a ordem',async({page,request,authenticatedGoto},info)=>{
 await authenticatedGoto('/projects');
 await withBoardFactory(page,request,async f=>{
  const board=await f.board();const stages=await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`);const original=stages.map(stage=>stage.id);
  await page.goto(`/boards/${board.id}`);await expect.poll(()=>ids(page)).toEqual(original);
  let attempts=0;const route=`**/api/Stages/board/${board.id}/order`;
  await page.route(route,async request=>{attempts++;await request.fulfill({status:409,contentType:'application/problem+json',body:JSON.stringify({detail:'Falha sintética de ordenação'})});});
  try{
   await drag(page,stages[0].id,stages[1].id,info.project.name.includes('mobile'),true);
   expect(attempts).toBe(0);await expect.poll(()=>ids(page)).toEqual(original);
   const message=new Promise<string>(resolve=>page.once('dialog',async dialog=>{const text=dialog.message();await dialog.dismiss();resolve(text);}));
   await drag(page,stages[0].id,stages[1].id,info.project.name.includes('mobile'));
   expect(await message).toContain('Falha sintética de ordenação');expect(attempts).toBe(1);
   await expect.poll(()=>ids(page)).toEqual(original);
   expect((await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`)).map(stage=>stage.id)).toEqual(original);
   await capture(page,info,'rollback');
  }finally{await page.unroute(route);}
 },'Oficina rollback');
});
