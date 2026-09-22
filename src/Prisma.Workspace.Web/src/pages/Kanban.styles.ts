// Styled-components da tela Kanban (extraidos de Kanban.tsx).
import styled, { keyframes } from 'styled-components';
import { Play } from 'lucide-react';

export const pulse = keyframes`
  0% { box-shadow: 0 0 0 0 rgba(37, 99, 235, 0.4); }
  70% { box-shadow: 0 0 0 10px rgba(37, 99, 235, 0); }
  100% { box-shadow: 0 0 0 0 rgba(37, 99, 235, 0); }
`;

export const AppLayout = styled.div`
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  min-width: 0;
  width: 100%;
  max-width: 100vw;
  overflow-x: clip;
  box-sizing: border-box;
  background-color: ${props => props.theme.color.bg};
`;

// Fica aninhado dentro do <main> do AppShell. Precisa ser um contêiner neutro:
// dois landmarks main na mesma página quebram a navegação por leitor de tela.
export const MainContent = styled.div`
  flex: 1;
  min-width: 0;
  width: 100%;
  max-width: 100%;
  overflow-x: clip;
  box-sizing: border-box;
  padding: 20px clamp(14px, 2.2vw, 32px) 32px;
  display: flex;
  flex-direction: column;
  gap: ${props => props.theme.space[4]};
`;

export const BoardHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: ${props => props.theme.space[3]};
  padding: 3px 0 5px;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  box-sizing: border-box;

  @media (max-width: 640px) {
    flex-direction: column;
    align-items: stretch;
  }
`;

export const SelectorContainer = styled.div`
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: ${props => props.theme.space[3]};
  min-width: 0;
  max-width: 100%;
`;

export const BoardHelp = styled.p`
  margin: -8px 0 0;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12.5px;
  line-height: 1.45;
`;

export const BoardActions = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  min-width: 0;
  max-width: 100%;
  width: auto;

  @media (max-width: 640px) {
    width: 100%;
  }
`;

export const ViewSwitcher = styled.div`
  display: flex;
  flex-wrap: wrap;
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  overflow: hidden;
  max-width: 100%;
  min-width: 0;
`;

export const ViewSwitcherButton = styled.button<{ $active?: boolean }>`
  padding: 8px 14px;
  font-size: 15px;
  font-weight: 700;
  background: ${p => (p.$active ? p.theme.color.brandControlBackground : p.theme.color.surface)};
  color: ${p => (p.$active ? p.theme.color.onBrand : p.theme.color.textMutedAccessible)};
  border: none;
  cursor: pointer;

  @media (max-width: 640px) {
    padding: 8px 10px;
    font-size: 13px;
  }
`;

export const Select = styled.select`
  background: ${props => props.theme.color.surface};
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.md};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[4]};
  color: ${props => props.theme.color.textMutedAccessible};
  font-size: ${props => props.theme.fontSize.sm};
  outline: none;
  cursor: pointer;
  transition: ${props => props.theme.transition};
  max-width: 100%;
  min-width: 0;

  &:focus {
    border-color: ${props => props.theme.color.accentBlue};
  }
`;

export const ActionButton = styled.button`
  background: ${props => props.theme.color.surface};
  border: 1px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.text};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[4]};
  border-radius: ${props => props.theme.radius.md};
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.medium};
  display: flex;
  align-items: center;
  gap: ${props => props.theme.space[2]};
  transition: ${props => props.theme.transition};
  box-shadow: ${props => props.theme.shadow.sm};
  flex: 0 1 auto;
  min-width: 0;
  white-space: nowrap;

  &:hover {
    background: ${props => props.theme.color.neutral[50]};
    border-color: ${props => props.theme.color.accentBlue};
  }
`;

export const AddCardButton = styled.button`
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: ${props => props.theme.space[2]};
  padding: ${props => props.theme.space[2.5]};
  color: ${props => props.theme.color.textMuted};
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.medium};
  border-radius: ${props => props.theme.radius.md};
  border: 1px dashed ${props => props.theme.color.border};
  background: transparent;
  transition: ${props => props.theme.transition};

  &:hover {
    background: rgba(22, 65, 148, 0.04);
    color: ${props => props.theme.color.brand};
    border-color: ${props => props.theme.color.brand};
  }
`;

export const KanbanGrid = styled.div`
  display: flex;
  gap: 14px;
  overflow-x: auto;
  align-items: flex-start;
  padding-bottom: ${props => props.theme.space[3]};
  flex: 1;
  min-width: 0;
  width: 100%;
  max-width: 100%;
`;

