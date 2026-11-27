import { describe, expect, it } from 'vitest';
import {
  buildIndex, findArticleForPage, helpArticles, isStale, parseArticle, resolveHelpImage, resolveHelpRef, searchHelp, type HelpArticle,
} from './helpIndex';
import { buildAssistantHelp, helpArticlesFor, isHelpQuestion } from './helpAssistant';
import type { Block, Inline } from './markdown';
import { helpSubjects } from './subjects';
import { helpTargets } from './helpTargets';

/**
 * Guarda do conteúdo da central de ajuda. Roda no CI com o Vitest: um "?" que aponta para artigo
 * ou âncora inexistente, uma página de artigo que não existe no app, um link quebrado entre
 * artigos ou um valor com cara de segredo reprovam o PR.
 */

// Rotas declaradas no App.tsx, lidas como texto para o teste acompanhar o código.
const appSource = Object.values(import.meta.glob<string>('../../App.tsx', { eager: true, query: '?raw', import: 'default' }))[0];
const appRoutes = new Set(
  [...appSource.matchAll(/<Route\s+path="([^"]+)"/g)]
    .map((match) => match[1])
    .filter((path) => path !== '*')
    .map((path) => (path.startsWith('/') ? path : `/projects/:projectId/${path}`)),
);

function links(nodes: Inline[]): string[] {
  return nodes.flatMap((node) => {
    if (node.kind === 'link') return [node.href, ...links(node.children)];
    if (node.kind === 'strong' || node.kind === 'em') return links(node.children);
    return [];
  });
}

function blockLinks(blocks: Block[]): string[] {
  return blocks.flatMap((block) => {
    switch (block.kind) {
      case 'heading': case 'paragraph': return links(block.children);
      case 'list': return block.items.flatMap(links);
      case 'quote': return blockLinks(block.children);
      case 'table': return [block.header, ...block.rows].flat().flatMap(links);
      default: return [];
    }
  });
}

function images(blocks: Block[]): string[] {
  const fromInline = (nodes: Inline[]): string[] => nodes.flatMap((node) => {
    if (node.kind === 'image') return [node.src];
    if (node.kind === 'link' || node.kind === 'strong' || node.kind === 'em') return fromInline(node.children);
    return [];
  });
  return blocks.flatMap((block) => {
    switch (block.kind) {
      case 'heading': case 'paragraph': return fromInline(block.children);
      case 'list': return block.items.flatMap(fromInline);
      case 'quote': return images(block.children);
      case 'table': return [block.header, ...block.rows].flat().flatMap(fromInline);
      default: return [];
    }
  });
}

// Mesmos formatos que o servidor recusa em AiHelpContext (CA-6).
const credentialShape = /pmt_[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY|\bsk-[A-Za-z0-9_-]{16,}|\bAKIA[0-9A-Z]{16}\b|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}|(?=[A-Za-z0-9+/=_-]*\d)(?=[A-Za-z0-9+/=_-]*[A-Z])(?=[A-Za-z0-9+/=_-]*[a-z])[A-Za-z0-9+/=_-]{40,}/;
const rawArticles = import.meta.glob<string>('./articles/**/*.md', { eager: true, query: '?raw', import: 'default' });

