#!/usr/bin/env node
// Comando da suíte de produção: `npm run e2e:producao [-- T-XXXX] [-- opções do Playwright]`.
//
// - Sem argumento: roda só a base (desktop e Pixel 5).
// - Com T-XXXX: roda a base mais e2e-producao/tasks/T-XXXX.spec.ts.
// - Sem credencial: para aqui, antes de abrir qualquer navegador, citando só o nome
//   da variável que falta (nunca o valor).
// - Nunca no CI nem no servidor: o repositório é público e a senha fica só no PC do desenvolvedor.
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, rmSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { carregarCredenciais, mensagemDeCredenciaisAusentes } from './credenciais.mjs';

const raizWeb = join(dirname(fileURLToPath(import.meta.url)), '..');
const argumentos = process.argv.slice(2);

if (process.env.CI || process.env.GITHUB_ACTIONS) {
  console.error('A suíte de produção não roda no CI: só no PC do desenvolvedor (HU-0006).');
  process.exit(1);
}

const tarefas = argumentos.filter(argumento => /^T-\d{4}$/i.test(argumento));
const desconhecidos = argumentos.filter(argumento => !argumento.startsWith('-') && !/^T-\d{4}$/i.test(argumento));
if (tarefas.length > 1 || desconhecidos.length > 0) {
  console.error('Uso: npm run e2e:producao [-- T-XXXX] [--list | --headed | outra opção do Playwright]');
  process.exit(1);
}

const tarefa = tarefas[0]?.toUpperCase() ?? '';
if (tarefa && !existsSync(join(raizWeb, 'e2e-producao', 'tasks', `${tarefa}.spec.ts`))) {
  console.error(`Spec da task não encontrado: e2e-producao/tasks/${tarefa}.spec.ts`);
  process.exit(1);
}

const opcoes = argumentos.filter(argumento => argumento.startsWith('-'));
if (!opcoes.includes('--list')) {
  const parada = mensagemDeCredenciaisAusentes(carregarCredenciais(raizWeb));
  if (parada) {
    console.error(parada);
    process.exit(2);
  }
}

const cli = createRequire(import.meta.url).resolve('@playwright/test/cli');
// O filho relê o arquivo; nada é passado pela linha de comando (e as credenciais saem do ambiente herdado).
const ambiente = { ...process.env, PROD_E2E_TASK: tarefa };
for (const nome of ['PROD_E2E_EMAIL', 'PROD_E2E_PASSWORD', 'PROD_E2E_BASE_URL']) delete ambiente[nome];
const execucao = spawnSync(process.execPath, [cli, 'test', '-c', 'playwright.producao.config.ts', ...opcoes], {
  cwd: raizWeb,
  env: ambiente,
  stdio: 'inherit',
});

// O trace guarda o token Bearer das requisições. Em rodada verde ninguém precisa dele: apaga.
if (execucao.status === 0) apagarTraces(join(raizWeb, 'e2e-producao', 'test-results'));
process.exit(execucao.status ?? 1);

function apagarTraces(pasta) {
  if (!existsSync(pasta)) return;
  for (const item of readdirSync(pasta, { withFileTypes: true })) {
    const caminho = join(pasta, item.name);
    if (item.isDirectory()) apagarTraces(caminho);
    else if (item.name === 'trace.zip') rmSync(caminho, { force: true });
  }
}
