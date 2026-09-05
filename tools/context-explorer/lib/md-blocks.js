'use strict';

// Utilitarios para extrair informacao de arquivos Markdown (zero dependencias).

function extractYamlBlocks(markdown) {
  const text = String(markdown);
  const fenceRegex = /```yaml\r?\n([\s\S]*?)```/g;
  const blocks = [];
  let match;
  while ((match = fenceRegex.exec(text)) !== null) {
    blocks.push(match[1].replace(/\r\n/g, '\n').replace(/\n$/, ''));
  }
  return blocks;
}

function parseSpecHeader(text) {
  const content = String(text);
  const result = { id: null, title: null, status: null };

  const titleMatch = content.match(/^#\s*(SPEC-[^:\n]+):\s*(.+?)\s*$/m);
  if (titleMatch) {
    result.id = titleMatch[1].trim();
    result.title = titleMatch[2].trim();
  }

  const statusMatch = content.match(/\*\*Status:\*\*\s*(.+?)\s*$/m);
  if (statusMatch) {
    result.status = statusMatch[1].trim();
  }

  return result;
}

module.exports = {
  extractYamlBlocks,
  parseSpecHeader
};
