import { test, expect } from '@playwright/test';
import { createServer, type Server } from 'node:http';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
const orgId = '11111111-1111-4111-8111-111111111111';
let server: Server; let providerUrl = ''; let token = ''; let connectionId = ''; let itemId = ''; let modelRequests = 0;
const headers = () => ({ Authorization: `Bearer ${token}`, 'X-Organization-Id': orgId });

test.beforeAll(async ({ request }) => {
  const auth = await request.post(`${apiUrl}/api/auth/login`, { data: { email: process.env.E2E_TEST_USER_EMAIL, password: process.env.E2E_TEST_USER_PASSWORD } });
  expect(auth.ok()).toBeTruthy(); token = (await auth.json()).accessToken;
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString());
  const userId = payload.sub ?? payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
  const boards = await request.get(`${apiUrl}/api/boards`, { headers: headers() });
  const board = (await boards.json())[0];
  const item = await request.post(`${apiUrl}/api/workitems`, { headers: headers(), data: { boardId: board.id, title: 'IA E2E atribuída', priority: 1, position: 10000, responsibleId: userId } });
  expect(item.ok()).toBeTruthy(); itemId = await item.json();
  server = createServer(async (req, res) => {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const body = JSON.parse(raw); modelRequests++;
    res.writeHead(200, { 'Content-Type': 'text/event-stream' });
    const send = (payload: unknown) => res.write(`data: ${JSON.stringify(payload)}\n\n`);
    const last = body.messages.at(-1);
    if (body.messages.some((m: { content: string }) => m.content.includes('Responda apenas OK'))) {
      send({ choices: [{ delta: { content: 'OK' } }] });
    } else if (last.role !== 'tool') {
      send({ choices: [{ delta: { tool_calls: [{ index: 0, id: 'read-work', function: { name: 'get_my_work', arguments: '{}' } }] } }] });
    } else {
      const rows = JSON.parse(last.content.slice(last.content.indexOf(': ') + 2));
      const row = rows[0]; const text = row ? `Encontrei ${row.title}. Veja [T:${row.number}].` : 'Não há tarefas atribuídas neste recorte.';
      for (const part of text.match(/.{1,8}/g) ?? []) send({ choices: [{ delta: { content: part } }] });
    }
    send({ choices: [], usage: { prompt_tokens: 10, completion_tokens: 5 } }); res.end('data: [DONE]\n\n');
  });
  await new Promise<void>(resolve => server.listen(0, '0.0.0.0', resolve));
  const address = server.address(); providerUrl = `http://${process.env.E2E_AI_PROVIDER_HOST ?? '127.0.0.1'}:${typeof address === 'object' && address ? address.port : 0}/v1`;
  const create = await request.post(`${apiUrl}/api/admin/ai/connections`, { headers: headers(), data: { name: 'E2E IA', type: 'OpenAiCompatible', provider: 'Custom', baseUrl: providerUrl, model: 'fake-e2e', secret: 'e2e-fake-secret-ABCD' } });
  expect(create.ok()).toBeTruthy(); const connection = await create.json(); connectionId = connection.id;
  expect(JSON.stringify(connection)).not.toContain('e2e-fake-secret'); expect(connection.secretSuffix).toBe('ABCD');
  const probe = await request.post(`${apiUrl}/api/admin/ai/connections/${connectionId}/test`, { headers: headers() });
  expect(probe.ok()).toBeTruthy(); expect((await probe.json()).testSucceeded).toBe(true);
  expect((await request.post(`${apiUrl}/api/admin/ai/connections/${connectionId}/activate`, { headers: headers() })).ok()).toBeTruthy();
  expect((await request.put(`${apiUrl}/api/organizations/current/ai`, { headers: headers(), data: { enabled: true, dailyTokenLimit: 0, userTokenLimit: 0 } })).ok()).toBeTruthy();
});
test.afterAll(async ({ request }) => {
  await request.put(`${apiUrl}/api/organizations/current/ai`, { headers: headers(), data: { enabled: false, dailyTokenLimit: null, userTokenLimit: null } });
  if (connectionId) await request.delete(`${apiUrl}/api/admin/ai/connections/${connectionId}`, { headers: headers() });
  if (itemId) await request.delete(`${apiUrl}/api/workitems/${itemId}`, { headers: headers() });
  if (server) await new Promise<void>(resolve => server.close(() => resolve()));
});

