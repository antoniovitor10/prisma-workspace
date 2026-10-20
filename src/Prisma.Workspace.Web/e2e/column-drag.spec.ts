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
 const visible=await page.locator(`[data-stage-id="${toId}"]`).evaluate(element=>{
  const rect=element.getBoundingClientRect();let left=Math.max(0,rect.left);let right=Math.min(innerWidth,rect.right);
  for(let parent=element.parentElement;parent;parent=parent.parentElement){
   if(['auto','scroll','hidden','clip'].includes(getComputedStyle(parent).overflowX)){
    const clip=parent.getBoundingClientRect();left=Math.max(left,clip.left);right=Math.min(right,clip.right);
   }
  }
  return{left,right};
 });
 expect(visible.right-visible.left).toBeGreaterThan(0);
 const end={x:(visible.left+visible.right)/2,y:start.y};
 expect(await page.evaluate(({point,id})=>document.elementFromPoint(point.x,point.y)?.closest('[data-stage-id]')?.getAttribute('data-stage-id')===id,{point:end,id:toId})).toBe(true);
 if(mobile){
  await page.evaluate(({from,to})=>{
   const events:Array<{type:string;pointer:string;button:number;x:number;y:number;onSource:boolean;overTarget:boolean}>=[];
   const listener=(raw:Event)=>{const event=raw as PointerEvent;const element=event.target instanceof Element?event.target:null;events.push({type:event.type,pointer:event.pointerType,button:event.button,x:event.clientX,y:event.clientY,onSource:element?.closest('[data-stage-drag-handle]')?.getAttribute('data-stage-drag-handle')===from,overTarget:document.elementFromPoint(event.clientX,event.clientY)?.closest('[data-stage-id]')?.getAttribute('data-stage-id')===to});};
   const types=['pointerdown','pointermove','pointerup','pointercancel','lostpointercapture'];
   types.forEach(type=>document.addEventListener(type,listener,true));
   (window as Window & {oficinaTouchEvidence?:()=>unknown}).oficinaTouchEvidence=()=>{types.forEach(type=>document.removeEventListener(type,listener,true));return{events,width:innerWidth,viewportScale:visualViewport?.scale,ratio:devicePixelRatio};};
  },{from:fromId,to:toId});
  const cdp=await page.context().newCDPSession(page);
  try{
   await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{...start,id:1}]});
   for(let i=1;i<=8;i++)await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:start.x+(end.x-start.x)*i/8,y:start.y,id:1}]});
   await cdp.send('Input.dispatchTouchEvent',{type:cancel?'touchCancel':'touchEnd',touchPoints:[]});
  }finally{
   await cdp.detach();
   const evidence=await page.evaluate(()=>{const target=window as Window & {oficinaTouchEvidence?:()=>unknown};const value=target.oficinaTouchEvidence?.();delete target.oficinaTouchEvidence;return value;});
   console.info('OFICINA_TOUCH_EVIDENCE',JSON.stringify({start,end,evidence}));
  }
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
   let message:string|undefined;
   const receive=async(dialog:import('@playwright/test').Dialog)=>{message=dialog.message();await dialog.dismiss();};
   page.on('dialog',receive);
   try{
    await drag(page,stages[0].id,stages[1].id,info.project.name.includes('mobile'));
    await expect.poll(()=>message).toContain('Falha sintética de ordenação');
   }finally{page.off('dialog',receive);}
   expect(attempts).toBe(1);
   await expect.poll(()=>ids(page)).toEqual(original);
   expect((await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`)).map(stage=>stage.id)).toEqual(original);
   await capture(page,info,'rollback');
  }finally{await page.unroute(route);}
 },'Oficina rollback');
});
