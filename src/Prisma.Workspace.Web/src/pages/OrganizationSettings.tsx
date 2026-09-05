import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, Check, Copy, KeyRound, Mail, ShieldCheck, UserRoundCheck, Users } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { useOrganization } from '../features/organizations/OrganizationState';
import { previewMode } from '../preview';
import { api } from '../services/api';
import { AuditLogPanel } from '../features/audit/AuditLogPanel';
import { WorkflowTemplatesPanel } from '../features/organizations/WorkflowTemplatesPanel';
import { HeaderLead, Page, PageHeader, PrimaryAction } from '../components/PageLayout';

const roles = [
  [1, 'Administrador'], [2, 'Gestor'], [3, 'Gerente de projeto'], [4, 'Scrum Master'],
  [5, 'Product Owner'], [6, 'Membro da equipe'], [7, 'Desenvolvedor'],
  [8, 'Solicitante externo'], [9, 'Cliente'], [10, 'Visualizador'],
] as const;

const scopes = [
  [1, 'Organização'], [2, 'Equipe'], [3, 'Projeto'], [4, 'Tarefa'],
  [5, 'Relatório'], [6, 'Formulário'], [7, 'Solicitação'],
] as const;

const permissions = [
  [1, 'Visualizar'], [2, 'Criar'], [3, 'Editar'], [4, 'Excluir'], [5, 'Atribuir'],
  [6, 'Alterar status'], [7, 'Comentar'], [8, 'Responder ao solicitante'],
  [9, 'Gerenciar sprint'], [10, 'Visualizar relatório'], [11, 'Criar relatório'],
  [12, 'Administrar organização'], [13, 'Gerenciar membros'], [14, 'Gerenciar permissões'],
] as const;

interface Member {
  userId: string;
  name: string;
  displayName?: string | null;
  email?: string | null;
  userName?: string | null;
  role: number;
  isActive: boolean;
  joinedAt: string;
}
interface Access { role: number; allowedPermissions: number[]; }
interface Grant { id: string; userId: string; scope: number; scopeId?: string; permission: number; isAllowed: boolean; }

const SettingsPage = styled(Page)`max-width:1280px;`;
const Grid = styled.div`display:grid;grid-template-columns:minmax(0,1.2fr) minmax(320px,.8fr);gap:18px;@media(max-width:900px){grid-template-columns:1fr;}`;
const Section = styled.section`
  align-self:start;overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};
  > header { display:flex;align-items:center;gap:9px;min-height:56px;padding:14px 18px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.surfaceSubtle};h2{font-size:14px;letter-spacing:-.01em;}p{margin-left:auto;color:${({theme})=>theme.color.textMuted};font-size:12px;}svg{color:${({theme})=>theme.color.brand};}
`;
const Form = styled.form`
  display: grid; grid-template-columns: 1fr 1fr; gap: 13px; padding: 18px;
  label { display: grid; gap: 5px; color: ${({theme}) => theme.color.textMuted}; font-size: 13px; font-weight: 800; }
  label:first-child { grid-column: 1/-1; }
  input, select { min-height: 42px; padding: 0 11px; border: 1px solid ${({theme}) => theme.color.border}; border-radius: ${({theme}) => theme.radius.md}; background:${({theme})=>theme.color.surface};color: ${({theme}) => theme.color.text}; }
  footer { grid-column: 1/-1; display: flex; align-items: center; justify-content: flex-end; gap: 10px; }
  small { margin-right: auto; color: ${({theme}) => theme.color.danger}; }
  @media(max-width: 600px){grid-template-columns:1fr; label:first-child, footer {grid-column:auto;}}
`;
const Button = styled(PrimaryAction)`min-height:38px;`;
const Members = styled.div`display: grid;`;
const MemberRow = styled.div`
  display: grid; grid-template-columns: minmax(260px, 1fr) 180px 86px; gap: 10px; align-items: center; min-height: 68px; padding: 9px 18px; border-bottom: 1px solid ${({theme}) => theme.color.neutral[100]};
  strong { display: block; overflow: hidden; text-overflow: ellipsis; font-size: 13.5px; white-space: nowrap; } small { color: ${({theme}) => theme.color.textMuted}; }
  select { min-height: 34px; padding: 0 8px; border: 1px solid ${({theme}) => theme.color.border}; border-radius: ${({theme}) => theme.radius.md}; font-size: 13px; }
  label { display: flex; align-items: center; gap: 6px; color: ${({theme}) => theme.color.textMuted}; font-size: 13px; }
  @media(max-width: 640px){grid-template-columns:1fr 1fr; >div{grid-column:1/-1;}}
`;
const MemberIdentity = styled.div`
  display: grid; grid-template-columns: minmax(130px, 1fr) auto; gap: 5px 7px; align-items: center;
  small { grid-column: 1/-1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  input { min-width: 0; min-height: 34px; padding: 0 9px; border: 1px solid ${({theme}) => theme.color.border}; border-radius: ${({theme}) => theme.radius.md}; color: ${({theme}) => theme.color.text}; font-size: 13.5px; font-weight: 700; }
  button { min-height: 32px; padding: 0 9px; border-radius: ${({theme}) => theme.radius.md}; background: ${({theme}) => theme.color.neutral[100]}; color: ${({theme}) => theme.color.text}; font-size: 12px; font-weight: 800; }
  button:disabled { opacity: .5; }
`;
const Invite = styled(Form)`grid-template-columns: 1fr 170px; border-top: 1px solid ${({theme}) => theme.color.border}; label:first-child { grid-column: auto; }`;
const InviteResult = styled.div`grid-column: 1/-1; display: flex; align-items: center; gap: 8px; padding: 10px; border-radius: ${({theme}) => theme.radius.md}; background: ${({theme}) => theme.color.neutral[50]}; code { flex:1; overflow:hidden; text-overflow:ellipsis; font-size:13px; }`;
const Wide = styled(Section)`grid-column: 1/-1;`;
const GrantForm = styled(Form)`grid-template-columns: 1fr 150px 1fr 190px 120px auto; label:first-child {grid-column:auto;} @media(max-width:1000px){grid-template-columns:1fr 1fr;}`;
const GrantRow = styled.div`display:grid; grid-template-columns:1fr 130px 1fr 110px 32px; gap:10px; align-items:center; padding:10px 18px; border-top:1px solid ${({theme}) => theme.color.neutral[100]}; font-size:13px;`;
const Empty = styled.p`padding: 22px; color: ${({theme}) => theme.color.textMuted}; font-size: 13.5px; text-align: center;`;

