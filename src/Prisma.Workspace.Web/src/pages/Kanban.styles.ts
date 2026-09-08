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
  background-color: ${props => props.theme.color.bg};
`;

// Fica aninhado dentro do <main> do AppShell. Precisa ser um contêiner neutro:
// dois landmarks main na mesma página quebram a navegação por leitor de tela.
export const MainContent = styled.div`
  flex: 1;
  padding: 20px clamp(14px, 2.2vw, 32px) 32px;
  display: flex;
  flex-direction: column;
  gap: ${props => props.theme.space[4]};
`;

export const BoardHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: ${props => props.theme.space[4]};
  padding: 3px 0 5px;
`;

export const SelectorContainer = styled.div`
  display: flex;
  align-items: center;
  gap: ${props => props.theme.space[3]};
`;

export const Select = styled.select`
  background: ${props => props.theme.color.surface};
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.md};
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[4]};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  outline: none;
  cursor: pointer;
  transition: ${props => props.theme.transition};

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

export const DetailSection = styled.div`
  display: flex;
  flex-direction: column;
  gap: ${props => props.theme.space[2]};
`;

export const DetailLabel = styled.div`
  font-size: ${props => props.theme.fontSize.xs};
  font-weight: ${props => props.theme.fontWeight.bold};
  text-transform: uppercase;
  color: ${props => props.theme.color.textMuted};
`;

export const DetailText = styled.div`
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  line-height: 1.5;
  white-space: pre-wrap;
`;

export const SectionHeader = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: ${props => props.theme.space[3]};
`;

export const SectionTitle = styled.h4`
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};
  font-weight: ${props => props.theme.fontWeight.bold};
  display: flex;
  align-items: center;
  gap: ${props => props.theme.space[2]};
`;

export const ListPanel = styled.div`
  border: 1px solid ${props => props.theme.color.border};
  border-radius: ${props => props.theme.radius.md};
  overflow: hidden;
  background-color: ${props => props.theme.color.neutral[50]};
`;

export const ListItem = styled.div`
  padding: ${props => props.theme.space[2]} ${props => props.theme.space[3]};
  border-bottom: 1px solid ${props => props.theme.color.border};
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: ${props => props.theme.space[3]};
  color: ${props => props.theme.color.text};
  font-size: ${props => props.theme.fontSize.sm};

  &:last-child {
    border-bottom: 0;
  }
`;

export const MutedText = styled.span`
  color: ${props => props.theme.color.textMuted};
  font-size: ${props => props.theme.fontSize.xs};
`;

export const InlineForm = styled.form`
  display: flex;
  gap: ${props => props.theme.space[2]};
  align-items: center;
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

export const FileInput = styled.input`
  color: ${props => props.theme.color.textMuted};
  font-size: ${props => props.theme.fontSize.xs};
  max-width: 220px;
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

export const TaskModal = styled.div`
  background: #ffffff;
  border-radius: 12px;
  width: min(1080px, 96vw);
  max-height: 92vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.25);
`;

export const TaskTopbar = styled.div`
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 20px;
  border-bottom: 1px solid #e2e8f0;
`;

export const TaskTimerBtn = styled.button<{ $running?: boolean }>`
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border: none;
  cursor: pointer;
  border-radius: 8px;
  padding: 8px 14px;
  font-weight: 700;
  font-size: 15px;
  color: #ffffff;
  background: ${p => (p.$running ? '#e53e3e' : '#2b6cb0')};
`;

export const TaskBody = styled.div`
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  /* Em telas estreitas (ou com zoom) as colunas empilham para não cortar a lateral. */
  @media (max-width: 1024px) { flex-direction: column; overflow-y: auto; }
`;

export const TaskMain = styled.div`
  flex: 1;
  min-width: 0;
  padding: 20px 24px;
  overflow-y: auto;
`;

export const TaskSidebar = styled.div`
  width: 340px;
  flex: 0 0 340px;
  border-left: 1px solid #e2e8f0;
  padding: 18px 20px;
  overflow-y: auto;
  /* Sem deslize lateral: a coluna é fixa e o conteúdo se ajusta à largura. */
  overflow-x: hidden;
  background: #fafbfc;
  & select, & input, & button { max-width: 100%; min-width: 0; }
  @media (max-width: 1024px) {
    width: 100%;
    flex: 1 1 auto;
    border-left: none;
    border-top: 1px solid #e2e8f0;
  }
`;

export const TaskH1 = styled.h2`
  margin: 0 0 4px;
  font-size: 22px;
  color: #1a202c;
`;

export const TaskMeta = styled.div`
  font-size: 14px;
  color: ${p => p.theme.color.textMuted};
  margin-bottom: 16px;
`;

export const SidebarRow = styled.div`
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid #edf2f7;
  font-size: 15px;
`;

export const SidebarLabel = styled.span`
  color: ${p => p.theme.color.textMuted};
  display: inline-flex;
  align-items: center;
  gap: 6px;
`;

export const SidebarValue = styled.span`
  color: #1a202c;
  font-weight: 600;
  text-align: right;
  min-width: 0;
  overflow-wrap: anywhere;
`;

export const Avatars = styled.div`
  display: flex;
  align-items: center;
`;

export const Avatar = styled.span`
  width: 30px;
  height: 30px;
  border-radius: 50%;
  background: #2b6cb0;
  color: #ffffff;
  font-size: 14px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 2px solid #ffffff;
  margin-left: -6px;
`;

export const ProgressTrack = styled.div`
  height: 8px;
  border-radius: 999px;
  background: #e2e8f0;
  overflow: hidden;
  margin-top: 6px;
`;

export const ProgressFill = styled.div<{ $pct: number }>`
  height: 100%;
  width: ${p => Math.min(100, Math.max(0, p.$pct))}%;
  background: #38a169;
`;

export const TaskTabs = styled.div`
  display: flex;
  gap: 4px;
  border-bottom: 1px solid #e2e8f0;
  margin-bottom: 16px;
`;

export const TaskTab = styled.button<{ $active?: boolean }>`
  padding: 8px 14px;
  font-size: 15px;
  font-weight: 700;
  color: ${p => (p.$active ? p.theme.color.brand : p.theme.color.textMuted)};
  border-bottom: 2px solid ${p => (p.$active ? p.theme.color.brand : 'transparent')};
  margin-bottom: -1px;
  &:hover { color: ${p => p.theme.color.text}; }
`;