export const Column = styled.div<{ $isDropTarget?: boolean; $isDraggingAny?: boolean }>`
  position: relative;
  overflow: hidden;
  background: ${props => props.theme.color.surfaceSubtle};
  border: 1px solid ${props => props.$isDropTarget ? props.theme.color.accentBlue : props.theme.color.border};
  border-radius: ${props => props.theme.radius.card};
  width: 310px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 220px);
  box-shadow: ${props => props.theme.shadow.sm};
  background: ${props => props.$isDropTarget ? props.theme.color.surface : props.theme.color.surfaceSubtle};

  &::before {
    content: '';
    position: absolute;
    inset: 0 0 auto;
    z-index: 1;
    height: 2px;
    background: ${props => props.theme.color.gradient};
    opacity: ${props => props.$isDropTarget ? 1 : .35};
  }
`;

export const ColumnHeader = styled.div`
  min-height: 52px;
  padding: 13px 15px;
  border-bottom: 1px solid ${props => props.theme.color.border};
  display: flex;
  justify-content: space-between;
  align-items: center;
`;

export const ColumnTitle = styled.h3`
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.bold};
  color: ${props => props.theme.color.text};
`;

export const CardCount = styled.span`
  background: ${props => props.theme.color.neutral[200]};
  border: 1px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.textMuted};
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.bold};
  padding: 2px 8px;
  border-radius: ${props => props.theme.radius.pill};
`;

export const CardList = styled.div`
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  overflow-y: auto;
  flex: 1;
`;

export const Card = styled.div<{ $isRunning?: boolean; $isDragging?: boolean; $priority: number; $late?: boolean }>`
  background: ${props => props.$late ? `color-mix(in srgb, ${props.theme.color.danger} 6%, ${props.theme.color.surface})` : props.theme.color.surface};
  border: 1px solid ${props => props.$isRunning ? props.theme.color.accentBlue : props.theme.color.border};
  border-left: 3px solid ${props => {
    if (props.$priority >= 2) return props.theme.color.danger; // Vermelho
    if (props.$priority === 1) return '#FBBA00'; // Dourado
    return '#76B82A'; // Verde
  }};
  border-radius: ${props => props.theme.radius.md};
  padding: 13px 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  transition: ${props => props.theme.transition};
  box-shadow: ${props => props.theme.shadow.sm};
  animation: ${props => props.$isRunning ? pulse : 'none'} 2s infinite;
  cursor: grab;
  opacity: ${props => props.$isDragging ? 0.55 : 1};
  user-select: none;
  touch-action: none;

  &:hover {
    transform: translateY(-2px);
    border-color: ${props => props.$isRunning ? props.theme.color.accentBlue : props.theme.color.neutral[300]};
    box-shadow: ${props => props.theme.shadow.md};
  }

  &:active {
    cursor: grabbing;
  }
`;

export const CardTitle = styled.h4`
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.bold};
  color: ${props => props.theme.color.text};
  line-height: 1.4;
`;

export const CardSubtitle = styled.div`
  font-size: ${props => props.theme.fontSize.xs};
  color: ${props => props.theme.color.textMuted};
  margin-top: -4px;
`;

export const CardEstimate = styled.div`
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 13.5px;
  color: ${props => props.theme.color.textMuted};
`;

export const DragPreview = styled.div<{ $priority: number }>`
  position: fixed;
  left: 0;
  top: 0;
  width: 280px;
  z-index: 2000;
  pointer-events: none;
  transform: translate(-50%, -50%) rotate(1deg);
  background: ${props => props.theme.color.surface};
  border: 1px solid ${props => props.theme.color.accentBlue};
  border-left: 5px solid ${props => {
    if (props.$priority === 2) return props.theme.color.danger;
    if (props.$priority === 1) return '#FBBA00';
    return '#76B82A';
  }};
  border-radius: ${props => props.theme.radius.md};
  padding: ${props => props.theme.space[3]} ${props => props.theme.space[4]};
  box-shadow: ${props => props.theme.shadow.lg};
  color: ${props => props.theme.color.text};
`;

export const DragPreviewTitle = styled.div`
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.bold};
  line-height: 1.35;
`;

export const DragPreviewSubtitle = styled.div`
  color: ${props => props.theme.color.textMuted};
  font-size: ${props => props.theme.fontSize.xs};
  margin-top: 4px;
`;

export const CardMeta = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: ${props => props.theme.fontSize.xs};
  color: ${props => props.theme.color.textMuted};
  flex-wrap: wrap;
  gap: ${props => props.theme.space[2]};
