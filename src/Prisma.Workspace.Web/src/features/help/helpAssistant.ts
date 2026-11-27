import { findArticle, findArticleForPage, helpArticles, isStale, searchHelp, type HelpArticle } from './helpIndex';
import { inlineText, type Block } from './markdown';

/**
 * Ponte entre a central de ajuda e o assistente de IA (CA-4).
 *
 * Quando a pergunta é de uso do produto, a tela manda ao servidor o artigo da página atual e os
 * relacionados; o servidor anexa esse texto ao prompt e pede a resposta só a partir dele, com a
 * referência `[A:assunto/artigo#ancora]`, que vira o link "Abrir na ajuda".
 */

const helpIntent = /\b(ajuda|socorro|tutorial|passo a passo|como (?:fa[cç]o|faz|fazer|posso|eu|se|crio|criar|configur\w*|ligo|ligar|ativ\w*|paus\w*|convid\w*|emit\w*|gero|gerar|vincul\w*|conect\w*|mov\w*|uso|usar)|onde (?:fica|ficam|est[aá]|encontro|acho|vejo)|o que (?:[eé](?=[\s?.,!]|$)|significa|quer dizer)|por que|porque (?:o|a) )/i;

export function isHelpQuestion(text: string): boolean {
  return helpIntent.test(text);
}

export const helpArticleLimit = 3;
const maxArticleChars = 7000;

/** Texto do artigo para o modelo: cada título leva a própria âncora, para a citação apontar a seção. */
function articleForModel(article: HelpArticle): string {
  const lines: string[] = [];
  const visit = (blocks: Block[]) => {
    for (const block of blocks) {
      switch (block.kind) {
        case 'heading': lines.push(`${'#'.repeat(block.level)} ${block.text} (âncora: ${block.id})`); break;
        case 'paragraph': lines.push(inlineText(block.children)); break;
        case 'list': block.items.forEach((item, index) => lines.push(`${block.ordered ? `${index + 1}.` : '-'} ${inlineText(item)}`)); break;
        case 'code': lines.push(block.text); break;
        case 'quote': visit(block.children); break;
        case 'table': [block.header, ...block.rows].forEach((row) => lines.push(row.map(inlineText).join(' | '))); break;
        default: break;
      }
    }
  };
  visit(article.blocks);
  const text = lines.join('\n');
  return text.length > maxArticleChars ? `${text.slice(0, maxArticleChars)}\n[artigo truncado]` : text;
}

export interface AssistantHelpPayload {
  page: string;
  articles: { ref: string; title: string; updatedAt: string; stale: boolean; content: string }[];
}

/** Artigo da página, os relacionados e, se a página não tiver artigo, os melhores resultados da busca. */
export function helpArticlesFor(pathname: string, search: string, question: string, articles: HelpArticle[] = helpArticles): HelpArticle[] {
  const chosen: HelpArticle[] = [];
  const add = (article?: HelpArticle) => {
    if (article && !chosen.some((item) => item.ref === article.ref) && chosen.length < helpArticleLimit) chosen.push(article);
  };
  const page = findArticleForPage(pathname, search, articles);
  if (page) {
    add(page.article);
    page.article.related.forEach((ref) => add(articles.find((item) => item.ref === ref) ?? findArticle(ref)));
  } else {
    searchHelp(question, articles, { matchAll: false }).slice(0, 2).forEach((result) => add(result.article));
  }
  return chosen;
}

export function buildAssistantHelp(pathname: string, search: string, question: string): AssistantHelpPayload {
  return {
    page: pathname.slice(0, 300),
    articles: helpArticlesFor(pathname, search, question).map((article) => ({
      ref: article.ref,
      title: article.title,
      updatedAt: article.updatedAt,
      stale: isStale(article),
      content: articleForModel(article),
    })),
  };
}

/** `[A:assunto/artigo#ancora]` nas respostas do assistente. */
export const helpReferencePattern = /\[A:([a-z0-9-]+\/[a-z0-9-]+(?:#[a-z0-9-]+)?)\]/;
