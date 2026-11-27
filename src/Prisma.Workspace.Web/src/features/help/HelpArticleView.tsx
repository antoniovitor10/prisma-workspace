import { TriangleAlert } from 'lucide-react';
import { Link } from 'react-router-dom';
import styled from 'styled-components';
import { findArticle, formatHelpDate, helpPath, isStale, resolveHelpImage, type HelpArticle } from './helpIndex';
import { MarkdownView, Prose } from './MarkdownView';

const Meta = styled.p`
  margin: 6px 0 0;
  color: ${({ theme }) => theme.color.textMutedAccessible};
  font-size: 13px;
`;

const StaleNotice = styled.p`
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin: 14px 0 0;
  padding: 10px 12px;
  border: 1px solid color-mix(in srgb, ${({ theme }) => theme.color.warning} 45%, transparent);
  border-radius: ${({ theme }) => theme.radius.md};
  background: color-mix(in srgb, ${({ theme }) => theme.color.warning} 10%, ${({ theme }) => theme.color.surface});
  color: ${({ theme }) => theme.color.text};
  font-size: 13.5px;
  line-height: 1.5;

  svg { flex: 0 0 auto; margin-top: 2px; color: ${({ theme }) => theme.color.warning}; }
`;

const Related = styled.nav`
  margin-top: 28px;
  padding-top: 16px;
  border-top: 1px solid ${({ theme }) => theme.color.border};

  h2, h3 { margin-bottom: 8px; font-size: 14px; }
  ul { display: grid; gap: 6px; padding-left: 18px; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; font-size: 13.5px; text-decoration: underline; text-underline-offset: 3px; }
`;

export function StaleWarning({ article }: { article: HelpArticle }) {
  if (!isStale(article)) return null;
  return (
    <StaleNotice role="note">
      <TriangleAlert size={16} aria-hidden="true" />
      <span>Este artigo pode estar desatualizado: a revisão venceu em {formatHelpDate(article.reviewBy)}. Confira os passos na tela antes de seguir.</span>
    </StaleNotice>
  );
}

export function ArticleMeta({ article }: { article: HelpArticle }) {
  return (
    <>
      <Meta>Atualizado em <time dateTime={article.updatedAt}>{formatHelpDate(article.updatedAt)}</time></Meta>
      <StaleWarning article={article} />
    </>
  );
}

export function HelpArticleBody({ article, compact = false, onAnchor }: {
  article: HelpArticle;
  /** No painel lateral os ids das seções ganham prefixo, para não colidir com a página. */
  compact?: boolean;
  onAnchor?: (anchor: string) => void;
}) {
  const related = article.related.map(findArticle).filter((item): item is HelpArticle => Boolean(item));
  return (
    <>
      <Prose>
        <MarkdownView
          blocks={article.blocks}
          headingShift={1}
          idPrefix={compact ? helpPanelIdPrefix : ''}
          resolveImage={(src) => resolveHelpImage(article, src)}
          onAnchor={onAnchor}
        />
      </Prose>
      {related.length > 0 && (
        <Related aria-label="Artigos relacionados">
          <h3>Veja também</h3>
          <ul>{related.map((item) => <li key={item.ref}><Link to={helpPath(item.ref)}>{item.title}</Link></li>)}</ul>
        </Related>
      )}
    </>
  );
}

export const helpPanelIdPrefix = 'ajuda-painel-';
