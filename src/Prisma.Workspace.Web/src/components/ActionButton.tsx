import { Loader2 } from 'lucide-react';
import {
  useEffect,
  useRef,
  useState,
  type ButtonHTMLAttributes,
  type CSSProperties,
  type MouseEvent,
  type Ref,
} from 'react';
import styled, { keyframes } from 'styled-components';

/** Depois deste tempo em espera, o botão mostra o aviso de demora (HU-0001, CA-4). */
export const SLOW_ACTION_MS = 10_000;
export const SLOW_ACTION_MESSAGE = 'Ainda processando...';
/**
 * Com `loading` controlado e um `onClick` que não devolve Promise (ex.: `mutation.mutate()`),
 * o botão já entra em espera no clique e aguarda o `loading` chegar. Se ele não chegar neste
 * prazo (validação falhou, ação cancelada), o botão volta ao normal.
 */
export const LOADING_HANDOFF_MS = 600;

export interface ActionButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'onClick'> {
  /** Espera controlada de fora, ex.: `mutation.isPending`. */
  loading?: boolean;
  /** Texto anunciado ao leitor de tela durante a espera. */
  loadingLabel?: string;
  /** Se devolver uma Promise (ex.: `mutation.mutateAsync`), o botão fica em espera até ela terminar. */
  onClick?: (event: MouseEvent<HTMLButtonElement>) => unknown;
  ref?: Ref<HTMLButtonElement>;
}

interface WidthLock { width: number; paddingLeft: number; paddingRight: number; hasLeadingIcon: boolean }

const spin = keyframes`
  to { transform: rotate(360deg); }
`;

const Root = styled.button`
  &[aria-busy='true'] {
    cursor: progress;
  }

  /* O spinner ocupa o lugar do ícone inicial, para o texto não andar. */
  &[aria-busy='true'] > [data-action-spinner] + svg {
    display: none;
  }

  > [data-action-spinner] {
    flex: none;
    width: 1em;
    height: 1em;
    animation: ${spin} 0.9s linear infinite;
  }

  @media (prefers-reduced-motion: reduce) {
    > [data-action-spinner] { animation-duration: 2.4s; }
  }
`;

// &&& sobe a especificidade: regras da tela como `Card span { ... }` não podem mostrar o anúncio.
const LiveRegion = styled.span`
  &&& {
    position: absolute;
    display: block;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }
`;

const SlowNotice = styled.span`
  align-self: center;
  color: ${({ theme }) => theme.color.textMutedAccessible};
  font-size: ${({ theme }) => theme.fontSize.xs};
  white-space: nowrap;
`;

const SPINNER_SPACE_PX = 20;

function isThenable(value: unknown): value is PromiseLike<unknown> {
  return typeof value === 'object' && value !== null && typeof (value as PromiseLike<unknown>).then === 'function';
}

function hasLeadingIcon(button: HTMLButtonElement) {
  for (const node of button.childNodes) {
    if (node.nodeType === Node.TEXT_NODE && !node.textContent?.trim()) continue;
    return node instanceof SVGElement;
  }
  return false;
}

function measure(button: HTMLButtonElement): WidthLock | null {
  const width = button.offsetWidth;
  if (!width) return null;
  const style = window.getComputedStyle(button);
  return {
    width,
    paddingLeft: Number.parseFloat(style.paddingLeft) || 0,
    paddingRight: Number.parseFloat(style.paddingRight) || 0,
    hasLeadingIcon: hasLeadingIcon(button),
  };
}

function lockedStyle(lock: WidthLock, style: CSSProperties | undefined): CSSProperties {
  const base: CSSProperties = { ...style, width: lock.width, minWidth: lock.width, boxSizing: 'border-box', justifyContent: 'center' };
  if (lock.hasLeadingIcon) return base;
  // Sem ícone para trocar: o spinner usa parte do respiro lateral e a largura fica a mesma.
  const take = SPINNER_SPACE_PX / 2;
  return { ...base, paddingLeft: Math.max(0, lock.paddingLeft - take), paddingRight: Math.max(0, lock.paddingRight - take) };
}

/**
 * Botão de ação com o loading padrão do Prisma (HU-0001).
 *
 * Herda a aparência do botão da tela pelo `as` do Styled Components:
 * `<Primary as={ActionButton} loading={mutation.isPending} onClick={...}>Salvar</Primary>`.
 *
 * Na espera: spinner ao lado do texto (que não muda) sem alterar a largura, `aria-busy`,
 * anúncio para leitor de tela, nenhum clique novo (nem Enter no formulário) e, depois de
 * 10 s, o aviso "Ainda processando...". O botão não recebe `disabled` durante a espera
 * (usa `aria-disabled`), para não perder o foco nem cancelar o envio do formulário.
 */
export function ActionButton({
  loading,
  loadingLabel = 'Processando, aguarde.',
  onClick,
  children,
  style,
  ref,
  'aria-disabled': ariaDisabled,
  ...props
}: ActionButtonProps) {
  const [pending, setPending] = useState(false);
  const [awaitingLoading, setAwaitingLoading] = useState(false);
  const [slow, setSlow] = useState(false);
  const [lock, setLock] = useState<WidthLock | null>(null);
  const inFlight = useRef(false);
  const mounted = useRef(true);
  const busy = Boolean(loading) || pending || awaitingLoading;

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  useEffect(() => {
    if (!awaitingLoading) return;
    if (loading) { setAwaitingLoading(false); return; }
    const timer = window.setTimeout(() => setAwaitingLoading(false), LOADING_HANDOFF_MS);
    return () => window.clearTimeout(timer);
  }, [awaitingLoading, loading]);

  useEffect(() => {
    if (!busy) {
      inFlight.current = false;
      setLock(null);
      return;
    }
    const timer = window.setTimeout(() => setSlow(true), SLOW_ACTION_MS);
    return () => { window.clearTimeout(timer); setSlow(false); };
  }, [busy]);

  function handleClick(event: MouseEvent<HTMLButtonElement>) {
    if (busy || inFlight.current) {
      // Também barra o envio do formulário por Enter, que chega aqui como clique.
      event.preventDefault();
      return;
    }
    const measured = measure(event.currentTarget);
    const result = onClick?.(event);
    if (isThenable(result)) {
      inFlight.current = true;
      setLock(measured);
      setPending(true);
      Promise.resolve(result)
        // O erro é de quem chamou (onError da mutation, mensagem da tela); aqui só importa o fim.
        .catch(() => undefined)
        .finally(() => {
          inFlight.current = false;
          if (mounted.current) setPending(false);
        });
      return;
    }
    if (loading !== undefined && !event.defaultPrevented) {
      inFlight.current = true;
      setLock(measured);
      setAwaitingLoading(true);
    }
  }

  return (
    <>
      <Root
        {...props}
        ref={ref}
        style={busy && lock ? lockedStyle(lock, style) : style}
        aria-busy={busy || undefined}
        aria-disabled={busy || ariaDisabled || undefined}
        onClick={handleClick}
      >
        {busy && <Loader2 aria-hidden="true" focusable="false" data-action-spinner="" />}
        {children}
      </Root>
      <LiveRegion role="status">{busy ? loadingLabel : ''}</LiveRegion>
      {busy && slow && <SlowNotice role="status">{SLOW_ACTION_MESSAGE}</SlowNotice>}
    </>
  );
}