describe('conteúdo da central de ajuda', () => {
  it('carrega todos os artigos com título, resumo e datas', () => {
    expect(helpArticles.length).toBe(Object.keys(rawArticles).length);
    for (const article of helpArticles) {
      expect(article.title, article.ref).not.toBe('');
      expect(article.summary, article.ref).not.toBe('');
      expect(article.updatedAt <= article.reviewBy, `${article.ref}: revisão antes da atualização`).toBe(true);
      expect(article.anchors.length, `${article.ref}: sem seções`).toBeGreaterThan(0);
    }
  });

  it('cada artigo está num assunto conhecido e os quatro assuntos da primeira versão têm conteúdo (CA-5)', () => {
    const known = new Set(helpSubjects.map((subject) => subject.slug));
    for (const article of helpArticles) expect(known.has(article.subject), `${article.ref}: assunto desconhecido`).toBe(true);
    for (const subject of ['organizacao-e-equipes', 'integracoes', 'repositorios-e-git', 'quadro-backlog-e-sprints']) {
      expect(helpArticles.some((article) => article.subject === subject), subject).toBe(true);
    }
  });

  it('cobre os tópicos pedidos para Integrações e Git (CA-5)', () => {
    const anchors = (ref: string) => resolveHelpRef(ref)?.article.anchors ?? [];
    expect(anchors('integracoes/integracoes')).toEqual(expect.arrayContaining(['ativar-ou-pausar', 'pausada-recusa-token']));
    expect(anchors('integracoes/tokens')).toEqual(expect.arrayContaining(['papel-agente', 'papel-humano', 'papel-sistema']));
    expect(anchors('integracoes/mcp')).toEqual(expect.arrayContaining(['variaveis-de-ambiente', 'configurar-o-cliente']));
    expect(anchors('repositorios-e-git/github-app')).toEqual(expect.arrayContaining(['app-id', 'installation-id', 'referencias-de-segredo', 'webhook', 'sem-admin']));
    expect(anchors('repositorios-e-git/branch-e-pr')).toEqual(expect.arrayContaining(['vincular-branch', 'o-que-move-o-card']));
  });

  it('todo botão "?" de seção aponta para artigo e âncora existentes (CA-8)', () => {
    for (const [key, target] of Object.entries(helpTargets)) {
      expect(resolveHelpRef(target), `helpTargets.${key} -> ${target}`).toBeDefined();
    }
  });

  it('todo "?" de página aponta para rota do app, artigo e âncora existentes (CA-8)', () => {
    for (const article of helpArticles) {
      for (const page of article.pages) {
        expect(appRoutes.has(page.pattern), `${article.ref}: rota ${page.pattern} não existe no App.tsx`).toBe(true);
        if (page.anchor) expect(article.anchors, `${article.ref}: âncora #${page.anchor}`).toContain(page.anchor);
      }
    }
  });

  it('a checagem falha quando o artigo ou a âncora não existem (CA-8)', () => {
    expect(resolveHelpRef('integracoes/nao-existe')).toBeUndefined();
    expect(resolveHelpRef('integracoes/tokens#secao-removida')).toBeUndefined();
  });

  it('links entre artigos e relacionados não estão quebrados', () => {
    for (const article of helpArticles) {
      for (const related of article.related) expect(resolveHelpRef(related), `${article.ref}: relacionado ${related}`).toBeDefined();
      for (const href of blockLinks(article.blocks)) {
        if (href.startsWith('/ajuda/')) expect(resolveHelpRef(href.slice('/ajuda/'.length)), `${article.ref}: link ${href}`).toBeDefined();
        if (href.startsWith('#')) expect(article.anchors, `${article.ref}: link ${href}`).toContain(href.slice(1));
      }
    }
  });

  it('imagens citadas existem no pacote', () => {
    for (const article of helpArticles) {
      for (const src of images(article.blocks)) expect(resolveHelpImage(article, src), `${article.ref}: imagem ${src}`).toBeDefined();
    }
  });

  it('usa o nome Integrações, nunca o nome antigo (CA-10)', () => {
    for (const [path, source] of Object.entries(rawArticles)) expect(source, path).not.toMatch(/m[eé]todo de trabalho/i);
  });

  it('nunca traz valor de segredo, só nomes de configuração (CA-6)', () => {
    for (const [path, source] of Object.entries(rawArticles)) {
      expect(source, path).not.toMatch(credentialShape);
      // Variável de segredo seguida de valor: PRISMA_SETUP_TOKEN=algo, Jwt__Key=algo...
      expect(source, path).not.toMatch(/(?:PASSWORD|TOKEN|KEY|SECRET|Secrets__\S+)\s*=\s*(?!false|true)[^\s`|]+/i);
    }
  });
});

describe('índice da ajuda', () => {
  const article = (path: string, front: string, body = '## Seção {#secao}\n\nTexto.') =>
    parseArticle(path, `---\ntitulo: Teste\nresumo: Resumo\natualizado_em: 2026-01-01\nrevisar_ate: 2026-06-30\n${front}---\n\n${body}`);

  it('recusa artigo fora de articles/<assunto>/<artigo>.md ou sem campo obrigatório', () => {
    expect(() => parseArticle('./articles/solto.md', '---\ntitulo: x\n---\n')).toThrow(/assunto/);
    expect(() => parseArticle('./articles/a/b.md', '---\ntitulo: x\n---\n')).toThrow(/obrigatório/);
  });

  it('escolhe o artigo da rota mais específica, inclusive pela seção da URL', () => {
    const index = buildIndex({
      './articles/organizacao-e-equipes/projetos.md': '---\ntitulo: Projetos\nresumo: r\natualizado_em: 2026-01-01\nrevisar_ate: 2026-06-30\npaginas:\n  - /projects/:projectId/settings#geral\n---\n\n## Geral\n',
      './articles/integracoes/integracoes.md': '---\ntitulo: Integrações\nresumo: r\natualizado_em: 2026-01-01\nrevisar_ate: 2026-06-30\npaginas:\n  - /projects/:projectId/settings?secao=metodo#ativar\n---\n\n## Ativar {#ativar}\n',
    });
    expect(findArticleForPage('/projects/1/settings', '', index)).toMatchObject({ article: { ref: 'organizacao-e-equipes/projetos' }, anchor: 'geral' });
    expect(findArticleForPage('/projects/1/settings', '?secao=metodo', index)).toMatchObject({ article: { ref: 'integracoes/integracoes' }, anchor: 'ativar' });
    expect(findArticleForPage('/reports', '', index)).toBeUndefined();
  });

  it('as páginas reais do app abrem o artigo certo', () => {
    expect(findArticleForPage('/projects/abc/settings', '?secao=metodo')?.article.ref).toBe('integracoes/integracoes');
    expect(findArticleForPage('/projects/abc/backlog')?.article.ref).toBe('quadro-backlog-e-sprints/backlog');
    expect(findArticleForPage('/boards/abc')?.article.ref).toBe('quadro-backlog-e-sprints/quadro-kanban');
    expect(findArticleForPage('/teams')?.article.ref).toBe('organizacao-e-equipes/equipes');
  });

  it('marca como desatualizado depois da data de revisão (CA-9)', () => {
    const sample = article('./articles/a/b.md', '');
    expect(isStale(sample, '2026-06-30')).toBe(false);
    expect(isStale(sample, '2026-07-01')).toBe(true);
  });

  it('busca por palavras, ignorando acentos e palavras comuns', () => {
    expect(searchHelp('como faço para emitir token')[0]?.article.ref).toBe('integracoes/tokens');
    expect(searchHelp('installation id')[0]?.article.ref).toBe('repositorios-e-git/github-app');
    expect(searchHelp('xyzinexistente')).toEqual([]);
    expect(searchHelp('como')).toEqual([]);
  });

  it('resolve imagem só dentro do pacote', () => {
    const images = { './articles/a/imagens/tela.png': '/assets/tela.png' };
    expect(resolveHelpImage({ folder: './articles/a' }, './imagens/tela.png', images)).toBe('/assets/tela.png');
    expect(resolveHelpImage({ folder: './articles/a' }, 'https://exemplo.invalid/x.png', images)).toBeUndefined();
  });
});

describe('ajuda para o assistente (CA-4)', () => {
  it('reconhece perguntas de uso do produto', () => {
    expect(isHelpQuestion('Preciso de ajuda com o webhook')).toBe(true);
    expect(isHelpQuestion('Como faço para emitir um token?')).toBe(true);
    expect(isHelpQuestion('Onde fica o campo Secret?')).toBe(true);
    expect(isHelpQuestion('Quais são minhas tarefas?')).toBe(false);
    expect(isHelpQuestion('O que está atrasado neste projeto?')).toBe(false);
  });

  it('manda o artigo da página e os relacionados, com âncoras e o aviso de revisão', () => {
    const help = buildAssistantHelp('/projects/abc/settings', '?secao=metodo', 'como pauso?');
    expect(help.articles[0].ref).toBe('integracoes/integracoes');
    expect(help.articles.map((item) => item.ref)).toContain('integracoes/tokens');
    expect(help.articles.length).toBeLessThanOrEqual(3);
    expect(help.articles[0].content).toContain('(âncora: ativar-ou-pausar)');
    expect(help.articles[0].content).not.toMatch(credentialShape);
    expect(typeof help.articles[0].stale).toBe('boolean');
  });

  it('sem artigo da página, usa a busca; sem resultado, manda a lista vazia', () => {
    expect(helpArticlesFor('/reports', '', 'como conecto o webhook do github?').map((item: HelpArticle) => item.ref)).toContain('repositorios-e-git/github-app');
    expect(helpArticlesFor('/reports', '', 'como faço xyzinexistente?')).toEqual([]);
  });
});
