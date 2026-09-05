import { useEffect, useState, useCallback } from 'react';
import { api } from '../../services/api';

interface ChecklistItem { id: string; text: string; done: boolean; position: number; }

interface Props {
  workItemId: string;
  onChanged?: () => void;
}

const label: React.CSSProperties = { fontSize: 14, fontWeight: 700, color: '#64748B', textTransform: 'uppercase', letterSpacing: 0.4 };

export const TaskChecklistPanel: React.FC<Props> = ({ workItemId, onChanged }) => {
  const [items, setItems] = useState<ChecklistItem[]>([]);
  const [text, setText] = useState('');

  const load = useCallback(async () => {
    try { setItems(await api.getChecklist(workItemId)); } catch { /* silencioso */ }
  }, [workItemId]);

  useEffect(() => { load(); }, [load]);

  const add = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return;
    await api.addChecklistItem(workItemId, text.trim());
    setText('');
    await load();
    onChanged?.();
  };

  const toggle = async (item: ChecklistItem) => {
    await api.toggleChecklistItem(workItemId, item.id, !item.done);
    await load();
    onChanged?.();
  };

  const remove = async (item: ChecklistItem) => {
    await api.deleteChecklistItem(workItemId, item.id);
    await load();
    onChanged?.();
  };

  const done = items.filter(i => i.done).length;

  return (
    <div style={{ padding: '14px 0', borderBottom: '1px solid #edf2f7' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
        <span style={label}>Checklist</span>
        {items.length > 0 && <span style={{ fontSize: 14, fontWeight: 700, color: done === items.length ? '#10B981' : '#64748B' }}>{done}/{items.length}</span>}
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {items.map(item => (
          <div key={item.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <input type="checkbox" checked={item.done} onChange={() => toggle(item)} />
            <span style={{ flex: 1, fontSize: 15, textDecoration: item.done ? 'line-through' : 'none', color: item.done ? '#94A3B8' : '#334155' }}>{item.text}</span>
            <button type="button" onClick={() => remove(item)} style={{ color: '#CBD5E1', fontSize: 15, lineHeight: 1 }}>×</button>
          </div>
        ))}
        {items.length === 0 && <span style={{ fontSize: 14, color: '#94A3B8' }}>Nenhum item ainda.</span>}
      </div>
      <form onSubmit={add} style={{ display: 'flex', gap: 6, marginTop: 10 }}>
        <input style={{ flex: 1, padding: '7px 8px', borderRadius: 6, border: '1px solid #cbd5e1', fontSize: 15 }} placeholder="Novo item..." value={text} onChange={e => setText(e.target.value)} />
        <button type="submit" style={{ padding: '7px 12px', borderRadius: 6, background: '#1E7BD7', color: '#fff', fontWeight: 700, fontSize: 14 }}>+ Add</button>
      </form>
    </div>
  );
};
