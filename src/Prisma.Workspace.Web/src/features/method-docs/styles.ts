import styled from 'styled-components';

/** Peças visuais compartilhadas pelas abas de documento do método (só tokens do tema). */
export const Button = styled.button<{ $secondary?: boolean }>`
  display: inline-flex;
  min-height: 36px;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0 12px;
  border: 1px solid ${({ theme, $secondary }) => ($secondary ? theme.color.borderStrong : 'transparent')};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $secondary }) => ($secondary ? theme.color.surface : theme.color.brandControlBackground)};
  color: ${({ theme, $secondary }) => ($secondary ? theme.color.text : theme.color.onBrand)};
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
  &:focus-visible { outline: 2px solid ${({ theme }) => theme.color.accentBlueAccessible}; outline-offset: 2px; }
  &:disabled { opacity: .55; cursor: default; }
`;

export const Notice = styled.p`
  margin: 0;
  padding: 10px 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  line-height: 1.5;
`;

export const Alert = styled.p.attrs({ role: 'alert' })`
  margin: 0;
  padding: 10px 12px;
  border: 1px solid color-mix(in srgb, ${({ theme }) => theme.color.danger} 50%, ${({ theme }) => theme.color.surface});
  border-radius: ${({ theme }) => theme.radius.md};
  background: color-mix(in srgb, ${({ theme }) => theme.color.danger} 10%, ${({ theme }) => theme.color.surface});
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  line-height: 1.5;
`;

export const Muted = styled.p`
  margin: 0;
  color: ${({ theme }) => theme.color.textMutedAccessible};
  font-size: 12.5px;
  line-height: 1.5;
`;
