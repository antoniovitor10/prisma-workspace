import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Archive, Check, Crown, FolderKanban, Pencil, Plus, RotateCcw, Users, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { previewMode, previewProject, previewTeams } from '../preview';
import { api } from '../services/api';

interface Member {
  userId: string; name: string; weeklyCapacityHours: number; weekHours: number; isLeader?: boolean;
}
interface Team {
  id: string; name: string; isActive: boolean; leaderId?: string; leaderName?: string;
  defaultWeeklyCapacityHours: number; totalWeeklyCapacityHours: number;
  projectIds: string[]; members: Member[];
}
interface UserDto { id: string; displayName?: string; email?: string; userName?: string; }
interface ProjectDto { id: string; key: string; name: string; teams: Array<{id:string;name:string}>; }

const Page = styled.div`
  max-width: 1180px; margin: auto; padding: 28px 32px 60px;
  width: 100%; box-sizing: border-box; overflow-x: clip;
  @media (max-width: 700px) { padding: 16px 12px 40px; }
`;
const Header = styled.header`
  display:flex; align-items:end; justify-content:space-between; gap:20px; margin-bottom:22px; flex-wrap:wrap;
  h1{display:flex;align-items:center;gap:9px;font-size:24px;} p{margin-top:5px;color:${({theme})=>theme.color.textMuted};font-size:14px;}
`;
const Create = styled.form`
  display:flex; flex-wrap:wrap; gap:8px;
  input{width:230px;max-width:100%;min-height:38px;padding:0 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};}
  @media (max-width:700px){width:100%; input{width:100%;flex:1 1 100%;}}
`;
const Button = styled.button`
  display:inline-flex;min-height:36px;align-items:center;justify-content:center;gap:6px;padding:0 12px;border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.brand};color:white;font-size:13.5px;font-weight:800;
  &:disabled{opacity:.55;}
`;
const Grid = styled.div`display:grid;gap:15px;min-width:0;`;
const Card = styled.section<{ $inactive:boolean }>`
  overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surface};opacity:${({$inactive})=>$inactive?.72:1};min-width:0;max-width:100%;
`;
const CardHeader = styled.header`
  display:flex;align-items:center;gap:12px;min-height:62px;padding:10px 17px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.neutral[50]};min-width:0;
  h2{font-size:15px;overflow:hidden;text-overflow:ellipsis;} p{margin-top:3px;color:${({theme})=>theme.color.textMuted};font-size:13px;} >span{margin-left:auto;display:flex;gap:5px;flex-shrink:0;}
  >div{min-width:0;flex:1;}
  button{display:grid;width:32px;height:32px;place-items:center;border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.textMuted};&:hover{background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};}}
`;
const Badge = styled.em`
  padding:3px 7px;border-radius:${({theme})=>theme.radius.pill};background:${({theme})=>theme.color.neutral[200]};color:${({theme})=>theme.color.textMuted};font-size:12px;font-style:normal;font-weight:800;
`;
const Editor = styled.form`
  display:grid;grid-template-columns:1fr 150px 1fr auto;gap:10px;align-items:end;padding:14px 17px;border-bottom:1px solid ${({theme})=>theme.color.border};
  label{display:grid;gap:5px;color:${({theme})=>theme.color.textMuted};font-size:12px;font-weight:800;} input,select{min-height:36px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13.5px;max-width:100%;}
  @media(max-width:760px){grid-template-columns:1fr 1fr;}
  @media(max-width:700px){grid-template-columns:1fr;}
`;
const MembersHead = styled.div`display:grid;grid-template-columns:minmax(0,1fr) 220px 120px 34px;gap:12px;padding:10px 17px;color:${({theme})=>theme.color.textMuted};font-size:12px;font-weight:800;text-transform:uppercase;@media(max-width:700px){display:none;}`;
const MemberRow = styled.div`
  display:grid;grid-template-columns:minmax(0,1fr) 220px 120px 34px;gap:12px;align-items:center;min-height:52px;padding:8px 17px;border-top:1px solid ${({theme})=>theme.color.neutral[100]};font-size:13.5px;min-width:0;
  strong{display:flex;align-items:center;gap:6px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;min-width:0;} input{width:74px;min-height:32px;padding:0 7px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.sm};}
  button{display:grid;width:30px;height:30px;place-items:center;color:${({theme})=>theme.color.textMuted};}
  @media(max-width:700px){grid-template-columns:minmax(0,1fr) auto;gap:8px;.usage{display:none;}}
`;
const Track = styled.div`height:7px;overflow:hidden;border-radius:99px;background:${({theme})=>theme.color.neutral[100]};span{display:block;height:100%;border-radius:inherit;background:${({theme})=>theme.color.accentBlue};}`;
const AddMember = styled.div`
  display:flex;flex-wrap:wrap;gap:8px;padding:13px 17px;border-top:1px solid ${({theme})=>theme.color.border};min-width:0;
  select{flex:1 1 160px;min-width:0;max-width:100%;min-height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13.5px;}
`;
const Projects = styled.div`
  display:flex;align-items:center;flex-wrap:wrap;gap:8px;padding:12px 17px;border-top:1px solid ${({theme})=>theme.color.border};color:${({theme})=>theme.color.textMuted};font-size:13px;min-width:0;
  >span{display:flex;align-items:center;gap:5px;font-weight:800;} label{display:flex;align-items:center;gap:5px;padding:5px 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.pill};background:${({theme})=>theme.color.surface};max-width:100%;}
`;
const Message = styled.div`padding:50px;text-align:center;color:${({theme})=>theme.color.textMuted};`;
const ActionFeedback = styled.p`margin:0;padding:0 17px 12px;color:${({theme})=>theme.color.success};font-size:12.5px;font-weight:700;`;

