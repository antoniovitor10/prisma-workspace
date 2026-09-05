import { useEffect, useState, useCallback, useRef } from 'react';
import styled from 'styled-components';
import { Send, ArrowRightLeft, UserPlus, UserMinus } from 'lucide-react';
import { api } from '../../services/api';
import { resolveUserDisplayLabel, userDisplayLabel } from '../../utils/userDisplayName';

interface MentionUser { id: string; displayName?: string; userName?: string; email?: string; }
const mentionLabel = (user: MentionUser) => userDisplayLabel(user);

interface Comment {
  id: string;
  userId: string;
  userName?: string;
  text: string;
  createdAt: string;
}

interface TaskEvent {
  id: string;
  actorId: string;
  kind: string;
  payload?: string | null;
  createdAt: string;
}

type FeedEntry =
  | { type: 'comment'; at: string; data: Comment }
  | { type: 'event'; at: string; data: TaskEvent };

const Wrap = styled.div`
  display: flex;
  flex-direction: column;
  gap: 12px;
`;

const Feed = styled.div`
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-height: 360px;
  overflow-y: auto;
  padding-right: 4px;
`;

const CommentBubble = styled.div`
  border: 1px solid ${p => p.theme.color.border};
  border-radius: 10px;
  padding: 10px 12px;
  background: ${p => p.theme.color.surface};
`;

const CommentHead = styled.div`
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 4px;
`;

const Author = styled.span`
  font-weight: 700;
  font-size: 15px;
  color: ${p => p.theme.color.text};
`;

const When = styled.span`
  font-size: 13.5px;
  color: ${p => p.theme.color.textMuted};
`;

const CommentText = styled.div`
  font-size: 15px;
  color: ${p => p.theme.color.text};
  white-space: pre-wrap;
`;

const EventLine = styled.div`
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: #8a6d3b;
  background: #FCF8E3;
  border: 1px solid #FAEBCC;
  border-radius: 8px;
  padding: 8px 10px;
`;

const Composer = styled.form`
  position: relative;
  display: flex;
  gap: 8px;
  align-items: flex-end;
`;

const MentionMenu = styled.div`
  position: absolute;
  bottom: 100%;
  left: 0;
  margin-bottom: 6px;
  width: 240px;
  max-height: 190px;
  overflow-y: auto;
  z-index: 20;
  border: 1px solid ${p => p.theme.color.border};
  border-radius: 10px;
  background: ${p => p.theme.color.surface};
  box-shadow: 0 10px 30px rgba(16, 24, 40, .14);
  padding: 5px;
`;
const MentionItem = styled.button<{ $active?: boolean }>`
  display: flex;
  flex-direction: column;
  gap: 1px;
  width: 100%;
  text-align: left;
  padding: 7px 9px;
  border: none;
  border-radius: 7px;
  background: ${p => p.$active ? p.theme.color.neutral[100] : 'transparent'};
  strong { font-size: 14px; font-weight: 700; color: ${p => p.theme.color.text}; }
  span { font-size: 13px; color: ${p => p.theme.color.textMuted}; }
  &:hover { background: ${p => p.theme.color.neutral[100]}; }
`;

const Box = styled.textarea`
  flex: 1;
  min-height: 44px;
  resize: vertical;
  border: 1px solid ${p => p.theme.color.border};
  border-radius: 8px;
  padding: 8px 10px;
  font-size: 15px;
  font-family: inherit;
  outline: none;
  &:focus { border-color: ${p => p.theme.color.accentBlue}; }
`;

const SendBtn = styled.button`
  background: ${p => p.theme.color.brand};
  color: #fff;
  border-radius: 8px;
  padding: 10px 14px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-weight: 700;
  font-size: 15px;
  &:disabled { opacity: 0.5; cursor: not-allowed; }
`;

const Empty = styled.div`
  font-size: 15px;
  color: ${p => p.theme.color.textMuted};
  padding: 12px 0;
`;

const fmtWhen = (iso: string) => {
  const d = new Date(iso);
  return d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
};

const parsePayload = (payload?: string | null): Record<string, unknown> => {
  if (!payload) return {};
  try { return JSON.parse(payload); } catch { return {}; }
};

const renderEvent = (ev: TaskEvent, users: MentionUser[]) => {
  const p = parsePayload(ev.payload);
  const actor = resolveUserDisplayLabel(users, p.actorName as string | undefined, ev.actorId);
  if (ev.kind === 'stage_changed') {
    return {
      icon: <ArrowRightLeft size={14} />,
      text: `${actor} moveu de "${(p.fromStageName as string) || (p.from as string) || 'Backlog'}" para "${(p.toStageName as string) || (p.to as string) || 'Backlog'}"${p.reason ? ` · Motivo: ${p.reason}` : ''}`
    };
  }
  if (ev.kind === 'updated') {
    return { icon: <ArrowRightLeft size={14} />, text: `${actor} atualizou os dados da tarefa` };
  }
  if (ev.kind === 'assigned') {
    return { icon: <UserPlus size={14} />, text: `${actor} alocou ${resolveUserDisplayLabel(users, p.target as string | undefined)}` };
  }
  if (ev.kind === 'unassigned') {
    return { icon: <UserMinus size={14} />, text: `${actor} desalocou ${resolveUserDisplayLabel(users, p.target as string | undefined)}` };
  }
  return { icon: <ArrowRightLeft size={14} />, text: `${actor}: ${ev.kind}` };
};

