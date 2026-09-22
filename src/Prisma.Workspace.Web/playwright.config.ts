import { defineConfig, devices } from '@playwright/test';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';

const e2eEnvFile = resolve('.env.e2e.local');
if (existsSync(e2eEnvFile)) {
  // O arquivo local é a fonte explícita do ambiente E2E. Evita que valores
  // herdados de outro terminal façam o browser apontar para portas diferentes.
  for (const key of ['E2E_BASE_URL', 'E2E_API_URL', 'E2E_TEST_USER_EMAIL', 'E2E_TEST_USER_PASSWORD'])
    delete process.env[key];
  process.loadEnvFile(e2eEnvFile);
}

/**
 * Configuracao de testes E2E com Playwright.
 *
 * Variaveis de ambiente:
 *   E2E_BASE_URL  - URL do frontend (padrao: http://localhost:5450)
 *   E2E_API_URL   - URL da API   (padrao: http://localhost:5400)
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // Os cenários compartilham a mesma conta E2E; concorrência dispara o rate
  // limiter da própria API e gera falsos negativos 429.
  workers: 1,
  reporter: [
    ['html', { open: 'never' }],
    ['list'],
  ],

  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5450',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'setup',
      testMatch: /.*\.setup\.ts/,
    },
    {
      name: 'chromium-desktop',
      testIgnore: /.*\.setup\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        storageState: 'e2e/.auth/user.json',
      },
      dependencies: ['setup'],
    },
    {
      name: 'chromium-mobile',
      testIgnore: /.*\.setup\.ts/,
      use: {
        ...devices['Pixel 5'],
        storageState: 'e2e/.auth/user.json',
      },
      dependencies: ['setup'],
    },
  ],
});
