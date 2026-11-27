import { Fragment, type MouseEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import styled from 'styled-components';
import { safeHref, type Block, type Inline } from './markdown';

/**
 * Desenha a árvore do Markdown com elementos React. Não usa `dangerouslySetInnerHTML`:
 * todo texto vira nó de texto, links só aceitam caminhos internos, âncoras, https e mailto,
 * e imagens só aparecem quando apontam para um arquivo empacotado junto com o artigo.
 */
export interface MarkdownViewProps {
  blocks: Block[];
  /** Prefixo dos ids das seções, para não colidir com ids da página quando o artigo abre no painel. */
  idPrefix?: string;
  /** Desloca o nível dos títulos (`##` vira `h3` com 1). */
  headingShift?: number;
  resolveImage: (src: string) => string | undefined;
  /** Clique num link `#ancora` do próprio artigo. */
  onAnchor?: (anchor: string) => void;
}

function renderInline(nodes: Inline[], props: MarkdownViewProps, key = ''): ReactNode[] {
  return nodes.map((node, index) => {
    const id = `${key}${index}`;
    switch (node.kind) {
      case 'text': return <Fragment key={id}>{node.text}</Fragment>;
      case 'code': return <code key={id}>{node.text}</code>;
      case 'strong': return <strong key={id}>{renderInline(node.children, props, `${id}-`)}</strong>;
      case 'em': return <em key={id}>{renderInline(node.children, props, `${id}-`)}</em>;
      case 'image': {
        const src = props.resolveImage(node.src);
        return src
          ? <img key={id} src={src} alt={node.alt} loading="lazy" />
          : <span key={id} className="missing-image">[imagem indisponível: {node.alt}]</span>;
      }
      case 'link': {
        const target = safeHref(node.href);
        const children = renderInline(node.children, props, `${id}-`);
        if (!target) return <Fragment key={id}>{children}</Fragment>;
        if (target.kind === 'internal') return <Link key={id} to={target.to}>{children}</Link>;
        if (target.kind === 'anchor') {
          const onClick = (event: MouseEvent<HTMLAnchorElement>) => {
            if (!props.onAnchor) return;
            event.preventDefault();
            props.onAnchor(target.anchor);
          };
          return <a key={id} href={`#${props.idPrefix ?? ''}${target.anchor}`} onClick={onClick}>{children}</a>;
        }
        return <a key={id} href={target.href} target="_blank" rel="noopener noreferrer">{children}</a>;
      }
      default: return null;
    }
  });
}

function renderBlocks(blocks: Block[], props: MarkdownViewProps, key = ''): ReactNode[] {
  return blocks.map((block, index) => {
    const id = `${key}${index}`;
    switch (block.kind) {
      case 'heading': {
        const level = Math.min(6, block.level + (props.headingShift ?? 0));
        const Tag = `h${level}` as 'h2';
        return <Tag key={id} id={`${props.idPrefix ?? ''}${block.id}`} tabIndex={-1}>{renderInline(block.children, props, `${id}-`)}</Tag>;
      }
      case 'paragraph': return <p key={id}>{renderInline(block.children, props, `${id}-`)}</p>;
      case 'list': {
        const items = block.items.map((item, itemIndex) => <li key={itemIndex}>{renderInline(item, props, `${id}-${itemIndex}-`)}</li>);
        return block.ordered ? <ol key={id}>{items}</ol> : <ul key={id}>{items}</ul>;
      }
      case 'code': return <pre key={id}><code>{block.text}</code></pre>;
      case 'quote': return <blockquote key={id}>{renderBlocks(block.children, props, `${id}-`)}</blockquote>;
      case 'table': return (
        <div key={id} className="table-scroll" role="region" aria-label="Tabela" tabIndex={0}>
          <table>
            <thead><tr>{block.header.map((cell, cellIndex) => <th key={cellIndex} scope="col">{renderInline(cell, props, `${id}-h${cellIndex}-`)}</th>)}</tr></thead>
            <tbody>{block.rows.map((row, rowIndex) => <tr key={rowIndex}>{row.map((cell, cellIndex) => <td key={cellIndex}>{renderInline(cell, props, `${id}-${rowIndex}-${cellIndex}-`)}</td>)}</tr>)}</tbody>
          </table>
        </div>
      );
      case 'rule': return <hr key={id} />;
      default: return null;
    }
  });
}

export const Prose = styled.div`
  color: ${({ theme }) => theme.color.text};
  font-size: 14.5px;
  line-height: 1.7;
  overflow-wrap: anywhere;

  h2, h3, h4, h5 { margin: 28px 0 10px; line-height: 1.3; letter-spacing: -0.015em; scroll-margin-top: 120px; }
  h2, h3 { font-size: 19px; }
  h4 { font-size: 16px; }
  h5 { font-size: 15px; }
  h2:focus, h3:focus, h4:focus, h5:focus { outline: none; }
  h2:focus-visible, h3:focus-visible, h4:focus-visible, h5:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 4px; }
  > :first-child { margin-top: 0; }
  p, ul, ol, pre, blockquote, .table-scroll { margin: 0 0 14px; }
  ul, ol { padding-left: 22px; }
  li + li { margin-top: 4px; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; text-decoration: underline; text-underline-offset: 3px; }
  code {
    padding: 1px 5px;
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.neutral[100]};
    font-family: ${({ theme }) => theme.font.mono};
    font-size: 0.88em;
  }
  pre {
    overflow-x: auto;
    padding: 12px 14px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surfaceSubtle};
  }
  pre code { padding: 0; background: transparent; }
  blockquote {
    padding: 10px 14px;
    border-left: 4px solid ${({ theme }) => theme.color.accentBlueAccessible};
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.surfaceSubtle};
    color: ${({ theme }) => theme.color.text};
  }
  blockquote > :last-child { margin-bottom: 0; }
  .table-scroll { overflow-x: auto; }
  table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
  th, td { padding: 8px 10px; border: 1px solid ${({ theme }) => theme.color.border}; text-align: left; vertical-align: top; }
  th { background: ${({ theme }) => theme.color.surfaceSubtle}; font-weight: 750; }
  img { display: block; max-width: 100%; height: auto; margin: 6px 0; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.md}; }
  .missing-image { color: ${({ theme }) => theme.color.textMutedAccessible}; font-style: italic; }
  hr { margin: 22px 0; border: 0; border-top: 1px solid ${({ theme }) => theme.color.border}; }
`;

export function MarkdownView(props: MarkdownViewProps) {
  return <>{renderBlocks(props.blocks, props)}</>;
}