`;

export const Tag = styled.span<{ $priority: number }>`
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.bold};
  padding: 2px 10px;
  border-radius: ${props => props.theme.radius.pill};
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: ${props => {
    if (props.$priority === 2) return 'rgba(217, 45, 32, 0.1)';
    if (props.$priority === 1) return 'rgba(251, 186, 0, 0.1)';
    return 'rgba(118, 184, 42, 0.1)';
  }};
  color: ${props => {
    if (props.$priority === 2) return props.theme.color.danger;
    if (props.$priority === 1) return props.theme.color.warning;
    return props.theme.color.success;
  }};

  &::before {
    content: '';
    display: inline-block;
    width: 6px;
    height: 6px;
    border-radius: 50%;
    background-color: currentColor;
  }
`;

export const CardActions = styled.div`
  border-top: 1px solid ${props => props.theme.color.border};
  padding-top: ${props => props.theme.space[3]};
  display: flex;
  justify-content: space-between;
  align-items: center;
  opacity: 0.15;
  transition: opacity 0.2s ease-in-out;

  ${Card}:hover & {
    opacity: 1;
  }

  @media (max-width: 768px) {
    opacity: 1;
  }
`;

export const ActionIcon = styled.button<{ $color?: string }>`
  color: ${props => props.$color || props.theme.color.textMuted};
  transition: ${props => props.theme.transition};
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 6px;
  border-radius: ${props => props.theme.radius.sm};

  &:hover {
    background: ${props => props.theme.color.neutral[100]};
    color: ${props => props.$color || props.theme.color.text};
  }

  &:disabled {
    opacity: 0.3;
    cursor: not-allowed;
  }
`;

export const ModalOverlay = styled.div`
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
`;

export const Modal = styled.div<{ wide?: boolean }>`
  background: ${props => props.theme.color.surface};
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.card};
  width: 100%;
  max-width: ${props => props.wide ? '760px' : '480px'};
  max-height: calc(100vh - 48px);
  overflow-y: auto;
  padding: ${props => props.theme.space[6]};
  display: flex;
  flex-direction: column;
  gap: ${props => props.theme.space[4]};
  box-shadow: ${props => props.theme.shadow.lg};
`;

export const ModalTitle = styled.h3`
  font-size: ${props => props.theme.fontSize.lg};
  font-weight: ${props => props.theme.fontWeight.bold};
  color: ${props => props.theme.color.text};
  font-family: ${props => props.theme.font.display};
`;

export const DetailText = styled.div`
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  line-height: 1.5;
  white-space: pre-wrap;
`;

export const SmallButton = styled.button`
  border: 1px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.text};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[3]};
  border-radius: ${props => props.theme.radius.md};
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.bold};
  display: inline-flex;
  align-items: center;
  gap: ${props => props.theme.space[1]};
  transition: ${props => props.theme.transition};

  &:hover {
    background: ${props => props.theme.color.neutral[100]};
    border-color: ${props => props.theme.color.accentBlue};
  }

  &:disabled {
    cursor: not-allowed;
    opacity: 0.45;
  }
`;

export const ModalForm = styled.form`
  display: flex;
  flex-direction: column;
  gap: ${props => props.theme.space[4]};
`;

export const FormRow = styled.div`
  display: flex;
  gap: ${props => props.theme.space[4]};

  & > * {
    flex: 1;
  }
`;

export const Input = styled.input`
  background: ${props => props.theme.color.neutral[50]};
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.md};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[3]};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  outline: none;
  transition: ${props => props.theme.transition};

  &:focus {
    border-color: ${props => props.theme.color.accentBlue};
    background-color: ${props => props.theme.color.surface};
  }
`;

export const Textarea = styled.textarea`
  background: ${props => props.theme.color.neutral[50]};
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.md};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[3]};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  outline: none;
  min-height: 80px;
  resize: vertical;
  transition: ${props => props.theme.transition};

  &:focus {
    border-color: ${props => props.theme.color.accentBlue};
    background-color: ${props => props.theme.color.surface};
  }
`;

export const ModalActions = styled.div`
  display: flex;
  justify-content: flex-end;
  gap: ${props => props.theme.space[3]};
  margin-top: ${props => props.theme.space[2]};