function MemberNameEditor({ member, canEdit, pending, onSave }: {
  member: Member;
  canEdit: boolean;
  pending: boolean;
  onSave: (displayName: string | null) => void;
}) {
  const [value, setValue] = useState(member.displayName ?? '');
  useEffect(() => setValue(member.displayName ?? ''), [member.displayName]);
  const normalized = value.trim();
  const isValid = normalized.length === 0 || normalized.length >= 2;
  const changed = normalized !== (member.displayName ?? '').trim();
  const secondaryIdentity = member.email ?? member.userName ?? member.userId;

  if (!canEdit) return <div><strong>{member.name}</strong><small>{secondaryIdentity}</small></div>;

  return <MemberIdentity>
    <input
      aria-label={`Nome de exibição de ${member.name}`}
      maxLength={200}
      value={value}
      onChange={event => setValue(event.target.value)}
      placeholder={member.name}
    />
    <button
      type="button"
      disabled={pending || !changed || !isValid}
      onClick={() => onSave(normalized || null)}
      aria-label={`Salvar nome de ${member.name}`}
    >Salvar</button>
    <small>{secondaryIdentity}</small>
  </MemberIdentity>;
}

export function OrganizationSettings() {
  const queryClient = useQueryClient();
  const { current } = useOrganization();
  const [form, setForm] = useState({ name: current.name, locale: current.locale, timeZone: current.timeZone, weekStartDay: current.weekStartDay });
  const [invite, setInvite] = useState({ email: '', role: 6 });
  const [inviteLink, setInviteLink] = useState('');
  const [grant, setGrant] = useState({ userId: '', scope: 1, scopeId: '', permission: 1, isAllowed: true });

  const organizationQuery = useQuery({ queryKey: ['organization', 'current'], queryFn: () => api.getCurrentOrganization(), enabled: !previewMode });
  const accessQuery = useQuery<Access>({
    queryKey: ['organization', 'access'], queryFn: () => api.getOrganizationAccess(), enabled: !previewMode,
    initialData: previewMode ? { role: 1, allowedPermissions: permissions.map(([id]) => id) } : undefined,
  });
  const membersQuery = useQuery<Member[]>({
    queryKey: ['organization', 'members'], queryFn: () => api.getOrganizationMembers(), enabled: !previewMode,
    initialData: previewMode ? [
      { userId: 'user-ana', name: 'Ana Costa', displayName: 'Ana Costa', email: 'ana.costa@empresa.com', role: 1, isActive: true, joinedAt: '2026-01-01' },
      { userId: 'user-bruno', name: 'Bruno Lima', displayName: 'Bruno Lima', email: 'bruno.lima@empresa.com', role: 7, isActive: true, joinedAt: '2026-02-10' },
    ] : undefined,
  });
  const canAdmin = accessQuery.data?.allowedPermissions.includes(12) ?? false;
  const canManageMembers = accessQuery.data?.allowedPermissions.includes(13) ?? false;
  const canManagePermissions = accessQuery.data?.allowedPermissions.includes(14) ?? false;
  const grantsQuery = useQuery<Grant[]>({
    queryKey: ['organization', 'permissions'], queryFn: () => api.getPermissionGrants(), enabled: !previewMode && canManagePermissions,
    initialData: previewMode ? [] : undefined,
  });

  useEffect(() => {
    const organization = organizationQuery.data;
    if (organization) setForm({ name: organization.name, locale: organization.locale, timeZone: organization.timeZone, weekStartDay: organization.weekStartDay });
  }, [organizationQuery.data]);

  const saveOrganization = useMutation({
    mutationFn: () => api.updateCurrentOrganization(form),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['organizations'] }),
        queryClient.invalidateQueries({ queryKey: ['organization', 'current'] }),
      ]);
    },
  });
  const updateMember = useMutation({
    mutationFn: ({ userId, role, isActive, displayName }: { userId: string; role: number; isActive: boolean; displayName?: string | null }) =>
      api.updateOrganizationMember(userId, { role, isActive, displayName }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['organization', 'members'] }),
        queryClient.invalidateQueries({ queryKey: ['assignable-users'] }),
        queryClient.invalidateQueries({ queryKey: ['work-item'] }),
      ]);
    },
  });
  const sendInvite = useMutation({
    mutationFn: () => api.inviteOrganizationMember({ ...invite, expiresInDays: 7 }),
    onSuccess: data => {
      const url = new URL(window.location.origin);
      url.searchParams.set('invite', data.token);
      setInviteLink(url.toString());
      setInvite(currentInvite => ({ ...currentInvite, email: '' }));
    },
  });
  const saveGrant = useMutation({
    mutationFn: () => api.setPermissionGrant({
      ...grant,
      scopeId: grant.scope === 1 ? undefined : grant.scopeId,
    }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['organization', 'permissions'] }),
  });
  const deleteGrant = useMutation({
    mutationFn: (id: string) => api.deletePermissionGrant(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['organization', 'permissions'] }),
  });

  const submitOrganization = (event: FormEvent) => { event.preventDefault(); if (canAdmin) saveOrganization.mutate(); };
  const submitInvite = (event: FormEvent) => { event.preventDefault(); if (canManageMembers) sendInvite.mutate(); };
  const submitGrant = (event: FormEvent) => { event.preventDefault(); if (canManagePermissions) saveGrant.mutate(); };
  const roleLabel = (id: number) => roles.find(([value]) => value === id)?.[1] ?? String(id);
  const scopeLabel = (id: number) => scopes.find(([value]) => value === id)?.[1] ?? String(id);
  const permissionLabel = (id: number) => permissions.find(([value]) => value === id)?.[1] ?? String(id);

  return <SettingsPage>
    <PageHeader><HeaderLead><span className="eyebrow"><Building2 size={13}/>Administração</span><h1>Configurações da organização</h1><p>Preferências, acesso, pessoas e governança de {current.name}.</p></HeaderLead></PageHeader>
    <Grid>
      <Section><header><Building2 size={16}/><h2>Preferências gerais</h2>{!canAdmin && <p>Somente leitura</p>}</header>
        <Form onSubmit={submitOrganization}>
          <label>Nome<input disabled={!canAdmin} value={form.name} onChange={event => setForm({...form, name: event.target.value})}/></label>
          <label>Idioma<select disabled={!canAdmin} value={form.locale} onChange={event => setForm({...form, locale: event.target.value})}><option value="pt-BR">Português (Brasil)</option><option value="en-US">English (US)</option></select></label>
          <label>Fuso horário<input disabled={!canAdmin} value={form.timeZone} onChange={event => setForm({...form, timeZone: event.target.value})}/></label>
          <label>Início da semana<select disabled={!canAdmin} value={form.weekStartDay} onChange={event => setForm({...form, weekStartDay: Number(event.target.value)})}><option value={1}>Segunda-feira</option><option value={0}>Domingo</option></select></label>
          <footer>{saveOrganization.error && <small>{(saveOrganization.error as Error).message}</small>}{canAdmin && <Button disabled={saveOrganization.isPending}><Check size={14}/>Salvar preferências</Button>}</footer>
        </Form>
      </Section>

      <Section><header><ShieldCheck size={16}/><h2>Seu acesso</h2></header>
        <div style={{padding:18}}><strong style={{fontSize:15}}>{roleLabel(accessQuery.data?.role ?? current.role)}</strong><p style={{marginTop:7,fontSize:13.5,color:'#64748b'}}>Os módulos e ações são liberados pelo perfil e por permissões específicas.</p></div>
      </Section>

      <Wide><header><Users size={16}/><h2>Membros</h2><p>{membersQuery.data?.filter(item => item.isActive).length ?? 0} ativos</p></header>
        <Members>{membersQuery.data?.map(member => <MemberRow key={member.userId}>
          <MemberNameEditor
            member={member}
            canEdit={canManageMembers}
            pending={updateMember.isPending}
            onSave={displayName => updateMember.mutate({ ...member, displayName })}
          />
          <select disabled={!canManageMembers || updateMember.isPending} value={member.role} onChange={event => updateMember.mutate({...member, role:Number(event.target.value)})}>{roles.map(([id,label]) => <option key={id} value={id}>{label}</option>)}</select>
          <label><input type="checkbox" disabled={!canManageMembers || updateMember.isPending} checked={member.isActive} onChange={event => updateMember.mutate({...member, isActive:event.target.checked})}/>Ativo</label>
        </MemberRow>)}</Members>
        {canManageMembers && <Invite onSubmit={submitInvite}>
          <label>E-mail<input required type="email" value={invite.email} onChange={event => setInvite({...invite,email:event.target.value})} placeholder="pessoa@empresa.com"/></label>
          <label>Perfil<select value={invite.role} onChange={event => setInvite({...invite,role:Number(event.target.value)})}>{roles.map(([id,label]) => <option key={id} value={id}>{label}</option>)}</select></label>
          <footer>{sendInvite.error && <small>{(sendInvite.error as Error).message}</small>}<Button disabled={sendInvite.isPending}><Mail size={14}/>Gerar convite</Button></footer>
          {inviteLink && <InviteResult><UserRoundCheck size={15}/><code>{inviteLink}</code><button type="button" onClick={() => navigator.clipboard.writeText(inviteLink)} aria-label="Copiar convite"><Copy size={14}/></button></InviteResult>}
        </Invite>}
      </Wide>

      {canManagePermissions && <Wide><header><KeyRound size={16}/><h2>Permissões específicas</h2><p>Uma negação explícita prevalece</p></header>
        <GrantForm onSubmit={submitGrant}>
          <label>Membro<select required value={grant.userId} onChange={event => setGrant({...grant,userId:event.target.value})}><option value="">Selecione...</option>{membersQuery.data?.filter(item => item.isActive).map(item => <option key={item.userId} value={item.userId}>{item.name}</option>)}</select></label>
          <label>Escopo<select value={grant.scope} onChange={event => setGrant({...grant,scope:Number(event.target.value),scopeId:''})}>{scopes.map(([id,label]) => <option key={id} value={id}>{label}</option>)}</select></label>
          <label>ID do recurso<input disabled={grant.scope === 1} required={grant.scope !== 1} value={grant.scopeId} onChange={event => setGrant({...grant,scopeId:event.target.value})} placeholder="UUID"/></label>
          <label>Permissão<select value={grant.permission} onChange={event => setGrant({...grant,permission:Number(event.target.value)})}>{permissions.map(([id,label]) => <option key={id} value={id}>{label}</option>)}</select></label>
          <label>Decisão<select value={grant.isAllowed ? 'allow':'deny'} onChange={event => setGrant({...grant,isAllowed:event.target.value==='allow'})}><option value="allow">Permitir</option><option value="deny">Negar</option></select></label>
          <footer><Button disabled={saveGrant.isPending}>Aplicar</Button></footer>
        </GrantForm>
        {grantsQuery.data?.length ? grantsQuery.data.map(item => <GrantRow key={item.id}><span>{membersQuery.data?.find(member => member.userId === item.userId)?.name ?? item.userId}</span><span>{scopeLabel(item.scope)}</span><span>{permissionLabel(item.permission)}</span><strong>{item.isAllowed ? 'Permitido':'Negado'}</strong><button onClick={() => deleteGrant.mutate(item.id)} aria-label="Remover permissão">×</button></GrantRow>) : <Empty>Nenhuma exceção cadastrada. Os perfis-base estão em uso.</Empty>}
      </Wide>}
      <div style={{gridColumn:'1/-1'}}><WorkflowTemplatesPanel organizationId={current.id} canManage={canAdmin}/></div>
      {canAdmin&&<div style={{gridColumn:'1/-1'}}><AuditLogPanel/></div>}
    </Grid>
  </SettingsPage>;
}
