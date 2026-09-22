import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import styled from 'styled-components';

interface ContextBarContextType {
  target: HTMLDivElement | null;
  setTarget: (el: HTMLDivElement | null) => void;
}

const ContextBarContext = createContext<ContextBarContextType>({
  target: null,
  setTarget: () => {},
});

export function ContextBarProvider({ children }: { children: ReactNode }) {
  const [target, setTarget] = useState<HTMLDivElement | null>(null);
  return (
    <ContextBarContext.Provider value={{ target, setTarget }}>
      {children}
    </ContextBarContext.Provider>
  );
}

const Bar = styled.nav`
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-height: 46px;
  padding: 0 clamp(12px, 2vw, 28px);
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  background: color-mix(in srgb, ${({ theme }) => theme.color.surfaceSubtle} 94%, transparent);
  box-shadow: 0 5px 18px rgba(15, 23, 42, 0.035);
  font-size: 13px;

  &:empty { display: none; min-height: 0; }

  @media (max-width: 640px) { padding: 0 12px; }
`;

export function ContextBarSlot() {
  const { setTarget } = useContext(ContextBarContext);
  // useCallback makes the ref stable so it's called only on mount/unmount
  const refCb = useCallback(
    (el: HTMLDivElement | null) => setTarget(el),
    [setTarget],
  );
  return <Bar ref={refCb} aria-label="Navegação estrutural" />;
}

/**
 * Render children into the ContextBarSlot via portal.
 * Drop this anywhere inside a route to inject contextual content
 * (breadcrumbs, view switcher, filters) into the sticky context bar.
 */
export function ContextBarInjector({ children }: { children: ReactNode }) {
  const { target } = useContext(ContextBarContext);
  if (!target) return null;
  return createPortal(children, target);
}
