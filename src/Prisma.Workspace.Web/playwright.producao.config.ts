import { defineConfig, devices } from '@playwright/test';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { carregarCredenciais, enderecoDaProducao } from './e2e-producao/credenciais.mjs';

/**
 * Suíte Playwright de PRODUÇÃO (HU-0006). Separada da E2E (`playwright.config.ts`,
 * pasta `e2e/`), que continua proibida contra produção.
 *
 * Rode sempre por `npm run e2e:producao [-- T-XXXX]` (e2e-producao/README.md).
 * - Credenciais: `.env.producao.local` (ou `PROD_E2E_ENV_FILE`), lido só aqui e no login.
 * - `PROD_E2E_TASK=T-XXXX` inclui `e2e-producao/tasks/T-XXXX.spec.ts`; sem ela, só a base.
 * - Um worker, sem paralelismo e sem nova tentativa: não dispara o limitador da API
 *   e uma falha em produção não é mascarada.
 */
if (process.env.CI || process.env.GITHUB_ACTIONS) throw new Error('A suíte de produção não roda no CI.');
carregarCredenciais(dirname(fileURLToPath(import.meta.url)));
const endereco = enderecoDaProducao();
const tarefa = (process.env.PROD_E2E_TASK ?? '').trim().toUpperCase();
if (tarefa && !/^T-\d{4}$/.test(tarefa)) throw new Error('PROD_E2E_TASK precisa ter o formato T-XXXX.');

const specs = [/e2e-producao[\\/]base[\\/].*\.spec\.ts$/];
if (tarefa) specs.push(new RegExp(`e2e-producao[\\\\/]tasks[\\\\/]${tarefa}\\.spec\\.ts$`));
const sessao = 'e2e-producao/.auth/sessao.json';

export default defineConfig({
  testDir: './e2e-producao',
  outputDir: './e2e-producao/test-results',
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: './e2e-producao/playwright-report' }],
    ['./e2e-producao/relatorio-evidencias.ts'],
  ],
  use: {
    baseURL: endereco,
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      // Login pela API, sem navegador: nada de trace, vídeo ou print.
      name: 'login',
      testMatch: /fixtures[\\/]auth\.producao\.setup\.ts$/,
      use: { trace: 'off', video: 'off', screenshot: 'off' },
    },
    {
      // Trava no projeto de teste da org "pessoal" e arquiva sobras [e2e-prod].
      name: 'preparar',
      testMatch: /fixtures[\\/]preparar\.producao\.setup\.ts$/,
      use: { trace: 'off', video: 'off', screenshot: 'off' },
      dependencies: ['login'],
    },
    {
      name: 'desktop',
      testMatch: specs,
      use: { ...devices['Desktop Chrome'], baseURL: endereco, storageState: sessao },
      dependencies: ['preparar'],
    },
    {
      name: 'celular',
      testMatch: specs,
      use: { ...devices['Pixel 5'], baseURL: endereco, storageState: sessao },
      dependencies: ['preparar'],
    },
  ],
});
