import { matchPath } from 'react-router-dom';
import { parseFrontmatter } from './frontmatter';
import { blocksText, headingAnchors, parseMarkdown, slugify, type Block } from './markdown';
import { helpSubjects } from './subjects';

/**
 * Índice da central de ajuda.
 *
 * Os artigos são arquivos Markdown versionados em `src/features/help/articles/<assunto>/<artigo>.md`
 * e entram no bundle do front pelo `import.meta.glob` (como texto). Não há banco nem chamada de
 * API: a central funciona numa instalação nova assim que a imagem sobe.
 */

export interface HelpPageLink {
  /** Padrão de rota do React Router, como `/projects/:projectId/backlog`. */
  pattern: string;
  /** Parâmetro de busca exigido (`secao=metodo`), quando a página muda de seção pela URL. */
  query?: { key: string; value: string };
  /** Seção do artigo que abre por essa página. */
  anchor?: string;
}

export interface HelpArticle {
  /** `<assunto>/<artigo>`, igual ao caminho do arquivo sem `.md`. */
  ref: string;
  subject: string;
  slug: string;
  title: string;
  summary: string;
  order: number;
  updatedAt: string;
  reviewBy: string;
  pages: HelpPageLink[];
  related: string[];
  blocks: Block[];
  anchors: string[];
  text: string;
  /** Pasta do arquivo, para resolver imagens relativas. */
  folder: string;
}

const isoDate = /^\d{4}-\d{2}-\d{2}$/;

function required(data: Record<string, string | string[]>, key: string, origin: string): string {
  const value = data[key];
  if (typeof value !== 'string' || !value.trim()) throw new Error(`${origin}: campo "${key}" obrigatório no cabeçalho`);
  return value.trim();
}

function list(data: Record<string, string | string[]>, key: string): string[] {
  const value = data[key];
  if (value === undefined) return [];
  return Array.isArray(value) ? value : [value];
}

export function parsePageLink(value: string): HelpPageLink {
  const [beforeHash, anchor] = value.split('#');
  const [pattern, search] = beforeHash.split('?');
  let query: HelpPageLink['query'];
  if (search) {
    const [key, queryValue] = search.split('=');
    query = { key, value: queryValue ?? '' };
  }
  return { pattern: pattern.trim(), query, anchor: anchor?.trim() || undefined };
}

export function parseArticle(path: string, source: string): HelpArticle {
  const match = /articles\/([a-z0-9-]+)\/([a-z0-9-]+)\.md$/.exec(path);
  if (!match) throw new Error(`${path}: o artigo deve ficar em articles/<assunto>/<artigo>.md, com nomes em minúsculas e hífens`);
  const [, subject, slug] = match;
  const { data, body } = parseFrontmatter(source, path);
  const updatedAt = required(data, 'atualizado_em', path);
  const reviewBy = required(data, 'revisar_ate', path);
  if (!isoDate.test(updatedAt) || !isoDate.test(reviewBy)) throw new Error(`${path}: datas no formato AAAA-MM-DD`);
  const blocks = parseMarkdown(body);
  return {
    ref: `${subject}/${slug}`,
    subject,
    slug,
    title: required(data, 'titulo', path),
    summary: required(data, 'resumo', path),
    order: Number(data.ordem ?? 100),
    updatedAt,
    reviewBy,
    pages: list(data, 'paginas').map(parsePageLink),
    related: list(data, 'relacionados'),
    blocks,
    anchors: headingAnchors(blocks),
    text: blocksText(blocks),
    folder: path.slice(0, path.lastIndexOf('/')),
  };
}

export function buildIndex(sources: Record<string, string>): HelpArticle[] {
  const order = new Map(helpSubjects.map((subject, index) => [subject.slug, index]));
  return Object.entries(sources)
    .map(([path, source]) => parseArticle(path, source))
    .sort((a, b) => (order.get(a.subject) ?? 99) - (order.get(b.subject) ?? 99) || a.order - b.order || a.title.localeCompare(b.title, 'pt-BR'));
}

const rawArticles = import.meta.glob<string>('./articles/**/*.md', { eager: true, query: '?raw', import: 'default' });
const rawImages = import.meta.glob<string>('./articles/**/*.{png,jpg,jpeg,webp,svg}', { eager: true, query: '?url', import: 'default' });

export const helpArticles: HelpArticle[] = buildIndex(rawArticles);

export function findArticle(ref: string): HelpArticle | undefined {
  return helpArticles.find((article) => article.ref === ref);
}

export interface HelpTarget { article: HelpArticle; anchor?: string }

