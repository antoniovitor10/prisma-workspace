import { CheckCheck, Layers3, X } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { BULK_ACTION } from './KanbanBulkActions';

const Bar = styled.section`
  display:flex;align-items:center;gap:9px;flex-wrap:wrap;padding:9px 12px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:10px;
  background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};
`;
const Selection = styled.label`
  display:inline-flex;align-items:center;gap:7px;font-size:13px;font-weight:800;
  color:${({theme})=>theme.color.text};cursor:pointer;
  input{width:15px;height:15px;accent-color:${({theme})=>theme.color.brand};}
`;
const Count = styled.span`
  display:inline-flex;align-items:center;gap:5px;padding:5px 8px;border-radius:999px;
  background:#e8f3ff;color:#1769aa;font-size:13px;font-weight:800;
`;
const Select = styled.select`
  height:32px;max-width:190px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};
  border-radius:7px;background:${({theme})=>theme.color.surface};font-size:13px;
  color:${({theme})=>theme.color.text};
`;
const Button = styled.button<{ $primary?: boolean }>`
  height:32px;display:inline-flex;align-items:center;gap:5px;padding:0 10px;
  border:1px solid ${({theme,$primary})=>$primary?'transparent':theme.color.border};border-radius:7px;
  background:${({theme,$primary})=>$primary?theme.color.brand:theme.color.surface};
  color:${({theme,$primary})=>$primary?theme.color.onBrand:theme.color.textMuted};
  font-size:13px;font-weight:800;
  &:disabled{opacity:.5;cursor:not-allowed;}
`;
const Feedback = styled.span<{ $error?: boolean }>`
  margin-left:auto;font-size:13px;color:${({$error,theme})=>$error?'#b42318':theme.color.textMuted};
`;

export interface BulkOption { id: string; label: string }

export function KanbanBulkToolbar({
  selectedCount,
  visibleCount,
  allVisibleSelected,
  stages,
  users,
  tags,
  sprints,
  busy,
  feedback,
  onToggleVisible,
  onClear,
  onApply
}: {
  selectedCount: number;
  visibleCount: number;
  allVisibleSelected: boolean;
  stages: BulkOption[];
  users: BulkOption[];
  tags: BulkOption[];
  sprints: BulkOption[];
  busy: boolean;
  feedback?: { message: string; error?: boolean } | null;
  onToggleVisible: () => void;
  onClear: () => void;
  onApply: (action: number, targetValue?: string, priority?: number) => Promise<void>;
}) {
  const [action, setAction] = useState<number>(BULK_ACTION.move);
  const [targetValue, setTargetValue] = useState('');
  const [priority, setPriority] = useState(1);

  useEffect(() => setTargetValue(''), [action]);

  const options = action === BULK_ACTION.move
    ? stages
    : action === BULK_ACTION.assign || action === BULK_ACTION.unassign
      ? users
      : action === BULK_ACTION.addTag || action === BULK_ACTION.removeTag
        ? tags
        : action === BULK_ACTION.sprint
          ? sprints
          : [];
  const requiresTarget = action !== BULK_ACTION.priority && action !== BULK_ACTION.sprint;
  const canApply = selectedCount > 0 && !busy && (!requiresTarget || !!targetValue);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!canApply) return;
    await onApply(
      action,
      action === BULK_ACTION.priority ? undefined : targetValue || undefined,
      action === BULK_ACTION.priority ? priority : undefined);
  };

  return (
    <Bar aria-label="Ações em massa do quadro">
      <Selection>
        <input
          type="checkbox"
          checked={allVisibleSelected && visibleCount > 0}
          onChange={onToggleVisible}
          aria-label="Selecionar tarefas visíveis"
        />
        Selecionar visíveis ({Math.min(visibleCount, 200)})
      </Selection>
      {selectedCount > 0 && (
        <>
          <Count><CheckCheck size={13}/>{selectedCount} selecionada(s)</Count>
          <form onSubmit={submit} style={{display:'contents'}}>
            <Select aria-label="Ação em massa" value={action} onChange={e=>setAction(Number(e.target.value))}>
              <option value={BULK_ACTION.move}>Mover para etapa</option>
              <option value={BULK_ACTION.assign}>Atribuir usuário</option>
              <option value={BULK_ACTION.unassign}>Remover usuário</option>
              <option value={BULK_ACTION.priority}>Alterar prioridade</option>
              <option value={BULK_ACTION.addTag}>Adicionar tag</option>
              <option value={BULK_ACTION.removeTag}>Remover tag</option>
              <option value={BULK_ACTION.sprint}>Definir sprint</option>
            </Select>
            {action === BULK_ACTION.priority ? (
              <Select aria-label="Prioridade" value={priority} onChange={e=>setPriority(Number(e.target.value))}>
                <option value={0}>Baixa</option>
                <option value={1}>Média</option>
                <option value={2}>Alta</option>
                <option value={3}>Crítica</option>
              </Select>
            ) : (
              <Select aria-label="Destino da ação" value={targetValue} onChange={e=>setTargetValue(e.target.value)}>
                <option value="">{action === BULK_ACTION.sprint ? 'Product Backlog' : 'Selecione o destino'}</option>
                {options.map(option=><option key={option.id} value={option.id}>{option.label}</option>)}
              </Select>
            )}
            <Button $primary disabled={!canApply} type="submit">
              <Layers3 size={13}/>{busy ? 'Aplicando…' : 'Aplicar'}
            </Button>
          </form>
          <Button type="button" onClick={onClear} disabled={busy}><X size={13}/>Limpar seleção</Button>
        </>
      )}
      {feedback && <Feedback role="status" aria-live="polite" $error={feedback.error}>{feedback.message}</Feedback>}
    </Bar>
  );
}
