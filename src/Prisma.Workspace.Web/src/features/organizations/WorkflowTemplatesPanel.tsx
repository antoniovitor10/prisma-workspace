import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { GitBranch, Plus, Trash2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';

interface WorkflowTemplate {
  id: string; name: string; isDefault: boolean; isActive: boolean; version: number;
  statuses: Array<{ id:string; key:string; name:string; isActive:boolean }>;
}

const Shell=styled.section`border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surface};overflow:hidden;>header{display:flex;align-items:center;gap:8px;min-height:52px;padding:0 16px;border-bottom:1px solid ${({theme})=>theme.color.border};}h2{font-size:15px;}header p{margin-left:auto;font-size:12px;color:${({theme})=>theme.color.textMuted};}`;
const List=styled.div`display:grid;gap:9px;padding:16px;`;
const Row=styled.article`display:flex;align-items:center;justify-content:space-between;gap:12px;padding:12px;border:1px solid ${({theme})=>theme.color.border};border-radius:9px;strong,small{display:block;}small{margin-top:5px;color:${({theme})=>theme.color.textMuted};}button{display:grid;width:34px;height:34px;place-items:center;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;color:${({theme})=>theme.color.danger};}`;
const Form=styled.form`display:flex;gap:8px;padding:0 16px 16px;input{flex:1;min-height:36px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;}button{display:flex;align-items:center;gap:6px;padding:0 12px;border-radius:7px;background:${({theme})=>theme.color.brand};color:${({theme})=>theme.color.onBrand};font-weight:800;}`;
const Message=styled.p`padding:0 16px 14px;color:${({theme})=>theme.color.danger};font-size:13px;font-weight:700;`;

export function WorkflowTemplatesPanel({ organizationId, canManage }: { organizationId:string; canManage:boolean }) {
  const client=useQueryClient();
  const [name,setName]=useState('');
  const [message,setMessage]=useState('');
  const query=useQuery<WorkflowTemplate[]>({queryKey:['organization-workflow-templates',organizationId],queryFn:()=>api.getOrganizationWorkflowTemplates(organizationId)});
  const refresh=()=>client.invalidateQueries({queryKey:['organization-workflow-templates',organizationId]});
  const create=useMutation({mutationFn:()=>api.createOrganizationWorkflowTemplate(organizationId,{
    name,isDefault:!(query.data??[]).some(template=>template.isDefault&&template.isActive),
    statuses:[
      {key:'READY',name:'A fazer',color:'#64748B',position:0,category:2,isInitial:true,isFinal:false,isActive:true},
      {key:'DOING',name:'Em andamento',color:'#3B82F6',position:1,category:3,isInitial:false,isFinal:false,isActive:true},
      {key:'DONE',name:'Concluído',color:'#22C55E',position:2,category:5,isInitial:false,isFinal:true,isActive:true}
    ],transitions:[{sourceKey:'READY',targetKey:'DOING'},{sourceKey:'DOING',targetKey:'DONE'}]
  }),onSuccess:async()=>{setName('');setMessage('');await refresh();},onError:error=>setMessage((error as Error).message)});
  const disable=useMutation({mutationFn:(id:string)=>api.disableOrganizationWorkflowTemplate(organizationId,id),onSuccess:refresh,onError:error=>setMessage((error as Error).message)});
  const submit=(event:FormEvent)=>{event.preventDefault();if(name.trim())create.mutate();};
  return <Shell><header><GitBranch size={16}/><h2>Templates de workflow</h2><p>Herança dinâmica para novos projetos</p></header><List>
    {(query.data??[]).filter(template=>template.isActive).map(template=><Row key={template.id}><div><strong>{template.name}{template.isDefault?' · padrão':''}</strong><small>Versão {template.version} · {template.statuses.filter(status=>status.isActive).map(status=>status.name).join(' → ')}</small></div>{canManage&&<button aria-label={`Desativar ${template.name}`} title="Desativar" onClick={()=>window.confirm('Desativar este template sem projetos herdados?')&&disable.mutate(template.id)}><Trash2 size={14}/></button>}</Row>)}
    {!query.isLoading&&!(query.data??[]).some(template=>template.isActive)&&<p>Nenhum template ativo. Crie o fluxo padrão da organização.</p>}
  </List>{canManage&&<Form onSubmit={submit}><input required maxLength={160} placeholder="Nome do novo template" value={name} onChange={event=>setName(event.target.value)}/><button disabled={create.isPending}><Plus size={14}/>Criar fluxo padrão</button></Form>}{message&&<Message role="alert">{message}</Message>}</Shell>;
}
