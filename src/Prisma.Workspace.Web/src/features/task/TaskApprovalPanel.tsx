import { useEffect, useState, useCallback } from 'react';
import { api } from '../../services/api';
import { resolveUserDisplayLabel, userDisplayLabel } from '../../utils/userDisplayName';

interface Approval {
  id: string;
  status: number; // 0 pendente, 1 aprovada, 2 rejeitada
  requesterName: string;
  requesterId: string;
  approverName: string;
  approverId: string;
  note?: string | null;
  createdAt: string;
  decidedAt?: string | null;
}

interface UserDto { id: string; displayName?: string; email?: string; userName?: string; }

interface Props {
  workItemId: string;
  users: UserDto[];
}

const label: React.CSSProperties = { fontSize: 14, fontWeight: 700, color: '#64748B', textTransform: 'uppercase', letterSpacing: 0.4, display: 'block', marginBottom: 8 };
const select: React.CSSProperties = { width: '100%', padding: '7px 8px', borderRadius: 6, border: '1px solid #cbd5e1', fontSize: 15 };

const statusChip = (s: number) => {
  const map: Record<number, [string, string, string]> = {
    0: ['Pendente', '#B45309', '#FEF3C7'],
    1: ['Aprovada', '#065F46', '#D1FAE5'],
    2: ['Rejeitada', '#B42318', '#FDEEEE']
  };
  const [text, color, bg] = map[s] || map[0];
  return <span style={{ fontSize: 13.5, fontWeight: 700, padding: '2px 8px', borderRadius: 999, color, background: bg }}>{text}</span>;
};

export const TaskApprovalPanel: React.FC<Props> = ({ workItemId, users }) => {
  const [approvals, setApprovals] = useState<Approval[]>([]);
  const [approverId, setApproverId] = useState('');

  const load = useCallback(async () => {
    try { setApprovals(await api.getApprovals(workItemId)); } catch { /* silencioso */ }
  }, [workItemId]);

  useEffect(() => { load(); }, [load]);

  const hasPending = approvals.some(a => a.status === 0);

  const request = async () => {
    if (!approverId) return;
    try {
      await api.requestApproval(workItemId, approverId);
      setApproverId('');
      await load();
    } catch (e) {
      alert((e as Error).message || 'Erro ao solicitar aprovação.');
    }
  };

  return (
    <div style={{ padding: '14px 0', borderBottom: '1px solid #edf2f7' }}>
      <span style={label}>Aprovação</span>

      {approvals.slice(0, 3).map(a => (
        <div key={a.id} style={{ display: 'flex', flexDirection: 'column', gap: 2, padding: '6px 0', borderBottom: '1px dashed #F1F5F9' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            {statusChip(a.status)}
            <span style={{ fontSize: 13.5, color: '#94A3B8' }}>{new Date(a.createdAt).toLocaleDateString('pt-BR')}</span>
          </div>
          <span style={{ fontSize: 14, color: '#64748B' }}>
            {resolveUserDisplayLabel(users, a.requesterName, a.requesterId)} → {resolveUserDisplayLabel(users, a.approverName, a.approverId)}
          </span>
          {a.note && <span style={{ fontSize: 14, color: '#334155', fontStyle: 'italic' }}>&quot;{a.note}&quot;</span>}
        </div>
      ))}

      {!hasPending && (
        <div style={{ display: 'flex', gap: 6, marginTop: 10 }}>
          <select style={{ ...select, flex: 1 }} value={approverId} onChange={e => setApproverId(e.target.value)}>
            <option value="">Aprovador...</option>
            {users.map(u => <option key={u.id} value={u.id}>{userDisplayLabel(u)}</option>)}
          </select>
          <button
            type="button"
            onClick={request}
            disabled={!approverId}
            style={{ padding: '7px 10px', borderRadius: 6, background: approverId ? '#1E7BD7' : '#CBD5E1', color: '#fff', fontWeight: 700, fontSize: 14 }}
          >Solicitar</button>
        </div>
      )}
      {hasPending && <span style={{ fontSize: 14, color: '#B45309' }}>Aguardando decisão do aprovador.</span>}
    </div>
  );
};
