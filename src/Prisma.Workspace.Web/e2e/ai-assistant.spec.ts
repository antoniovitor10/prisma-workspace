import { test, expect } from '@playwright/test';
import { createServer, type Server } from 'node:http';

const apiUrl = process.env.E2E_API_URL ?? 'http://127.0.0.1:5400';
const orgId = '11111111-1111-4111-8111-111111111111';
let server: Server; let providerUrl = ''; let token = ''; let connectionId = ''; let itemId = ''; let modelRequests = 0;
const cliSessions = new Map<string, { owner: string; adapter: string; cancelled: boolean; authenticated: boolean }>();
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
    const body = raw ? JSON.parse(raw) : {};
    // Mesmo contrato HTTP da ponte Python real: corpo com tamanho conhecido.
    if ((req.url?.startsWith('/v1/cli/') || body.adapter) && (!req.headers['content-length'] || req.headers['transfer-encoding'])) {
      res.writeHead(502, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ error: { message: 'Ponte exige Content-Length.' } })); return;
    }
    if (req.url === '/v1/models') { res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ data: [{ id: 'fake-e2e', name: 'Modelo E2E' }] })); return; }
    if (req.url?.startsWith('/v1/cli/')) {
      const [, , , adapter, operation] = req.url.split('/');
      const id = 'a'.repeat(32); const state = cliSessions.get(id);
      let result: unknown;
      if (operation === 'status') { const authenticated = state?.adapter === adapter && state.authenticated; result = { available: true, authenticated: authenticated ?? false, state: authenticated ? 'authenticated' : 'authenticationRequired', message: 'Entre na conta.' }; }
      else if (operation === 'models') result = state?.authenticated && state.adapter === adapter ? { models: [{ id: adapter === 'claude' ? 'sonnet' : adapter === 'gemini' ? 'auto' : 'codex-e2e', name: 'Modelo CLI E2E' }], state: 'ready', message: 'Catálogo CLI de teste.' } : { models: [], state: 'authenticationRequired', message: 'Entre na conta.' };
      else if (operation === 'login') { cliSessions.set(id, { owner: body.owner, adapter, cancelled: false, authenticated: false }); result = { sessionId: id, state: 'waiting', available: true, authenticated: false, url: adapter === 'codex' ? 'https://auth.openai.com/codex/device' : adapter === 'gemini' ? 'https://accounts.google.com/o/oauth2/v2/auth' : 'https://claude.com/oauth/authorize', deviceCode: adapter === 'codex' ? 'TEST-CODE' : null, requiresCode: adapter !== 'codex', message: 'Autorize no provedor.' }; }
      else {
        if (!state || state.owner !== body.owner || state.adapter !== adapter) { res.writeHead(404); res.end('{}'); return; }
        if (operation === 'cancel') state.cancelled = true;
        if (operation === 'complete') state.authenticated = true;
        result = { sessionId: id, state: state.cancelled ? 'cancelled' : state.authenticated ? 'authenticated' : 'waiting', available: true, authenticated: state.authenticated, requiresCode: !state.authenticated && adapter !== 'codex', url: state.cancelled || state.authenticated ? null : adapter === 'codex' ? 'https://auth.openai.com/codex/device' : adapter === 'gemini' ? 'https://accounts.google.com/o/oauth2/v2/auth' : 'https://claude.com/oauth/authorize', deviceCode: !state.cancelled && adapter === 'codex' ? 'TEST-CODE' : null, message: state.authenticated ? 'Login confirmado.' : 'Autorize no provedor.' };
      }
      res.writeHead(200, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(result)); return;
    }
    modelRequests++;
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
  await new Promise<void>(resolve => server.listen(18747, '0.0.0.0', resolve));
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
  await page.getByRole('button', { name: 'Adicionar conexão' }).click();
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA' });
  await expect(form).toBeVisible();
  const name = `IA UI ${testInfo.project.name}`;
  await form.getByLabel('Provedor', { exact: true }).selectOption('Custom');
  await form.getByLabel('URL base').fill(providerUrl);
  await form.getByLabel('Chave de API', { exact: true }).fill('e2e-ui-fake-secret-WXYZ');
  await form.getByLabel('Chave de API', { exact: true }).blur();
  await expect(form.getByRole('option', { name: 'Modelo E2E — fake-e2e' })).toBeAttached();
  await form.getByLabel('Modelo', { exact: true }).focus();
  await expect(form.getByLabel('Modelo', { exact: true })).toBeEnabled();
  await form.getByLabel('Modelo', { exact: true }).selectOption('fake-e2e');
  await form.getByText('Opções avançadas', { exact: true }).click();
  await form.getByLabel('Nome da conexão').fill(name);
  await expect(form.getByRole('button', { name: 'Salvar conexão', exact: true })).toBeEnabled();
  expect(await form.evaluate(element => (element as HTMLFormElement).checkValidity())).toBe(true);
  const saving = page.waitForResponse(response => response.url().endsWith('/api/admin/ai/connections') && response.request().method() === 'POST');
  await form.getByRole('button', { name: 'Salvar conexão', exact: true }).click();
  expect((await saving).ok()).toBeTruthy();
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

