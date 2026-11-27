import styled from 'styled-components';
import { headerNoun, statusLabel } from './labels';
import type { MethodItemStatus } from './useMethodDocuments';

const Tag = styled.span`
  display: inline-flex;
  align-items: center;
  min-height: 18px;
  margin-left: 6px;
  padding: 0 7px;
  border: 1px solid ${({ theme }) => theme.color.borderStrong};
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.text};
  font-size: 10.5px;
  font-weight: 800;
  letter-spacing: .02em;
  white-space: nowrap;
  vertical-align: middle;
`;

const VisuallyHidden = styled.span`
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
`;

/** Etiqueta do status da HU (ou do épico) na linha do backlog. */
export function MethodStatusTag({ status }: { status: MethodItemStatus | undefined }) {
  if (!status?.status) return null;
  const noun = headerNoun[status.kind];
  return (
    <Tag data-method-status={status.status} title={`Status ${noun} ${status.key}`}>
      <VisuallyHidden>{`Status ${noun}: `}</VisuallyHidden>{statusLabel(status.status)}
    </Tag>
  );
}
