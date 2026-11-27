import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import styled from 'styled-components';

/**
 * Markdown do método formatado como elementos React (D9).
 * Segurança: `skipHtml` descarta todo HTML cru (sem rehype-raw, sem dangerouslySetInnerHTML) e o
 * `urlTransform` padrão do react-markdown troca `javascript:`, `vbscript:` e `data:` por URL vazia.
 */
export const Prose = styled.div`
  color: ${({ theme }) => theme.color.text};
  font-family: ${({ theme }) => theme.font.body};
  font-size: 14px;
  line-height: 1.6;
  overflow-wrap: anywhere;
  > :first-child { margin-top: 0; }
  > :last-child { margin-bottom: 0; }
  h1, h2, h3, h4, h5, h6 {
    margin: 1.4em 0 0.5em;
    color: ${({ theme }) => theme.color.text};
    font-family: ${({ theme }) => theme.font.display};
    font-weight: 800;
    line-height: 1.25;
  }
  h1 { font-size: 22px; }
  h2 { font-size: 18px; padding-bottom: 4px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; }
  h3 { font-size: 16px; }
  h4, h5, h6 { font-size: 14px; }
  p, ul, ol, blockquote, pre, table { margin: 0 0 0.9em; }
  ul, ol { padding-left: 1.4em; }
  ul { list-style: disc; }
  ol { list-style: decimal; }
  li + li { margin-top: 0.25em; }
  li.task-list-item { list-style: none; margin-left: -1.2em; }
  li.task-list-item input { margin-right: 6px; vertical-align: middle; accent-color: ${({ theme }) => theme.color.brand}; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; text-decoration: underline; text-underline-offset: 2px; }
  strong { font-weight: 800; }
  code {
    padding: 1px 5px;
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.neutral[100]};
    color: ${({ theme }) => theme.color.text};
    font-family: ${({ theme }) => theme.font.mono};
    font-size: 0.92em;
  }
  pre {
    padding: 12px 14px;
    overflow-x: auto;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surfaceSubtle};
    code { padding: 0; background: transparent; }
  }
  blockquote {
    padding: 4px 12px;
    border-left: 3px solid ${({ theme }) => theme.color.borderStrong};
    color: ${({ theme }) => theme.color.textMutedAccessible};
  }
  hr { margin: 1.4em 0; border: 0; border-top: 1px solid ${({ theme }) => theme.color.border}; }
  img { max-width: 100%; height: auto; }
  .table-scroll { max-width: 100%; margin: 0 0 0.9em; overflow-x: auto; }
  .table-scroll table { margin: 0; }
  table { width: 100%; border-collapse: collapse; font-size: 13px; }
  th, td {
    padding: 6px 9px;
    border: 1px solid ${({ theme }) => theme.color.border};
    text-align: left;
    vertical-align: top;
  }
  th { background: ${({ theme }) => theme.color.neutral[50]}; color: ${({ theme }) => theme.color.text}; font-weight: 800; }
`;

const components: Components = {
  // Tabelas largas rolam dentro do bloco, sem rolagem horizontal da página no celular.
  table: ({ node: _node, ...props }) => <div className="table-scroll"><table {...props} /></div>,
  a: ({ node: _node, href, ...props }) => href
    ? <a href={href} target="_blank" rel="noopener noreferrer nofollow" {...props} />
    : <span {...props} />,
  img: ({ node: _node, ...props }) => <img loading="lazy" referrerPolicy="no-referrer" {...props} alt={props.alt ?? ''} />,
};

interface MarkdownViewProps {
  content: string;
  className?: string;
  'aria-label'?: string;
}

export function MarkdownView({ content, className, 'aria-label': ariaLabel }: MarkdownViewProps) {
  return (
    <Prose className={className} aria-label={ariaLabel} role={ariaLabel ? 'region' : undefined}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} skipHtml components={components}>{content}</ReactMarkdown>
    </Prose>
  );
}