/** Resolve `assunto/artigo#ancora`; devolve `undefined` se o artigo ou a âncora não existem. */
export function resolveHelpRef(value: string, articles: HelpArticle[] = helpArticles): HelpTarget | undefined {
  const [ref, anchor] = value.split('#');
  const article = articles.find((item) => item.ref === ref);
  if (!article) return undefined;
  if (anchor && !article.anchors.includes(anchor)) return undefined;
  return { article, anchor: anchor || undefined };
}

export function helpPath(ref: string, anchor?: string): string {
  return `/ajuda/${ref}${anchor ? `#${anchor}` : ''}`;
}

/** Imagem relativa ao artigo (`./imagens/quadro.png`) para a URL do bundle; só aceita arquivos empacotados. */
export function resolveHelpImage(article: Pick<HelpArticle, 'folder'>, src: string, images: Record<string, string> = rawImages): string | undefined {
  if (!src.startsWith('./') && !src.startsWith('../')) return undefined;
  const parts = `${article.folder}/${src}`.split('/');
  const resolved: string[] = [];
  for (const part of parts) {
    if (part === '.' || part === '') continue;
    if (part === '..') resolved.pop();
    else resolved.push(part);
  }
  return images[`./${resolved.join('/')}`];
}

function staticWeight(pattern: string): number {
  return pattern.split('/').filter((segment) => segment && !segment.startsWith(':') && segment !== '*').length;
}

/** Artigo (e seção) ligado à página atual, escolhendo a rota mais específica. */
export function findArticleForPage(pathname: string, search = '', articles: HelpArticle[] = helpArticles): HelpTarget | undefined {
  const params = new URLSearchParams(search);
  let best: { target: HelpTarget; score: number } | undefined;
  for (const article of articles) {
    for (const page of article.pages) {
      if (!matchPath({ path: page.pattern, end: true }, pathname)) continue;
      if (page.query && params.get(page.query.key) !== page.query.value) continue;
      const score = staticWeight(page.pattern) * 10 + (page.query ? 5 : 0);
      if (!best || score > best.score) best = { target: { article, anchor: page.anchor }, score };
    }
  }
  return best?.target;
}

/** Hoje no fuso de quem usa, como AAAA-MM-DD. */
export function today(date = new Date()): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

/** CA-9: artigo com data de revisão vencida pode estar desatualizado. */
export function isStale(article: Pick<HelpArticle, 'reviewBy'>, now = today()): boolean {
  return now > article.reviewBy;
}

export function formatHelpDate(iso: string): string {
  const [year, month, day] = iso.split('-');
  return `${day}/${month}/${year}`;
}

export interface HelpSearchResult { article: HelpArticle; anchor?: string; score: number }

const stopwords = new Set(['como', 'faco', 'faz', 'fazer', 'para', 'pra', 'de', 'do', 'da', 'dos', 'das', 'os', 'as', 'um', 'uma',
  'eu', 'que', 'em', 'no', 'na', 'nos', 'nas', 'onde', 'fica', 'posso', 'preciso', 'ajuda', 'qual', 'quais', 'por', 'se', 'meu',
  'minha', 'com', 'ao', 'aos', 'sobre', 'quero', 'consigo', 'tem', 'ter', 'esta', 'isso', 'isto', 'ou', 'mais']);

/**
 * Busca simples por palavras. Na tela todas as palavras precisam aparecer no artigo; para o
 * assistente (`matchAll: false`) basta uma, e o placar ordena. Título e seções pesam mais.
 */
export function searchHelp(query: string, articles: HelpArticle[] = helpArticles, { matchAll = true } = {}): HelpSearchResult[] {
  const terms = slugify(query).split('-').filter((term) => term.length >= 2 && !stopwords.has(term));
  if (terms.length === 0) return [];
  const results: HelpSearchResult[] = [];
  for (const article of articles) {
    const title = slugify(article.title);
    const summary = slugify(article.summary);
    const body = slugify(article.text);
    const found = (term: string) => title.includes(term) || summary.includes(term) || body.includes(term);
    if (matchAll ? !terms.every(found) : !terms.some(found)) continue;
    let score = 0;
    for (const term of terms) {
      if (title.includes(term)) score += 5;
      if (summary.includes(term)) score += 3;
      if (body.includes(term)) score += 1;
    }
    // A seção cujo título casa com mais palavras vira a âncora do resultado.
    let anchor: string | undefined;
    let best = 0;
    for (const block of article.blocks) {
      if (block.kind !== 'heading' || block.level < 2) continue;
      const text = slugify(block.text);
      const hits = terms.filter((term) => text.includes(term)).length;
      if (hits > best) { best = hits; anchor = block.id; }
    }
    results.push({ article, anchor, score });
  }
  return results.sort((a, b) => b.score - a.score);
}
