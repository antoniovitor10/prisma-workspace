import react from '@vitejs/plugin-react';
import { configDefaults, defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    exclude: [...configDefaults.exclude, 'e2e/**'],
    css: true,
    // Renders com styled-components + jsdom passam de 5s em maquinas carregadas;
    // timeout folgado evita falso negativo (flakiness) local e no CI.
    testTimeout: 20_000,
    // Spies em `api` compartilhado entre arquivos falham com paralelismo.
    fileParallelism: false,
    maxWorkers: 1,
  },
});
