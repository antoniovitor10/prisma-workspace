/**
 * Leitor de Markdown da central de ajuda.
 *
 * Cobre só o que os artigos usam: títulos com âncora, parágrafos, listas, citações (avisos),
 * tabelas simples, blocos de código, negrito, itálico, código em linha, links e imagens.
 * O resultado é uma árvore de dados; quem desenha é `MarkdownView`, sempre com elementos
 * React. Nenhum HTML do texto chega ao DOM: `<script>` vira texto literal.
 */

export type Inline =
  | { kind: 'text'; text: string }
  | { kind: 'code'; text: string }
  | { kind: 'strong'; children: Inline[] }
  | { kind: 'em'; children: Inline[] }
  | { kind: 'link'; href: string; children: Inline[] }
  | { kind: 'image'; src: string; alt: string };

export type Block =
  | { kind: 'heading'; level: 1 | 2 | 3 | 4; id: string; text: string; children: Inline[] }
  | { kind: 'paragraph'; children: Inline[] }
  | { kind: 'list'; ordered: boolean; items: Inline[][] }
  | { kind: 'code'; lang: string; text: string }
  | { kind: 'quote'; children: Block[] }
  | { kind: 'table'; header: Inline[][]; rows: Inline[][][] }
  | { kind: 'rule' };

