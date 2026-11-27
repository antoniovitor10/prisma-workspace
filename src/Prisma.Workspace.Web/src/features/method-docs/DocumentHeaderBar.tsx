import styled from 'styled-components';
import type { FrontmatterEntry } from './frontmatter';
import { statusLabel } from './labels';

/** Faixa de metadados: o cabeçalho YAML vira pares rótulo/valor, nunca texto cru. */
const Strip = styled.dl`
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin: 0;
  padding: 10px 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surfaceSubtle};
  > div {
    display: inline-flex;
    align-items: baseline;
    gap: 5px;
    max-width: 100%;
    min-height: 26px;
    padding: 3px 8px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.surface};
  }
  dt {
    color: ${({ theme }) => theme.color.textMutedAccessible};
    font-size: 11px;
    font-weight: 800;
    letter-spacing: .03em;
    text-transform: uppercase;
  }
  dd {
    margin: 0;
    min-width: 0;
    overflow: hidden;
    color: ${({ theme }) => theme.color.text};
    font-size: 12.5px;
    font-weight: 700;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
`;

/** O título aparece no campo próprio; o resto do cabeçalho é só leitura. */
const HIDDEN_KEYS = new Set(['titulo']);

export function DocumentHeaderBar({ entries }: { entries: FrontmatterEntry[] }) {
  const visible = entries.filter(entry => entry.value && !HIDDEN_KEYS.has(entry.key));
  if (visible.length === 0) return null;
  return (
    <Strip aria-label="Metadados do documento">
      {visible.map((entry, index) => (
        <div key={`${entry.key}-${index}`}title={`${entry.key}: ${entry.value}`}>
          <dt>{entry.key}</dt>
          <dd>{entry.key === 'status' ? statusLabel(entry.value) : entry.value}</dd>
        </div>
      ))}
    </Strip>
  );
}