test('modelos e métodos acompanham todos os provedores; OAuth inicia login explícito', async ({ page, request }) => {
  await page.route('**/api/admin/ai/models', async route => {
    const input = route.request().postDataJSON();
    await route.fulfill({ json: { models: [{ id: `${input.provider}-chat`, name: `${input.provider} chat` }], state: 'ready', message: 'Escolha um modelo.' } });
  });
  await page.goto('/settings'); await page.getByRole('button', { name: 'Adicionar conexão' }).click();
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA' });
  for (const provider of ['OpenRouter', 'OpenAI', 'Anthropic', 'Gemini', 'Custom']) {
    await form.getByLabel('Provedor', { exact: true }).selectOption(provider);
    if (provider === 'Custom') await form.getByLabel('URL base').fill(providerUrl);
    if (['OpenAI', 'Anthropic', 'Gemini'].includes(provider)) {
      await expect(form.getByLabel('Modelo', { exact: true })).toBeDisabled();
      await expect(form.getByText(/Cole a chave de API do/)).toBeVisible();
      await form.getByLabel('Chave de API', { exact: true }).fill('e2e-catalog-key');
    }
    await expect(form.getByRole('option', { name: `${provider} chat — ${provider}-chat` })).toBeAttached();
    await expect(form.getByLabel('Modelo', { exact: true })).toHaveValue('');
    await form.getByLabel('Modelo', { exact: true }).selectOption(`${provider}-chat`);
    if (provider === 'Custom') await expect(form.getByLabel('Como conectar')).toHaveCount(0);
  }
  await form.getByLabel('Provedor', { exact: true }).selectOption('OpenRouter');
  await form.getByRole('option', { name: 'OpenRouter chat — OpenRouter-chat' }).waitFor({ state: 'attached' });
  await form.getByLabel('Modelo', { exact: true }).selectOption('OpenRouter-chat');
  const create = await request.post(`${apiUrl}/api/admin/ai/connections`, { headers: headers(), data: { name: 'OAuth E2E', type: 'OAuth', provider: 'OpenRouter', model: 'OpenRouter-chat' } });
  expect(create.ok()).toBeTruthy(); const id = (await create.json()).id;
  const start = await request.post(`${apiUrl}/api/admin/ai/connections/${id}/oauth/start`, { headers: headers() }); expect(start.ok()).toBeTruthy();
  expect((await start.json()).url).toContain('https://openrouter.ai/auth?');
  expect((await request.post(`${apiUrl}/api/admin/ai/connections/${id}/oauth/complete`, { headers: headers(), data: { code: 'invalid', state: 'invalid' } })).status()).toBe(400);
  await request.delete(`${apiUrl}/api/admin/ai/connections/${id}`, { headers: headers() });
});

test('conexão OpenRouter CLI antiga orienta a correção e carrega modelo após colar chave', async ({ page, request }, testInfo) => {
  // O fixture representa a combinação histórica recusada pelo backend atual.
  await page.route('**/api/admin/ai/connections', async route => {
    if (route.request().method() !== 'GET') { await route.continue(); return; }
    await route.fulfill({ json: [{ id: '11111111-1111-4111-8111-111111111112', name: 'OpenRouter antiga', provider: 'OpenRouter', type: 'CliSubscription', model: 'opus', baseUrl: null, hasSecret: false, isActive: false, testSucceeded: false, secretSuffix: null, inputPrice: null, outputPrice: null, testMessage: null, latencyMs: null }] });
  });
  await page.goto('/settings'); await page.getByRole('button', { name: 'Editar OpenRouter antiga' }).click();
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA' });
  await expect(form.getByLabel('Como conectar')).toHaveValue('CliSubscription');
  await expect(form.getByLabel('Modelo', { exact: true })).toBeDisabled();
  await expect(form.getByText(/não oferece esta forma de conexão/)).toBeVisible();
  await form.screenshot({ path: `../../.local/ai-e2e/legacy-correction-${testInfo.project.name}.png` });
  await form.getByRole('button', { name: 'Usar login com OpenRouter' }).click();
  await expect(form.getByLabel('Como conectar')).toHaveValue('OAuth');
  await expect(form.getByText(/Escolha o modelo e entre com sua conta no OpenRouter/)).toBeVisible();
  // O endpoint falso exercita a API real; a consulta usa o texto atual sem blur.
  await form.getByLabel('Provedor', { exact: true }).selectOption('Custom');
  await form.getByLabel('URL base').fill(providerUrl);
  await form.getByLabel('Chave de API', { exact: true }).fill('e2e-current-key');
  // Edição guarda o id legado apenas na UI, enquanto uma chave nova evita lookup.
  await expect(form.getByRole('option', { name: 'Modelo E2E — fake-e2e' })).toBeAttached();
  await form.getByLabel('Modelo', { exact: true }).selectOption('fake-e2e');
  await form.getByLabel('Provedor', { exact: true }).selectOption('Custom');
  await expect(form.getByLabel('Modelo', { exact: true })).toHaveValue('fake-e2e');
  await expect(form.getByLabel('Chave de API', { exact: true })).toHaveValue('e2e-current-key');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await form.screenshot({ path: `../../.local/ai-e2e/catalog-recovery-${testInfo.project.name}.png` });
  await form.getByRole('button', { name: 'Cancelar', exact: true }).click();
  const catalog = await request.post(`${apiUrl}/api/admin/ai/models`, { headers: headers(), data: { provider: 'Gemini', type: 'ApiKey' } });
  expect((await catalog.json()).state).toBe('authenticationRequired');
});

