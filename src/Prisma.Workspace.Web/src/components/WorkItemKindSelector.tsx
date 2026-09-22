import React from 'react';
import styled from 'styled-components';
import { Layers, Sparkles, BookOpen, CheckSquare, Bug, ListTree } from 'lucide-react';
import { WorkItemKind, kindMeta } from '../features/workItems/workItemKinds';

export const KIND_ICONS: Record<number, React.ComponentType<{ size?: number; color?: string; style?: React.CSSProperties }>> = {
  [WorkItemKind.Epic]: Layers,
  [WorkItemKind.Feature]: Sparkles,
  [WorkItemKind.UserStory]: BookOpen,
  [WorkItemKind.Task]: CheckSquare,
  [WorkItemKind.Bug]: Bug,
  [WorkItemKind.Subtask]: ListTree,
};

export const KIND_COLORS: Record<number, string> = {
  [WorkItemKind.Epic]: '#7A5CC6',
  [WorkItemKind.Feature]: '#5B7CFA',
  [WorkItemKind.UserStory]: '#0EA5A5',
  [WorkItemKind.Task]: '#64748B',
  [WorkItemKind.Bug]: '#D92D20',
  [WorkItemKind.Subtask]: '#94A3B8',
};

export const DEFAULT_CREATE_KINDS: number[] = [
  WorkItemKind.Epic,
  WorkItemKind.Feature,
  WorkItemKind.UserStory,
  WorkItemKind.Task,
  WorkItemKind.Bug,
];

const Container = styled.div`
  position: relative;
  display: inline-flex;
  align-items: center;
  width: 100%;
`;

const SelectBadge = styled.div<{ $color: string }>`
  position: absolute;
  left: 10px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  pointer-events: none;
  font-size: 13px;
  font-weight: 700;
  color: ${({ $color }) => $color};
`;

const StyledSelect = styled.select`
  width: 100%;
  min-height: 38px;
  padding: 0 10px 0 34px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  font-size: 13.5px;
  outline: none;
  cursor: pointer;

  &:focus {
    border-color: ${({ theme }) => theme.color.accentBlue};
  }

  &:disabled {
    cursor: not-allowed;
    opacity: 0.6;
  }
`;

export interface WorkItemKindSelectorProps {
  value: number;
  onChange: (kind: number) => void;
  allowedKinds?: number[];
  ariaLabel?: string;
  id?: string;
  className?: string;
  style?: React.CSSProperties;
  disabled?: boolean;
}

export function WorkItemKindSelector({
  value,
  onChange,
  allowedKinds = DEFAULT_CREATE_KINDS,
  ariaLabel = 'Tipo de item',
  id,
  className,
  style,
  disabled = false,
}: WorkItemKindSelectorProps) {
  const meta = kindMeta(value);
  const color = KIND_COLORS[value] ?? meta.color;
  const IconComp = KIND_ICONS[value] ?? CheckSquare;

  return (
    <Container className={className} style={style}>
      <SelectBadge $color={color}>
        <IconComp size={15} color={color} />
      </SelectBadge>
      <StyledSelect
        id={id}
        aria-label={ariaLabel}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        {allowedKinds.map((k) => {
          const m = kindMeta(k);
          return (
            <option key={k} value={k}>
              {m.label}
            </option>
          );
        })}
      </StyledSelect>
    </Container>
  );
}
