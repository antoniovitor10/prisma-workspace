import { CircleHelp } from 'lucide-react';
import { useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import styled from 'styled-components';
import { findArticleForPage } from './helpIndex';
import { HelpIconButton, HelpPanel } from './HelpPanel';

const HelpLink = styled(Link)`
  display: inline-grid;
  flex: 0 0 auto;
  place-items: center;
  width: 32px;
  height: 32px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.textMuted};
  &:hover { border-color: ${({ theme }) => theme.color.brandAccessible}; color: ${({ theme }) => theme.color.brandAccessible}; }
`;

/**
 * "?" da barra superior. Na página que tem artigo relacionado (campo `paginas` do artigo),
 * abre o artigo na seção certa sem sair da página. Nas demais, leva à central de ajuda.
 */
export function PageHelpButton() {
  const location = useLocation();
  const [openFor, setOpenFor] = useState<string | null>(null);
  const button = useRef<HTMLButtonElement>(null);
  const key = `${location.pathname}${location.search}`;
  const target = findArticleForPage(location.pathname, location.search);

  if (location.pathname.startsWith('/ajuda')) return null;
  if (!target) {
    return (
      <HelpLink to="/ajuda" aria-label="Central de ajuda" title="Central de ajuda">
        <CircleHelp size={16} aria-hidden="true" />
      </HelpLink>
    );
  }
  return (
    <>
      <HelpIconButton ref={button} type="button" aria-label="Ajuda desta página" title="Ajuda desta página" aria-haspopup="dialog"
        onClick={() => setOpenFor(key)}>
        <CircleHelp size={16} aria-hidden="true" />
      </HelpIconButton>
      {/* Fecha sozinho se a rota mudar enquanto o painel está aberto. */}
      <HelpPanel target={target} open={openFor === key} onOpenChange={(open) => setOpenFor(open ? key : null)} returnFocusTo={button} />
    </>
  );
}
