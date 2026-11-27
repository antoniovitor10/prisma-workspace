// Diferença por seção (D6, CA-7): o texto é cortado nos títulos `## `; o que vem antes do primeiro
// (cabeçalho YAML e título `#`) é a seção "cabeçalho". Nada é juntado automaticamente: a montagem só
// usa a escolha do desenvolvedor para cada seção que não é igual.
import { toLf } from './files.js';

export const HEADER_SECTION = 'cabeçalho';

export interface Section { id: string; titulo: string; texto: string; }
export type SectionClass = 'igual' | 'so_repo' | 'so_prisma' | 'nos_dois' | 'nova' | 'removida';
export const SECTION_CLASS_LABEL: Record<SectionClass, string> = {
  igual: 'igual', so_repo: 'mudou só no repo', so_prisma: 'mudou só no Prisma',
  nos_dois: 'mudou nos dois', nova: 'nova', removida: 'removida',
};
export interface SectionDiff {
  secao: string;
  classe: SectionClass;
  /** Em `nova` e `removida`: de que lado a seção apareceu ou sumiu. */
  origem?: 'repo' | 'prisma' | 'ambos';
  base?: string; repo?: string; prisma?: string;
}
export type SectionChoice = 'repo' | 'prisma' | { juntar: string };

/** Corta o texto em seções; juntar `texto` de todas devolve o texto original exatamente. */
export function splitSections(input: string): Section[] {
  const text = toLf(input);
  const lines = text.split(/(?<=\n)/);
  const sections: Section[] = [{ id: HEADER_SECTION, titulo: HEADER_SECTION, texto: '' }];
  let i = 0;
  // Cabeçalho YAML: nada dentro dele corta seção (um comentário `## ` no YAML não é título).
  if (/^﻿?---\n$/.test(lines[0] ?? '')) {
    let j = 1;
    while (j < lines.length && !/^---(\n|$)/.test(lines[j])) j++;
    if (j < lines.length) { sections[0].texto = lines.slice(0, j + 1).join(''); i = j + 1; }
  }
  let fence: string | null = null;
  const seen = new Map<string, number>();
  for (; i < lines.length; i++) {
    const line = lines[i];
    const fenceMatch = /^ {0,3}(`{3,}|~{3,})/.exec(line);
    if (fenceMatch) {
      const marker = fenceMatch[1];
      if (fence === null) fence = marker;
      else if (marker[0] === fence[0] && marker.length >= fence.length && /^ {0,3}(`+|~+)\s*$/.test(line)) fence = null;
    } else if (fence === null && /^## /.test(line)) {
      const titulo = line.slice(3).replace(/\n$/, '').trim();
      const count = (seen.get(titulo) ?? 0) + 1;
      seen.set(titulo, count);
      sections.push({ id: count === 1 ? titulo : `${titulo} (${count})`, titulo, texto: line });
      continue;
    }
    sections[sections.length - 1].texto += line;
  }
  return sections;
}

/** Ordem da união: a do repo, com as seções que só existem no outro lado depois da vizinha anterior. */
function mergeOrder(primary: string[], ...others: string[][]): string[] {
  const order = [...primary];
  for (const other of others) {
    other.forEach((id, index) => {
      if (order.includes(id)) return;
      let at = 0;
      for (let k = index - 1; k >= 0; k--) {
        const pos = order.indexOf(other[k]);
        if (pos >= 0) { at = pos + 1; break; }
      }
      order.splice(at, 0, id);
    });
  }
  return order;
}

/**
 * Classifica cada seção comparando repo e Prisma com a base (a revisão da última sincronização).
 * Sem base (documento nunca sincronizado), o que difere nos dois lados sai como "mudou nos dois".
 */
