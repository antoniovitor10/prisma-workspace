/**
 * Separa o cabeçalho YAML (frontmatter) do corpo de um documento do método, sem reescrever nada.
 *
 * `head` é o trecho exato do início do arquivo até a linha `---` que fecha o cabeçalho (inclusive
 * a quebra de linha, e o BOM se houver). `head + body` devolve o texto original byte a byte, então
 * a tela edita só o corpo e grava `head + corpoEditado`: o cabeçalho nunca muda pelo editor (D4).
 */
export interface FrontmatterEntry {
  key: string;
  value: string;
}

export interface SplitDocument {
  head: string;
  body: string;
  entries: FrontmatterEntry[];
}

const OPEN = /^(﻿)?---[ \t]*\r?\n/;
const CLOSE = /(^|\r?\n)---[ \t]*(\r?\n|$)/;

export function splitFrontmatter(content: string): SplitDocument {
  const open = OPEN.exec(content);
  if (!open) return { head: '', body: content, entries: [] };
  const afterOpen = content.slice(open[0].length);
  const close = CLOSE.exec(afterOpen);
  if (!close) return { head: '', body: content, entries: [] };
  const yaml = afterOpen.slice(0, close.index);
  const headLength = open[0].length + close.index + close[0].length;
  return { head: content.slice(0, headLength), body: content.slice(headLength), entries: parseEntries(yaml) };
}

/** Leitura rasa do YAML só para exibir: chaves de primeiro nível; listas em bloco viram "a, b". */
function parseEntries(yaml: string): FrontmatterEntry[] {
  const entries: FrontmatterEntry[] = [];
  for (const line of yaml.split(/\r?\n/)) {
    if (!line.trim() || line.trimStart().startsWith('#')) continue;
    const item = /^\s+-\s*(.*)$/.exec(line);
    if (item && entries.length > 0) {
      const last = entries[entries.length - 1];
      const value = unquote(item[1]);
      last.value = last.value ? `${last.value}, ${value}` : value;
      continue;
    }
    const pair = /^([A-Za-z0-9_-]+)\s*:\s*(.*)$/.exec(line);
    if (!pair) continue;
    entries.push({ key: pair[1], value: unquote(pair[2]) });
  }
  return entries.map(entry => ({ ...entry, value: normalizeInlineList(entry.value) }));
}

function unquote(value: string): string {
  const trimmed = value.trim();
  if (trimmed.length >= 2 && ((trimmed.startsWith('"') && trimmed.endsWith('"')) || (trimmed.startsWith("'") && trimmed.endsWith("'")))) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

function normalizeInlineList(value: string): string {
  if (value === '[]' || value === 'null' || value === '~') return '';
  const list = /^\[(.*)\]$/.exec(value);
  if (!list) return value;
  return list[1].split(',').map(part => unquote(part)).filter(Boolean).join(', ');
}
