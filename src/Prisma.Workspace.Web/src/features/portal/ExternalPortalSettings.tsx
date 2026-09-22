import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Copy, ExternalLink, FileText, Globe2, Mail, Plus, Save, Trash2 } from 'lucide-react';
import styled from 'styled-components';
import type { ProjectSummary } from '../../pages/Projects';
import { previewMode } from '../../preview';
import { api } from '../../services/api';
import type { ExternalForm, ExternalFormField, ExternalPortal } from '../../types/portal';

interface OrganizationMember { userId: string; name: string; isActive: boolean; }
interface Workflow { stages: Array<{ id: string; boardId: string; name: string }>; }
type FormPayload = Omit<ExternalForm, 'id' | 'projectId' | 'externalPortalId' | 'publicPath'>;

const Section=styled.section`grid-column:1/-1;overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surface};>header{display:flex;align-items:center;gap:8px;min-height:50px;padding:0 16px;border-bottom:1px solid ${({theme})=>theme.color.border};h2{font-size:15px;}small{margin-left:auto;color:${({theme})=>theme.color.textMuted};font-size:12px;}}`;
const Form=styled.form`display:grid;grid-template-columns:1fr 1fr;gap:12px;padding:17px;label{display:grid;gap:5px;color:${({theme})=>theme.color.textMuted};font-size:13px;font-weight:800;}label.wide{grid-column:1/-1;}input,select,textarea{width:100%;min-height:38px;padding:8px 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font:inherit;}textarea{min-height:76px;resize:vertical;}footer{grid-column:1/-1;display:flex;align-items:center;justify-content:flex-end;gap:8px;}@media(max-width:650px){grid-template-columns:1fr;label.wide,footer{grid-column:auto;}}`;
const Modes=styled.div`grid-column:1/-1;display:flex;flex-wrap:wrap;gap:8px;label{display:flex;flex-direction:row;align-items:center;padding:8px 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};input{width:auto;min-height:auto;}}`;
const CheckboxLabel = styled.label`&&{display:flex;align-items:center;gap:8px;line-height:1.4;color:${({theme})=>theme.color.text};font-size:13px;font-weight:700;}&& input[type=checkbox]{width:16px;min-width:16px;max-width:16px;height:16px;min-height:16px;margin:0;padding:0;flex:0 0 16px;accent-color:${({theme})=>theme.color.brand};}`;
const Button=styled.button<{ $secondary?:boolean; $danger?:boolean }>`display:inline-flex;min-height:34px;align-items:center;justify-content:center;gap:6px;padding:0 11px;border:1px solid ${({$secondary,$danger,theme})=>$danger?theme.color.danger:$secondary?theme.color.border:'transparent'};border-radius:${({theme})=>theme.radius.md};background:${({$secondary,$danger,theme})=>$secondary||$danger?theme.color.surface:theme.color.brand};color:${({$danger,$secondary,theme})=>$danger?theme.color.danger:$secondary?theme.color.text:theme.color.onBrand};font-size:13px;font-weight:800;&:disabled{opacity:.5;}`;
const PublicLink=styled.div`grid-column:1/-1;display:flex;flex-wrap:wrap;align-items:center;gap:8px;padding:10px;border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.neutral[50]};code{flex:1;min-width:220px;font-size:13px;overflow-wrap:anywhere;}`;
const Invitation=styled.form`display:flex;flex-wrap:wrap;gap:8px;padding:0 17px 17px;input{flex:1;min-width:230px;min-height:36px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};}`;
const Notice=styled.p`grid-column:1/-1;color:${({theme})=>theme.color.textMuted};font-size:13px;line-height:1.5;`;
const Builder=styled.div`border-top:1px solid ${({theme})=>theme.color.border};padding:17px;h3{font-size:14px;}p{margin-top:4px;color:${({theme})=>theme.color.textMuted};font-size:13px;}`;
const BuilderGrid=styled.div`display:grid;grid-template-columns:230px minmax(0,1fr);gap:14px;margin-top:14px;@media(max-width:840px){grid-template-columns:1fr;}`;
const FormList=styled.div`display:grid;align-content:start;gap:7px;`;
const FormCard=styled.button<{ $active:boolean }>`display:grid;gap:3px;padding:11px;border:1px solid ${({$active,theme})=>$active?theme.color.accentBlue:theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({$active,theme})=>$active?`color-mix(in srgb, ${theme.color.accentBlue} 7%, white)`:theme.color.surface};text-align:left;strong{font-size:13px;}small{color:${({theme})=>theme.color.textMuted};font-size:11px;}`;
const Editor=styled.div`display:grid;gap:12px;`;
const EditorGrid=styled.div`display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:9px;label{display:grid;gap:4px;color:${({theme})=>theme.color.textMuted};font-size:12px;font-weight:800;}label.wide{grid-column:1/-1;}input,select,textarea{width:100%;min-height:35px;padding:7px 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font:inherit;font-size:13px;}textarea{min-height:68px;}@media(max-width:620px){grid-template-columns:1fr;label.wide{grid-column:auto;}}`;
const Fields=styled.div`display:grid;gap:7px;`;
const FieldRow=styled.div`display:grid;grid-template-columns:minmax(120px,1fr) 112px 112px 78px minmax(120px,1fr) 32px;gap:6px;align-items:end;padding:8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};label{display:grid;gap:3px;color:${({theme})=>theme.color.textMuted};font-size:11px;font-weight:750;}input,select{width:100%;min-height:31px;padding:0 6px;border:1px solid ${({theme})=>theme.color.border};border-radius:6px;font-size:12px;}label.check{display:flex;min-height:31px;align-items:center;gap:4px;}label.check input{width:auto;min-height:auto;}@media(max-width:980px){grid-template-columns:1fr 1fr 1fr;}`;
const RuleRow=styled(FieldRow)`grid-template-columns:minmax(100px,1fr) 100px minmax(100px,1fr) minmax(120px,1fr) minmax(120px,1fr) 90px 32px;`;
const Toolbar=styled.div`display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:8px;h4{font-size:13px;}`;

