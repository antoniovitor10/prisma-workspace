import * as Dialog from '@radix-ui/react-dialog';
import { BookOpen, CircleHelp, X } from 'lucide-react';
import { useCallback, useEffect, useRef, useState, type RefObject } from 'react';
import { Link } from 'react-router-dom';
import styled from 'styled-components';
import { ArticleMeta, HelpArticleBody, helpPanelIdPrefix } from './HelpArticleView';
import { helpPath, resolveHelpRef, type HelpTarget } from './helpIndex';
import { helpTargets, type HelpTargetKey } from './helpTargets';

/**
 * Painel lateral da ajuda. Abre por cima da página (diálogo modal), então a tela de baixo
 * continua montada: formulário, filtro e rolagem ficam como estavam ao fechar (CA-3).
 */
const Overlay = styled(Dialog.Overlay)`
  position: fixed;
  inset: 0;
  z-index: 210;
  background: rgba(15, 23, 42, 0.38);
`;

const Sheet = styled(Dialog.Content)`
  position: fixed;
  z-index: 211;
  top: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  width: min(580px, 100vw);
  border-left: 1px solid ${({ theme }) => theme.color.border};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  box-shadow: ${({ theme }) => theme.shadow.lg};

  > header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
    padding: 18px 20px 14px;
    border-bottom: 1px solid ${({ theme }) => theme.color.border};
  }
  > header h2 { font-size: 19px; line-height: 1.3; letter-spacing: -0.015em; }
  .eyebrow { display: block; margin-bottom: 4px; color: ${({ theme }) => theme.color.brandAccessible}; font-size: 11px; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
  .body { flex: 1; min-height: 0; overflow-y: auto; overscroll-behavior: contain; padding: 18px 20px 24px; }
  > footer { display: flex; flex-wrap: wrap; gap: 10px; justify-content: space-between; align-items: center; padding: 12px 20px; border-top: 1px solid ${({ theme }) => theme.color.border}; }
  > footer a { display: inline-flex; align-items: center; gap: 6px; min-height: 40px; color: ${({ theme }) => theme.color.brandAccessible}; font-size: 13.5px; font-weight: 750; text-decoration: underline; text-underline-offset: 3px; }

  @media (max-width: 640px) {
    width: 100vw;
    border-left: 0;
    > header, .body, > footer { padding-left: 16px; padding-right: 16px; }
  }
`;

const CloseButton = styled.button`
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-width: 44px;
  min-height: 44px;
  padding: 0 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  font-weight: 750;
  &:hover { background: ${({ theme }) => theme.color.surfaceSubtle}; }
`;

export function HelpPanel({ target, open, onOpenChange, returnFocusTo }: {
  target: HelpTarget;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Botão que abriu o painel; recebe o foco de volta ao fechar. */
  returnFocusTo?: RefObject<HTMLElement | null>;
}) {
  const body = useRef<HTMLDivElement>(null);
  const { article, anchor } = target;

  const goTo = useCallback((id: string) => {
    const element = body.current?.querySelector<HTMLElement>(`#${CSS.escape(helpPanelIdPrefix + id)}`);
    if (!element) return;
    element.scrollIntoView?.({ block: 'start' });
    element.focus({ preventScroll: true });
  }, []);

  // Ao abrir, leva o leitor à seção da página atual; sem âncora, o foco fica no título.
  useEffect(() => {
    if (!open || !anchor) return;
    const frame = requestAnimationFrame(() => goTo(anchor));
    return () => cancelAnimationFrame(frame);
  }, [open, anchor, goTo]);

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Overlay />
        <Sheet aria-describedby={undefined}
          onOpenAutoFocus={(event) => { if (anchor) event.preventDefault(); }}
          onCloseAutoFocus={(event) => { if (returnFocusTo?.current) { event.preventDefault(); returnFocusTo.current.focus(); } }}>
          <header>
            <div>
              <span className="eyebrow">Ajuda</span>
              <Dialog.Title>{article.title}</Dialog.Title>
              <ArticleMeta article={article} />
            </div>
            <Dialog.Close asChild>
              <CloseButton type="button" aria-label="Fechar ajuda"><X size={16} aria-hidden="true" /></CloseButton>
            </Dialog.Close>
          </header>
          <div className="body" ref={body}>
            <HelpArticleBody article={article} compact onAnchor={goTo} />
          </div>
          <footer>
            <Link to={helpPath(article.ref, anchor)} onClick={() => onOpenChange(false)}>
              <BookOpen size={15} aria-hidden="true" />Abrir na central de ajuda
            </Link>
          </footer>
        </Sheet>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

export const HelpIconButton = styled.button`
  display: inline-grid;
  flex: 0 0 auto;
  place-items: center;
  width: 32px;
  height: 32px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.brandAccessible};
  &:hover { border-color: ${({ theme }) => theme.color.brandAccessible}; background: ${({ theme }) => theme.color.surfaceSubtle}; }
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 2px; }
`;

/**
 * Botão "?" de uma seção. O alvo vem do registro `helpTargets`; o teste da ajuda falha se o
 * artigo ou a âncora de algum alvo deixar de existir (CA-8).
 */
export function HelpButton({ target, label }: { target: HelpTargetKey; label: string }) {
  const [open, setOpen] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
  const resolved = resolveHelpRef(helpTargets[target]);
  if (!resolved) return null;
  return (
    <>
      <HelpIconButton ref={button} type="button" aria-label={label} title={label} aria-haspopup="dialog" onClick={() => setOpen(true)}>
        <CircleHelp size={16} aria-hidden="true" />
      </HelpIconButton>
      <HelpPanel target={resolved} open={open} onOpenChange={setOpen} returnFocusTo={button} />
    </>
  );
}

/** Título de seção com o "?" ao lado. */
export const HelpTitleRow = styled.div`
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
`;
