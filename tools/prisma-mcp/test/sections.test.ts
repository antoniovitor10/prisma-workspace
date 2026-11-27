// CA-7 (unit): corte por `## `, seção "cabeçalho", as seis classificações e a montagem da resolução.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { assembleResolution, diffSections, sectionsNeedingChoice, splitSections, HEADER_SECTION } from '../src/sections.js';

const DOC = `---
id: HU-0001
titulo: "Exemplo"
## comentário no YAML não corta seção
---

# HU-0001 · Exemplo

## História

Como dev, quero.

## Critérios de aceite

- CA-1

\`\`\`md
## isto está num bloco de código e não corta
\`\`\`

## Notas

fim
`;

test('splitSections: cabeçalho (YAML e título #) e um corte por "## ", fora de bloco de código', () => {
  const s = splitSections(DOC);
  assert.deepEqual(s.map(x => x.id), [HEADER_SECTION, 'História', 'Critérios de aceite', 'Notas']);
  assert.match(s[0].texto, /^---\nid: HU-0001[\s\S]*# HU-0001 · Exemplo\n\n$/);
  assert.ok(s[2].texto.includes('## isto está num bloco de código'));
  assert.equal(s.map(x => x.texto).join(''), DOC, 'juntar as seções devolve o texto original');
});

test('splitSections: BOM, CRLF (vira LF), títulos repetidos e texto sem "## "', () => {
  const s = splitSections('﻿# T\r\n\r\n## A\r\nx\r\n## A\r\ny\r\n');
  assert.deepEqual(s.map(x => x.id), [HEADER_SECTION, 'A', 'A (2)']);
  assert.ok(s[0].texto.startsWith('﻿# T\n'));
  assert.equal(s[2].texto, '## A\ny\n');
  assert.deepEqual(splitSections('só texto\n').map(x => [x.id, x.texto]), [[HEADER_SECTION, 'só texto\n']]);
  assert.deepEqual(splitSections('').map(x => x.texto), ['']);
});

const doc = (secs: Record<string, string>) => `# Doc\n\n${Object.entries(secs).map(([t, b]) => `## ${t}\n${b}\n`).join('')}`;

test('diffSections: as seis classificações contra a base', () => {
  const base = doc({ Igual: 'a', Repo: 'b', Prisma: 'c', Dois: 'd', SomeNoRepo: 'e', SomeNoPrisma: 'f' });
  const repo = doc({ Igual: 'a', Repo: 'B!', Prisma: 'c', Dois: 'D-repo', SomeNoPrisma: 'f', NovaRepo: 'n' });
  const prisma = doc({ Igual: 'a', Repo: 'b', Prisma: 'C!', Dois: 'D-prisma', SomeNoRepo: 'e', NovaPrisma: 'p' });
  const d = Object.fromEntries(diffSections(base, repo, prisma).map(s => [s.secao, s]));
  assert.equal(d[HEADER_SECTION].classe, 'igual');
  assert.equal(d.Igual.classe, 'igual');
  assert.equal(d.Repo.classe, 'so_repo');
  assert.equal(d.Prisma.classe, 'so_prisma');
  assert.equal(d.Dois.classe, 'nos_dois');
  assert.deepEqual([d.NovaRepo.classe, d.NovaRepo.origem], ['nova', 'repo']);
  assert.deepEqual([d.NovaPrisma.classe, d.NovaPrisma.origem], ['nova', 'prisma']);
  assert.deepEqual([d.SomeNoRepo.classe, d.SomeNoRepo.origem], ['removida', 'repo']);
  assert.deepEqual([d.SomeNoPrisma.classe, d.SomeNoPrisma.origem], ['removida', 'prisma']);
  assert.equal(d.Dois.repo, '## Dois\nD-repo\n');
  assert.equal(d.Dois.prisma, '## Dois\nD-prisma\n');
  assert.equal(d.Dois.base, '## Dois\nd\n');
});

test('diffSections sem base (nunca sincronizado): diferente nos dois = "mudou nos dois"; de um lado só = nova', () => {
  const d = Object.fromEntries(diffSections(undefined, doc({ A: '1', B: 'r' }), doc({ A: '1', B: 'p', C: 'x' })).map(s => [s.secao, s]));
  assert.equal(d.A.classe, 'igual');
  assert.equal(d.B.classe, 'nos_dois');
  assert.deepEqual([d.C.classe, d.C.origem], ['nova', 'prisma']);
});

test('assembleResolution: repo, prisma e juntar montam o texto esperado, na ordem do documento', () => {
  const base = doc({ A: 'a', B: 'b', C: 'c' });
  const repo = doc({ A: 'a-repo', B: 'b', C: 'c-repo', R: 'nova no repo' });
  const prisma = doc({ A: 'a', B: 'b-prisma', C: 'c-prisma' });
  const diff = diffSections(base, repo, prisma);
  assert.deepEqual(sectionsNeedingChoice(diff), ['A', 'B', 'C', 'R']);
  const texto = assembleResolution(diff, { A: 'repo', B: 'prisma', C: { juntar: '## C\nc-repo e c-prisma' }, R: 'prisma' });
  assert.equal(texto, '# Doc\n\n## A\na-repo\n## B\nb-prisma\n## C\nc-repo e c-prisma\n');
  const tudoRepo = assembleResolution(diff, { A: 'repo', B: 'repo', C: 'repo', R: 'repo' });
  assert.equal(tudoRepo, repo);
  const tudoPrisma = assembleResolution(diff, { A: 'prisma', B: 'prisma', C: 'prisma', R: 'prisma' });
  assert.equal(tudoPrisma, prisma);
});

test('assembleResolution recusa escolha faltando ou seção desconhecida, sem montar nada', () => {
  const diff = diffSections(doc({ A: 'a' }), doc({ A: 'r' }), doc({ A: 'p' }));
  assert.throws(() => assembleResolution(diff, {}), /Falta escolher.*A/);
  assert.throws(() => assembleResolution(diff, { A: 'repo', Z: 'repo' }), /desconhecidas nas escolhas: Z/);
  assert.throws(() => assembleResolution(diff, { A: 'outro' as never }), /inválida/);
});
