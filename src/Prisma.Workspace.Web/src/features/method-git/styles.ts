import styled from 'styled-components';

export const Surface = styled.section`
  min-width: 0;
  color: ${({ theme }) => theme.color.text};
  h2 { margin: 0 0 8px; font-size: 20px; line-height: 1.3; }
  h3 { margin: 0 0 8px; font-size: 16px; line-height: 1.4; }
  p { margin: 0 0 16px; max-width: 72ch; line-height: 1.6; }
  a { color: ${({ theme }) => theme.color.brandAccessible}; text-underline-offset: 3px; overflow-wrap: anywhere; }
  button, input, select, textarea { font: inherit; }
  button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible, a:focus-visible {
    outline: 2px solid ${({ theme }) => theme.color.brandAccessible}; outline-offset: 3px;
  }
`;
export const Header = styled.div`
  display: flex; align-items: flex-start; justify-content: space-between; flex-wrap: wrap; gap: 16px;
  margin-bottom: 20px;
`;
export const Hint = styled.p`color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 13px;`;
export const Form = styled.form`
  display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin-bottom: 24px;
  fieldset { grid-column: 1 / -1; border: 0; padding: 0; margin: 0; min-width: 0; display: grid; grid-template-columns: subgrid; gap: 16px; }
  @media (max-width: 640px) { grid-template-columns: minmax(0, 1fr); }
`;
export const Field = styled.label`
  display: grid; gap: 6px; min-width: 0; align-content: start; font-size: 13px; font-weight: 600;
  input, select, textarea {
    width: 100%; min-width: 0; min-height: 44px; padding: 10px 12px; box-sizing: border-box;
    border: 1px solid ${({ theme }) => theme.color.borderStrong}; border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
    &::placeholder { color: ${({ theme }) => theme.color.textMutedAccessible}; }
    &:disabled { opacity: .65; cursor: not-allowed; }
  }
  small { font-weight: 400; line-height: 1.5; color: ${({ theme }) => theme.color.textMutedAccessible}; }
`;
export const Actions = styled.div`display: flex; flex-wrap: wrap; align-items: center; gap: 10px; grid-column: 1 / -1;`;
export const Button = styled.button<{ $primary?: boolean }>`
  min-height: 44px; max-width: 100%; padding: 9px 14px; white-space: normal; cursor: pointer; font-weight: 600;
  border: 1px solid ${({ theme, $primary }) => $primary ? theme.color.brandControlBackground : theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $primary }) => $primary ? theme.color.brandControlBackground : theme.color.surface};
  color: ${({ theme, $primary }) => $primary ? theme.color.neutral[0] : theme.color.text};
  ${({ $primary }) => $primary && 'color: #FFFFFF;'}
  &:hover:not(:disabled) { filter: brightness(.95); }
  &:disabled { opacity: .6; cursor: not-allowed; }
`;
export const Feedback = styled.div`
  padding: 12px; margin-bottom: 16px; overflow-wrap: anywhere; line-height: 1.5;
  border: 1px solid ${({ theme }) => theme.color.borderStrong}; border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
`;
export const List = styled.ul`list-style: none; margin: 0; padding: 0;`;
export const Row = styled.li`
  padding: 16px 0; border-top: 1px solid ${({ theme }) => theme.color.border}; min-width: 0; overflow-wrap: anywhere;
  &:first-child { border-top: 0; }
  > div:first-child { display: flex; align-items: flex-start; flex-wrap: wrap; justify-content: space-between; gap: 12px; }
  strong { display: block; margin-bottom: 4px; }
  small { display: block; color: ${({ theme }) => theme.color.textMutedAccessible}; font-size: 12px; line-height: 1.6; }
  time { font-variant-numeric: tabular-nums; }
`;
export const Navigation = styled.nav`
  display: flex; flex-wrap: wrap; gap: 8px; padding-bottom: 16px; margin-bottom: 24px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  button[aria-pressed="true"] { border-color: ${({ theme }) => theme.color.brandAccessible}; color: ${({ theme }) => theme.color.brandAccessible}; }
`;
export const Secret = styled.div`
  margin-bottom: 24px; padding: 16px; border: 1px solid ${({ theme }) => theme.color.brandAccessible};
  border-radius: ${({ theme }) => theme.radius.md}; background: ${({ theme }) => theme.color.surface};
  textarea { resize: vertical; font-family: ${({ theme }) => theme.font.mono}; overflow-wrap: anywhere; }
`;
