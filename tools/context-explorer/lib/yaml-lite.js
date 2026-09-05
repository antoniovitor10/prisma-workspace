'use strict';

// Mini parser YAML (subset) - zero dependencias.
//
// Suporta apenas o subset realmente usado nos arquivos deste repositorio
// (context/index.yaml, workflows/feature.yaml, profiles/runrun-loop.yaml,
// agents/*.yaml):
//   - Indentacao de exatamente 2 espacos por nivel (tabs NAO suportadas).
//   - Mapas (`chave: valor` ou `chave:` seguido de bloco filho).
//   - Listas com `- ` (incluindo itens de lista que sao mapas inline,
//     ex: `- from: "a"` seguido de `  to: "b"` na mesma indentacao do item).
//   - Arrays inline simples: `["x", "y"]` (sem arrays/objetos aninhados
//     dentro do array inline).
//   - Escalares com aspas simples ou duplas, booleanos (`true`/`false`),
//     `null`/`~`, inteiros e floats simples, e strings sem aspas.
//   - Comentarios `#` iniciados no começo da linha ou precedidos de espaco,
//     fora de strings entre aspas.
//
// NAO suporta (limitacoes conhecidas): tabs, anchors/aliases (&/*), tags
// (!!type), block scalars (`|` ou `>`), multi-document (`---`), chaves
// nao-string, arrays/objetos aninhados dentro de arrays inline, ou
// indentacao irregular/variavel. Este parser existe apenas para os
// arquivos de configuracao conhecidos deste repositorio, nao para YAML
// genérico.

function leadingSpaces(raw) {
  const match = raw.match(/^ */);
  return match ? match[0].length : 0;
}

function stripComment(raw) {
  let inQuote = null;
  for (let i = 0; i < raw.length; i++) {
    const ch = raw[i];
    if (inQuote) {
      if (ch === inQuote) inQuote = null;
      continue;
    }
    if (ch === '"' || ch === "'") {
      inQuote = ch;
      continue;
    }
    if (ch === '#' && (i === 0 || raw[i - 1] === ' ' || raw[i - 1] === '\t')) {
      return raw.slice(0, i).replace(/\s+$/, '');
    }
  }
  return raw;
}

function unquote(str) {
  if (str.length >= 2) {
    const first = str[0];
    const last = str[str.length - 1];
    if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
      return str.slice(1, -1);
    }
  }
  return str;
}

function parseScalar(str) {
  const trimmed = str.trim();
  if (trimmed === '') return null;
  if (trimmed.length >= 2) {
    const first = trimmed[0];
    const last = trimmed[trimmed.length - 1];
    if ((first === '"' && last === '"') || (first === "'" && last === "'")) {
      return trimmed.slice(1, -1);
    }
  }
  if (trimmed === 'true') return true;
  if (trimmed === 'false') return false;
  if (trimmed === 'null' || trimmed === '~') return null;
  if (/^-?\d+$/.test(trimmed)) return parseInt(trimmed, 10);
  if (/^-?\d+\.\d+$/.test(trimmed)) return parseFloat(trimmed);
  return trimmed;
}

function splitInlineArrayItems(inner) {
  const items = [];
  let current = '';
  let inQuote = null;
  for (let i = 0; i < inner.length; i++) {
    const ch = inner[i];
    if (inQuote) {
      current += ch;
      if (ch === inQuote) inQuote = null;
      continue;
    }
    if (ch === '"' || ch === "'") {
      inQuote = ch;
      current += ch;
      continue;
    }
    if (ch === ',') {
      items.push(current);
      current = '';
      continue;
    }
    current += ch;
  }
  if (current.trim() !== '') items.push(current);
  return items;
}

function parseInlineArray(str) {
  const trimmed = str.trim();
  const inner = trimmed.slice(1, -1).trim();
  if (inner === '') return [];
  return splitInlineArrayItems(inner).map((item) => parseScalar(item.trim()));
}

function parseScalarOrInline(valueStr) {
  const trimmed = valueStr.trim();
  if (trimmed.startsWith('[') && trimmed.endsWith(']')) {
    return parseInlineArray(trimmed);
  }
  return parseScalar(trimmed);
}

