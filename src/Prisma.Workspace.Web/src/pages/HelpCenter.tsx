import { ArrowLeft, Search } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router-dom';
import styled from 'styled-components';
import { HeaderLead, Page, PageHeader, SearchControl, Surface } from '../components/PageLayout';
import { ArticleMeta, HelpArticleBody } from '../features/help/HelpArticleView';
import { findArticle, helpArticles, helpPath, isStale, searchHelp, type HelpArticle } from '../features/help/helpIndex';
import { helpSubjects } from '../features/help/subjects';

/**
 * Central de ajuda (`/ajuda`). Lista os artigos por assunto, busca por palavras e mostra o
 * artigo com a data de atualização. Os artigos vêm do bundle; nada aqui chama a API.
 */
const Layout = styled.div`
  display: grid;
  grid-template-columns: minmax(240px, 300px) minmax(0, 1fr);
  gap: 22px;
  align-items: start;

  @media (max-width: 900px) { grid-template-columns: minmax(0, 1fr); }
`;

const Sidebar = styled.aside`
  position: sticky;
  top: 132px;
  display: grid;
  gap: 14px;
  max-height: calc(100vh - 150px);
  overflow-y: auto;
  padding: 2px;

  @media (max-width: 900px) { position: static; max-height: none; overflow: visible; }
`;

const ArticleList = styled.nav`
  display: grid;
  gap: 16px;

  h2 { margin-bottom: 6px; color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 11.5px; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
  ul { display: grid; gap: 2px; list-style: none; }
  a {
    display: flex;
    align-items: center;
    min-height: 40px;
    padding: 6px 10px;
    border-radius: ${({ theme }) => theme.radius.md};
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
    font-weight: 650;
    line-height: 1.35;
  }
  a:hover { background: ${({ theme }) => theme.color.surfaceSubtle}; }
  a[aria-current='page'] {
    background: color-mix(in srgb, ${({ theme }) => theme.color.brand} 10%, ${({ theme }) => theme.color.surface});
    color: ${({ theme }) => theme.color.brandAccessible};
  }
  .stale { margin-left: 6px; color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 11.5px; font-weight: 600; }
`;

const Collapsible = styled.details`
  summary {
    display: flex;
    align-items: center;
    min-height: 44px;
    padding: 0 12px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
    font-weight: 750;
    cursor: pointer;
  }
  &[open] summary { margin-bottom: 12px; }

  @media (min-width: 901px) {
    summary { display: none; }
  }
`;

const ArticleSurface = styled(Surface)`
  padding: clamp(18px, 2.4vw, 32px);

  > header { margin-bottom: 20px; padding-bottom: 16px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  > header h2 { font-size: clamp(22px, 2vw, 28px); line-height: 1.2; letter-spacing: -0.025em; }
  > header h2:focus { outline: none; }
  > header .summary { margin-top: 6px; color: ${({ theme }) => theme.color.textMuted}; font-size: 14px; line-height: 1.55; }
`;

const Overview = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;

  section { padding: 18px; }
  h2 { font-size: 16px; }
  p { margin: 6px 0 12px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; line-height: 1.5; }
  ul { display: grid; gap: 6px; padding-left: 18px; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; font-size: 13.5px; text-decoration: underline; text-underline-offset: 3px; }
`;

const Results = styled.div`
  display: grid;
  gap: 8px;

  p { color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 13px; }
  ul { display: grid; gap: 6px; list-style: none; }
  a {
    display: grid;
    gap: 2px;
    padding: 9px 10px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
    font-weight: 700;
  }
  a small { color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 12px; font-weight: 500; }
  a:hover { border-color: ${({ theme }) => theme.color.brandAccessible}; }
`;

const BackLink = styled(Link)`
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 40px;
  margin-bottom: 8px;
  color: ${({ theme }) => theme.color.brandAccessible};
  font-size: 13.5px;
  font-weight: 700;
