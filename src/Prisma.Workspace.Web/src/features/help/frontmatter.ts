/**
 * Cabeçalho (frontmatter) dos artigos da ajuda: um subconjunto pequeno de YAML.
 *
 * Aceita `chave: valor` (com ou sem aspas) e listas com `- item` logo abaixo de `chave:`.
 * Qualquer outra forma é recusada com erro, para que um artigo malformado quebre o teste
 * em vez de sumir da central.
 */
export type FrontmatterValue = string | string[];

export interface ParsedDocument {
  data: Record<string, FrontmatterValue>;
  body: string;
}

function unquote(value: string): string {
  const trimmed = value.trim();
  if (trimmed.length >= 2 && ((trimmed.startsWith('"') && trimmed.endsWith('"')) || (trimmed.startsWith("'") && trimmed.endsWith("'")))) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

export function parseFrontmatter(source: string, origin = 'artigo'): ParsedDocument {
  const text = source.replace(/\r\n?/g, '\n').replace(/^﻿/, '');
  if (!text.startsWith('---\n')) throw new Error(`${origin}: falta o cabeçalho entre linhas ---`);
  const end = text.indexOf('\n---', 4);
  if (end < 0) throw new Error(`${origin}: cabeçalho sem a linha --- de fechamento`);
  const header = text.slice(4, end).split('\n');
  const body = text.slice(text.indexOf('\n', end + 1) + 1);
  const data: Record<string, FrontmatterValue> = {};
  let currentList: string[] | null = null;
  for (const line of header) {
    if (!line.trim() || line.trim().startsWith('#')) continue;
    const item = /^\s+-\s+(.*)$/.exec(line);
    if (item) {
      if (!currentList) throw new Error(`${origin}: item de lista fora de uma chave: "${line}"`);
      currentList.push(unquote(item[1]));
      continue;
    }
    const pair = /^([a-z_]+):\s*(.*)$/.exec(line);
    if (!pair) throw new Error(`${origin}: linha de cabeçalho inválida: "${line}"`);
    const [, key, value] = pair;
    if (value.trim()) { data[key] = unquote(value); currentList = null; }
    else { currentList = []; data[key] = currentList; }
  }
  return { data, body };
}
