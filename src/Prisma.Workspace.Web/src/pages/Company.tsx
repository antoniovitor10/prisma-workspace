import { useEffect, useState, useCallback } from 'react';
import styled from 'styled-components';
import { useNavigate } from 'react-router-dom';
import { Building2, Users, Plus, Trash2 } from 'lucide-react';
import { api } from '../services/api';

interface Project {
  id: string;
  name: string;
  description?: string | null;
  clientId?: string | null;
  clientName?: string | null;
  tasksTotal: number;
  tasksDone: number;
  progress: number;
  totalHours: number;
}

interface Client { id: string; name: string; boardsCount: number; }

const Page = styled.div`
  padding: 24px 32px;
  max-width: 1200px;
  margin: 0 auto;
`;

const Header = styled.div`
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
  h1 { font-size: 22px; font-weight: 800; color: #1D2D3E; }
`;

const Sub = styled.p`
  color: #64748B;
  font-size: 15px;
  margin-bottom: 24px;
`;

const Tabs = styled.div`
  display: flex;
  gap: 6px;
  border-bottom: 1px solid #E2E8F0;
  margin-bottom: 24px;
`;

const Tab = styled.button<{ $active: boolean }>`
  padding: 10px 16px;
  font-size: 15px;
  font-weight: 600;
  color: ${p => p.$active ? '#1E7BD7' : '#64748B'};
  border-bottom: 2px solid ${p => p.$active ? '#1E7BD7' : 'transparent'};
  margin-bottom: -1px;
`;

const Grid = styled.div`
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 18px;
`;

const Card = styled.div`
  border: 1px solid #E2E8F0;
  border-radius: 10px;
  padding: 18px;
  background: #fff;
  box-shadow: 0 1px 2px rgba(0,0,0,0.04);
  display: flex;
  flex-direction: column;
  gap: 10px;
`;

const CardName = styled.h3`
  font-size: 16px;
  font-weight: 700;
  color: #1D2D3E;
`;

const ClientBadge = styled.span`
  font-size: 14px;
  color: #1E7BD7;
  font-weight: 600;
`;

const Desc = styled.p`
  font-size: 15px;
  color: #64748B;
  min-height: 18px;
`;

const Bar = styled.div`
  height: 8px;
  background: #EDF2F7;
  border-radius: 999px;
  overflow: hidden;
`;

const Fill = styled.div<{ $pct: number }>`
  height: 100%;
  width: ${p => p.$pct}%;
  background: #10B981;
`;

const Meta = styled.div`
  display: flex;
  justify-content: space-between;
  font-size: 14px;
  color: #64748B;
`;

const EnterBtn = styled.button`
  margin-top: 4px;
  padding: 8px 12px;
  border-radius: 6px;
  background: #1E7BD7;
  color: #fff;
  font-weight: 700;
  font-size: 15px;
  &:hover { filter: brightness(1.08); }
`;

const Field = styled.select`
  padding: 6px 8px;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  font-size: 14px;
  width: 100%;
`;

const Row = styled.div`
  display: flex;
  gap: 8px;
  margin-bottom: 10px;
`;

const Input = styled.input`
  flex: 1;
  padding: 8px 10px;
  border-radius: 6px;
  border: 1px solid #cbd5e1;
  font-size: 15px;
`;

const AddBtn = styled.button`
  padding: 8px 14px;
  border-radius: 6px;
  background: #1E7BD7;
  color: #fff;
  font-weight: 700;
  font-size: 15px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
`;

const ClientItem = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 14px;
  border: 1px solid #E2E8F0;
  border-radius: 8px;
  margin-bottom: 8px;
`;

export const Company: React.FC = () => {
  const navigate = useNavigate();
  const [tab, setTab] = useState<'projetos' | 'clientes'>('projetos');
  const [projects, setProjects] = useState<Project[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [newClient, setNewClient] = useState('');

  const load = useCallback(async () => {
    try {
      const [p, c] = await Promise.all([api.getCompanyProjects(), api.getClients()]);
      setProjects(p); setClients(c);
    } catch { /* silencioso */ }
  }, []);

  useEffect(() => { load(); }, [load]);

  const changeClient = async (project: Project, clientId: string) => {
    await api.updateProject(project.id, clientId || null, project.description ?? null);
    await load();
  };

  const addClient = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newClient.trim()) return;
    await api.createClient(newClient.trim());
    setNewClient('');
    await load();
  };

  const removeClient = async (id: string) => {
    await api.deleteClient(id);
    await load();
  };

  return (
    <Page>
      <Header>
        <Building2 size={24} color="#1E7BD7" />
        <h1>Empresa</h1>
      </Header>
      <Sub>Projetos e clientes da organização.</Sub>

      <Tabs>
        <Tab $active={tab === 'projetos'} onClick={() => setTab('projetos')}>Projetos</Tab>
        <Tab $active={tab === 'clientes'} onClick={() => setTab('clientes')}>Clientes</Tab>
      </Tabs>

      {tab === 'projetos' && (
        <Grid>
          {projects.length === 0 && <p style={{ color: '#94A3B8' }}>Nenhum projeto ainda.</p>}
          {projects.map(p => (
            <Card key={p.id}>
              <div>
                <CardName>{p.name}</CardName>
                {p.clientName && <ClientBadge>{p.clientName}</ClientBadge>}
              </div>
              <Desc>{p.description || 'Sem descrição.'}</Desc>
              <Bar><Fill $pct={p.progress} /></Bar>
              <Meta>
                <span>{p.progress}% · {p.tasksDone}/{p.tasksTotal} tarefas</span>
                <span>{p.totalHours}h</span>
              </Meta>
              <Field value={p.clientId ?? ''} onChange={e => changeClient(p, e.target.value)}>
                <option value="">Sem cliente</option>
                {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </Field>
              <EnterBtn onClick={() => navigate('/')}>Entrar</EnterBtn>
            </Card>
          ))}
        </Grid>
      )}

      {tab === 'clientes' && (
        <div style={{ maxWidth: 560 }}>
          <form onSubmit={addClient}>
            <Row>
              <Input placeholder="Nome do cliente" value={newClient} onChange={e => setNewClient(e.target.value)} />
              <AddBtn type="submit"><Plus size={15} /> Adicionar</AddBtn>
            </Row>
          </form>
          {clients.length === 0 && <p style={{ color: '#94A3B8' }}>Nenhum cliente cadastrado.</p>}
          {clients.map(c => (
            <ClientItem key={c.id}>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8, fontWeight: 600, color: '#1D2D3E' }}>
                <Users size={16} color="#64748B" /> {c.name}
              </span>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 12, color: '#64748B', fontSize: 15 }}>
                {c.boardsCount} projeto(s)
                <button type="button" onClick={() => removeClient(c.id)} title="Excluir"><Trash2 size={15} color="#CBD5E1" /></button>
              </span>
            </ClientItem>
          ))}
        </div>
      )}
    </Page>
  );
};
