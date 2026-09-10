import { test, expect, authenticatedApiGet, type SeedProject } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

type PortalDto = {
  id: string;
  publicSlug: string;
  isEnabled: boolean;
  boardId: string;
  forms: Array<{ id: string; publicSlug: string; title: string; isDefault: boolean }>;
};

type ExternalRequest = {
  id: string;
  protocol: string;
  title: string;
  triageStatus: number;
  messages: Array<{ id: string; content: string; authorType: number }>;
};

async function apiJson<T>(
  page: Parameters<typeof authenticatedApiGet>[0],
  path: string,
  init?: { method?: string; body?: unknown },
): Promise<T> {
  return page.evaluate(async ({ baseUrl, requestPath, requestInit }) => {
    const token = localStorage.getItem('prisma_workspace_token');
    const organizationId = localStorage.getItem('prisma_workspace_organization');
    const response = await fetch(`${baseUrl}${requestPath}`, {
      method: requestInit?.method ?? 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Organization-Id': organizationId ?? '',
        'Content-Type': 'application/json',
      },
      body: requestInit?.body === undefined ? undefined : JSON.stringify(requestInit.body),
    });
    if (!response.ok) throw new Error(`${requestInit?.method ?? 'GET'} ${requestPath} → ${response.status} ${await response.text()}`);
    if (response.status === 204) return null as T;
    return response.json() as Promise<T>;
  }, { baseUrl: apiUrl, requestPath: path, requestInit: init });
}

async function ensurePortal(page: Parameters<typeof authenticatedApiGet>[0], project: SeedProject): Promise<PortalDto> {
  const boardId = project.defaultBoardId ?? project.boards[0]?.id;
  if (!boardId) throw new Error(`Projeto ${project.key} sem quadro.`);

  let portal = await apiJson<PortalDto | null>(page, `/api/projects/${project.id}/external-portal`);
  if (!portal?.id) {
    portal = await apiJson<PortalDto>(page, `/api/projects/${project.id}/external-portal`, {
      method: 'PUT',
      body: {
        boardId,
        publicSlug: 'demo',
        isEnabled: true,
        requiresAuthentication: false,
        accessModes: 1,
      },
    });
  } else if (!portal.isEnabled || portal.publicSlug !== 'demo') {
    portal = await apiJson<PortalDto>(page, `/api/projects/${project.id}/external-portal`, {
      method: 'PUT',
      body: {
        boardId: portal.boardId || boardId,
        publicSlug: 'demo',
        isEnabled: true,
        requiresAuthentication: false,
        accessModes: 1,
      },
    });
  }

  if (!portal.forms?.length) {
    await apiJson(page, `/api/projects/${project.id}/external-portal/forms`, {
      method: 'POST',
      body: {
        publicSlug: 'solicitacao',
        title: 'Solicitação E2E',
        description: 'Formulário de teste',
        confirmationMessage: 'Recebemos sua solicitação.',
        isEnabled: true,
        isDefault: true,
        defaultPriority: 1,
        maxFiles: 5,
        maxFileSizeBytes: 10_000_000,
        allowedExtensions: '.pdf,.png,.jpg',
        allowedMimeTypes: 'application/pdf,image/png,image/jpeg',
        minimumCompletionSeconds: 0,
        fields: [
          { key: 'requesterName', label: 'Nome', type: 1, kind: 5, isRequired: true, position: 0, maxLength: 200 },
          { key: 'requesterEmail', label: 'E-mail', type: 3, kind: 6, isRequired: true, position: 1, maxLength: 320 },
          { key: 'subject', label: 'Assunto', type: 1, kind: 1, isRequired: true, position: 2, maxLength: 500 },
          { key: 'description', label: 'Descrição', type: 2, kind: 2, isRequired: true, position: 3, maxLength: 10000 },
        ],
        assignmentRules: [],
      },
    });
    portal = await apiJson<PortalDto>(page, `/api/projects/${project.id}/external-portal`);
  }

  return portal!;
}