export const TaskFeed: React.FC<{ workItemId: string; mode?: 'comments' | 'history' }> = ({ workItemId, mode = 'comments' }) => {
  const [entries, setEntries] = useState<FeedEntry[]>([]);
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [loading, setLoading] = useState(true);
  const [users, setUsers] = useState<MentionUser[]>([]);
  const [mentions, setMentions] = useState<Array<{ userId: string; token: string }>>([]);
  const [menu, setMenu] = useState<{ query: string; at: number } | null>(null);
  const boxRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => { void (api.getAssignableUsers() as Promise<MentionUser[]>).then(setUsers).catch(() => undefined); }, []);

  const onType = (event: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = event.target.value;
    setText(value);
    const caret = event.target.selectionStart ?? value.length;
    const before = value.slice(0, caret);
    const match = /(^|\s)@([\p{L}0-9._-]*)$/u.exec(before);
    setMenu(match ? { query: match[2].toLowerCase(), at: caret - match[2].length - 1 } : null);
  };

  const pickMention = (user: MentionUser) => {
    if (!menu) return;
    const token = `@${mentionLabel(user)}`;
    const caret = boxRef.current?.selectionStart ?? text.length;
    const next = text.slice(0, menu.at) + token + ' ' + text.slice(caret);
    setText(next);
    setMentions(current => [...current, { userId: user.id, token }]);
    setMenu(null);
    setTimeout(() => boxRef.current?.focus(), 0);
  };

  const menuUsers = menu
    ? users.filter(user => `${user.userName ?? ''} ${user.email ?? ''}`.toLowerCase().includes(menu.query)).slice(0, 6)
    : [];

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [comments, events] = await Promise.all([
        mode === 'comments' ? api.getComments(workItemId) as Promise<Comment[]> : Promise.resolve([]),
        mode === 'history' ? api.getTaskEvents(workItemId) as Promise<TaskEvent[]> : Promise.resolve([])
      ]);
      const merged: FeedEntry[] = [
        ...comments.map(c => ({ type: 'comment' as const, at: c.createdAt, data: c })),
        ...events.map(e => ({ type: 'event' as const, at: e.createdAt, data: e }))
      ].sort((a, b) => new Date(a.at).getTime() - new Date(b.at).getTime());
      setEntries(merged);
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, [mode, workItemId]);

  useEffect(() => { load(); }, [load]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return;
    setSending(true);
    try {
      const ids = Array.from(new Set(mentions.filter(m => text.includes(m.token)).map(m => m.userId)));
      await api.addComment(workItemId, text.trim(), ids);
      setText('');
      setMentions([]);
      setMenu(null);
      await load();
    } catch {
      alert('Erro ao enviar comentario.');
    } finally {
      setSending(false);
    }
  };

  return (
    <Wrap>
      <Feed>
        {loading ? (
          <Empty>Carregando...</Empty>
        ) : entries.length === 0 ? (
          <Empty>{mode === 'comments' ? 'Nenhum comentario ainda.' : 'Nenhum evento registrado ainda.'}</Empty>
        ) : (
          entries.map(entry => {
            if (entry.type === 'comment') {
              const c = entry.data;
              return (
                <CommentBubble key={`c-${c.id}`}>
                  <CommentHead>
                    <Author>{resolveUserDisplayLabel(users, c.userName, c.userId)}</Author>
                    <When>{fmtWhen(c.createdAt)}</When>
                  </CommentHead>
                  <CommentText>{c.text}</CommentText>
                </CommentBubble>
              );
            }
            const r = renderEvent(entry.data, users);
            return (
              <EventLine key={`e-${entry.data.id}`}>
                {r.icon}
                <span>{r.text}</span>
                <span style={{ marginLeft: 'auto', fontSize: 13.5, opacity: 0.7 }}>{fmtWhen(entry.at)}</span>
              </EventLine>
            );
          })
        )}
      </Feed>

      {mode === 'comments' && <Composer onSubmit={submit}>
        {menu && menuUsers.length > 0 && (
          <MentionMenu>
            {menuUsers.map(user => (
              <MentionItem key={user.id} type="button" onMouseDown={e => { e.preventDefault(); pickMention(user); }}>
                <strong>{mentionLabel(user)}</strong>
              </MentionItem>
            ))}
          </MentionMenu>
        )}
        <Box
          ref={boxRef}
          placeholder="Escreva um comentario... use @ para mencionar"
          value={text}
          onChange={onType}
          onKeyDown={e => { if (e.key === 'Escape') setMenu(null); }}
        />
        <SendBtn type="submit" disabled={sending || !text.trim()}>
          <Send size={14} />
          <span>Enviar</span>
        </SendBtn>
      </Composer>}
    </Wrap>
  );
};
