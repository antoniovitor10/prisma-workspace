import { defineConfig } from '@playwright/test';
import config from './playwright.config';

// Opt-in: conserva projetos, prazos, retries e asserções da suíte nativa.
export default defineConfig(config, {
  reporter: [
    ['list'],
    ['./e2e/reporters/diagnostic.ts'],
  ],
});
