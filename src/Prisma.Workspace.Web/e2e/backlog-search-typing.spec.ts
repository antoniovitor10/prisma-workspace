import {test,expect} from './fixtures/test';
import {withBoardFactory} from './fixtures/boards';

// T-0016: a busca do backlog guarda todas as teclas, mesmo digitadas sem intervalo, e
// continua indo para a URL (recarregar, voltar/avançar e "Limpar filtros" acompanham).
test('T-0016 busca do backlog guarda o que foi digitado rápido e segue a URL',async({page,request,authenticatedGoto})=>{
 await authenticatedGoto('/projects');
 await withBoardFactory(page,request,async f=>{
  const board=await f.board();const stages=await f.get<Array<{id:string}>>(`/api/Stages/board/${board.id}`);
  const card=await f.item(board.id,stages[0].id,{title:f.name('Card')});
  const other=await f.item(board.id,stages[0].id);
  const backlog=`/projects/${f.projectId}/backlog`;
  const search=page.getByRole('textbox',{name:'Pesquisar backlog',exact:true});
  const cardRow=page.getByRole('textbox',{name:`Título de ${card.title}`,exact:true});
  const otherRow=page.getByRole('textbox',{name:`Título de ${other.title}`,exact:true});
  const query=()=>new URL(page.url()).searchParams.get('bl.q');

  // CA-1: teclas em sequência imediata, sem atraso entre elas.
  await page.goto(backlog);
  await expect(cardRow).toBeVisible();await expect(otherRow).toBeVisible();
  await search.click();
  await search.pressSequentially('Card');
  await expect(search).toHaveValue('Card');
  await expect.poll(query).toBe('Card');
  await expect(search).toHaveValue('Card');
  await expect(cardRow).toBeVisible();await expect(otherRow).toHaveCount(0);

  // CA-2: recarregar mantém a busca e o filtro.
  await page.reload();
  await expect(search).toHaveValue('Card');
  await expect(cardRow).toBeVisible();await expect(otherRow).toHaveCount(0);

  // CA-3: voltar e avançar levam o campo junto com a URL.
  await page.goto(`${backlog}?bl.q=tarefa`);
  await expect(search).toHaveValue('tarefa');
  await expect(otherRow).toBeVisible();await expect(cardRow).toHaveCount(0);
  await page.goBack();
  await expect(search).toHaveValue('Card');
  await expect(cardRow).toBeVisible();await expect(otherRow).toHaveCount(0);
  await page.goForward();
  await expect(search).toHaveValue('tarefa');
  await expect(otherRow).toBeVisible();

  // CA-3: "Limpar filtros" esvazia o campo e tira a busca da URL.
  await page.getByRole('button',{name:'Limpar filtros',exact:true}).click();
  await expect(search).toHaveValue('');
  await expect.poll(query).toBeNull();
  await expect(cardRow).toBeVisible();await expect(otherRow).toBeVisible();
 },'T0016 busca');
});
