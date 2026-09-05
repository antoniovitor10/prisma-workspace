import { useState, useEffect } from 'react';
import styled from 'styled-components';
import { Pencil } from 'lucide-react';
import { api } from '../../services/api';

interface Props {
  workItemId: string;
  description?: string | null;
  onChanged?: (text: string | null) => void;
}

const Wrap = styled.div`
  margin-top: 8px;
`;

const Rendered = styled.div`
  font-size: 15px;
  line-height: 1.6;
  color: #334155;
  cursor: text;
  padding: 12px;
  border-radius: 8px;
  border: 1px solid transparent;
  transition: border-color 0.15s ease, background 0.15s ease;
  &:hover { border-color: #E2E8F0; background: #F8FAFC; }
  ul { margin: 8px 0 8px 20px; }
  li { list-style: disc; margin: 2px 0; }
  strong { font-weight: 700; }
  em { font-style: italic; }
`;

const Empty = styled.div`
  font-size: 15px;
  color: #94A3B8;
  cursor: text;
  padding: 16px 12px;
  border: 1px dashed #CBD5E1;
  border-radius: 8px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  &:hover { border-color: #1E7BD7; color: #1E7BD7; }
`;

const Editor = styled.textarea`
  width: 100%;
  min-height: 160px;
  padding: 12px;
  border-radius: 8px;
  border: 1px solid #1E7BD7;
  font-size: 15px;
  line-height: 1.6;
  font-family: inherit;
  resize: vertical;
`;

const Row = styled.div`
  display: flex;
  gap: 8px;
  margin-top: 10px;
  align-items: center;
`;

const Save = styled.button`
  padding: 8px 16px;
  border-radius: 6px;
  background: #1E7BD7;
  color: #fff;
  font-weight: 700;
  font-size: 15px;
`;

const Cancel = styled.button`
  padding: 8px 16px;
  border-radius: 6px;
  background: #EDF2F7;
  color: #475569;
  font-weight: 600;
  font-size: 15px;
`;

const Hint = styled.span`
  font-size: 14px;
  color: #94A3B8;
`;

function escapeHtml(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// Markdown leve: **negrito**, *itálico*, listas com "- ", quebras de linha.
function renderMarkdown(text: string): string {
  const lines = escapeHtml(text).split('\n');
  const html: string[] = [];
  let inList = false;
  for (const line of lines) {
    const isBullet = /^\s*-\s+/.test(line);
    if (isBullet) {
      if (!inList) { html.push('<ul>'); inList = true; }
      html.push('<li>' + inline(line.replace(/^\s*-\s+/, '')) + '</li>');
    } else {
      if (inList) { html.push('</ul>'); inList = false; }
      html.push(line.trim() === '' ? '<br/>' : '<div>' + inline(line) + '</div>');
    }
  }
  if (inList) html.push('</ul>');
  return html.join('');
}

function inline(s: string): string {
  return s
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\*([^*]+)\*/g, '<em>$1</em>');
}

export const TaskDescriptionPanel: React.FC<Props> = ({ workItemId, description, onChanged }) => {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(description ?? '');
  const [saving, setSaving] = useState(false);

  useEffect(() => { setValue(description ?? ''); setEditing(false); }, [workItemId, description]);

  const save = async () => {
    setSaving(true);
    try {
      const text = value.trim() ? value : null;
      await api.setDescription(workItemId, text);
      onChanged?.(text);
      setEditing(false);
    } catch {
      alert('Erro ao salvar a descrição.');
    } finally {
      setSaving(false);
    }
  };

  if (editing) {
    return (
      <Wrap>
        <Editor
          autoFocus
          value={value}
          onChange={e => setValue(e.target.value)}
          placeholder="Descreva a tarefa... (use **negrito**, *itálico*, e - para listas)"
        />
        <Row>
          <Save type="button" onClick={save} disabled={saving}>{saving ? 'Salvando...' : 'Salvar'}</Save>
          <Cancel type="button" onClick={() => { setValue(description ?? ''); setEditing(false); }}>Cancelar</Cancel>
          <Hint>Suporta **negrito**, *itálico* e listas com &quot;- &quot;</Hint>
        </Row>
      </Wrap>
    );
  }

  return (
    <Wrap>
      {description && description.trim() ? (
        <Rendered onClick={() => setEditing(true)} dangerouslySetInnerHTML={{ __html: renderMarkdown(description) }} />
      ) : (
        <Empty onClick={() => setEditing(true)}>
          <Pencil size={15} />
          <span>Adicionar descrição...</span>
        </Empty>
      )}
    </Wrap>
  );
};
