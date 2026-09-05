import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // O Playwright reescreve estes artefatos durante a suíte. Sem ignorá-los,
    // o Vite recarrega a aplicação sob teste e transforma falhas isoladas em cascata.
    watch: {
      ignored: ['**/playwright-report/**', '**/test-results/**'],
    },
  },
})
