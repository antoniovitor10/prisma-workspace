import { test, expect } from './fixtures/test';
import {
  methodGitEnabled, humanSession, tokenSession, createMethodProject, createTask, getTask, expectStatus, expectStays,
  fake, sendWebhook, setPullRequest, INSTALLATION_ID, type Api, type MethodProject,
} from './fixtures/method-git';

/**
 * HU-0007 (abrir PR pelo card): rascunho do agente (T-0026), abrir PR pela App (T-0027), botão e modal no card e na
 * aba Git (T-0028). Dados próprios por execução no GitHub falso (e2e/fixtures/github-fake.mjs), nunca o GitHub real.
 * Só roda com E2E_METHOD_GIT=1 e o ambiente da integração Git (mesmas condições da fase 2a).
 */
test.describe('método Git: abrir PR pelo card (HU-0007)', () => {
  test.skip(!methodGitEnabled, 'Requer E2E_METHOD_GIT=1 e o GitHub falso da frente.');

  let human: Api;
  let agent: Api;
  let p: MethodProject;
  let sequence = 900;

  test.beforeAll(async () => {
    test.setTimeout(120_000);
    human = (await humanSession()).api;
    p = await createMethodProject(human, 'abrir-pr');
    agent = await tokenSession(p.agentToken);
    // A App de teste já aceitou a permissão nova (Pull requests: escrita).
    await fake('PUT', '/__fixture/app-permissions', { pull_requests: 'write' });
  });

  /** Task em desenvolvimento com a branch vinculada pelo agente e já vista no GitHub (webhook create). */
  async function taskWithPushedBranch(label: string, draft?: { title: string; body: string }) {
    const id = `T-0${++sequence}`;
    const branch = `task/${id}-${label}`;
    await createTask(human, p, id, 'em-desenvolvimento', { title: `Abrir PR ${label}` });
    await agent.json(agent.post(`/api/projects/${p.projectId}/method/tasks/${id}/branches`, { repository: p.repositoryFullName, branch }));
    const repository = { id: p.repositoryId, full_name: p.repositoryFullName, name: p.repositoryFullName.split('/')[1] };
    const pushed = await sendWebhook(p, 'create', { ref: branch, ref_type: 'branch', repository, installation: { id: INSTALLATION_ID }, sender: { login: 'dev-e2e', id: 4242 } });
    expect(pushed.status).toBe(202);
    await expect.poll(async () => {
      const task = await human.json<{ gitLinks?: { branchName: string | null; branchState: string }[] }>(human.get(`/api/projects/${p.projectId}/method/tasks/${id}`));
      return task.gitLinks?.find(link => link.branchName === branch)?.branchState;
    }, { timeout: 20_000, message: `${branch} deveria ficar enviada` }).toBe('remote');
    if (draft) await agent.json(agent.put(`/api/projects/${p.projectId}/method/tasks/${id}/pull-request-draft`, draft));
    return { id, branch };
  }

  async function created(branch: string) {
    const all = await fake('GET', '/__fixture/created') as { repo: string; head: string; title: string; body: string; base: string; draft: boolean }[];
    return all.filter(pr => pr.repo === p.repositoryFullName && pr.head === branch);
  }

  test.describe('API', () => {
    test.beforeEach(({ request: _request }, testInfo) => {
      test.skip(testInfo.project.name.includes('mobile'), 'Casos de API rodam uma vez, no desktop.');
    });

    test('rascunho pelo token do agente; token do método não abre PR (CA-2, CA-8)', async () => {
      const { id } = await taskWithPushedBranch('api', { title: 'Corrige consulta', body: 'Corpo do relatório' });
      const draft = await human.json<{ title: string; body: string; actorName: string }>(human.get(`/api/projects/${p.projectId}/method/tasks/${id}/pull-request-draft`));
      expect(draft).toMatchObject({ title: 'Corrige consulta', body: 'Corpo do relatório' });
      const refused = await agent.post(`/api/projects/${p.projectId}/method/tasks/${id}/pull-requests/open`, { linkId: '00000000-0000-4000-8000-000000000001', title: 'x', body: 'y', draft: false });
      expect(refused.status()).toBe(403);
      expect(JSON.stringify(await refused.json())).toContain('interactive_pr_required');
    });
  });

  test.describe('tela', () => {
    test('desktop: card → Abrir PR → modal com o rascunho → card em Em revisão com o PR (CA-1, CA-4, CA-5, CA-6)', async ({ page, authenticatedGoto }, testInfo) => {
      test.skip(testInfo.project.name.includes('mobile'), 'No celular o card não tem o botão (caso próprio abaixo).');
      const { id, branch } = await taskWithPushedBranch('desktop', { title: 'Corrige consulta de veículo', body: 'Resumo do agente' });
      await authenticatedGoto(`/boards/${p.boardId}`);
      await page.getByRole('button', { name: `Abrir PR da task ${id}` }).click();
      const dialog = page.getByRole('dialog', { name: `Abrir PR da task ${id}` });
      await expect(dialog.getByLabel('Título do PR')).toHaveValue('Corrige consulta de veículo');
      await expect(dialog.getByLabel('Corpo do PR')).toHaveValue('Resumo do agente');
      await expect(dialog.getByRole('checkbox', { name: 'Abrir como rascunho' })).not.toBeChecked();
      await expect(dialog.getByText(`O Prisma põe [${id}] no início do título ao abrir o PR.`)).toBeVisible();
      await dialog.getByRole('button', { name: 'Abrir PR' }).click();
      await expect(dialog.getByRole('status')).toContainText('o card foi para Em revisão');
      await expectStatus(human, p, id, 'em-revisao');
      const [pr] = await created(branch);
      expect(pr.title).toBe(`[${id}] Corrige consulta de veículo`);
      expect(pr.base).toBe('main');
      expect(pr.body).toMatch(/\n\nTask no Prisma: https?:\/\/\S+\/projects\/[^/]+\/backlog\?item=/);
      await dialog.getByRole('button', { name: 'Fechar' }).first().click();
      // Com o PR aberto, o botão some do card.
      await expect(page.getByRole('button', { name: `Abrir PR da task ${id}` })).toHaveCount(0);
    });

    test('celular e tema escuro: o card não tem o botão; a aba Git tem e o fluxo passa (CA-1, CA-2, CA-12)', async ({ page, authenticatedGoto }, testInfo) => {
      test.skip(!testInfo.project.name.includes('mobile'), 'Caso do layout de celular.');
      const { id, branch } = await taskWithPushedBranch('mobile', { title: 'Ajuste no celular', body: 'Corpo do celular' });
      await authenticatedGoto(`/boards/${p.boardId}`);
      await page.getByRole('button', { name: 'Ativar modo escuro' }).click();
      await expect(page.getByRole('button', { name: `Abrir PR da task ${id}` })).toHaveCount(0);
      await page.getByText(`${id} · Abrir PR mobile`).first().click();
      await page.getByRole('button', { name: 'Git', exact: true }).click();
      await expect(page.getByText(/Rascunho do PR: gravado em/)).toBeVisible();
      await page.getByRole('button', { name: `Abrir PR da task ${id}` }).click();
      const dialog = page.getByRole('dialog', { name: `Abrir PR da task ${id}` });
      await expect(dialog.getByLabel('Título do PR')).toHaveValue('Ajuste no celular');
      await testInfo.attach('modal-abrir-pr-celular-escuro', { body: await page.screenshot(), contentType: 'image/png' });
      await dialog.getByRole('button', { name: 'Abrir PR' }).click();
      await expect(dialog.getByRole('status')).toContainText(/PR #\d+ aberto/);
      await expectStatus(human, p, id, 'em-revisao');
      expect(await created(branch)).toHaveLength(1);
    });

    test('PR já aberto à mão: "Já havia um PR, vinculei o #N" sem criar outro (CA-7)', async ({ page, authenticatedGoto }, testInfo) => {
      test.skip(testInfo.project.name.includes('mobile'), 'Fluxo do modal já coberto no celular.');
      const { id, branch } = await taskWithPushedBranch('existente');
      const number = 7000 + sequence;
      await setPullRequest(p, { number, branch, title: 'Aberto à mão' });
      await authenticatedGoto(`/boards/${p.boardId}`);
      // CA-12 no desktop: o mesmo fluxo com o tema escuro ligado pela tela.
      await page.getByRole('button', { name: 'Ativar modo escuro' }).click();
      await page.getByRole('button', { name: `Abrir PR da task ${id}` }).click();
      const dialog = page.getByRole('dialog', { name: `Abrir PR da task ${id}` });
      // Sem rascunho: título padrão com o ID da task.
      await expect(dialog.getByLabel('Título do PR')).toHaveValue(`[${id}] Abrir PR existente`);
      await testInfo.attach('modal-abrir-pr-desktop-escuro', { body: await page.screenshot(), contentType: 'image/png' });
      await dialog.getByRole('button', { name: 'Abrir PR' }).click();
      await expect(dialog.getByRole('status')).toContainText(`Já havia um PR, vinculei o #${number}`);
      expect(await created(branch)).toHaveLength(0);
    });

    test('GitHub recusa (nada a comparar): mensagem em role=alert, texto mantido e card parado (CA-10)', async ({ page, authenticatedGoto }, testInfo) => {
      test.skip(testInfo.project.name.includes('mobile'), 'Erro do modal coberto no desktop.');
      const { id, branch } = await taskWithPushedBranch('sem-diff', { title: 'Sem diferença', body: 'Texto digitado' });
      await fake('POST', '/__fixture/pr-failure', { kind: 'nothing_to_compare', repo: p.repositoryFullName });
      await authenticatedGoto(`/boards/${p.boardId}`);
      await page.getByRole('button', { name: `Abrir PR da task ${id}` }).click();
      const dialog = page.getByRole('dialog', { name: `Abrir PR da task ${id}` });
      await dialog.getByRole('button', { name: 'Abrir PR' }).click();
      await expect(dialog.getByRole('alert')).toHaveText(`Não há diferença entre ${branch} e main: nada para abrir no PR.`);
      await expect(dialog.getByLabel('Corpo do PR')).toHaveValue('Texto digitado');
      await expectStays(human, p, id, 'em-desenvolvimento', 2_000);
      expect((await getTask(human, p, id)).pr).toBeNull();
    });
  });
});