`;

export const SubmitButton = styled.button`
  background: ${props => props.theme.color.brand};
  color: ${props => props.theme.color.onBrand};
  font-weight: ${props => props.theme.fontWeight.bold};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[4]};
  border-radius: ${props => props.theme.radius.md};
  font-size: ${props => props.theme.fontSize.sm};
  box-shadow: ${props => props.theme.shadow.sm};
  transition: ${props => props.theme.transition};

  &:hover {
    filter: brightness(1.1);
  }
`;

export const CancelButton = styled.button`
  border: 1px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.text};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[4]};
  border-radius: ${props => props.theme.radius.md};
  font-size: ${props => props.theme.fontSize.sm};
  transition: ${props => props.theme.transition};

  &:hover {
    background: ${props => props.theme.color.neutral[50]};
  }
`;

export const TimerWrapper = styled.div<{ $isRunning?: boolean }>`
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.bold};
  display: flex;
  align-items: center;
  gap: ${props => props.theme.space[1]};
  color: ${props => props.$isRunning ? props.theme.color.accentBlue : props.theme.color.textMuted};
  background: ${props => props.$isRunning ? 'rgba(0, 142, 207, 0.08)' : 'transparent'};
  padding: ${props => props.$isRunning ? '2px 6px' : '0'};
  border-radius: 4px;
`;

export const PlayIcon = styled(Play)`
  fill: ${props => props.theme.color.success};
  stroke: ${props => props.theme.color.success};
`;

export const LeadTimeTable = styled.table`
  width: 100%;
  border-collapse: collapse;
  margin-top: ${props => props.theme.space[3]};
`;

export const LeadTimeTh = styled.th`
  text-align: left;
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[3]};
  border-bottom: 2px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.bold};
`;

export const LeadTimeTd = styled.td`
  padding: ${props => props.theme.space[3]};
  border-bottom: 1px solid ${props => props.theme.color.border};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
`;

export const TempoGrid = styled.div`
  display: flex;
  gap: ${p => p.theme.space[3]};
  justify-content: center;
  align-items: flex-end;
  padding: ${p => p.theme.space[8]} ${p => p.theme.space[2]} ${p => p.theme.space[2]};
  flex-wrap: nowrap;
  overflow-x: auto;
`;

export const TempoDay = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: ${p => p.theme.space[2]};
  width: 84px;
  flex: 0 0 auto;
`;

export const TempoDayLabel = styled.span`
  font-size: ${p => p.theme.fontSize.xs};
  color: ${p => p.theme.color.textMuted};
  white-space: nowrap;
`;

export const TempoBarTrack = styled.div`
  position: relative;
  width: 54px;
  height: 180px;
  background: #eef1f5;
  border-radius: ${p => p.theme.radius.md};
  display: flex;
  align-items: flex-end;
`;

export const TempoBarFill = styled.div<{ $pct: number; $today?: boolean }>`
  width: 100%;
  height: ${p => Math.max(p.$pct, 0)}%;
  min-height: ${p => (p.$pct > 0 ? '28px' : '0')};
  background: ${p => (p.$today ? '#2f855a' : '#9ae6b4')};
  color: ${p => (p.$today ? '#ffffff' : '#22543d')};
  border-radius: ${p => p.theme.radius.md};
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding-bottom: 6px;
  font-size: ${p => p.theme.fontSize.xs};
  font-weight: 600;
  transition: height 0.3s ease;
`;

export const TempoEmptyHours = styled.span`
  position: absolute;
  bottom: 8px;
  left: 0;
  right: 0;
  text-align: center;
  font-size: ${p => p.theme.fontSize.xs};
  color: ${p => p.theme.color.textMuted};
`;

export const TempoOverBadge = styled.span`
  position: absolute;
  top: -15px;
  left: 50%;
  transform: translateX(-50%);
  background: #2f855a;
  color: #ffffff;
  font-size: 13px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 6px 6px 0 0;
  white-space: nowrap;
`;

export const TempoPct = styled.span<{ $over?: boolean }>`
  font-size: ${p => p.theme.fontSize.xs};
  color: ${p => (p.$over ? '#2f855a' : p.theme.color.textMuted)};
  font-weight: 600;
  background: #edf2f7;
  padding: 2px 10px;
  border-radius: 999px;
`;

export const TempoAdjust = styled.button`
  background: none;
  border: none;
  color: #2b6cb0;
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  padding: 2px 4px;
  &:hover { text-decoration: underline; }
`;

export const TempoTodayBadge = styled.span`
  position: absolute;
  top: -24px;
  left: 50%;
  transform: translateX(-50%);
  background: #2b6cb0;
  color: #ffffff;
  font-size: 13px;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 999px;
`;