test('chat consulta dados, cita tarefa, fecha com Escape e respeita cota atingida', async ({ page, request }, testInfo) => {
  await page.goto('/home'); const launcher = page.getByRole('button', { name: 'Abrir assistente Prisma' });
  await expect(launcher).toBeVisible(); await launcher.click();
  const question = page.getByLabel('Pergunte ao workspace'); await expect(question).toBeFocused();
  await question.fill('Quais são minhas tarefas?'); await page.getByRole('button', { name: 'Enviar pergunta' }).click();
  await expect(page.getByRole('link', { name: /\[T:/ })).toBeVisible({ timeout: 20000 });
  await page.screenshot({ path: `../../.local/ai-e2e/ai-${testInfo.project.name}.png` });
  await page.keyboard.press('Escape'); await expect(page.getByRole('complementary', { name: 'Assistente Prisma' })).toBeHidden(); await expect(launcher).toBeFocused();
  const listing = await request.get(`${apiUrl}/api/ai/conversations`, { headers: headers() }); const conversation = (await listing.json())[0];
  const usage = await request.get(`${apiUrl}/api/organizations/current/ai/usage?groupBy=user`, { headers: headers() });
  const rows = await usage.json(); const used = rows[0].inputTokens + rows[0].outputTokens;
  await request.put(`${apiUrl}/api/organizations/current/ai`, { headers: headers(), data: { enabled: true, dailyTokenLimit: 0, userTokenLimit: used } });
  const callsBefore = modelRequests;
  const blocked = await request.post(`${apiUrl}/api/ai/conversations/${conversation.id}/messages`, { headers: headers(), data: { text: 'Resumo novamente' } });
  expect(blocked.status()).toBe(429); expect(await blocked.text()).toContain('Limite diário de usuário atingido'); expect(modelRequests).toBe(callsBefore);
  await request.delete(`${apiUrl}/api/ai/conversations/${conversation.id}`, { headers: headers() });
});

test('administrador de organização não recebe autoridade de instalação nem acesso a chats alheios', async ({ request }) => {
  const login = await request.post(`${apiUrl}/api/auth/login`, { data: { email: 'manager@prisma.example.invalid', password: process.env.E2E_TEST_USER_PASSWORD } });
  expect(login.ok()).toBeTruthy(); const managerHeaders = { ...headers(), Authorization: `Bearer ${(await login.json()).accessToken}` };
  expect((await request.get(`${apiUrl}/api/admin/ai/connections`, { headers: managerHeaders })).status()).toBe(403);
  await request.put(`${apiUrl}/api/organizations/current/ai`, { headers: headers(), data: { enabled: true, dailyTokenLimit: 0, userTokenLimit: 0 } });
  const c = await request.post(`${apiUrl}/api/ai/conversations`, { headers: headers() }); const id = (await c.json()).id;
  expect((await request.get(`${apiUrl}/api/ai/conversations/${id}`, { headers: managerHeaders })).status()).toBe(404);
  expect((await request.delete(`${apiUrl}/api/ai/conversations/${id}`, { headers: managerHeaders })).status()).toBe(404);
  await request.delete(`${apiUrl}/api/ai/conversations/${id}`, { headers: headers() });
});

test('tela administrativa cadastra, testa e ativa conexão sem devolver a credencial', async ({ page, request }, testInfo) => {
  await page.goto('/settings');
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA' });
  await expect(form).toBeVisible();
  const name = `IA UI ${testInfo.project.name}`;
  await form.getByLabel('Nome da conexão').fill(name);
  await form.getByLabel('Modelo', { exact: true }).fill('fake-e2e');
  await form.getByLabel('URL base').fill(providerUrl);
  await form.getByLabel('Chave de API', { exact: true }).fill('e2e-ui-fake-secret-WXYZ');
  await form.getByRole('button', { name: 'Cadastrar conexão' }).click();
  await expect(page.getByRole('button', { name: `Testar ${name}`, exact: true })).toBeVisible();
  await page.getByRole('button', { name: `Testar ${name}`, exact: true }).click();
  await expect(page.getByRole('button', { name: `Ativar ${name}`, exact: true })).toBeEnabled();
  await page.getByRole('button', { name: `Ativar ${name}`, exact: true }).click();
  await expect(page.getByText(`${name} — ativa`, { exact: true })).toBeVisible();
  await expect(page.getByText(/credencial salva \(final WXYZ\)/)).toBeVisible();
  await page.screenshot({ path: `../../.local/ai-e2e/settings-${testInfo.project.name}.png` });
  const connections = await request.get(`${apiUrl}/api/admin/ai/connections`, { headers: headers() });
  const saved = (await connections.json()).find((x: { name: string }) => x.name === name);
  await request.delete(`${apiUrl}/api/admin/ai/connections/${saved.id}`, { headers: headers() });
});