const modeOptions=[[1,'Link público'],[2,'Login'],[4,'Convite'],[8,'Código por e-mail']] as const;
const fieldTypes=[[1,'Texto'],[2,'Texto longo'],[3,'E-mail'],[4,'Telefone'],[5,'Seleção'],[6,'Número'],[7,'Sim/não'],[8,'Data'],[9,'Arquivo']] as const;
const fieldKinds=[[0,'Personalizado'],[1,'Assunto'],[2,'Descrição'],[3,'Categoria'],[4,'Prioridade'],[5,'Nome'],[6,'E-mail'],[7,'Telefone'],[8,'Serviço'],[9,'Anexos']] as const;

const defaultFields:ExternalFormField[]=[
  {key:'requesterName',label:'Nome',type:1,kind:5,isRequired:true,position:0,maxLength:200},
  {key:'requesterEmail',label:'E-mail',type:3,kind:6,isRequired:true,position:1,maxLength:320},
  {key:'requesterPhone',label:'Telefone',type:4,kind:7,isRequired:false,position:2,maxLength:30},
  {key:'subject',label:'Assunto',type:1,kind:1,isRequired:true,position:3,maxLength:500},
  {key:'description',label:'Descrição detalhada',type:2,kind:2,isRequired:true,position:4,maxLength:10000},
  {key:'category',label:'Categoria',type:1,kind:3,isRequired:false,position:5,maxLength:120},
  {key:'priority',label:'Prioridade informada',type:5,kind:4,isRequired:false,position:6,options:['Baixa','Média','Alta','Crítica']},
  {key:'relatedService',label:'Serviço relacionado',type:1,kind:8,isRequired:false,position:7,maxLength:200},
  {key:'attachments',label:'Anexos',type:9,kind:9,isRequired:false,position:8},
];

const newForm=(project:ProjectSummary,portal?:ExternalPortal|null):ExternalForm=>({
  id:'',projectId:project.id,externalPortalId:portal?.id??'',publicSlug:`formulario-${(portal?.forms.length??0)+1}`,
  title:'Novo formulário',description:'',category:'',confirmationMessage:'Recebemos sua solicitação. Guarde o protocolo e a chave de acompanhamento.',
  isEnabled:true,isDefault:!portal?.forms.length,defaultPriority:1,maxFiles:5,maxFileSizeBytes:10_000_000,
  allowedExtensions:'.pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx',
  allowedMimeTypes:'application/pdf,image/png,image/jpeg,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  minimumCompletionSeconds:2,fields:defaultFields.map(field=>({...field})),assignmentRules:[],publicPath:'',
});