function TeamCard({ team, users, projects, run }: {
  team: Team; users: UserDto[]; projects: ProjectDto[]; run: (action: () => Promise<unknown>) => Promise<unknown>;
}) {
  const [editing, setEditing] = useState(false);
  const [pick, setPick] = useState('');
  const [memberFeedback, setMemberFeedback] = useState('');
  const [form, setForm] = useState({ name: team.name, leaderId: team.leaderId ?? '', defaultWeeklyCapacityHours: team.defaultWeeklyCapacityHours });
  const submit = (event:FormEvent) => { event.preventDefault(); run(() => api.updateTeam(team.id, {...form, leaderId:form.leaderId || null})); setEditing(false); };
  const labelForUser = (user: UserDto) => user.displayName || user.email || user.userName || user.id;
  return <Card $inactive={!team.isActive}>
    <CardHeader><Users size={18}/><div><h2>{team.name} {!team.isActive && <Badge>Desativada</Badge>}</h2><p>{team.members.length} pessoas · {team.totalWeeklyCapacityHours}h de capacidade semanal{team.leaderName ? ` · Líder: ${team.leaderName}`:''}</p></div>
      <span><button onClick={() => setEditing(value => !value)} title="Configurar equipe"><Pencil size={15}/></button>{team.isActive ? <button onClick={() => run(() => api.setTeamActive(team.id,false))} title="Desativar equipe"><Archive size={15}/></button> : <button onClick={() => run(() => api.setTeamActive(team.id,true))} title="Reativar equipe"><RotateCcw size={15}/></button>}</span>
    </CardHeader>
    {editing && <Editor onSubmit={submit}>
      <label>Nome<input required value={form.name} onChange={event=>setForm({...form,name:event.target.value})}/></label>
      <label>Capacidade padrão<input type="number" min={1} max={168} value={form.defaultWeeklyCapacityHours} onChange={event=>setForm({...form,defaultWeeklyCapacityHours:Number(event.target.value)})}/></label>
      <label>Líder<select aria-label="Líder da equipe" value={form.leaderId} onChange={event=>setForm({...form,leaderId:event.target.value})}><option value="">Sem líder</option>{team.members.map(member=><option key={member.userId} value={member.userId}>{member.name}</option>)}</select></label>
      <Button><Check size={14}/>Salvar</Button>
    </Editor>}
    <MembersHead><span>Pessoa</span><span>Uso na semana</span><span>Capacidade</span><span/></MembersHead>
    {team.members.map(member => {
      const percentage = member.weeklyCapacityHours ? Math.min(100,(member.weekHours/member.weeklyCapacityHours)*100):0;
      return <MemberRow key={member.userId}>
        <strong>{member.isLeader && <Crown size={13}/>} {member.name}</strong>
        <div className="usage"><Track><span style={{width:`${percentage}%`}}/></Track><small>{member.weekHours}h de {member.weeklyCapacityHours}h</small></div>
        <input aria-label={`Capacidade de ${member.name}`} type="number" min={1} max={168} defaultValue={member.weeklyCapacityHours} onBlur={event => { const value=Number(event.target.value); if(value!==member.weeklyCapacityHours) run(()=>api.updateTeamMemberCapacity(team.id,member.userId,value)); }}/>
        <button onClick={()=>run(()=>api.removeTeamMember(team.id,member.userId))} title="Remover da equipe"><X size={14}/></button>
      </MemberRow>;
    })}
    {!team.members.length && <Message>Adicione pessoas para definir líder e capacidade.</Message>}
    {team.isActive && <>
      <AddMember>
        <select aria-label="Adicionar membro à equipe" value={pick} onChange={event=>setPick(event.target.value)}>
          <option value="">Adicionar membro...</option>
          {users.filter(user=>!team.members.some(member=>member.userId===user.id)).map(user=><option key={user.id} value={user.id}>{labelForUser(user)}</option>)}
        </select>
        <Button disabled={!pick} onClick={async()=>{
          if(!pick) return;
          setMemberFeedback('');
          try {
            await run(()=>api.addTeamMember(team.id,pick,team.defaultWeeklyCapacityHours));
            setPick('');
            setMemberFeedback('Membro adicionado à equipe.');
          } catch {
            setMemberFeedback('Não foi possível adicionar o membro.');
          }
        }}><Plus size={14}/>Adicionar</Button>
      </AddMember>
      {memberFeedback&&<ActionFeedback role="status">{memberFeedback}</ActionFeedback>}
    </>}
    <Projects><span><FolderKanban size={13}/>Projetos</span>{projects.map(project => <label key={project.id}><input type="checkbox" checked={team.projectIds.includes(project.id)} onChange={event=>run(()=>event.target.checked?api.addProjectTeam(project.id,team.id):api.removeProjectTeam(project.id,team.id))}/>{project.key} · {project.name}</label>)}</Projects>
  </Card>;
}