export function diffSections(base: string | undefined, repo: string, prisma: string): SectionDiff[] {
  const b = base === undefined ? undefined : new Map(splitSections(base).map(s => [s.id, s.texto]));
  const rList = splitSections(repo); const pList = splitSections(prisma);
  const r = new Map(rList.map(s => [s.id, s.texto])); const p = new Map(pList.map(s => [s.id, s.texto]));
  const order = mergeOrder(rList.map(s => s.id), pList.map(s => s.id), b ? [...b.keys()] : []);
  return order.map((secao): SectionDiff => {
    const bt = b?.get(secao); const rt = r.get(secao); const pt = p.get(secao);
    const texts = { ...(bt !== undefined ? { base: bt } : {}), ...(rt !== undefined ? { repo: rt } : {}), ...(pt !== undefined ? { prisma: pt } : {}) };
    if (rt !== undefined && pt !== undefined && rt === pt) return { secao, classe: 'igual', ...texts };
    if (b === undefined) {
      if (rt !== undefined && pt !== undefined) return { secao, classe: 'nos_dois', ...texts };
      return { secao, classe: 'nova', origem: rt !== undefined ? 'repo' : 'prisma', ...texts };
    }
    if (bt === undefined) {
      return { secao, classe: 'nova', origem: rt !== undefined && pt !== undefined ? 'ambos' : rt !== undefined ? 'repo' : 'prisma', ...texts };
    }
    if (rt === undefined || pt === undefined) {
      return { secao, classe: 'removida', origem: rt === undefined && pt === undefined ? 'ambos' : rt === undefined ? 'repo' : 'prisma', ...texts };
    }
    if (rt !== bt && pt === bt) return { secao, classe: 'so_repo', ...texts };
    if (rt === bt && pt !== bt) return { secao, classe: 'so_prisma', ...texts };
    return { secao, classe: 'nos_dois', ...texts };
  });
}

/** Seções que pedem escolha do desenvolvedor (todas, menos as iguais e as removidas nos dois lados). */
export function sectionsNeedingChoice(diff: SectionDiff[]): string[] {
  return diff.filter(s => s.classe !== 'igual' && !(s.classe === 'removida' && s.origem === 'ambos')).map(s => s.secao);
}

/**
 * Monta o texto final (LF) a partir das escolhas: `repo` ou `prisma` usam o texto daquele lado
 * (seção ausente nele fica de fora); `juntar` usa o texto aprovado pelo desenvolvedor, inteiro
 * (inclusive a linha `## `). Falta de escolha ou seção desconhecida é erro, sem montar nada.
 */
export function assembleResolution(diff: SectionDiff[], choices: Record<string, SectionChoice>): string {
  const known = new Set(diff.map(s => s.secao));
  const unknown = Object.keys(choices).filter(k => !known.has(k));
  if (unknown.length) throw new Error(`Seções desconhecidas nas escolhas: ${unknown.join(', ')}.`);
  const missing = sectionsNeedingChoice(diff).filter(id => choices[id] === undefined);
  if (missing.length) throw new Error(`Falta escolher repo, prisma ou juntar para: ${missing.join(', ')}.`);
  const pieces: string[] = [];
  for (const s of diff) {
    const choice = choices[s.secao];
    let piece: string | undefined;
    if (choice === undefined) piece = s.classe === 'igual' ? (s.repo ?? s.prisma) : undefined;
    else if (choice === 'repo') piece = s.repo;
    else if (choice === 'prisma') piece = s.prisma;
    else if (typeof choice === 'object' && typeof choice.juntar === 'string') {
      piece = toLf(choice.juntar);
      // O texto juntado mantém a quebra de linha final que a seção tinha nos dois lados.
      if (piece && !piece.endsWith('\n') && (s.repo ?? s.prisma ?? '').endsWith('\n')) piece += '\n';
    }
    else throw new Error(`Escolha inválida para ${s.secao}: use repo, prisma ou { juntar }.`);
    if (piece !== undefined && piece !== '') pieces.push(piece);
  }
  return pieces.map((piece, i) => (i < pieces.length - 1 && !piece.endsWith('\n') ? `${piece}\n` : piece)).join('');
}
