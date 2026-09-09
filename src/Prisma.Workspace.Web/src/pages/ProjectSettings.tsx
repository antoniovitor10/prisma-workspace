import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Archive, Check, Clock3, FolderCog, Plus, RotateCcw, Tags, Trash2, UserPlus, Users } from 'lucide-react';
import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useOutletContext } from 'react-router-dom';
import styled from 'styled-components';
import { previewMode } from '../preview';
import { api } from '../services/api';
import type { ProjectSummary } from './Projects';
import { workNatureOptions, workTypeOptions } from './projectClassification';
import { ProjectWorkflowSettings } from '../features/workflow/ProjectWorkflowSettings';
import { ExternalPortalSettings } from '../features/portal/ExternalPortalSettings';
import { Page as BasePage } from '../components/PageLayout';

interface OrganizationMember { userId: string; name: string; role: number; isActive: boolean; }
interface TeamSummary { id: string; name: string; }
interface CatalogTag { id: string; name: string; color: string; }
interface ProjectHistory { id: string; actorId: string; kind: string; payload?: string; createdAt: string; }

const Page = styled(BasePage)`max-width:1280px;padding-top:24px;`;
const Grid = styled.div`display:grid; grid-template-columns:minmax(0,1.15fr) minmax(300px,.85fr); gap:16px; @media(max-width:900px){grid-template-columns:1fr;}`;
const Section = styled.section`
  align-self:start;overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};
  >header{display:flex;align-items:center;gap:8px;min-height:54px;padding:0 17px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.surfaceSubtle};h2{font-size:14px;}small{margin-left:auto;color:${({theme})=>theme.color.textMuted};font-size:12px;}svg{color:${({theme})=>theme.color.brand};}}
`;
const Wide = styled(Section)`grid-column:1/-1;`;
const Form = styled.form`
  display:grid; grid-template-columns:1fr 1fr; gap:12px; padding:17px;
  label{display:grid; gap:5px; color:${({theme})=>theme.color.textMuted}; font-size:13px; font-weight:800;}
  label.wide{grid-column:1/-1;} input,textarea,select{width:100%;min-height:42px;padding:8px 11px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};font:inherit;} textarea{min-height:98px;resize:vertical;}
  footer{grid-column:1/-1;display:flex;align-items:center;justify-content:flex-end;gap:9px;} @media(max-width:580px){grid-template-columns:1fr;label.wide,footer{grid-column:auto;}}
`;
const Button = styled.button<{ $danger?: boolean; $secondary?: boolean }>`
  display:inline-flex; min-height:34px; align-items:center; justify-content:center; gap:6px; padding:0 11px; border:1px solid ${({theme,$danger,$secondary})=>$danger?theme.color.danger:$secondary?theme.color.border:'transparent'}; border-radius:${({theme})=>theme.radius.md}; background:${({theme,$danger,$secondary})=>$danger?'transparent':$secondary?theme.color.surface:theme.color.brand}; color:${({theme,$danger,$secondary})=>$danger?theme.color.danger:$secondary?theme.color.text:theme.color.onBrand}; font-size:13px; font-weight:800; &:disabled{opacity:.5;}
`;
const List = styled.div`display:grid;`;
const Row = styled.div`display:grid; grid-template-columns:minmax(0,1fr) auto; gap:10px; align-items:center; min-height:52px; padding:9px 16px; border-bottom:1px solid ${({theme})=>theme.color.neutral[100]}; strong{display:block;font-size:13.5px;} small{color:${({theme})=>theme.color.textMuted};font-size:12px;} select{min-height:32px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13px;}`;
const Chips = styled.div`display:flex;flex-wrap:wrap;gap:7px;padding:16px;`;
const Chip = styled.button<{ $on?: boolean; $color?: string }>`padding:5px 9px;border:1px solid ${({$color,theme})=>$color||theme.color.border};border-radius:999px;background:${({$on,$color})=>$on?($color||'#2563EB'):'transparent'};color:${({$on,$color,theme})=>$on?'white':($color||theme.color.textMuted)};font-size:13px;font-weight:750;`;
const InlineForm = styled.form`display:flex;flex-wrap:wrap;gap:8px;padding:14px 16px;border-top:1px solid ${({theme})=>theme.color.border};input,select{min-height:34px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13px;}input{flex:1;min-width:160px;}`;
const Empty = styled.p`padding:18px;color:${({theme})=>theme.color.textMuted};font-size:13px;text-align:center;`;
const History = styled.div`display:grid;max-height:330px;overflow:auto;`;
const Event = styled.div`padding:11px 16px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};font-size:13px;strong{display:block;margin-bottom:3px;}small{color:${({theme})=>theme.color.textMuted};}`;

