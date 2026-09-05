import { spawn, type ChildProcess } from 'node:child_process'
import net from 'node:net'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

const here = path.dirname(fileURLToPath(import.meta.url))
const apiScript = path.resolve(here, '..', 'server.js')
const API_PORT = 3847

// Sobe a API do Context Explorer junto com o dev server, se a porta estiver livre.
function apiServerPlugin(): Plugin {
  let child: ChildProcess | null = null

  return {
    name: 'context-explorer-api',
    configureServer() {
      const probe = net.createServer()
      probe.once('error', () => {
        // Porta ocupada: assume que a API ja esta rodando.
      })
      probe.once('listening', () => {
        probe.close(() => {
          child = spawn('node', [apiScript], { stdio: 'inherit' })
        })
      })
      probe.listen(API_PORT)

      process.on('exit', () => {
        child?.kill()
      })
    }
  }
}

export default defineConfig({
  plugins: [react(), apiServerPlugin()],
  server: {
    port: 5174,
    open: false,
    proxy: {
      '/api': {
        target: `http://localhost:${API_PORT}`,
        changeOrigin: true
      }
    }
  }
})
