// Ensaio da demo do método (fase 1). Grava no projeto informado: use um projeto de demo ou de teste.
// Uso, dentro de tools/prisma-mcp, depois de npm run build:
//   PRISMA_URL=... PRISMA_EMAIL=... PRISMA_PASSWORD=... node test/ensaio-demo.mjs <projeto> <pasta docs/trabalho>
// Importa duas vezes e, pelo MCP: listar_tasks, comentar na T-0001, agente recusado ao reabrir a T-0002,
// humano autoriza a T-0001, agente bloqueia a T-0001 e volta do próprio bloqueio.
import { execFileSync } from 'node:child_process';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
const [projeto, TRAB] = process.argv.slice(2);
if (!projeto || !TRAB) {
  console.error('Uso: node test/ensaio-demo.mjs <projeto> <pasta docs/trabalho>');
  process.exit(2);
}
const env = papel => ({ ...process.env, PRISMA_PROJECT: projeto, PRISMA_ROLE: papel });
const log = (...a) => console.log(...a);
for (const vez of [1, 2]) {
  log(`\n## import ${vez}`);
  log(execFileSync(process.execPath, ['dist/import.js', TRAB], { env: env('humano') }).toString().trim());
}
async function sessao(papel) {
  const c = new Client({ name: `ensaio-${papel}`, version: '0' });
  await c.connect(new StdioClientTransport({ command: process.execPath, args: ['dist/server.js'], env: env(papel) }));
  return async (name, args = {}) => {
    const r = await c.callTool({ name, arguments: args });
    const t = r.content.map(x => x.text).join('\n').split('\n\n')[0];
    log(`\n> [${papel}] ${name} ${JSON.stringify(args)}${r.isError ? '  => RECUSADO/ERRO' : ''}\n${t}`);
    return r;
  };
}
const ag = await sessao('agente');
const hu = await sessao('humano');
await ag('regras_do_projeto');
await ag('listar_tasks');
await ag('obter_task', { id: 'T-0001' });
await ag('comentar', { id: 'T-0001', texto: 'Ensaio da demo: relatório de execução registrado pelo agente via MCP.' });
await ag('mover_status', { id: 'T-0002', novo_status: 'em-desenvolvimento', motivo: 'agente tentando reabrir sem autorização' });
await hu('mover_status', { id: 'T-0001', novo_status: 'em-desenvolvimento', motivo: 'humano autoriza a execução' });
await ag('proximas_tasks');
await ag('mover_status', { id: 'T-0001', novo_status: 'bloqueada', motivo: 'aguardando a paleta original confirmada pelo desenvolvedor' });
await ag('obter_task', { id: 'T-0001' });
await ag('mover_status', { id: 'T-0001', novo_status: 'em-desenvolvimento', motivo: 'paleta confirmada, retomando' });
await ag('obter_task', { id: 'T-0001' });
process.exit(0);
