// CA-9: ida e volta byte a byte (salvo o fim de linha) com a API falsa guardando o texto:
// acentos, tabela GFM, bloco de código com emoji, BOM, CRLF e um arquivo de 1 MB; `git status` limpo.
import { afterEach, beforeEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { startFakePrisma, type FakePrisma } from './helpers/fake-prisma.js';
import { installFakeMethod, type FakeMethod } from './helpers/fake-method.js';
import { metodoDir, type MetodoDir } from './helpers/metodo-dir.js';

let fake: FakePrisma;
let m: FakeMethod;
let env: MetodoDir;
let fresh: MetodoDir | undefined;

const ACENTOS = `---\nid: HU-0001\ntitulo: "Ação, coração e pão: çãõéíú ÀÉÎÕÜ"\nstatus: planejada\n---\n\n# HU-0001 · Ação\n\n## História\n\nComo usuária, quero ver “aspas curvas”, travessão — e reticências…\n`;
const TABELA = `---\nhu: HU-0001\nstatus: aprovado\n---\n\n# PRD\n\n## Tabela\n\n| Coluna | Valor | Nota |\n|:---|---:|:---:|\n| a \\| b | 1 | ✓ |\n| ação | 2,5 | — |\n\n- [ ] tarefa\n- [x] feita\n`;
const CODIGO = `---\nid: EPIC-0001\ntitulo: "Código"\nstatus: aberta\n---\n\n# EPIC-0001\n\n## Exemplo\n\n\`\`\`ts\nconst foguete = '🚀'; // emoji fora do BMP 👩‍💻\n## não é título\n\`\`\`\n\nTabs:\tok  \nespaços no fim   \n`;
const BOM = `﻿---\nid: HU-0002\ntitulo: "Com BOM"\nstatus: aberta\n---\n\n# HU-0002\n\n## História\n\ncom BOM\n`;
const CRLF = `---\r\nid: HU-0003\r\ntitulo: "Windows"\r\nstatus: aberta\r\n---\r\n\r\n# HU-0003\r\n\r\n## História\r\n\r\nfim de linha CRLF ação\r\n`;
function big(): string {
  const linha = 'Linha com acentuação, emoji 🎉 e tabela | a | b |\n';
  let body = '';
  while (Buffer.byteLength(body) < 1024 * 1024) body += `## Seção ${body.length}\n\n${linha.repeat(40)}\n`;
  return `---\nhu: HU-0002\nstatus: aprovado\n---\n\n# PRD grande\n\n${body}`;
}
const FILES: Record<string, string> = {
  'HU-0001-acentos/hu.md': ACENTOS,
  'HU-0001-acentos/prd.md': TABELA,
  'epicos/EPIC-0001-codigo.md': CODIGO,
  'HU-0002-bom/hu.md': BOM,
  'HU-0002-bom/prd.md': big(),
  'HU-0003-crlf/hu.md': CRLF,
};
const KEYS: Record<string, string> = {
  'HU-0001-acentos/hu.md': 'HU-0001', 'HU-0001-acentos/prd.md': 'HU-0001/prd', 'epicos/EPIC-0001-codigo.md': 'EPIC-0001',
  'HU-0002-bom/hu.md': 'HU-0002', 'HU-0002-bom/prd.md': 'HU-0002/prd', 'HU-0003-crlf/hu.md': 'HU-0003',
};
const git = (cwd: string, ...args: string[]) => execFileSync('git', ['-c', 'core.autocrlf=false', '-c', 'core.safecrlf=false', ...args], { cwd, encoding: 'utf8' });

beforeEach(async () => { fake = await startFakePrisma(); m = installFakeMethod(fake); });
afterEach(async () => { await fake.close(); await env?.cleanup(); await fresh?.cleanup(); fresh = undefined; });

test('CA-9 · enviados e puxados de novo sem edição saem iguais byte a byte; git status limpo', async () => {
  assert.ok(Buffer.byteLength(FILES['HU-0002-bom/prd.md']) >= 1024 * 1024, 'arquivo de 1 MB');
  env = await metodoDir(fake, FILES);
  git(env.dir, 'init', '-q');
  git(env.dir, 'config', 'user.email', 'teste@example.invalid');
  git(env.dir, 'config', 'user.name', 'teste');
  git(env.dir, 'add', '-A');
  git(env.dir, 'commit', '-q', '-m', 'docs');
  const ida = await env.service.sincronizar({ criarNoPrisma: true });
  assert.ok(ida.itens.every(i => !i.erro && i.passos.includes('criar')), JSON.stringify(ida.itens.map(i => [i.chave, i.passos, i.erro])));
  // O Prisma guarda LF e o BOM.
  assert.equal(m.docs.get('HU-0003')!.revisions[0].content, CRLF.replaceAll('\r\n', '\n'));
  assert.ok(m.docs.get('HU-0002')!.revisions[0].content.startsWith('﻿'));
  // Uma gravação na tela sem mudar o texto cria versão nova: a sincronização puxa e regrava cada arquivo.
  for (const key of Object.values(KEYS)) m.editInPrisma(key, m.docs.get(key)!.revisions.at(-1)!.content);
  const volta = await env.service.sincronizar();
  assert.deepEqual(volta.itens.map(i => [i.chave, i.acao, i.executado]).sort(),
    Object.values(KEYS).map(k => [k, 'puxar', true]).sort());
  for (const [rel, original] of Object.entries(FILES)) {
    assert.ok((await env.read(rel)).equals(Buffer.from(original, 'utf8')), `${rel} igual byte a byte (com o fim de linha e o BOM que usava)`);
  }
  assert.equal(git(env.dir, 'status', '--porcelain', '--', '.', ':(exclude).prisma-sync.json'), '', 'git status limpo nos .md');
  // Terceira rodada: nada a fazer.
  fake.requisicoes.length = 0;
  const terceira = await env.service.sincronizar();
  assert.ok(terceira.itens.every(i => i.acao === 'nada'));
  assert.deepEqual(fake.escritas(), []);
});

test('CA-9 · puxados para uma pasta vazia saem iguais salvo o fim de linha (LF), com o BOM', async () => {
  env = await metodoDir(fake, FILES);
  await env.service.sincronizar({ criarNoPrisma: true });
  fresh = await metodoDir(fake);
  const report = await fresh.service.sincronizar({ criarNoRepo: true });
  assert.ok(report.itens.every(i => i.passos.includes('gravar_arquivo') && i.executado), JSON.stringify(report.itens.map(i => [i.chave, i.acao, i.erro])));
  for (const [rel, original] of Object.entries(FILES)) {
    assert.ok((await fresh.read(rel)).equals(Buffer.from(original.replaceAll('\r\n', '\n'), 'utf8')), `${rel} igual salvo o fim de linha`);
  }
});