export function Teams() {
  const queryClient = useQueryClient();
  const [name,setName]=useState('');
  const previewTeamData: Team[] = previewTeams.map(team=>({...team,isActive:true,leaderId:team.members[0]?.userId,leaderName:team.members[0]?.name,defaultWeeklyCapacityHours:40,totalWeeklyCapacityHours:team.members.reduce((sum,member)=>sum+member.weeklyCapacityHours,0),projectIds:[previewProject.id],members:team.members.map((member,index)=>({...member,isLeader:index===0}))}));
  const teamsQuery=useQuery<Team[]>({queryKey:['teams'],queryFn:()=>api.getTeams(),enabled:!previewMode,initialData:previewMode?previewTeamData:undefined});
  const usersQuery=useQuery<UserDto[]>({queryKey:['users','assignable'],queryFn:()=>api.getAssignableUsers(),enabled:!previewMode,initialData:previewMode?previewTeamData.flatMap(team=>team.members.map(member=>({id:member.userId,email:member.name}))):undefined});
  const projectsQuery=useQuery<ProjectDto[]>({queryKey:['projects'],queryFn:()=>api.getProjects(),enabled:!previewMode,initialData:previewMode?[previewProject]:undefined});
  const action=useMutation({mutationFn:(run:()=>Promise<unknown>)=>run(),onSuccess:()=>Promise.all([queryClient.invalidateQueries({queryKey:['teams']}),queryClient.invalidateQueries({queryKey:['projects']})])});
  const create=useMutation({mutationFn:()=>api.createTeam(name,40),onSuccess:()=>{setName('');return queryClient.invalidateQueries({queryKey:['teams']});}});
  const submit=(event:FormEvent)=>{event.preventDefault();if(name.trim())create.mutate();};
  return <Page>
    <Header><div><h1><Users size={25}/>Equipes</h1><p>Liderança, projetos e capacidade de trabalho em um único lugar.</p></div><Create onSubmit={submit}><input required maxLength={150} placeholder="Nome da nova equipe" value={name} onChange={event=>setName(event.target.value)}/><Button disabled={create.isPending}><Plus size={15}/>Criar equipe</Button></Create></Header>
    {action.error && <Message>{(action.error as Error).message}</Message>}
    <Grid>{teamsQuery.data?.map(team=><TeamCard key={team.id} team={team} users={usersQuery.data??[]} projects={projectsQuery.data??[]} run={run=>action.mutateAsync(run)}/>)}</Grid>
    {!teamsQuery.isLoading && !teamsQuery.data?.length && <Message>Nenhuma equipe cadastrada.</Message>}
  </Page>;
}
