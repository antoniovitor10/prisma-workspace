import { test as setup, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';

const currentDir = path.dirname(fileURLToPath(import.meta.url));
const authFile = path.join(currentDir, '..', '.auth', 'user.json');

/**
 * Setup de autenticacao para testes E2E.
 *
 * Faz login via API, armazena o storageState (localStorage + cookies)
 * e reutiliza em todos os testes subsequentes.
 *
 * Variaveis de ambiente obrigatorias:
 *   E2E_TEST_USER_EMAIL
 *   E2E_TEST_USER_PASSWORD
 *   E2E_API_URL (padrao: http://localhost:5400)
 */
setup('autenticar via API', async ({ request }) => {
  const email = process.env.E2E_TEST_USER_EMAIL;
  const password = process.env.E2E_TEST_USER_PASSWORD;
  const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5400';

  if (!email || !password) {
    throw new Error(
      'E2E_TEST_USER_EMAIL e E2E_TEST_USER_PASSWORD devem estar definidas. ' +
      'Copie .env.e2e para .env.e2e.local e preencha as credenciais.'
    );
  }

  const response = await request.post(`${apiUrl}/api/auth/login`, {
    data: { email, password },
  });

  expect(response.ok(), `Login falhou com status ${response.status()}`).toBeTruthy();

  const body = await response.json();
  const accessToken: string = body.accessToken;

  expect(accessToken, 'accessToken nao retornado pelo login').toBeTruthy();

  // Monta storageState com o token no localStorage e cookie de refresh
  const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:5450';

  const storageState = {
    cookies: [],
    origins: [
      {
        origin: baseURL,
        localStorage: [
          {
            name: 'prisma_workspace_token',
            value: accessToken,
          },
          {
            name: 'prisma_workspace_organization',
            value: '11111111-1111-4111-8111-111111111111',
          },
        ],
      },
    ],
  };

  // Garante que o diretorio .auth existe
  const authDir = path.dirname(authFile);
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  fs.writeFileSync(authFile, JSON.stringify(storageState, null, 2));
});
