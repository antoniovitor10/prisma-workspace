// Pasta temporária do método e serviço de documentos apontando para a API falsa.
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { PrismaApi } from '../../src/api.js';
import { readConfig } from '../../src/config.js';
import { MethodDocumentService, type SyncState } from '../../src/sync.js';
import { TOKEN } from './fake-method.js';
import type { FakePrisma } from './fake-prisma.js';

export const EPIC = (id = 'EPIC-0001') => `---\nid: ${id}\ntitulo: "Épico de exemplo"\nstatus: aberta\n---\n\n# ${id} · Épico de exemplo\n\n## Objetivo\n\nJuntar as HUs.\n`;
export const HU = (id: string, extra = '') => `---\nid: ${id}\ntitulo: "HU ${id}"\nstatus: planejada\n${extra}---\n\n# ${id} · HU\n\n## História\n\nComo dev, quero sincronizar.\n\n## Critérios de aceite\n\n- **CA-1:** ok.\n`;
export const PRD = (hu: string) => `---\nhu: ${hu}\nstatus: aprovado\n---\n\n# PRD · ${hu}\n\n## Abordagem\n\nMCP compara e grava.\n`;
export const TASK = (id: string, hu: string) => `---\nid: ${id}\ntitulo: "Task ${id}"\nhu: ${hu}\ndepende_de: []\ncobre: [CA-1]\nrisco: normal\n---\n\n# ${id} · Task\n\n## Objetivo\n\nFazer.\n`;

export interface MetodoDir {
  dir: string;
  service: MethodDocumentService;
  write(rel: string, content: string | Buffer): Promise<void>;
  read(rel: string): Promise<Buffer>;
  text(rel: string): Promise<string>;
  state(): Promise<SyncState | undefined>;
  /** Todos os arquivos da pasta com os bytes, para provar que nada mudou. */
  snapshot(): Promise<Map<string, string>>;
  cleanup(): Promise<void>;
}

export async function metodoDir(fake: FakePrisma, files: Record<string, string | Buffer> = {}): Promise<MetodoDir> {
  const dir = await mkdtemp(join(tmpdir(), 'prisma-sync-'));
  const write = async (rel: string, content: string | Buffer) => {
    await mkdir(dirname(join(dir, rel)), { recursive: true });
    await writeFile(join(dir, rel), content);
  };
  for (const [rel, content] of Object.entries(files)) await write(rel, content);
  const api = new PrismaApi(readConfig({ PRISMA_URL: fake.url, PRISMA_PROJECT: 'metodo-teste', PRISMA_TOKEN: TOKEN }));
  const walk = async (base: string, out: Map<string, string>) => {
    for (const e of await readdir(base, { withFileTypes: true })) {
      const p = join(base, e.name);
      if (e.isSymbolicLink()) out.set(p.slice(dir.length + 1).replaceAll('\\', '/'), 'link');
      else if (e.isDirectory()) await walk(p, out);
      else out.set(p.slice(dir.length + 1).replaceAll('\\', '/'), (await readFile(p)).toString('base64'));
    }
    return out;
  };
  return {
    dir, service: new MethodDocumentService(api, dir), write,
    read: rel => readFile(join(dir, rel)),
    text: rel => readFile(join(dir, rel), 'utf8'),
    async state() {
      try { return JSON.parse(await readFile(join(dir, '.prisma-sync.json'), 'utf8')) as SyncState; }
      catch { return undefined; }
    },
    snapshot: () => walk(dir, new Map()),
    cleanup: () => rm(dir, { recursive: true, force: true }),
  };
}