const statusOptions = [[1,'Planejamento'],[2,'Ativo'],[3,'Pausado'],[4,'Concluído'],[5,'Cancelado']] as const;
const projectRoles = [[1,'Visualizador'],[2,'Membro'],[3,'Scrum Master'],[4,'Product Owner'],[5,'Administrador']] as const;
const fieldTypes = [[1,'Texto'],[2,'Número'],[3,'Data'],[4,'Sim/não'],[5,'Seleção'],[6,'Seleção múltipla'],[7,'Texto longo'],[8,'Percentual'],[9,'Data e hora'],[10,'Usuário'],[11,'Equipe'],[12,'URL']] as const;

export function ProjectSettings() {
  const { project } = useOutletContext<{project: ProjectSummary}>();
  const queryClient = useQueryClient();
  const [savedAt, setSavedAt] = useState('');
  const [form, setForm] = useState({
    name: project.name, description: project.description ?? '', ownerId: project.ownerId ?? '',
    startDate: project.startDate ?? '', dueDate: project.dueDate ?? '', status: project.status ?? 2,
    methodology: project.methodology ?? 1, nature: project.nature ?? 0, workType: project.workType ?? 0,
    tagIds: project.tags?.map(tag=>tag.id) ?? [] as string[],
  });
  const [member, setMember] = useState({userId:'',role:2});
  const [field, setField] = useState({name:'',type:1,isRequired:false,options:''});

  const membersQuery = useQuery<OrganizationMember[]>({queryKey:['organization','members'],queryFn:()=>api.getOrganizationMembers(),enabled:!previewMode});
  const tagsQuery = useQuery<CatalogTag[]>({queryKey:['tags'],queryFn:()=>api.getTags(),enabled:!previewMode,initialData:previewMode?project.tags??[]:undefined});
  const teamsQuery = useQuery<TeamSummary[]>({queryKey:['teams'],queryFn:()=>api.getTeams(),enabled:!previewMode,initialData:previewMode?project.teams:undefined});
  const historyQuery = useQuery<ProjectHistory[]>({queryKey:['project-history',project.id],queryFn:()=>api.getProjectHistory(project.id),enabled:!previewMode});
  const memberNames = useMemo(()=>new Map(membersQuery.data?.map(item=>[item.userId,item.name])??[]),[membersQuery.data]);

  useEffect(()=>{
    const fallbackOwner=project.ownerId||project.members?.find(item=>item.role===5)?.userId||membersQuery.data?.find(item=>item.isActive)?.userId||'';
    setForm({name:project.name,description:project.description??'',ownerId:fallbackOwner,startDate:project.startDate??'',dueDate:project.dueDate??'',status:project.status??2,methodology:project.methodology??1,nature:project.nature??0,workType:project.workType??0,tagIds:project.tags?.map(tag=>tag.id)??[]});
  },[project,membersQuery.data]);

  const refresh=async()=>Promise.all([
    queryClient.invalidateQueries({queryKey:['project',project.id]}),
    queryClient.invalidateQueries({queryKey:['projects']}),
    queryClient.invalidateQueries({queryKey:['project-history',project.id]}),
  ]);
  const save=useMutation({
    mutationFn:()=>previewMode?Promise.resolve():api.updateProjectDetails(project.id,{...form,description:form.description||null,startDate:form.startDate||null,dueDate:form.dueDate||null,settingsJson:project.settingsJson??null}),
    onMutate:async()=>{
      await queryClient.cancelQueries({queryKey:['project',project.id]});
      const previous=queryClient.getQueryData<ProjectSummary>(['project',project.id]);
      queryClient.setQueryData<ProjectSummary>(['project',project.id],current=>current?{...current,...form,description:form.description||undefined}:current);
      return {previous};
    },
    onSuccess:async()=>{setSavedAt(new Date().toLocaleTimeString('pt-BR',{hour:'2-digit',minute:'2-digit'}));await refresh();},
    onError:(_error,_variables,context)=>{
      if(context?.previous)queryClient.setQueryData(['project',project.id],context.previous);
    },
  });
  const lifecycle=useMutation({mutationFn:(archive:boolean)=>previewMode?Promise.resolve():archive?api.archiveProject(project.id):api.reactivateProject(project.id),onSuccess:refresh});
  const saveMember=useMutation({mutationFn:(value:{userId:string;role:number})=>api.setProjectMember(project.id,value.userId,value.role),onSuccess:refresh});
  const removeMember=useMutation({mutationFn:(userId:string)=>api.removeProjectMember(project.id,userId),onSuccess:refresh});
  const setTeam=useMutation({mutationFn:({teamId,on}:{teamId:string;on:boolean})=>on?api.addProjectTeam(project.id,teamId):api.removeProjectTeam(project.id,teamId),onSuccess:refresh});
  const saveField=useMutation({mutationFn:()=>api.saveProjectCustomField(project.id,null,{name:field.name,type:field.type,isRequired:field.isRequired,optionsJson:field.type===5||field.type===6?JSON.stringify(field.options.split(',').map(value=>value.trim()).filter(Boolean)):null,position:(project.customFields?.length??0)*100}),onSuccess:async()=>{setField({name:'',type:1,isRequired:false,options:''});await refresh();}});
  const disableField=useMutation({mutationFn:(id:string)=>api.deleteProjectCustomField(project.id,id),onSuccess:refresh});
  const submit=(event:FormEvent)=>{event.preventDefault();if(form.ownerId)save.mutate();};
  const submitMember=(event:FormEvent)=>{event.preventDefault();if(member.userId){saveMember.mutate(member);setMember({userId:'',role:2});}};
  const submitField=(event:FormEvent)=>{event.preventDefault();if(field.name.trim())saveField.mutate();};

  return <Page><Grid>
    <Section><header><FolderCog size={16}/><h2>Dados do projeto</h2>{savedAt&&<small>Salvo às {savedAt}</small>}</header>
      <Form onSubmit={submit}>
        <label className="wide">Nome<input required value={form.name} onChange={e=>setForm({...form,name:e.target.value})}/></label>
        <label className="wide">Descrição<textarea value={form.description} onChange={e=>setForm({...form,description:e.target.value})}/></label>
        <label>Responsável<select aria-label="Responsável" required value={form.ownerId} onChange={e=>setForm({...form,ownerId:e.target.value})}><option value="">Selecione...</option>{membersQuery.data?.filter(item=>item.isActive).map(item=><option key={item.userId} value={item.userId}>{item.name}</option>)}</select></label>
        <label>Status{project.isArchived
          ?<select aria-label="Status do projeto" value={0} disabled title="Reative o projeto em Ciclo de vida para alterar o status"><option value={0}>Arquivado</option></select>
          :<select aria-label="Status do projeto" value={form.status} onChange={e=>setForm({...form,status:Number(e.target.value)})}>{statusOptions.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select>}</label>
        <label>Natureza<select required aria-label="Natureza" value={form.nature} onChange={e=>setForm({...form,nature:Number(e.target.value)})}><option value={0} disabled>Selecione...</option>{workNatureOptions.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></label>
        <label>Tipo de Trabalho<select required aria-label="Tipo de Trabalho" value={form.workType} onChange={e=>setForm({...form,workType:Number(e.target.value)})}><option value={0} disabled>Selecione...</option>{workTypeOptions.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></label>
        <label>Data de início<input type="date" value={form.startDate} onChange={e=>setForm({...form,startDate:e.target.value})}/></label>
        <label>Prazo<input type="date" min={form.startDate||undefined} value={form.dueDate} onChange={e=>setForm({...form,dueDate:e.target.value})}/></label>
        <footer>{save.error&&<small>{(save.error as Error).message}</small>}<Button disabled={save.isPending||!form.ownerId||!form.nature||!form.workType}><Check size={13}/>Salvar alterações</Button></footer>
      </Form>
    </Section>
    <Section><header><Archive size={16}/><h2>Ciclo de vida</h2></header><div style={{padding:17}}><p style={{fontSize:13.5,color:'#64748b',lineHeight:1.6,marginBottom:14}}>Arquivar preserva tarefas, horas, sprints e histórico. O projeto pode ser reativado depois.</p>{project.isArchived?<Button onClick={()=>lifecycle.mutate(false)}><RotateCcw size={13}/>Reativar projeto</Button>:<Button $danger onClick={()=>lifecycle.mutate(true)}><Archive size={13}/>Arquivar projeto</Button>}</div></Section>

    <Wide><header><Users size={16}/><h2>Membros e papéis</h2><small>{project.members?.length??0} pessoa(s)</small></header><List>{project.members?.map(item=><Row key={item.userId}><div><strong>{memberNames.get(item.userId)??item.userId}</strong><small>{item.userId===project.ownerId?'Responsável principal':'Membro do projeto'}</small></div><div style={{display:'flex',gap:7}}><select aria-label={`Papel de ${memberNames.get(item.userId)??item.userId}`} value={item.role} onChange={e=>saveMember.mutate({userId:item.userId,role:Number(e.target.value)})}>{projectRoles.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select>{item.userId!==project.ownerId&&<Button $danger aria-label="Remover membro" onClick={()=>removeMember.mutate(item.userId)}><Trash2 size={12}/></Button>}</div></Row>)}{!project.members?.length&&<Empty>Nenhum membro adicional.</Empty>}</List><InlineForm onSubmit={submitMember}><select aria-label="Adicionar membro" required value={member.userId} onChange={e=>setMember({...member,userId:e.target.value})}><option value="">Adicionar membro...</option>{membersQuery.data?.filter(item=>item.isActive&&!project.members?.some(current=>current.userId===item.userId)).map(item=><option key={item.userId} value={item.userId}>{item.name}</option>)}</select><select aria-label="Papel do novo membro" value={member.role} onChange={e=>setMember({...member,role:Number(e.target.value)})}>{projectRoles.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select><Button><UserPlus size={13}/>Adicionar</Button></InlineForm></Wide>

    <Section><header><Users size={16}/><h2>Equipes</h2><small>Salvo automaticamente ao marcar</small></header><List>{teamsQuery.data?.map(team=>{const on=project.teams.some(item=>item.id===team.id);return <Row key={team.id}><div><strong>{team.name}</strong><small>{on?'Associada ao projeto':'Disponível'}</small></div><label style={{fontSize:13}}><input type="checkbox" checked={on} onChange={e=>setTeam.mutate({teamId:team.id,on:e.target.checked})}/>Usar no projeto</label></Row>})}</List></Section>
    <Section><header><Tags size={16}/><h2>Etiquetas do projeto</h2></header><Chips>{tagsQuery.data?.map(tag=><Chip type="button" key={tag.id} $on={form.tagIds.includes(tag.id)} $color={tag.color} onClick={()=>setForm({...form,tagIds:form.tagIds.includes(tag.id)?form.tagIds.filter(id=>id!==tag.id):[...form.tagIds,tag.id]})}>{tag.name}</Chip>)}</Chips><Empty>As etiquetas selecionadas são salvas junto com os dados gerais.</Empty></Section>

    <Section><header><Plus size={16}/><h2>Campos personalizados</h2></header><List>{project.customFields?.filter(item=>item.isActive).map(item=><Row key={item.id}><div><strong>{item.name}</strong><small>{fieldTypes.find(([id])=>id===item.type)?.[1]}{item.isRequired?' · obrigatório':''}</small></div><Button $danger aria-label="Desativar campo" onClick={()=>disableField.mutate(item.id)}><Trash2 size={12}/></Button></Row>)}{!project.customFields?.some(item=>item.isActive)&&<Empty>Nenhum campo personalizado.</Empty>}</List><InlineForm onSubmit={submitField}><input required placeholder="Nome do campo" value={field.name} onChange={e=>setField({...field,name:e.target.value})}/><select aria-label="Tipo do campo personalizado" value={field.type} onChange={e=>setField({...field,type:Number(e.target.value)})}>{fieldTypes.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select>{(field.type===5||field.type===6)&&<input required placeholder="Opções separadas por vírgula" value={field.options} onChange={e=>setField({...field,options:e.target.value})}/>}<label style={{display:'flex',alignItems:'center',gap:5,fontSize:13}}><input type="checkbox" checked={field.isRequired} onChange={e=>setField({...field,isRequired:e.target.checked})}/>Obrigatório</label><Button><Plus size={13}/>Criar</Button></InlineForm></Section>
    <Section><header><Clock3 size={16}/><h2>Histórico do projeto</h2></header><History>{historyQuery.data?.map(item=><Event key={item.id}><strong>{item.kind.replaceAll('_',' ')}</strong><small>{new Date(item.createdAt).toLocaleString('pt-BR')} · {memberNames.get(item.actorId)??item.actorId}</small></Event>)}{!historyQuery.data?.length&&<Empty>Nenhuma alteração administrativa registrada.</Empty>}</History></Section>
    <ProjectWorkflowSettings projectId={project.id}/>
    <ExternalPortalSettings project={project}/>
  </Grid></Page>;
}
