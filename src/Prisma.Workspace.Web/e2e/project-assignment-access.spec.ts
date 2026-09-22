import { randomUUID } from 'node:crypto';
import type { Page } from '@playwright/test';
import { test, expect } from './fixtures/test';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';

async function adminApi(page: Page, path: string, method = 'GET', body?: unknown) {
  return page.evaluate(async ({ base, path, method, body }) => {
    const response = await fetch(`${base}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${localStorage.getItem('prisma_workspace_token')}`,
        'X-Organization-Id': localStorage.getItem('prisma_workspace_organization') ?? '',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const text = await response.text();
    return { status: response.status, body: text ? JSON.parse(text) : null };
  }, { base: apiUrl, path, method, body });
}

test('atribuir exige acesso prévio, não concede acesso e preserva atribuição após revogação', async ({
  page, request, resolveSeedProject, authenticatedGoto,
}) => {
  const project = await resolveSeedProject();
  const email = `assignment-${randomUUID()}@example.test`;
  const password = process.env.E2E_TEST_USER_PASSWORD!;
  const invitation = await adminApi(page, '/api/organizations/current/invitations', 'POST', {
    email, role: 7, expiresInDays: 1,
  });
  expect(invitation.status).toBe(200);
  const signup = await request.post(`${apiUrl}/api/auth/invitation/complete`, { data: {
    token: invitation.body.token, password, confirmPassword: password,
    createAccount: true, fullName: 'Pessoa QA sem acesso ao projeto',
  } });
  expect(signup.status()).toBe(200);
  const guestAuth = await signup.json();
  const guestHeaders = {
    Authorization: `Bearer ${guestAuth.accessToken}`,
    'X-Organization-Id': guestAuth.organizationId,
  };
  const users = await adminApi(page, '/api/Users/assignable');
  const person = users.body.find((user: { email: string }) => user.email === email);
  expect(person).toBeTruthy();

  const tasks = await adminApi(page, `/api/WorkItems/project/${project.id}`);
  const task = tasks.body[0];
  expect(task?.id).toBeTruthy();
  const target = `/api/WorkItems/${task.id}/assignees`;
  const membership = `/api/projects/${project.id}/members/${person.id}`;
  try {
    const denied = await adminApi(page, target, 'POST', { userId: person.id });
    expect(denied.status).toBe(400);
    expect(JSON.stringify(denied.body)).toContain('acesso ao projeto');
    let eligible = await adminApi(page, `/api/Users/assignable?projectId=${project.id}`);
    expect(eligible.body.some((user: { id: string }) => user.id === person.id)).toBe(false);
    const beforeAccess = await request.get(`${apiUrl}/api/WorkItems/${task.id}`, { headers: guestHeaders });
    expect([403, 404]).toContain(beforeAccess.status());

    expect((await adminApi(page, membership, 'PUT', { role: 2 })).status).toBe(204);
    eligible = await adminApi(page, `/api/Users/assignable?projectId=${project.id}`);
    expect(eligible.body.some((user: { id: string }) => user.id === person.id)).toBe(true);
    expect((await adminApi(page, target, 'POST', { userId: person.id })).status).toBe(204);
    const withAccess = await request.get(`${apiUrl}/api/WorkItems/${task.id}`, { headers: guestHeaders });
    expect(withAccess.status()).toBe(200);

    await authenticatedGoto(`/projects/${project.id}/backlog?item=${task.id}`);
    const dialog = page.locator('[role="dialog"][aria-describedby^="task-description-"]');
    await expect(dialog).toBeVisible();
    await dialog.getByTitle('Clique para adicionar ou remover responsáveis').click();
    await expect(dialog.locator(`[data-user-id="${person.id}"]`)).toBeVisible();

    expect((await adminApi(page, membership, 'DELETE')).status).toBe(204);
    const assigned = await adminApi(page, target);
    expect(assigned.body.some((user: { id: string }) => user.id === person.id)).toBe(true);
    eligible = await adminApi(page, `/api/Users/assignable?projectId=${project.id}`);
    expect(eligible.body.some((user: { id: string }) => user.id === person.id)).toBe(false);
    const myWork = await request.get(`${apiUrl}/api/me/work`, { headers: guestHeaders });
    expect(myWork.status()).toBe(200);
    expect((await myWork.json()).tasks.some((item: { id: string }) => item.id === task.id)).toBe(false);
    const afterRevocation = await request.get(`${apiUrl}/api/WorkItems/${task.id}`, { headers: guestHeaders });
    expect([403, 404]).toContain(afterRevocation.status());
  } finally {
    // Remove somente os vínculos criados por este teste, sem apagar a tarefa seed.
    await adminApi(page, `${target}/${person.id}`, 'DELETE');
    await adminApi(page, membership, 'DELETE');
  }
});