test('CLI recupera falha de disponibilidade, exige aceite e autentica Claude e Gemini', async ({ page, request }, testInfo) => {
  cliSessions.clear();
  let statusAttempts = 0;
  await page.route('**/api/admin/ai/cli/OpenAI/status', async route => {
    statusAttempts++;
    if (statusAttempts === 1) { await route.fulfill({ status: 503, json: { message: 'Disponibilidade temporariamente indisponível.' } }); return; }
    await route.continue();
  });
  await page.goto('/settings'); await page.getByRole('button', { name: 'Adicionar conexão' }).click();
  const form = page.getByRole('form', { name: 'Cadastro de conexão IA' });
  await form.getByLabel('Provedor', { exact: true }).selectOption('OpenAI'); await form.getByLabel('Como conectar').selectOption('CliSubscription');
  await expect(form.getByText(/O provedor pode bloquear ou encerrar sua conta/)).toBeVisible();
  const enter = form.getByRole('button', { name: 'Entrar com ChatGPT' }); await expect(enter).toBeDisabled();
  await expect(form.getByText(/Não foi possível verificar a conexão/)).toBeVisible();
  await form.getByRole('button', { name: 'Tentar verificar novamente' }).click();
  await expect(form.getByText('Entre na conta.', { exact: true })).toBeVisible();
  expect(statusAttempts).toBe(2);
  await form.getByLabel('Entendo o risco de bloqueio ou perda da conta e quero continuar').check(); await expect(enter).toBeEnabled(); await enter.click();
  await expect(form.getByText('TEST-CODE', { exact: true })).toBeVisible();
  await expect(form.getByRole('link', { name: 'Abrir login oficial do provedor' })).toHaveAttribute('href', 'https://auth.openai.com/codex/device');
  await form.getByRole('button', { name: 'Cancelar login' }).click(); await expect(form.getByText('TEST-CODE', { exact: true })).toHaveCount(0);
  await form.getByLabel('Provedor', { exact: true }).selectOption('Anthropic'); await form.getByLabel('Como conectar').selectOption('CliSubscription');
  await form.getByLabel('Entendo o risco de bloqueio ou perda da conta e quero continuar').check(); const claude = form.getByRole('button', { name: 'Entrar com Claude' }); await expect(claude).toBeEnabled(); await claude.click();
  await form.getByLabel('Código retornado pelo Claude').fill('E2E-temporary-code'); await form.getByRole('button', { name: 'Confirmar código' }).click();
  await expect(form.getByText('Conta autenticada', { exact: true })).toBeVisible(); await form.getByLabel('Modelo', { exact: true }).selectOption('sonnet');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await form.screenshot({ path: `../../.local/ai-e2e/cli-settings-${testInfo.project.name}.png` });
  await form.getByLabel('Provedor', { exact: true }).selectOption('Gemini');
  await form.getByLabel('Como conectar').selectOption('CliSubscription');
  await expect(form.getByLabel('Chave de API', { exact: true })).toHaveCount(0);
  await expect(form.getByLabel('Modelo', { exact: true })).toBeDisabled();
  await expect(form.getByText(/Entre com Google acima/)).toBeVisible();
  const google = form.getByRole('button', { name: 'Entrar com Google' }); await expect(google).toBeDisabled();
  await form.getByLabel('Entendo o risco de bloqueio ou perda da conta e quero continuar').check();
  await expect(google).toBeEnabled(); await google.click();
  await expect(form.getByRole('link', { name: 'Abrir login oficial do provedor' })).toHaveAttribute('href', 'https://accounts.google.com/o/oauth2/v2/auth');
  await expect(form.getByText(/expira em até 5 minutos/)).toBeVisible();
  await form.getByLabel('Código retornado pelo Google').fill('E2E-temporary-google-code');
  await form.getByRole('button', { name: 'Confirmar código' }).click();
  await expect(form.getByText('Conta autenticada', { exact: true })).toBeVisible();
  await expect(form.getByLabel('Código retornado pelo Google')).toHaveCount(0);
  await form.getByLabel('Modelo', { exact: true }).selectOption('auto');
  await expect(form.getByRole('button', { name: 'Salvar conexão', exact: true })).toBeEnabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await form.screenshot({ path: `../../.local/ai-e2e/gemini-settings-${testInfo.project.name}.png` });
  expect((await request.post(`${apiUrl}/api/admin/ai/cli/OpenAI/login`, { headers: headers(), data: { acceptedRisk: false } })).status()).toBe(400);
});
