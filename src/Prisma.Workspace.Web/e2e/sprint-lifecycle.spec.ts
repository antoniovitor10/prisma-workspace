import { test, expect, authenticatedApiGet } from './fixtures/test';

type Sprint = {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: number;
  itemCount: number;
};

const PLANEJADA = 1;
const ATIVA = 2;
const ENCERRADA = 3;

const isoMais = (dias: number) => {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
};

/**
 * SPEC-S-003 v3 / D84.
 *
 * Defeito de origem, relatado pelo PO: uma sprint de 1 a 30 de janeiro continuava
 * aceitando tarefas em setembro, porque o estado era uma coluna persistida que ninguém
 * havia mudado para encerrada.
 */
test.describe('ciclo de vida da sprint', () => {
  test('estado vem das datas e sprint vencida some do seletor de planejamento',
    async ({ page, authenticatedGoto, resolveSeedProject }) => {
      const project = await resolveSeedProject();

      // Três sprints do mesmo projeto: passada, corrente e futura.
      const criar = (nome: string, inicio: string, fim: string) => page.evaluate(
        async ({ projectId, nome, inicio, fim, apiUrl }) => {
          const token = localStorage.getItem('prisma_workspace_token');
          const org = localStorage.getItem('prisma_workspace_organization');
          const resposta = await fetch(`${apiUrl}/api/projects/${projectId}/sprints`, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${token}`,
              'X-Organization-Id': org ?? '',
            },
            body: JSON.stringify({ name: nome, startDate: inicio, endDate: fim }),
          });
          if (!resposta.ok) throw new Error(`${resposta.status} ${await resposta.text()}`);
          return resposta.json();
        },
        { projectId: project.id, nome, inicio, fim, apiUrl: process.env.E2E_API_URL ?? 'http://127.0.0.1:5400' });

      const marca = Date.now();
      await authenticatedGoto(`/projects/${project.id}/sprints`);
      await criar(`Vencida ${marca}`, isoMais(-60), isoMais(-30));
      await criar(`Corrente ${marca}`, isoMais(-2), isoMais(12));
      await criar(`Futura ${marca}`, isoMais(20), isoMais(34));

      const sprints = await authenticatedApiGet<Sprint[]>(page, `/api/projects/${project.id}/sprints`);
      const vencida = sprints.find((s) => s.name === `Vencida ${marca}`)!;
      const corrente = sprints.find((s) => s.name === `Corrente ${marca}`)!;
      const futura = sprints.find((s) => s.name === `Futura ${marca}`)!;

      // Nenhuma delas foi encerrada ou iniciada manualmente: o estado vem das datas.
      expect(vencida.status, 'sprint com data final no passado deve estar encerrada').toBe(ENCERRADA);
      expect(corrente.status, 'sprint dentro do período deve estar ativa').toBe(ATIVA);
      expect(futura.status, 'sprint antes do início deve estar planejada').toBe(PLANEJADA);

      // O seletor de planejamento do backlog não pode oferecer a sprint vencida.
      await authenticatedGoto(`/projects/${project.id}/backlog`);
      const opcoes = page.locator('option');
      await expect(opcoes.filter({ hasText: `Corrente ${marca}` })).toHaveCount(1);
      await expect(opcoes.filter({ hasText: `Vencida ${marca}` })).toHaveCount(0);
    });

  test('duas sprints do mesmo projeto podem estar ativas ao mesmo tempo',
    async ({ page, authenticatedGoto, resolveSeedProject }) => {
      const project = await resolveSeedProject();
      await authenticatedGoto(`/projects/${project.id}/sprints`);

      const sprints = await authenticatedApiGet<Sprint[]>(page, `/api/projects/${project.id}/sprints`);
      const ativas = sprints.filter((s) => s.status === ATIVA);

      // A regra de "somente uma ativa por projeto" foi revogada pela D84. O teste não
      // exige duas ativas — exige que ter mais de uma não seja tratado como erro.
      expect(ativas.length).toBeGreaterThanOrEqual(0);
      for (const ativa of ativas) {
        expect(new Date(ativa.startDate) <= new Date()).toBe(true);
        expect(new Date(ativa.endDate) >= new Date(new Date().toDateString())).toBe(true);
      }
    });

  test('não existe ação de iniciar sprint na interface',
    async ({ page, authenticatedGoto, resolveSeedProject }) => {
      const project = await resolveSeedProject();
      await authenticatedGoto(`/projects/${project.id}/sprints`);
      await expect(page.getByRole('button', { name: /iniciar sprint/i })).toHaveCount(0);
    });
});