const payloadOf=(form:ExternalForm):FormPayload=>({
  publicSlug:form.publicSlug,title:form.title,description:form.description,category:form.category,
  confirmationMessage:form.confirmationMessage,isEnabled:form.isEnabled,isDefault:form.isDefault,
  defaultPriority:form.defaultPriority,initialStageId:form.initialStageId,defaultTeamId:form.defaultTeamId,
  defaultResponsibleId:form.defaultResponsibleId,maxFiles:form.maxFiles,maxFileSizeBytes:form.maxFileSizeBytes,
  allowedExtensions:form.allowedExtensions,allowedMimeTypes:form.allowedMimeTypes,
  minimumCompletionSeconds:form.minimumCompletionSeconds,fields:form.fields,assignmentRules:form.assignmentRules,
});

export function ExternalPortalSettings({project}:{project:ProjectSummary}){
  const queryClient=useQueryClient();
  const [portalForm,setPortalForm]=useState({boardId:project.boards[0]?.id??'',publicSlug:project.key.toLowerCase().replace(/[^a-z0-9-]/g,'-'),isEnabled:false,requiresAuthentication:false,accessModes:9});
  const [inviteEmail,setInviteEmail]=useState('');
  const [invitationLink,setInvitationLink]=useState('');
  const [message,setMessage]=useState('');
  const [draft,setDraft]=useState<ExternalForm|null>(null);
  const portalQuery=useQuery<ExternalPortal|null>({queryKey:['external-portal',project.id],enabled:!previewMode,queryFn:()=>api.getExternalPortal(project.id)});
  const formsQuery=useQuery<ExternalForm[]>({queryKey:['external-forms',project.id],enabled:!previewMode&&Boolean(portalQuery.data?.id),queryFn:()=>api.getExternalForms(project.id)});
  const membersQuery=useQuery<OrganizationMember[]>({queryKey:['organization','members'],enabled:!previewMode,queryFn:()=>api.getOrganizationMembers()});
  const workflowQuery=useQuery<Workflow>({queryKey:['workflow',project.id],enabled:!previewMode,queryFn:()=>api.getProjectWorkflow(project.id)});
  const projectMembers=useMemo(()=>new Set(project.members?.map(item=>item.userId)??[]),[project.members]);
  const activeMembers=membersQuery.data?.filter(item=>item.isActive&&projectMembers.has(item.userId))??[];
  const boardStages=workflowQuery.data?.stages.filter(stage=>stage.boardId===portalForm.boardId)??[];

  useEffect(()=>{if(portalQuery.data)setPortalForm({boardId:portalQuery.data.boardId,publicSlug:portalQuery.data.publicSlug,isEnabled:portalQuery.data.isEnabled,requiresAuthentication:portalQuery.data.requiresAuthentication,accessModes:portalQuery.data.accessModes});},[portalQuery.data]);
  useEffect(()=>{if(!draft&&formsQuery.data?.length)setDraft(formsQuery.data.find(item=>item.isDefault)??formsQuery.data[0]);},[draft,formsQuery.data]);

  const savePortal=useMutation({mutationFn:()=>previewMode?Promise.resolve({...portalForm,id:'preview',projectId:project.id,projectName:project.name,publicPath:`/portal/${portalForm.publicSlug}`,forms:[]}):api.saveExternalPortal(project.id,portalForm),onSuccess:async()=>{setMessage('Configuração do portal salva.');await queryClient.invalidateQueries({queryKey:['external-portal',project.id]});await queryClient.invalidateQueries({queryKey:['external-forms',project.id]});},onError:error=>setMessage((error as Error).message)});
  const saveForm=useMutation({mutationFn:()=>draft?api.saveExternalForm(project.id,payloadOf(draft),draft.id||undefined):Promise.resolve(),onSuccess:async(value)=>{setMessage('Formulário salvo e publicado.');setDraft(value as ExternalForm);await queryClient.invalidateQueries({queryKey:['external-forms',project.id]});await queryClient.invalidateQueries({queryKey:['external-portal',project.id]});},onError:error=>setMessage((error as Error).message)});
  const invite=useMutation({mutationFn:()=>api.createExternalPortalInvitation(project.id,inviteEmail),onSuccess:(result)=>{setInvitationLink(`${window.location.origin}${result.invitationPath}`);setInviteEmail('');},onError:error=>setMessage((error as Error).message)});
  const toggleMode=(mode:number)=>setPortalForm({...portalForm,accessModes:(portalForm.accessModes&mode)?portalForm.accessModes&~mode:portalForm.accessModes|mode});
  const publicUrl=`${window.location.origin}/portal/${portalForm.publicSlug}`;
  const updateField=(index:number,change:Partial<ExternalFormField>)=>draft&&setDraft({...draft,fields:draft.fields.map((field,current)=>current===index?{...field,...change}:field)});
  const addField=()=>draft&&setDraft({...draft,fields:[...draft.fields,{key:`campo${draft.fields.length+1}`,label:'Novo campo',type:1,kind:0,isRequired:false,position:draft.fields.length}]});
  const removeField=(index:number)=>draft&&setDraft({...draft,fields:draft.fields.filter((_,current)=>current!==index).map((field,position)=>({...field,position}))});
  const addRule=()=>draft&&setDraft({...draft,assignmentRules:[...draft.assignmentRules,{fieldKey:draft.fields[0]?.key??'',operator:1,expectedValue:'',position:draft.assignmentRules.length}]});
  const updateRule=(index:number,change:Partial<ExternalForm['assignmentRules'][number]>)=>draft&&setDraft({...draft,assignmentRules:draft.assignmentRules.map((rule,current)=>current===index?{...rule,...change}:rule)});

  return <Section><header><Globe2 size={16}/><h2>Portal Externo e formulários</h2><small>{portalForm.isEnabled?'Publicado':'Desativado'}</small></header>
    <Form onSubmit={(event:FormEvent)=>{event.preventDefault();if(portalForm.boardId&&portalForm.accessModes)savePortal.mutate();}}>
      <label>Quadro de entrada<select required value={portalForm.boardId} onChange={event=>setPortalForm({...portalForm,boardId:event.target.value})}><option value="">Selecione...</option>{project.boards.map(board=><option key={board.id} value={board.id}>{board.name}</option>)}</select></label>
      <label>Endereço público<input required minLength={3} maxLength={80} value={portalForm.publicSlug} onChange={event=>setPortalForm({...portalForm,publicSlug:event.target.value.toLowerCase().replace(/[^a-z0-9-]/g,'-')})}/></label>
      <Modes>{modeOptions.map(([mode,label])=><label key={mode}><input type="checkbox" checked={Boolean(portalForm.accessModes&mode)} onChange={()=>toggleMode(mode)}/>{label}</label>)}</Modes>
      <CheckboxLabel><input type="checkbox" checked={portalForm.isEnabled} onChange={event=>setPortalForm({...portalForm,isEnabled:event.target.checked})}/>Portal habilitado</CheckboxLabel>
      <CheckboxLabel><input type="checkbox" checked={portalForm.requiresAuthentication} onChange={event=>setPortalForm({...portalForm,requiresAuthentication:event.target.checked})}/>Exigir autenticação</CheckboxLabel>
      <Notice>O acompanhamento individual sempre exige protocolo e chave. Os envios usam rate limiting, honeypot e validação de campos e arquivos.</Notice>
      {portalForm.isEnabled&&<PublicLink><code>{publicUrl}</code><Button type="button" $secondary onClick={()=>void navigator.clipboard.writeText(publicUrl)}><Copy size={12}/>Copiar</Button><a href={publicUrl} target="_blank" rel="noreferrer"><Button as="span" $secondary><ExternalLink size={12}/>Abrir</Button></a></PublicLink>}
      <footer>{message&&<Notice>{message}</Notice>}<Button disabled={savePortal.isPending||!project.boards.length}><Check size={13}/>Salvar portal</Button></footer>
    </Form>
    {portalForm.isEnabled&&Boolean(portalForm.accessModes&4)&&<Invitation onSubmit={event=>{event.preventDefault();if(inviteEmail)invite.mutate();}}><input required type="email" placeholder="E-mail para convite" value={inviteEmail} onChange={event=>setInviteEmail(event.target.value)}/><Button disabled={invite.isPending}><Mail size={13}/>Gerar convite</Button>{invitationLink&&<PublicLink><code>{invitationLink}</code><Button type="button" $secondary onClick={()=>void navigator.clipboard.writeText(invitationLink)}><Copy size={12}/>Copiar link</Button></PublicLink>}</Invitation>}
    {portalQuery.data&&<Builder><Toolbar><div><h3>Formulários de entrada</h3><p>Crie campos obrigatórios, opcionais ou condicionais e defina a fila e a atribuição.</p></div><Button type="button" onClick={()=>setDraft(newForm(project,portalQuery.data))}><Plus size={12}/>Novo formulário</Button></Toolbar>
      <BuilderGrid><FormList>{formsQuery.data?.map(item=><FormCard type="button" key={item.id} $active={draft?.id===item.id} onClick={()=>setDraft(item)}><strong>{item.title}</strong><small>{item.publicSlug} · {item.isEnabled?'publicado':'desativado'}{item.isDefault?' · padrão':''}</small></FormCard>)}{!formsQuery.data?.length&&<Notice>Nenhum formulário configurado.</Notice>}</FormList>
        {draft&&<Editor><EditorGrid>
          <label>Título<input required value={draft.title} onChange={e=>setDraft({...draft,title:e.target.value})}/></label><label>Slug<input required value={draft.publicSlug} onChange={e=>setDraft({...draft,publicSlug:e.target.value.toLowerCase().replace(/[^a-z0-9-]/g,'-')})}/></label>
          <label className="wide">Descrição<textarea value={draft.description??''} onChange={e=>setDraft({...draft,description:e.target.value})}/></label><label>Categoria padrão<input value={draft.category??''} onChange={e=>setDraft({...draft,category:e.target.value})}/></label><label>Prioridade padrão<select value={draft.defaultPriority} onChange={e=>setDraft({...draft,defaultPriority:Number(e.target.value)})}><option value={0}>Baixa</option><option value={1}>Média</option><option value={2}>Alta</option><option value={3}>Crítica</option></select></label>
          <label>Etapa/fila inicial<select value={draft.initialStageId??''} onChange={e=>setDraft({...draft,initialStageId:e.target.value||undefined})}><option value="">Etapa inicial do quadro</option>{boardStages.map(stage=><option key={stage.id} value={stage.id}>{stage.name}</option>)}</select></label><label>Equipe padrão<select value={draft.defaultTeamId??''} onChange={e=>setDraft({...draft,defaultTeamId:e.target.value||undefined})}><option value="">Equipe do quadro</option>{project.teams.map(team=><option key={team.id} value={team.id}>{team.name}</option>)}</select></label>
          <label>Responsável padrão<select value={draft.defaultResponsibleId??''} onChange={e=>setDraft({...draft,defaultResponsibleId:e.target.value||undefined})}><option value="">Sem responsável</option>{activeMembers.map(member=><option key={member.userId} value={member.userId}>{member.name}</option>)}</select></label><label>Máximo de anexos<input type="number" min={0} max={20} value={draft.maxFiles} onChange={e=>setDraft({...draft,maxFiles:Number(e.target.value)})}/></label>
          <label>Limite por arquivo (MB)<input type="number" min={1} max={50} value={Math.round(draft.maxFileSizeBytes/1_000_000)} onChange={e=>setDraft({...draft,maxFileSizeBytes:Number(e.target.value)*1_000_000})}/></label><label>Tempo anti-spam (segundos)<input type="number" min={0} max={60} value={draft.minimumCompletionSeconds} onChange={e=>setDraft({...draft,minimumCompletionSeconds:Number(e.target.value)})}/></label>
          <label className="wide">Extensões permitidas<input value={draft.allowedExtensions} onChange={e=>setDraft({...draft,allowedExtensions:e.target.value})}/></label><label className="wide">Tipos MIME permitidos<input value={draft.allowedMimeTypes} onChange={e=>setDraft({...draft,allowedMimeTypes:e.target.value})}/></label>
          <label className="wide">Mensagem de confirmação<textarea value={draft.confirmationMessage??''} onChange={e=>setDraft({...draft,confirmationMessage:e.target.value})}/></label>
          <label><span><input type="checkbox" checked={draft.isEnabled} onChange={e=>setDraft({...draft,isEnabled:e.target.checked})}/> Publicado</span></label><label><span><input type="checkbox" checked={draft.isDefault} onChange={e=>setDraft({...draft,isDefault:e.target.checked})}/> Formulário padrão</span></label>
        </EditorGrid>
        <Toolbar><h4>Campos</h4><Button type="button" $secondary onClick={addField}><Plus size={11}/>Adicionar campo</Button></Toolbar><Fields>{draft.fields.map((field,index)=><FieldRow key={`${field.key}-${index}`}><label>Rótulo<input value={field.label} onChange={e=>updateField(index,{label:e.target.value})}/></label><label>Finalidade<select value={field.kind} onChange={e=>updateField(index,{kind:Number(e.target.value)})}>{fieldKinds.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></label><label>Tipo<select value={field.type} onChange={e=>updateField(index,{type:Number(e.target.value)})}>{fieldTypes.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></label><CheckboxLabel><input type="checkbox" checked={field.isRequired} onChange={e=>updateField(index,{isRequired:e.target.checked})}/>Obrigatório</CheckboxLabel><label>{field.type===5?'Opções e condição':'Condição simples'}{field.type===5&&<input placeholder="Opções separadas por vírgula" value={field.options?.join(', ')??''} onChange={e=>updateField(index,{options:e.target.value.split(',').map(value=>value.trim()).filter(Boolean)})}/>} {field.kind===0&&<select aria-label="Campo interno associado" value={field.projectCustomFieldId??''} onChange={e=>updateField(index,{projectCustomFieldId:e.target.value||undefined})}><option value="">Somente na solicitação</option>{project.customFields?.filter(item=>item.isActive).map(item=><option key={item.id} value={item.id}>{item.name}</option>)}</select>}<select value={field.conditionalFieldKey??''} onChange={e=>updateField(index,{conditionalFieldKey:e.target.value||undefined})}><option value="">Sempre visível</option>{draft.fields.filter((_,i)=>i!==index).map(option=><option key={option.key} value={option.key}>{option.label}</option>)}</select>{field.conditionalFieldKey&&<input placeholder="Valor esperado" value={field.conditionalValue??''} onChange={e=>updateField(index,{conditionalValue:e.target.value})}/>}</label><Button type="button" $danger aria-label="Remover campo" onClick={()=>removeField(index)}><Trash2 size={12}/></Button></FieldRow>)}</Fields>
        <Toolbar><h4>Regras de atribuição</h4><Button type="button" $secondary onClick={addRule}><Plus size={11}/>Adicionar regra</Button></Toolbar><Fields>{draft.assignmentRules.map((rule,index)=><RuleRow key={index}><label>Campo<select value={rule.fieldKey} onChange={e=>updateRule(index,{fieldKey:e.target.value})}>{draft.fields.map(field=><option key={field.key} value={field.key}>{field.label}</option>)}</select></label><label>Operador<select value={rule.operator} onChange={e=>updateRule(index,{operator:Number(e.target.value)})}><option value={0}>Sempre</option><option value={1}>Igual</option><option value={2}>Contém</option></select></label><label>Valor<input disabled={rule.operator===0} value={rule.expectedValue??''} onChange={e=>updateRule(index,{expectedValue:e.target.value})}/></label><label>Responsável<select value={rule.responsibleId??''} onChange={e=>updateRule(index,{responsibleId:e.target.value||undefined})}><option value="">Manter padrão</option>{activeMembers.map(member=><option key={member.userId} value={member.userId}>{member.name}</option>)}</select></label><label>Equipe<select value={rule.teamId??''} onChange={e=>updateRule(index,{teamId:e.target.value||undefined})}><option value="">Manter padrão</option>{project.teams.map(team=><option key={team.id} value={team.id}>{team.name}</option>)}</select></label><label>Prioridade<select value={rule.priority??''} onChange={e=>updateRule(index,{priority:e.target.value===''?undefined:Number(e.target.value)})}><option value="">Manter</option><option value={0}>Baixa</option><option value={1}>Média</option><option value={2}>Alta</option><option value={3}>Crítica</option></select></label><Button type="button" $danger onClick={()=>setDraft({...draft,assignmentRules:draft.assignmentRules.filter((_,current)=>current!==index)})}><Trash2 size={12}/></Button></RuleRow>)}</Fields>
        {draft.id&&<PublicLink><FileText size={12}/><code>{window.location.origin}{draft.publicPath}</code><a href={draft.publicPath} target="_blank" rel="noreferrer"><Button as="span" $secondary><ExternalLink size={12}/>Visualizar</Button></a></PublicLink>}
        <Toolbar><Notice>{saveForm.error?(saveForm.error as Error).message:'Alterações entram em vigor imediatamente após salvar.'}</Notice><Button type="button" disabled={saveForm.isPending} onClick={()=>saveForm.mutate()}><Save size={12}/>Salvar formulário</Button></Toolbar>
        </Editor>}
      </BuilderGrid>
    </Builder>}
  </Section>;
}
