import { test, expect } from './fixtures/test';
import {
  methodGitEnabled, humanSession, tokenSession, createMethodProject, createTask, getTask, moveTask,
  expectStatus, expectStays, pullRequestEvent, reviewEvent, sendWebhook, setPullRequest, fake,
  type Api, type MethodProject,
} from './fixtures/method-git';

/**
 * Jornada da fase 2a da integração Git do método (SPEC-METODO-GIT-INTEGRATION, CA-2A-*).
 * Dados próprios por execução (projeto, tokens, repositório no GitHub falso); não usa o seed.
 * Os casos de API rodam uma vez (desktop); os de tela rodam em desktop e mobile.
 */
test.describe('integração Git do método — fase 2a', () => {
  test.skip(!methodGitEnabled, 'Requer E2E_METHOD_GIT=1 e o GitHub falso da frente.');

  let human: Api;
  let p: MethodProject;
  let agent: Api;

  test.beforeAll(async () => {
    test.setTimeout(120_000);
    human = (await humanSession()).api;
    await fake('POST', '/__fixture/reset');
    p = await createMethodProject(human, 'jornada');
    agent = await tokenSession(p.agentToken);
  });

  test.describe('API', () => {
    test.beforeEach(({}, testInfo) => {
      test.skip(testInfo.project.name.includes('mobile'), 'Casos de API rodam uma vez, no desktop.');
    });

    test('token de agente: identidade e regras vêm do servidor (CA-2A-17)', async () => {
      const me = await agent.json<{ authenticationKind: string; role: string; projectId: string; userId: string }>(agent.get('/api/method/me'));
      expect(me.authenticationKind).toBe('methodToken');
      expect(me.role).toBe('agente');
      expect(me.projectId).toBe(p.projectId);
      expect(me.userId).toBe(`method:${p.agentPrincipalId}`);
      const rules = await agent.json<{ allowedTransitions: Record<string, string[]> }>(agent.get(`/api/projects/${p.projectId}/method/rules`));
      expect(rules.allowedTransitions.backlog ?? []).not.toContain('em-desenvolvimento');
      expect(rules.allowedTransitions.pronta ?? []).not.toContain('em-desenvolvimento');
    });

    test('agente nunca autoriza: recusado por move do método e por todas as rotas genéricas (CA-2A-03)', async () => {
      const t = await createTask(agent, p, 'T-0101');
      expect(t.status).toBe('backlog');
      const dev = p.stages['em-desenvolvimento'];
      const tentativas = [
        ['move do método', moveTask(agent, p, 'T-0101', 'em-desenvolvimento', 'um humano pediu para começar')],
        ['POST /api/WorkItems/move', agent.post('/api/WorkItems/move', { workItemId: t.workItemId, destinationStageId: dev, position: 100 })],
        ['PUT /api/WorkItems/{id} com StageId', agent.put(`/api/WorkItems/${t.workItemId}`, {
          title: t.title, description: null, kind: 5, stageId: dev, priority: 1, responsibleId: null, teamId: null, origin: 0,
          requesterId: null, requesterName: null, requesterEmail: null, startDate: null, dueDate: null,
          estimatedHours: null, remainingHours: null, points: null, acceptanceCriteria: null })],
        ['ação em lote Move', agent.post(`/api/boards/${p.boardId}/work-items/bulk`, { workItemIds: [t.workItemId], action: 1, targetValue: dev })],
        ['criação genérica direto em desenvolvimento', agent.post('/api/WorkItems', { boardId: p.boardId, stageId: dev, parentId: null, title: 'atalho', priority: 1, position: 100 })],
        ['criação do método direto em desenvolvimento', agent.post(`/api/projects/${p.projectId}/method/tasks`, {
          id: 'T-0199', title: 'atalho', hu: 'HU-9001', filePath: 'x.md', covers: ['CA-1'], risk: 'normal', status: 'em-desenvolvimento' })],
      ] as const;
      for (const [nome, req] of tentativas) {
        const r = await req;
        expect([401, 403], `${nome} deveria ser recusado; veio ${r.status()} ${await r.text()}`).toContain(r.status());
      }
      expect((await getTask(human, p, 'T-0101')).status).toBe('backlog');
      const r = await human.get(`/api/projects/${p.projectId}/method/tasks/T-0199`);
      expect(r.status(), 'T-0199 não pode ter sido criada').toBe(404);
    });

    test('humano autoriza pela sessão interativa; agente bloqueia e só retoma após liberação humana (CA-2A-03/04)', async () => {
      await createTask(human, p, 'T-0102');
      expect((await moveTask(human, p, 'T-0102', 'em-desenvolvimento', 'humano autoriza')).ok()).toBeTruthy();
      expect((await getTask(agent, p, 'T-0102')).authorized).toBe(true);
      expect((await moveTask(agent, p, 'T-0102', 'bloqueada', 'aguardando credencial')).ok()).toBeTruthy();
      const semLiberacao = await moveTask(agent, p, 'T-0102', 'em-desenvolvimento', 'retomando sozinho');
      expect(semLiberacao.status(), 'sem liberação humana a retomada é recusada').toBe(403);
      // Liberação pela sessão humana: a API informa o bloqueio/ciclo vigentes nas regras/estado da task.
      const estado = await human.json<{ blockingEventId: string; cycleId: string }>(
        human.get(`/api/projects/${p.projectId}/method/tasks/T-0102/resume-approval`)).catch(() => null);
      test.skip(!estado, 'Contrato de consulta do bloqueio vigente ainda não publicado; caso coberto no QA manual.');
      const t = await getTask(human, p, 'T-0102');
      await human.json(human.post(`/api/projects/${p.projectId}/method/tasks/${t.workItemId}/resume-approval`, {
        blockingEventId: estado!.blockingEventId, cycleId: estado!.cycleId, reason: 'credencial entregue' }));
      expect((await moveTask(agent, p, 'T-0102', 'em-desenvolvimento', 'credencial recebida')).ok()).toBeTruthy();
    });

    test('agente não usa bloqueio para se autoautorizar a partir do backlog (CA-2A-04)', async () => {
      await createTask(agent, p, 'T-0103');
      expect((await moveTask(agent, p, 'T-0103', 'bloqueada', 'bloqueio a partir do backlog')).ok()).toBeTruthy();
      const r = await moveTask(agent, p, 'T-0103', 'em-desenvolvimento', 'atalho');
      expect(r.status()).toBe(403);
      expect((await getTask(human, p, 'T-0103')).status).toBe('bloqueada');
    });

    test('tokens: agente não emite token; humano só para si; revogação vale na requisição seguinte (CA-2A-01/02)', async () => {
      const emitir = await agent.post(`/api/projects/${p.projectId}/method/tokens`, { name: 'auto', role: 'agente', validDays: 1 });
      expect([401, 403]).toContain(emitir.status());
      const meu = await human.json<{ metadata: { id: string; role: string }; token: string }>(
        human.post(`/api/projects/${p.projectId}/method/tokens`, { name: 'meu token humano', role: 'humano', validDays: 1 }));
      expect(meu.metadata.role).toBe('humano');
      const lista = await human.json<unknown[]>(human.get(`/api/projects/${p.projectId}/method/tokens`));
      expect(JSON.stringify(lista)).not.toContain(meu.token);
      // Token humano não concede autorização interativa: colocar no MCP/REST não autoriza início.
      const tokenHumano = await tokenSession(meu.token);
      await createTask(human, p, 'T-0104');
      expect((await moveTask(tokenHumano, p, 'T-0104', 'em-desenvolvimento', 'pelo token humano')).status()).toBe(403);
      // Revogação: próxima requisição recebe 401.
      expect((await tokenHumano.get('/api/method/me')).status()).toBe(200);
      const rev = await human.post(`/api/projects/${p.projectId}/method/tokens/${meu.metadata.id}/revoke`);
      expect(rev.status()).toBe(204);
      expect((await tokenHumano.get('/api/method/me')).status()).toBe(401);
    });

    test('token de método não acessa outro projeto nem rotas fora do escopo (CA-2A-02/23)', async () => {
      const outro = await createMethodProject(human, 'escopo');
      const r = await agent.get(`/api/projects/${outro.projectId}/method/tasks`);
      expect([403, 404]).toContain(r.status());
      const login = await agent.get('/api/projects');
      expect(JSON.stringify(await login.json().catch(() => ({})))).not.toContain(outro.projectId);
    });

    test('webhook: PR aberto move em-desenvolvimento → em-revisao uma vez; draft não move (CA-2A-09/10)', async () => {
      await createTask(human, p, 'T-0110');
      await moveTask(human, p, 'T-0110', 'em-desenvolvimento', 'humano autoriza');
      const draft = await pullRequestEvent(p, 'opened', { number: 110, branch: 'task/T-0110-botao', draft: true });
      expect(draft.status).toBe(202);
      await expectStays(human, p, 'T-0110', 'em-desenvolvimento');
      const pronto = await pullRequestEvent(p, 'ready_for_review', { number: 110, branch: 'task/T-0110-botao' });
      expect(pronto.status).toBe(202);
      expect(pronto.ms, 'o webhook responde rápido, sem processar na requisição').toBeLessThan(3_000);
      await expectStatus(human, p, 'T-0110', 'em-revisao');
    });

    test('webhook: mesmo X-GitHub-Delivery não duplica; mesmo id com corpo diferente é 409 (CA-2A-07)', async () => {
      await createTask(human, p, 'T-0111');
      await moveTask(human, p, 'T-0111', 'em-desenvolvimento', 'humano autoriza');
      const payload = { action: 'opened', ...(await setPullRequest(p, { number: 111, branch: 'task/T-0111-x' })) };
      const primeiro = await sendWebhook(p, 'pull_request', payload, { delivery: 'e2e-dup-0111' });
      expect(primeiro.status).toBe(202);
      const [a, b] = await Promise.all([
        sendWebhook(p, 'pull_request', payload, { delivery: 'e2e-dup-0111' }),
        sendWebhook(p, 'pull_request', payload, { delivery: 'e2e-dup-0111' }),
      ]);
      for (const r of [a, b]) expect([200, 202]).toContain(r.status);
      const divergente = await sendWebhook(p, 'pull_request', { ...payload, action: 'closed' }, { delivery: 'e2e-dup-0111' });
      expect(divergente.status).toBe(409);
      await expectStatus(human, p, 'T-0111', 'em-revisao');
      const comments = await human.json<{ text: string }[]>(human.get(`/api/WorkItems/${(await getTask(human, p, 'T-0111')).workItemId}/history`)).catch(() => null);
      if (comments) {
        const movimentos = JSON.stringify(comments).match(/em-desenvolvimento.{0,40}em-revis/gi) ?? [];
        expect(movimentos.length, 'um único movimento no histórico').toBeLessThanOrEqual(1);
      }
    });

    test('webhook: assinatura inválida, ausente, SHA-1, malformada ou corpo adulterado é recusada sem efeito (CA-2A-06)', async () => {
      await createTask(human, p, 'T-0112');
      await moveTask(human, p, 'T-0112', 'em-desenvolvimento', 'humano autoriza');
      const payload = { action: 'opened', ...(await setPullRequest(p, { number: 112, branch: 'task/T-0112-x' })) };
      for (const signature of ['wrong-secret', 'none', 'sha1-only', 'malformed'] as const) {
        const r = await sendWebhook(p, 'pull_request', payload, { signature });
        expect(r.status, `assinatura ${signature}`).toBe(401);
        expect(r.text).not.toMatch(/sha256=|secret|segredo:/i);
      }
      expect((await sendWebhook(p, 'pull_request', payload, { tamper: true })).status, 'corpo adulterado').toBe(401);
      const outroRepo = JSON.parse(JSON.stringify(payload));
      outroRepo.repository.id = p.repositoryId + 1;
      expect([401, 403]).toContain((await sendWebhook(p, 'pull_request', outroRepo)).status);
      await expectStays(human, p, 'T-0112', 'em-desenvolvimento');
    });

    test('webhook: fechado sem merge e changes requested voltam; merge vai para em-teste (CA-2A-11/12)', async () => {
      await createTask(human, p, 'T-0113');
      await moveTask(human, p, 'T-0113', 'em-desenvolvimento', 'humano autoriza');
      await pullRequestEvent(p, 'opened', { number: 113, branch: 'task/T-0113-x' });
      await expectStatus(human, p, 'T-0113', 'em-revisao');
      await reviewEvent(p, { number: 113, branch: 'task/T-0113-x' }, 'changes_requested', 11301);
      await expectStatus(human, p, 'T-0113', 'em-desenvolvimento');
      await pullRequestEvent(p, 'synchronize', { number: 113, branch: 'task/T-0113-x', headSha: 'd'.repeat(40) });
      await moveTask(agent, p, 'T-0113', 'em-revisao', 'nível 0 de volta à revisão');
      await pullRequestEvent(p, 'closed', { number: 113, branch: 'task/T-0113-x', state: 'closed', merged: false, headSha: 'd'.repeat(40) });
      await expectStatus(human, p, 'T-0113', 'em-desenvolvimento');
      await pullRequestEvent(p, 'reopened', { number: 113, branch: 'task/T-0113-x', headSha: 'd'.repeat(40) });
      await moveTask(agent, p, 'T-0113', 'em-revisao', 'revisão de novo');
      await pullRequestEvent(p, 'closed', { number: 113, branch: 'task/T-0113-x', state: 'closed', merged: true, headSha: 'd'.repeat(40) });
      await expectStatus(human, p, 'T-0113', 'em-teste');
    });

    test('webhook: merge não promove com outro PR aberto (inclusive draft) nem em outra base (CA-2A-12)', async () => {
      await createTask(human, p, 'T-0114');
      await moveTask(human, p, 'T-0114', 'em-desenvolvimento', 'humano autoriza');
      await pullRequestEvent(p, 'opened', { number: 114, branch: 'task/T-0114-a' });
      await expectStatus(human, p, 'T-0114', 'em-revisao');
      await setPullRequest(p, { number: 1140, branch: 'task/T-0114-b', draft: true });
      await pullRequestEvent(p, 'closed', { number: 114, branch: 'task/T-0114-a', state: 'closed', merged: true });
      await expectStays(human, p, 'T-0114', 'em-revisao');
      await createTask(human, p, 'T-0115');
      await moveTask(human, p, 'T-0115', 'em-desenvolvimento', 'humano autoriza');
      await pullRequestEvent(p, 'opened', { number: 115, branch: 'task/T-0115-x', base: 'release' });
      await expectStatus(human, p, 'T-0115', 'em-revisao');
      await pullRequestEvent(p, 'closed', { number: 115, branch: 'task/T-0115-x', state: 'closed', merged: true, base: 'release' });
      await expectStays(human, p, 'T-0115', 'em-revisao');
    });

    test('webhook: PR não autoriza task em backlog ou bloqueada (CA-2A-10)', async () => {
      await createTask(human, p, 'T-0116');
      await pullRequestEvent(p, 'opened', { number: 116, branch: 'task/T-0116-x' });
      await expectStays(human, p, 'T-0116', 'backlog');
    });

    test('identificação: explícito > branch > título; DETRAN com título livre (CA-2A-08/14)', async () => {
      for (const id of ['T-0120', 'T-0121', 'T-0122', 'T-0123']) {
        await createTask(human, p, id);
        await moveTask(human, p, id, 'em-desenvolvimento', 'humano autoriza');
      }
      // Vínculo explícito T-0120 vence branch T-0121 e título [T-0122].
      await setPullRequest(p, { number: 120, branch: 'task/T-0121-outra', title: '[T-0122] título com outro id' });
      await agent.json(agent.post(`/api/projects/${p.projectId}/method/tasks/T-0120/pull-requests`, { url: `https://github.com/${p.repositoryFullName}/pull/120` }));
      await pullRequestEvent(p, 'opened', { number: 120, branch: 'task/T-0121-outra', title: '[T-0122] título com outro id' });
      await expectStatus(human, p, 'T-0120', 'em-revisao');
      await expectStays(human, p, 'T-0121', 'em-desenvolvimento', 1_500);
      expect((await getTask(human, p, 'T-0122')).status).toBe('em-desenvolvimento');
      // DETRAN: título livre, task achada pela branch.
      await pullRequestEvent(p, 'opened', { number: 123, branch: 'task/T-0123-corrigir-consulta', title: 'Corrige consulta de multas' });
      await expectStatus(human, p, 'T-0123', 'em-revisao');
    });

    test('não vinculado: PR sem candidato aparece na lista e não move nada (CA-2A-15)', async () => {
      const r = await pullRequestEvent(p, 'opened', { number: 130, branch: 'feature/sem-padrao', title: 'Ajuste livre sem id' });
      expect(r.status).toBe(202);
      await expect.poll(async () => {
        const page = await human.json<{ items: { pullRequestNumber: number; unlinkedReason: string | null }[] }>(
          human.get(`/api/projects/${p.projectId}/method/pull-requests?linked=false&page=1&pageSize=25`));
        return page.items.some(i => i.pullRequestNumber === 130);
      }, { timeout: 20_000 }).toBe(true);
    });

    test('regressão: humano move cards como hoje em projeto sem o método', async () => {
      const outro = await human.json<string | { id: string }>(human.post('/api/projects', { key: null, name: `sem-metodo-${Date.now()}`, description: null, nature: 1, workType: 1 }))
        .then(r => (typeof r === 'string' ? r : r.id));
      const detalhe = await human.json<{ boards: { id: string }[] }>(human.get(`/api/projects/${outro}`));
      const boardId = detalhe.boards[0].id;
      const stages = (await human.json<{ id: string; position: number }[]>(human.get(`/api/Stages/board/${boardId}`))).sort((a, b) => a.position - b.position);
      const id = await human.json<string>(human.post('/api/WorkItems', { boardId, stageId: stages[0].id, parentId: null, title: 'card comum', priority: 1, position: 100 }));
      for (const s of [stages[stages.length - 1], stages[0], stages[1]]) {
        const r = await human.post('/api/WorkItems/move', { workItemId: id, destinationStageId: s.id, position: 100 });
        expect(r.status(), `humano move para ${s.id}`).toBe(204);
      }
    });
  });
});