`;

function ArticlesBySubject({ current }: { current?: HelpArticle }) {
  return (
    <ArticleList aria-label="Artigos por assunto">
      {helpSubjects.map((subject) => {
        const articles = helpArticles.filter((article) => article.subject === subject.slug);
        if (articles.length === 0) return null;
        return (
          <div key={subject.slug}>
            <h2 id={`assunto-${subject.slug}`}>{subject.title}</h2>
            <ul aria-labelledby={`assunto-${subject.slug}`}>
              {articles.map((article) => (
                <li key={article.ref}>
                  <Link to={helpPath(article.ref)} aria-current={current?.ref === article.ref ? 'page' : undefined}>
                    {article.title}
                    {isStale(article) && <span className="stale">(pode estar desatualizado)</span>}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        );
      })}
    </ArticleList>
  );
}

export function HelpCenter() {
  const { subject, slug } = useParams();
  const location = useLocation();
  const [query, setQuery] = useState('');
  const results = useMemo(() => searchHelp(query), [query]);
  const article = subject && slug ? findArticle(`${subject}/${slug}`) : undefined;
  const notFound = Boolean(subject && slug && !article);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const searching = query.trim().length >= 2;

  // Ao trocar de artigo ou de âncora, leva a pessoa (e o foco do leitor de tela) ao ponto certo.
  useEffect(() => {
    if (!article) return;
    const anchor = decodeURIComponent(location.hash.replace(/^#/, ''));
    const frame = requestAnimationFrame(() => {
      const element = anchor ? document.getElementById(anchor) : null;
      if (element) {
        element.scrollIntoView?.({ block: 'start' });
        element.focus({ preventScroll: true });
      } else {
        window.scrollTo?.({ top: 0 });
        titleRef.current?.focus({ preventScroll: true });
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [article, location.hash]);

  useEffect(() => {
    document.title = article ? `${article.title} · Ajuda · Prisma WorkSpace` : 'Central de ajuda · Prisma WorkSpace';
  }, [article]);

  const navigation = (
    <>
      <SearchControl>
        <Search size={15} aria-hidden="true" />
        <span className="sr-only">Buscar na ajuda</span>
        <input type="search" value={query} placeholder="Buscar na ajuda" onChange={(event) => setQuery(event.target.value)} />
      </SearchControl>
      {searching ? (
        <Results>
          <p role="status" aria-live="polite">{results.length === 0 ? 'Nenhum artigo encontrado.' : `${results.length} artigo(s) encontrado(s).`}</p>
          {results.length > 0 && (
            <ul aria-label="Resultados da busca">
              {results.map((result) => (
                <li key={result.article.ref}>
                  <Link to={helpPath(result.article.ref, result.anchor)}>
                    {result.article.title}
                    <small>{helpSubjects.find((item) => item.slug === result.article.subject)?.title}</small>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Results>
      ) : <ArticlesBySubject current={article} />}
    </>
  );

  return (
    <Page>
      <PageHeader>
        <HeaderLead>
          <span className="eyebrow">Ajuda</span>
          <h1>Central de ajuda</h1>
          <p>Como usar e administrar o Prisma WorkSpace: organização, projetos, quadro, integrações e Git.</p>
        </HeaderLead>
      </PageHeader>
      <Layout>
        <Sidebar aria-label="Navegação da ajuda">
          {article || notFound
            ? <Collapsible><summary>Todos os artigos e busca</summary>{navigation}</Collapsible>
            : navigation}
        </Sidebar>
        <div>
          {article ? (
            <ArticleSurface as="article" aria-labelledby="artigo-titulo">
              <BackLink to="/ajuda"><ArrowLeft size={15} aria-hidden="true" />Todos os assuntos</BackLink>
              <header>
                <h2 id="artigo-titulo" ref={titleRef} tabIndex={-1}>{article.title}</h2>
                <p className="summary">{article.summary}</p>
                <ArticleMeta article={article} />
              </header>
              <HelpArticleBody article={article} />
            </ArticleSurface>
          ) : notFound ? (
            <ArticleSurface>
              <header><h2>Artigo não encontrado</h2></header>
              <p>Este endereço não corresponde a nenhum artigo da ajuda. <Link to="/ajuda">Voltar para a central de ajuda</Link>.</p>
            </ArticleSurface>
          ) : (
            <Overview>
              {helpSubjects.map((item) => {
                const articles = helpArticles.filter((entry) => entry.subject === item.slug);
                if (articles.length === 0) return null;
                return (
                  <Surface key={item.slug} aria-labelledby={`visao-${item.slug}`}>
                    <h2 id={`visao-${item.slug}`}>{item.title}</h2>
                    <p>{item.description}</p>
                    <ul>{articles.map((entry) => <li key={entry.ref}><Link to={helpPath(entry.ref)}>{entry.title}</Link></li>)}</ul>
                  </Surface>
                );
              })}
            </Overview>
          )}
        </div>
      </Layout>
    </Page>
  );
}