function findTopLevelColon(content) {
  let inQuote = null;
  for (let i = 0; i < content.length; i++) {
    const ch = content[i];
    if (inQuote) {
      if (ch === inQuote) inQuote = null;
      continue;
    }
    if (ch === '"' || ch === "'") {
      inQuote = ch;
      continue;
    }
    if (ch === ':' && (i === content.length - 1 || content[i + 1] === ' ')) {
      return i;
    }
  }
  return -1;
}

function splitKeyValue(content) {
  const idx = findTopLevelColon(content);
  if (idx === -1) {
    return { key: unquote(content.trim()), valueStr: '', hasColon: false };
  }
  const key = unquote(content.slice(0, idx).trim());
  const valueStr = content.slice(idx + 1).trim();
  return { key, valueStr, hasColon: true };
}

function looksLikeMapEntry(remainder) {
  return splitKeyValue(remainder).hasColon;
}

function isListItem(line) {
  return line.raw === '-' || line.raw.startsWith('- ');
}

function parseBlockAt(lines, pos, indent) {
  if (pos >= lines.length) return [null, pos];
  if (lines[pos].indent !== indent) return [null, pos];
  if (isListItem(lines[pos])) return parseList(lines, pos, indent);
  return parseMap(lines, pos, indent);
}

function consumeMapEntry(lines, pos, indent, overrideContent) {
  let content;
  let nextPos = pos;
  if (overrideContent !== undefined) {
    content = overrideContent;
  } else {
    content = lines[pos].raw;
    nextPos = pos + 1;
  }
  const { key, valueStr } = splitKeyValue(content);
  let value;
  if (valueStr === '') {
    if (nextPos < lines.length && lines[nextPos].indent > indent) {
      const childIndent = lines[nextPos].indent;
      const [child, newPos] = parseBlockAt(lines, nextPos, childIndent);
      value = child;
      nextPos = newPos;
    } else {
      value = null;
    }
  } else {
    value = parseScalarOrInline(valueStr);
  }
  return { pos: nextPos, key, value };
}

function parseMap(lines, pos, indent, initialContent) {
  const obj = {};
  let cursor = pos;
  if (initialContent !== undefined) {
    const r = consumeMapEntry(lines, cursor, indent, initialContent);
    obj[r.key] = r.value;
    cursor = r.pos;
  }
  while (cursor < lines.length && lines[cursor].indent === indent && !isListItem(lines[cursor])) {
    const r = consumeMapEntry(lines, cursor, indent);
    obj[r.key] = r.value;
    cursor = r.pos;
  }
  return [obj, cursor];
}

function parseList(lines, pos, indent) {
  const arr = [];
  let cursor = pos;
  while (cursor < lines.length && lines[cursor].indent === indent && isListItem(lines[cursor])) {
    const raw = lines[cursor].raw;
    const remainder = raw === '-' ? '' : raw.slice(2).trim();
    cursor++;
    if (remainder === '') {
      if (cursor < lines.length && lines[cursor].indent > indent) {
        const childIndent = lines[cursor].indent;
        const [val, newPos] = parseBlockAt(lines, cursor, childIndent);
        arr.push(val);
        cursor = newPos;
      } else {
        arr.push(null);
      }
    } else if (looksLikeMapEntry(remainder)) {
      const itemIndent = indent + 2;
      const [val, newPos] = parseMap(lines, cursor, itemIndent, remainder);
      arr.push(val);
      cursor = newPos;
    } else {
      arr.push(parseScalarOrInline(remainder));
    }
  }
  return [arr, cursor];
}

function parseYaml(text) {
  const rawLines = String(text).split(/\r\n|\n/);
  const lines = [];
  for (const raw of rawLines) {
    const noComment = stripComment(raw);
    if (noComment.trim() === '') continue;
    lines.push({ raw: noComment.trim(), indent: leadingSpaces(noComment) });
  }
  if (lines.length === 0) return {};
  const rootIndent = lines[0].indent;
  const [result] = parseBlockAt(lines, 0, rootIndent);
  return result;
}

module.exports = {
  parseYaml
};
