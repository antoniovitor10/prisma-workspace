// Arquivos do método (T-0023): guarda de caminho, leitura byte a byte, fim de linha, BOM e gravação atômica.
// O conteúdo dos documentos nunca vai para stderr nem para log: as mensagens daqui só citam o caminho relativo.
import { createHash, randomBytes } from 'node:crypto';
import { lstat, mkdir, readFile, realpath, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';

export const STATE_FILE = '.prisma-sync.json';
const REPO_PREFIX = 'docs/trabalho/';
const BOM = '﻿';

export class PathGuardError extends Error {
  constructor(message: string) { super(message); this.name = 'PathGuardError'; }
}

/** Caminho já conferido: absoluto (na pasta real), relativo à pasta (com `/`) e como o Prisma guarda. */
export interface SafePath { abs: string; rel: string; filePath: string; }

/** Texto com fim de linha LF (o servidor guarda LF). BOM, se houver, fica. */
export function toLf(text: string): string { return text.replace(/\r\n/g, '\n'); }
/** SHA-256 hexadecimal minúsculo do texto em UTF-8 (o mesmo `contentHash` da API). */
export function hashText(text: string): string { return createHash('sha256').update(text, 'utf8').digest('hex'); }

async function isMissing(path: string): Promise<boolean> {
  try { await lstat(path); return false; }
  catch (error) {
    if ((error as NodeJS.ErrnoException).code === 'ENOENT') return true;
    throw error;
  }
}

/**
 * Resolve um caminho de documento dentro da pasta do método e recusa o resto, sem ler nem gravar:
 * `..` em qualquer segmento, caminho que sai da pasta (absoluto ou não), extensão que não é `.md`
 * e link simbólico (ou junção) em qualquer parte do caminho abaixo da pasta.
 * Aceita relativo à pasta, `docs/trabalho/...` (o `filePath` do Prisma) ou absoluto dentro da pasta.
 */
export async function resolveMetodoPath(root: string, input: string, options: { mustExist?: boolean; allowState?: boolean } = {}): Promise<SafePath> {
  const mustExist = options.mustExist ?? true;
  if (typeof input !== 'string' || !input.trim() || input.includes('\0')) throw new PathGuardError('Caminho vazio ou inválido.');
  const raw = input.trim();
  if (raw.split(/[\\/]+/).includes('..')) throw new PathGuardError('Caminho recusado: ".." não é aceito.');
  const realRoot = await realpath(resolve(root)).catch(() => { throw new PathGuardError('A pasta do método (PRISMA_METODO_DIR) não existe.'); });
  let candidate: string;
  if (isAbsolute(raw)) candidate = resolve(raw);
  else {
    const normalized = raw.replaceAll('\\', '/').replace(/^(\.\/)+/, '');
    candidate = resolve(realRoot, normalized.startsWith(REPO_PREFIX) ? normalized.slice(REPO_PREFIX.length) : normalized);
  }
  // Caminho absoluto pode citar a pasta configurada (não a real) quando ela própria é um link.
  let rel = relative(realRoot, candidate);
  if (isAbsolute(raw) && (rel.startsWith('..') || isAbsolute(rel))) {
    const configured = resolve(root);
    const alt = relative(configured, candidate);
    if (!alt.startsWith('..') && !isAbsolute(alt)) { rel = alt; candidate = resolve(realRoot, alt); }
  }
  if (!rel || rel.startsWith('..') || isAbsolute(rel)) throw new PathGuardError('Caminho recusado: fora da pasta do método (PRISMA_METODO_DIR).');
  const relPosix = rel.split(sep).join('/');
  const isState = options.allowState === true && relPosix === STATE_FILE;
  if (!isState && !relPosix.endsWith('.md')) throw new PathGuardError('Caminho recusado: só arquivos .md são lidos ou gravados.');
  // Nenhum componente abaixo da pasta pode ser link simbólico ou junção.
  let current = realRoot;
  const parts = rel.split(sep);
  for (let i = 0; i < parts.length; i++) {
    current = join(current, parts[i]);
    let info;
    try { info = await lstat(current); }
    catch (error) {
      if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error;
      if (mustExist) throw new PathGuardError(`Arquivo não encontrado: ${relPosix}.`);
      break;
    }
    if (info.isSymbolicLink()) throw new PathGuardError('Caminho recusado: link simbólico não é aceito.');
    if (i === parts.length - 1 && !info.isFile()) throw new PathGuardError(`Não é um arquivo: ${relPosix}.`);
    if (i < parts.length - 1 && !info.isDirectory()) throw new PathGuardError(`Caminho inválido: ${relPosix}.`);
  }
  return { abs: candidate, rel: relPosix, filePath: `${REPO_PREFIX}${relPosix}` };
}

/** Arquivo lido: bytes originais, texto LF (o que vai ao Prisma), fim de linha e BOM que ele usa. */
export interface MetodoFile { bytes: Buffer; lf: string; hash: string; eol: '\n' | '\r\n'; bom: boolean; }

export function describeText(bytes: Buffer, name: string): MetodoFile {
  const text = bytes.toString('utf8');
  if (!Buffer.from(text, 'utf8').equals(bytes)) throw new Error(`${name} não é UTF-8 válido; o MCP não sincroniza arquivos que perderiam bytes.`);
  const crlf = (text.match(/\r\n/g) ?? []).length;
  const lfOnly = (text.match(/\n/g) ?? []).length - crlf;
  const lf = toLf(text);
  return { bytes, lf, hash: hashText(lf), eol: crlf > lfOnly ? '\r\n' : '\n', bom: text.startsWith(BOM) };
}

export async function readMetodoFile(path: SafePath): Promise<MetodoFile> {
  return describeText(await readFile(path.abs), path.rel);
}

/**
 * Texto do Prisma (LF) como vai para o arquivo: mantém o fim de linha e o BOM que o arquivo já usava.
 * Arquivo novo: grava como veio (LF).
 */
export function toFileText(content: string, previous?: Pick<MetodoFile, 'eol' | 'bom'>): string {
  let text = toLf(content);
  if (previous) {
    if (previous.bom && !text.startsWith(BOM)) text = BOM + text;
    if (!previous.bom && text.startsWith(BOM)) text = text.slice(1);
    if (previous.eol === '\r\n') text = text.replace(/\n/g, '\r\n');
  }
  return text;
}

/** Gravação atômica: arquivo temporário na mesma pasta + renomear. Nunca segue link simbólico. */
export async function atomicWrite(abs: string, data: string | Buffer): Promise<void> {
  await mkdir(dirname(abs), { recursive: true });
  if (!(await isMissing(abs)) && (await lstat(abs)).isSymbolicLink()) throw new PathGuardError('Caminho recusado: link simbólico não é aceito.');
  const tmp = join(dirname(abs), `.${randomBytes(6).toString('hex')}.prisma-sync.tmp`);
  try {
    await writeFile(tmp, data, { flag: 'wx' });
    await rename(tmp, abs);
  } catch (error) {
    await rm(tmp, { force: true }).catch(() => {});
    throw error;
  }
}

/** Grava o texto do Prisma no arquivo; devolve o arquivo como ficou (hash LF incluído). */
export async function writeMetodoFile(path: SafePath, content: string, previous?: Pick<MetodoFile, 'eol' | 'bom'>): Promise<MetodoFile> {
  const text = toFileText(content, previous);
  const bytes = Buffer.from(text, 'utf8');
  await atomicWrite(path.abs, bytes);
  return describeText(bytes, path.rel);
}