/** Âncora estável a partir de um texto em português: sem acento, minúsculas e hífens. */
export function slugify(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

/** Texto puro de uma sequência de elementos em linha (para busca e para o título). */
export function inlineText(nodes: Inline[]): string {
  return nodes.map((node) => {
    if (node.kind === 'text' || node.kind === 'code') return node.text;
    if (node.kind === 'image') return node.alt;
    return inlineText(node.children);
  }).join('');
}

function findClosing(source: string, marker: string, from: number): number {
  let index = from;
  while (index < source.length) {
    if (source[index] === '\\') { index += 2; continue; }
    if (source.startsWith(marker, index)) return index;
    index++;
  }
  return -1;
}

/** Acha o `]` que fecha o `[` aberto em `open`, respeitando colchetes aninhados. */
function findBracket(source: string, open: number): number {
  let depth = 0;
  for (let index = open; index < source.length; index++) {
    const char = source[index];
    if (char === '\\') { index++; continue; }
    if (char === '[') depth++;
    if (char === ']') { depth--; if (depth === 0) return index; }
  }
  return -1;
}

export function parseInline(source: string): Inline[] {
  const nodes: Inline[] = [];
  let buffer = '';
  const flush = () => { if (buffer) { nodes.push({ kind: 'text', text: buffer }); buffer = ''; } };
  let index = 0;
  while (index < source.length) {
    const char = source[index];
    if (char === '\\' && index + 1 < source.length) { buffer += source[index + 1]; index += 2; continue; }
    if (char === '`') {
      const end = source.indexOf('`', index + 1);
      if (end > index) { flush(); nodes.push({ kind: 'code', text: source.slice(index + 1, end) }); index = end + 1; continue; }
    }
    if (char === '!' && source[index + 1] === '[') {
      const close = findBracket(source, index + 1);
      if (close > 0 && source[close + 1] === '(') {
        const end = source.indexOf(')', close + 2);
        if (end > 0) {
          flush();
          nodes.push({ kind: 'image', alt: source.slice(index + 2, close), src: source.slice(close + 2, end).trim() });
          index = end + 1; continue;
        }
      }
    }
    if (char === '[') {
      const close = findBracket(source, index);
      if (close > 0 && source[close + 1] === '(') {
        const end = source.indexOf(')', close + 2);
        if (end > 0) {
          flush();
          nodes.push({ kind: 'link', href: source.slice(close + 2, end).trim(), children: parseInline(source.slice(index + 1, close)) });
          index = end + 1; continue;
        }
      }
    }
    if (source.startsWith('**', index)) {
      const end = findClosing(source, '**', index + 2);
      if (end > index + 2) { flush(); nodes.push({ kind: 'strong', children: parseInline(source.slice(index + 2, end)) }); index = end + 2; continue; }
    }
    if (char === '*' || (char === '_' && !/\w/.test(source[index - 1] ?? ''))) {
      const end = findClosing(source, char, index + 1);
      const valid = end > index + 1 && (char === '*' || !/\w/.test(source[end + 1] ?? ''));
      if (valid) { flush(); nodes.push({ kind: 'em', children: parseInline(source.slice(index + 1, end)) }); index = end + 1; continue; }
    }
    buffer += char;
    index++;
  }
  flush();
  return nodes;
}

const headingPattern = /^(#{1,4})\s+(.+?)\s*$/;
const explicitAnchor = /\s*\{#([a-z0-9-]+)\}$/;
const unorderedItem = /^\s*[-*]\s+(.*)$/;
const orderedItem = /^\s*\d+[.)]\s+(.*)$/;
const tableRow = /^\s*\|.*\|\s*$/;
const tableSeparator = /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)*\|?\s*$/;

function splitRow(line: string): string[] {
  return line.trim().replace(/^\|/, '').replace(/\|$/, '').split(/(?<!\\)\|/).map((cell) => cell.trim().replace(/\\\|/g, '|'));
}

function isBlockStart(line: string): boolean {
  return headingPattern.test(line) || unorderedItem.test(line) || orderedItem.test(line)
    || line.startsWith('```') || line.startsWith('>') || /^\s*(---|\*\*\*)\s*$/.test(line) || tableRow.test(line);
}

function parseLines(lines: string[], ids: Map<string, number>): Block[] {
  const blocks: Block[] = [];
  let index = 0;
  while (index < lines.length) {
    const line = lines[index];
    if (!line.trim()) { index++; continue; }

    if (line.startsWith('```')) {
      const lang = line.slice(3).trim();
      const body: string[] = [];
      index++;
      while (index < lines.length && !lines[index].startsWith('```')) { body.push(lines[index]); index++; }
      index++; // fecha o bloco (ou termina o arquivo)
      blocks.push({ kind: 'code', lang, text: body.join('\n') });
      continue;
    }

    const heading = headingPattern.exec(line);
    if (heading) {
      const level = heading[1].length as 1 | 2 | 3 | 4;
      let raw = heading[2];
      const explicit = explicitAnchor.exec(raw);
      if (explicit) raw = raw.slice(0, explicit.index);
      const children = parseInline(raw);
      const text = inlineText(children);
      const base = explicit?.[1] ?? (slugify(text) || 'secao');
      const seen = ids.get(base) ?? 0;
      ids.set(base, seen + 1);
      blocks.push({ kind: 'heading', level, id: seen === 0 ? base : `${base}-${seen + 1}`, text, children });
      index++;
      continue;
    }

    if (/^\s*(---|\*\*\*)\s*$/.test(line)) { blocks.push({ kind: 'rule' }); index++; continue; }

    if (line.startsWith('>')) {
      const body: string[] = [];
      while (index < lines.length && lines[index].startsWith('>')) { body.push(lines[index].replace(/^>\s?/, '')); index++; }
      blocks.push({ kind: 'quote', children: parseLines(body, ids) });
      continue;
    }

    if (tableRow.test(line) && index + 1 < lines.length && tableSeparator.test(lines[index + 1])) {
      const header = splitRow(line).map(parseInline);
      index += 2;
      const rows: Inline[][][] = [];
      while (index < lines.length && tableRow.test(lines[index])) { rows.push(splitRow(lines[index]).map(parseInline)); index++; }
      blocks.push({ kind: 'table', header, rows });
      continue;
    }

    const ordered = orderedItem.test(line);
    if (ordered || unorderedItem.test(line)) {
      const pattern = ordered ? orderedItem : unorderedItem;
      const items: string[] = [];
      while (index < lines.length) {
        const current = lines[index];
        const match = pattern.exec(current);
        if (match) { items.push(match[1]); index++; continue; }
        // Linha recuada logo abaixo de um item continua o mesmo item.
        if (current.trim() && /^\s{2,}/.test(current) && items.length) { items[items.length - 1] += ` ${current.trim()}`; index++; continue; }
        break;
      }
      blocks.push({ kind: 'list', ordered, items: items.map(parseInline) });
      continue;
    }

    const paragraph: string[] = [];
    while (index < lines.length && lines[index].trim() && (paragraph.length === 0 || !isBlockStart(lines[index]))) {
      paragraph.push(lines[index].trim());
      index++;
    }
    blocks.push({ kind: 'paragraph', children: parseInline(paragraph.join(' ')) });
  }
  return blocks;
}

export function parseMarkdown(source: string): Block[] {
  return parseLines(source.replace(/\r\n?/g, '\n').split('\n'), new Map());
}

/** Âncoras (ids de títulos) presentes no documento, na ordem em que aparecem. */
export function headingAnchors(blocks: Block[]): string[] {
  return blocks.flatMap((block) => {
    if (block.kind === 'heading') return [block.id];
    if (block.kind === 'quote') return headingAnchors(block.children);
    return [];
  });
}

/** Texto puro do documento, usado na busca e no contexto do assistente. */
export function blocksText(blocks: Block[]): string {
  return blocks.map((block) => {
    switch (block.kind) {
      case 'heading': return `${'#'.repeat(block.level)} ${block.text}`;
      case 'paragraph': return inlineText(block.children);
      case 'list': return block.items.map((item, i) => `${block.ordered ? `${i + 1}.` : '-'} ${inlineText(item)}`).join('\n');
      case 'code': return block.text;
      case 'quote': return blocksText(block.children);
      case 'table': return [block.header, ...block.rows].map((row) => row.map(inlineText).join(' | ')).join('\n');
      default: return '';
    }
  }).filter(Boolean).join('\n\n');
}

export type SafeHref = { kind: 'internal'; to: string } | { kind: 'anchor'; anchor: string } | { kind: 'external'; href: string };

/** Destinos aceitos nos links dos artigos; `javascript:`, `data:` e afins viram texto. */
export function safeHref(href: string): SafeHref | undefined {
  if (href.startsWith('#') && /^#[a-z0-9-]+$/.test(href)) return { kind: 'anchor', anchor: href.slice(1) };
  if (href.startsWith('/') && !href.startsWith('//')) return { kind: 'internal', to: href };
  if (/^https:\/\/[^\s]+$/i.test(href) || /^mailto:[^\s]+$/i.test(href)) return { kind: 'external', href };
  return undefined;
}
