import { useEffect, useState, useCallback } from 'react';
import { api } from '../../services/api';

interface Catalog { id: string; name: string; color: string; }

interface Props {
  workItemId: string;
  taskTypeId?: string | null;
  points?: number | null;
  tagIds: string[];
  showPoints?: boolean;
  onChanged: () => void;
}

const box: React.CSSProperties = { padding: '14px 0', borderBottom: '1px solid #edf2f7' };
const label: React.CSSProperties = { fontSize: 14, fontWeight: 700, color: '#64748B', textTransform: 'uppercase', letterSpacing: 0.4, display: 'block', marginBottom: 8 };
const select: React.CSSProperties = { width: '100%', padding: '7px 8px', borderRadius: 6, border: '1px solid #cbd5e1', fontSize: 15 };

export const TaskTaxonomyPanel: React.FC<Props> = ({ workItemId, taskTypeId, points, tagIds, showPoints = true, onChanged }) => {
  const [types, setTypes] = useState<Catalog[]>([]);
  const [tags, setTags] = useState<Catalog[]>([]);
  const [newType, setNewType] = useState('');
  const [newTag, setNewTag] = useState('');

  const loadCatalogs = useCallback(async () => {
    try {
      const [t, g] = await Promise.all([api.getTaskTypes(), api.getTags()]);
      setTypes(t); setTags(g);
    } catch { /* silencioso */ }
  }, []);

  useEffect(() => { loadCatalogs(); }, [loadCatalogs]);

  const save = async (typeId: string | null, pts: number | null, tIds: string[]) => {
    await api.setTaxonomy(workItemId, typeId, pts, tIds);
    onChanged();
  };

  const toggleTag = async (id: string) => {
    const next = tagIds.includes(id) ? tagIds.filter(x => x !== id) : [...tagIds, id];
    await save(taskTypeId ?? null, points ?? null, next);
  };

  const createType = async () => {
    if (!newType.trim()) return;
    const created = await api.createTaskType(newType.trim(), '#1E7BD7');
    setNewType('');
    await loadCatalogs();
    await save(created.id, points ?? null, tagIds);
  };

  const createTag = async () => {
    if (!newTag.trim()) return;
    const palette = ['#EF4444', '#F59E0B', '#10B981', '#3B82F6', '#8B5CF6', '#EC4899'];
    const color = palette[Math.floor(Math.random() * palette.length)];
    const created = await api.createTag(newTag.trim(), color);
    setNewTag('');
    await loadCatalogs();
    await save(taskTypeId ?? null, points ?? null, [...tagIds, created.id]);
  };

  return (
    <>
      <div style={box}>
        <span style={label}>Tipo de tarefa</span>
        <select
          style={select}
          value={taskTypeId ?? ''}
          onChange={e => save(e.target.value || null, points ?? null, tagIds)}
        >
          <option value="">Sem tipo</option>
          {types.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
        <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
          <input style={{ ...select, flex: 1 }} placeholder="Novo tipo..." value={newType} onChange={e => setNewType(e.target.value)} />
          <button type="button" onClick={createType} style={{ padding: '7px 10px', borderRadius: 6, background: '#1E7BD7', color: '#fff', fontWeight: 700, fontSize: 14 }}>+ Criar</button>
        </div>
      </div>

      <div style={box}>
        <span style={label}>Tags</span>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
          {tags.map(t => {
            const on = tagIds.includes(t.id);
            return (
              <button
                key={t.id}
                type="button"
                onClick={() => toggleTag(t.id)}
                style={{
                  fontSize: 14, fontWeight: 600, padding: '3px 10px', borderRadius: 999,
                  border: `1px solid ${t.color}`,
                  color: on ? '#fff' : t.color, background: on ? t.color : 'transparent'
                }}
              >
                {t.name}
              </button>
            );
          })}
          {tags.length === 0 && <span style={{ fontSize: 14, color: '#94A3B8' }}>Nenhuma tag ainda.</span>}
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
          <input style={{ ...select, flex: 1 }} placeholder="Nova tag..." value={newTag} onChange={e => setNewTag(e.target.value)} />
          <button type="button" onClick={createTag} style={{ padding: '7px 10px', borderRadius: 6, background: '#64748B', color: '#fff', fontWeight: 700, fontSize: 14 }}>+ Criar</button>
        </div>
      </div>

      {showPoints && <div style={box}>
        <span style={label}>Pontos</span>
        <input
          type="number"
          min="0"
          style={select}
          defaultValue={points ?? ''}
          placeholder="—"
          onBlur={e => {
            const v = e.target.value ? Number(e.target.value) : null;
            if (v !== (points ?? null)) save(taskTypeId ?? null, v, tagIds);
          }}
        />
      </div>}
    </>
  );
};