test.describe('portal externo e solicitações (pós-SLA)', () => {
  test('configurações do projeto não exibem SLA e o fluxo público→fila→resposta funciona', async ({
    page,
    authenticatedGoto,
    resolveSeedProject,
  }) => {
    test.setTimeout(120_000);
    await authenticatedGoto('/projects');
    await page.getByLabel('Selecionar organização').selectOption({ label: 'Prisma Demo' }).catch(() => undefined);
    await page.waitForTimeout(400);

    const projects = await authenticatedApiGet<SeedProject[]>(page, '/api/projects');
    const project = projects.find((item) => item.key === 'DEMO') ?? await resolveSeedProject();
    const detail = await authenticatedApiGet<SeedProject>(page, `/api/projects/${project.id}`);

    await authenticatedGoto(`/projects/${detail.id}/settings?secao=portal`);
    await expect(page.getByRole('heading', { name: /Portal Externo/i })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/\bSLA\b/i)).toHaveCount(0);
    await expect(page.getByText(/prazo de primeira resposta/i)).toHaveCount(0);
    await expect(page.getByText(/política de SLA/i)).toHaveCount(0);

    const portal = await ensurePortal(page, detail);
    expect(portal.isEnabled).toBeTruthy();
    expect(portal.publicSlug).toBe('demo');
    const formSlug = portal.forms.find((item) => item.isDefault)?.publicSlug
      ?? portal.forms[0]?.publicSlug
      ?? 'solicitacao';

    const subject = `E2E portal ${Date.now()}`;
    await page.goto(`/portal/${portal.publicSlug}?form=${encodeURIComponent(formSlug)}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/\bSLA\b/i)).toHaveCount(0);

    // Anti-spam do formulário exige startedAt no passado relativo a MinimumCompletionSeconds.
    await page.waitForTimeout(2_500);
    await page.getByLabel('Nome').fill('Solicitante E2E');
    await page.getByLabel('E-mail').fill('solicitante.e2e@example.invalid');
    await page.getByLabel('Assunto').fill(subject);
    await page.getByLabel(/Descri/i).fill('Descrição gerada pelo teste E2E do portal pós-SLA.');
    const submit = page.getByRole('button', { name: /Gerar protocolo/i });
    await expect(submit).toBeEnabled({ timeout: 10_000 });
    await submit.click();
    await expect(page.getByText('Solicitação registrada')).toBeVisible({ timeout: 30_000 });
    const protocol = (await page.locator('code').first().textContent())?.trim();
    const accessKey = (await page.locator('code').nth(1).textContent())?.trim();
    expect(protocol).toBeTruthy();
    expect(accessKey).toBeTruthy();

    await authenticatedGoto('/requests');
    await expect(page.getByRole('heading', { name: 'Solicitações' })).toBeVisible();
    await expect(page.getByText(/\bSLA\b/i)).toHaveCount(0);
    await page.getByLabel('Buscar solicitações').fill(protocol!);
    await expect(page.getByText(protocol!, { exact: true })).toBeVisible({ timeout: 15_000 });
    await page.getByRole('button', { name: new RegExp(protocol!) }).click();
    await expect(page.getByRole('heading', { name: subject })).toBeVisible();

    await page.getByRole('button', { name: 'Aceitar' }).click();
    await expect(page.getByText(/Aceita/i).first()).toBeVisible({ timeout: 10_000 });

    const replyText = `Resposta interna E2E ${Date.now()}`;
    await page.getByPlaceholder(/resposta ficará visível/i).fill(replyText);
    await page.getByRole('button', { name: /Enviar resposta/i }).click();
    await expect(page.getByText(replyText)).toBeVisible({ timeout: 10_000 });

    const queue = await authenticatedApiGet<ExternalRequest[]>(page, '/api/external-requests');
    const created = queue.find((item) => item.protocol === protocol);
    expect(created).toBeTruthy();
    expect(created!.messages.some((message) => message.content === replyText)).toBeTruthy();

    // Tracking público pode atrasar a projeção da mensagem sob carga da suíte.
    await expect
      .poll(
        async () => {
          await page.goto(
            `/portal/${portal.publicSlug}/acompanhar?protocol=${encodeURIComponent(protocol!)}&key=${encodeURIComponent(accessKey!)}`,
          );
          await expect(page.getByRole('heading', { name: subject })).toBeVisible({ timeout: 15_000 });
          return page.getByText(replyText).count();
        },
        { timeout: 30_000 },
      )
      .toBeGreaterThan(0);
  });
});
