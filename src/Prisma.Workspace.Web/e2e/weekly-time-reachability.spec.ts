import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {test,expect} from './fixtures/test';
const apiUrl=process.env.E2E_API_URL??'http://127.0.0.1:5400';
test('QA-H10 todos os dias da semana são alcançáveis sem forçar clique',async({page,request,authenticatedGoto},info)=>{
 await page.setViewportSize(info.project.name.includes('mobile')?{width:390,height:844}:{width:1440,height:900});
 await authenticatedGoto('/projects');
 const session=await page.evaluate(()=>({token:localStorage.getItem('prisma_workspace_token')!,organizationId:localStorage.getItem('prisma_workspace_organization')!}));
 const headers={Authorization:`Bearer ${session.token}`,'X-Organization-Id':session.organizationId};
 const projectsResponse=await request.get(`${apiUrl}/api/projects`,{headers});expect(projectsResponse.status()).toBe(200);
 const projects:Array<{id:string}>=await projectsResponse.json();expect(projects.length).toBeGreaterThan(0);
 const boardsResponse=await request.get(`${apiUrl}/api/Boards`,{headers});expect(boardsResponse.status()).toBe(200);
 const boards:Array<{id:string;projectId:string}>=await boardsResponse.json();const board=boards.find(value=>value.projectId===projects[0].id);expect(board).toBeTruthy();
 await page.goto(`/boards/${board!.id}`);
 const loaded=page.waitForResponse(response=>new URL(response.url()).pathname.toLowerCase()==='/api/timeentries/my/weekly'&&response.request().method()==='GET');
 await page.getByRole('button',{name:'Meu Tempo',exact:true}).click();
 const response=await loaded;expect(response.status()).toBe(200);const week:{days:Array<{date:string}>}=await response.json();expect(week.days).toHaveLength(7);
 const panel=page.getByRole('heading',{name:'Meu Tempo',exact:true}).locator('..');
 const directory=resolve('../../.runlogs/oficina/screenshots');mkdirSync(directory,{recursive:true});
 await page.screenshot({path:`${directory}/weekly-start-${info.project.name}-${process.env.OFICINA_SCREENSHOT_PHASE??'after'}.png`,scale:'css'});
 for(let index=0;index<week.days.length;index++){
  const date=week.days[index].date;
  const matches=(response:import('@playwright/test').Response,path:string)=>{const url=new URL(response.url());return url.pathname.toLowerCase()===path&&url.searchParams.get('date')===date;};
  const daily=page.waitForResponse(response=>matches(response,'/api/me/time/daily-by-task'));
  const justifications=page.waitForResponse(response=>matches(response,'/api/me/time/justifications'));
  await panel.getByRole('button',{name:'Ajustar',exact:true}).nth(index).click();
  expect((await daily).status()).toBe(200);expect((await justifications).status()).toBe(200);
  const adjust=page.getByRole('heading',{name:/^Ajustar: /}).locator('..');await expect(adjust).toBeVisible();
  await adjust.getByRole('button',{name:'Fechar',exact:true}).click();
 }
 await page.screenshot({path:`${directory}/weekly-end-${info.project.name}-${process.env.OFICINA_SCREENSHOT_PHASE??'after'}.png`,scale:'css'});
});
