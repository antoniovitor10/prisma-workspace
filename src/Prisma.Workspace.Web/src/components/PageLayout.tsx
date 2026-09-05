import styled from 'styled-components';

// O AppShell já oferece o landmark principal. Páginas reutilizam apenas o contêiner visual
// para manter a semântica HTML válida e um único destino para leitores de tela/atalhos.
export const Page = styled.div`
  width: 100%;
  max-width: 1540px;
  margin: 0 auto;
  padding: clamp(20px, 2.4vw, 36px) clamp(16px, 2.8vw, 42px) 64px;

  @media (max-width: 720px) {
    padding: 18px 14px 44px;
  }
`;

export const PageHeader = styled.header`
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 22px;

  @media (max-width: 720px) {
    align-items: stretch;
    flex-direction: column;
    gap: 14px;
  }
`;

export const HeaderLead = styled.div`
  min-width: 0;

  .eyebrow {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    margin-bottom: 7px;
    color: ${({ theme }) => theme.color.brand};
    font-size: 11px;
    font-weight: 800;
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }

  h1 {
    color: ${({ theme }) => theme.color.text};
    font-size: clamp(24px, 2.2vw, 32px);
    font-weight: 800;
    letter-spacing: -0.035em;
    line-height: 1.15;
  }

  p {
    max-width: 720px;
    margin-top: 7px;
    color: ${({ theme }) => theme.color.textMuted};
    font-size: 14px;
    line-height: 1.55;
  }
`;

export const HeaderActions = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 9px;

  @media (max-width: 720px) {
    justify-content: stretch;
    > * { flex: 1 1 auto; }
  }
`;

export const ToolbarSurface = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 9px;
  padding: 9px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
`;

export const Surface = styled.section`
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
`;

export const EmptyState = styled.div`
  display: grid;
  min-height: 220px;
  place-items: center;
  padding: 38px 24px;
  color: ${({ theme }) => theme.color.textMuted};
  text-align: center;
  font-size: 13.5px;
`;

export const PrimaryAction = styled.button`
  display: inline-flex;
  min-height: 40px;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 0 15px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.gradient};
  color: #fff;
  box-shadow: 0 7px 18px rgba(79, 70, 229, 0.2);
  font-size: 13px;
  font-weight: 800;
  transition: transform 160ms ease, box-shadow 160ms ease;

  &:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: 0 10px 24px rgba(219, 39, 119, 0.24);
  }

  &:disabled { cursor: not-allowed; opacity: 0.55; }
`;

export const SecondaryAction = styled.button`
  display: inline-flex;
  min-height: 40px;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 0 13px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  font-weight: 700;

  &:hover { border-color: ${({ theme }) => theme.color.borderStrong}; background: ${({ theme }) => theme.color.surfaceSubtle}; }
`;

export const SearchControl = styled.label`
  display: inline-flex;
  min-height: 40px;
  min-width: min(280px, 42vw);
  align-items: center;
  gap: 9px;
  padding: 0 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  color: ${({ theme }) => theme.color.textMuted};

  input {
    width: 100%;
    min-width: 0;
    border: 0;
    outline: 0;
    background: transparent;
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
  }

  &:focus-within {
    border-color: ${({ theme }) => theme.color.brand};
    box-shadow: 0 0 0 3px color-mix(in srgb, ${({ theme }) => theme.color.brand} 12%, transparent);
  }

  @media (max-width: 720px) { min-width: 100%; }
`;
