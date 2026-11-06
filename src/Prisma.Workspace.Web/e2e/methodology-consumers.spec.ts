import {randomUUID} from 'node:crypto';
import {mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {test,expect} from './fixtures/test';
const apiUrl=process.env.E2E_API_URL??'http://127.0.0.1:5400';
test.beforeEach(async({page},info)=>{await page.setViewportSize(info.project.name.includes('mobile')?{width:390,height:844}:{width:1440,height:900});});
for(const methodology of [1,2,3,4])test(`TASK123 mesmas estimativas para valor interno ${methodology}`,async({page,request,authenticatedGoto},info)=>{
 await authenticatedGoto('/projects');
 const session=await page.evaluate(()=>({token:localStorage.getItem('prisma_workspace_token')!,org:localStorage.getItem('prisma_workspace_organization')!}));
 const claims=JSON.parse(Buffer.from(session.token.split('.')[1],'base64url').toString());
 const actor=claims.sub??claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
 const headers={Authorization:`Bearer ${session.token}`,'X-Organization-Id':session.org};
 const created=await request.post(`${apiUrl}/api/projects`,{headers,data:{key:`M${randomUUID().replaceAll('-','').slice(0,7)}`,name:`Oficina estimativa ${randomUUID()}`,nature:1,workType:1,methodology}});
 expect(created.status()).toBe(201);const projectId:string=await created.json();
 try{
  const projectResponse=await request.get(`${apiUrl}/api/projects/${projectId}`,{headers});expect(projectResponse.status()).toBe(200);
  const project=await projectResponse.json();expect(project.methodology).toBe(methodology);
  const boardId=project.boards[0].id;
  const stagesResponse=await request.get(`${apiUrl}/api/Stages/board/${boardId}`,{headers});expect(stagesResponse.status()).toBe(200);
  const stageId=(await stagesResponse.json())[0].id;
  const title=`Estimativa preservada ${randomUUID()}`;
  const itemResponse=await request.post(`${apiUrl}/api/WorkItems`,{headers,data:{boardId,stageId,title,responsibleId:actor,priority:1,position:100}});expect(itemResponse.status()).toBe(201);
  const itemId:string=await itemResponse.json();
  const taxonomy=await request.put(`${apiUrl}/api/WorkItems/${itemId}/taxonomy`,{headers,data:{taskTypeId:null,points:8,tagIds:[]}});expect(taxonomy.status()).toBe(204);
  const directory=resolve('../../.runlogs/oficina/screenshots');mkdirSync(directory,{recursive:true});
  await page.goto(`/boards/${boardId}`);await expect(page.getByText('8 pts',{exact:true})).toBeVisible();
  await page.screenshot({path:`${directory}/methodology-${methodology}-board-${info.project.name}.png`,scale:'css'});
  await page.goto(`/projects/${projectId}/backlog`);await expect(page.getByRole('spinbutton',{name:`Story points de ${title}`,exact:true})).toHaveValue('8');
  await page.goto(`/projects/${projectId}/backlog?item=${itemId}`);
  const drawer=page.getByRole('dialog');const points=drawer.getByRole('spinbutton',{name:'Story points',exact:true});await expect(points).toHaveValue('8');
  await points.scrollIntoViewIfNeeded();await expect(points).toBeVisible();
  await page.screenshot({path:`${directory}/methodology-${methodology}-drawer-${info.project.name}.png`,scale:'css'});
  await page.goto(`/projects/${projectId}/reports`);await expect(page.getByRole('heading',{name:'Velocidade das sprints',exact:true})).toBeVisible();
  const preserved=await request.get(`${apiUrl}/api/projects/${projectId}`,{headers});expect((await preserved.json()).methodology).toBe(methodology);
 }finally{
  const archived=await request.post(`${apiUrl}/api/projects/${projectId}/archive`,{headers});expect(archived.status()).toBe(204);
 }
});
