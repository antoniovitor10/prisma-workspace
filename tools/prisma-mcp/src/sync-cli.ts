#!/usr/bin/env node
// prisma-metodo-sync: a mesma sincronização do MCP, fora dele (T-0023; usada na T-0025).
// Imprime só chaves, ações e nomes de seção: nunca o texto dos documentos nem o token.
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { PrismaApi } from './api.js';
import { readMetodoDir } from './config.js';
import { MethodDocumentService, describeReport, snapshotMarkdown, type SyncReport } from './sync.js';

export const USAGE = `Uso: prisma-metodo-sync [escopo] [--dry-run] [--criar-no-prisma] [--criar-no-repo] [--json]
       prisma-metodo-sync --snapshot <arquivo.json|arquivo.md>

  escopo             sem escopo: tudo (épicos → HUs e Avulsas → PRDs → tasks). Ou uma chave
                     (EPIC-0001, HU-0001 = HU com PRD e tasks, HU-0001/prd, T-0001, AVULSAS) ou um arquivo.
  --dry-run          só mostra o plano; não grava nada no arquivo, no estado nem no Prisma.
  --criar-no-prisma  documento só no repositório é criado no Prisma (Histórias, PRDs, Avulsas, tasks).
  --criar-no-repo    documento só no Prisma é gravado no arquivo do filePath dele.
  --json             imprime o relatório em JSON (sem o texto das seções em conflito).
  --snapshot <arq>   só grava o retrato das tasks (id, status, pai, autorizada, comentários, PR); não sincroniza.

Ambiente: PRISMA_URL, PRISMA_TOKEN, PRISMA_PROJECT (como o MCP) e PRISMA_METODO_DIR (padrão ./docs/trabalho).`;

export interface CliOptions {
  escopo?: string; dryRun: boolean; criarNoPrisma: boolean; criarNoRepo: boolean; json: boolean; snapshot?: string;
}

export function parseArgs(args: string[]): CliOptions {
  const options: CliOptions = { dryRun: false, criarNoPrisma: false, criarNoRepo: false, json: false };
  const positional: string[] = [];
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--dry-run') options.dryRun = true;
    else if (arg === '--criar-no-prisma') options.criarNoPrisma = true;
    else if (arg === '--criar-no-repo') options.criarNoRepo = true;
    else if (arg === '--json') options.json = true;
    else if (arg === '--snapshot') {
      const file = args[++i];
      if (!file || file.startsWith('--')) throw new Error(`--snapshot exige o arquivo de saída.\n${USAGE}`);
      options.snapshot = file;
    } else if (arg === '--help' || arg === '-h') throw new Error(USAGE);
    else if (arg.startsWith('--')) throw new Error(`Opção desconhecida: ${arg}\n${USAGE}`);
    else positional.push(arg);
  }
  if (positional.length > 1) throw new Error(`Informe no máximo um escopo.\n${USAGE}`);
  options.escopo = positional[0];
  if (options.snapshot && (options.escopo || options.dryRun || options.criarNoPrisma || options.criarNoRepo)) {
    throw new Error(`--snapshot não se combina com sincronização; rode separado.\n${USAGE}`);
  }
  return options;
}

/** Relatório sem o texto das seções (só nome e classe), para imprimir em JSON. */
function withoutContent(report: SyncReport): SyncReport {
  return { ...report, itens: report.itens.map(item => item.diferenca
    ? { ...item, diferenca: item.diferenca.map(s => ({ secao: s.secao, classe: s.classe, ...(s.origem ? { origem: s.origem } : {}) })) }
    : item) };
}

export async function main(args = process.argv.slice(2), deps: { api?: PrismaApi; out?: (line: string) => void; dir?: string } = {}): Promise<number> {
  const options = parseArgs(args);
  const out = deps.out ?? ((line: string) => { process.stdout.write(`${line}\n`); });
  const api = deps.api ?? PrismaApi.fromEnv();
  const service = new MethodDocumentService(api, deps.dir ?? readMetodoDir());
  if (options.snapshot) {
    const snapshot = await service.snapshot();
    const target = resolve(options.snapshot);
    await writeFile(target, target.toLowerCase().endsWith('.md') ? snapshotMarkdown(snapshot) : `${JSON.stringify(snapshot, null, 2)}\n`, 'utf8');
    out(`Retrato de ${snapshot.tasks.length} tasks gravado em ${target}.`);
    return 0;
  }
  const report = await service.sincronizar({ escopo: options.escopo, ensaio: options.dryRun,
    criarNoPrisma: options.criarNoPrisma, criarNoRepo: options.criarNoRepo });
  out(options.json ? JSON.stringify(withoutContent(report), null, 2) : `${describeReport(report)}\nPasta: ${report.pasta}`);
  if (report.itens.some(i => i.erro)) return 1;
  if (report.itens.some(i => i.acao === 'conflito')) return 2;
  return 0;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().then(code => { process.exitCode = code; }).catch(error => {
    // Mensagens de erro do cliente já vêm sem token; nenhuma traz conteúdo de documento.
    process.stderr.write(`${error instanceof Error ? error.message : 'Falha ao sincronizar. Pare e avise.'}\n`);
    process.exitCode = 1;
  });
}
