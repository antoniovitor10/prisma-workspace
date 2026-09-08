import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, GitBranch, Plus, Save, Trash2 } from 'lucide-react';
import styled from 'styled-components';
import { api } from '../../services/api';
import { previewMode } from '../../preview';
import { useOrganization } from '../organizations/OrganizationState';
import { stageCategoryOptions } from './stageCategories';

interface WorkflowStatus {
  id: string; name: string; color: string; position: number; category: number;
  isInitial: boolean; isFinal: boolean;
}
interface WorkflowTransition { sourceStatusId: string; targetStatusId: string; }
interface WorkflowStage {
  id: string; boardId: string; boardName: string; name: string; position: number;
  wipLimit?: number | null; workflowStatusId?: string | null;
}
interface ProjectWorkflow {
  inheritanceMode: number; workflowTemplateId?: string | null; workflowTemplateName?: string | null;
  isSynchronized: boolean;
  statuses: WorkflowStatus[]; transitions: WorkflowTransition[]; stages: WorkflowStage[];
  boards: { id: string; name: string; cardSettingsJson?: string | null }[];
}
interface WorkflowTemplate { id:string; name:string; isDefault:boolean; isActive:boolean; version:number; }

const Shell = styled.section`
  grid-column:1/-1; border:1px solid ${({theme})=>theme.color.border};
  border-radius:${({theme})=>theme.radius.lg}; background:${({theme})=>theme.color.surface}; overflow:hidden;
  >header{display:flex;align-items:center;gap:8px;min-height:52px;padding:0 16px;border-bottom:1px solid ${({theme})=>theme.color.border};}
  h2{font-size:15px;} header p{margin-left:auto;font-size:12px;color:${({theme})=>theme.color.textMuted};}
`;
const Content = styled.div`display:grid;gap:22px;padding:18px;`;
const Block = styled.div`display:grid;gap:10px;h3{font-size:14px;}p{font-size:13px;color:${({theme})=>theme.color.textMuted};line-height:1.5;}`;
const StatusRow = styled.div`
  display:grid;grid-template-columns:34px minmax(150px,1fr) 140px 72px 72px auto;gap:8px;align-items:center;
  padding:8px;border:1px solid ${({theme})=>theme.color.border};border-radius:9px;
  input,select{min-height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;font-size:13px;}
  input[type=color]{padding:3px;width:34px;} label{display:flex;align-items:center;gap:5px;font-size:12px;font-weight:700;}
  @media(max-width:760px){grid-template-columns:34px 1fr 110px;label{grid-row:2}.actions{grid-column:3;}}
`;
const Actions = styled.div`display:flex;gap:4px;justify-content:flex-end;button{width:30px;height:30px;display:grid;place-items:center;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;color:${({theme})=>theme.color.textMuted};}`;
const AddForm = styled.form`display:flex;gap:8px;flex-wrap:wrap;input,select{min-height:35px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;font-size:13px;}input[type=text]{flex:1;min-width:180px;}`;
const Primary = styled.button`display:inline-flex;align-items:center;justify-content:center;gap:6px;min-height:35px;padding:0 12px;border-radius:7px;background:${({theme})=>theme.color.brand};color:${({theme})=>theme.color.onBrand};font-size:13px;font-weight:800;&:disabled{opacity:.5;}`;
const MatrixWrap = styled.div`overflow:auto;border:1px solid ${({theme})=>theme.color.border};border-radius:9px;table{border-collapse:collapse;min-width:520px;width:100%;}th,td{padding:8px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};text-align:center;font-size:12px;}th:first-child{text-align:left;position:sticky;left:0;background:${({theme})=>theme.color.surface};}input{width:16px;height:16px;}`;
const BoardGroup = styled.div`border:1px solid ${({theme})=>theme.color.border};border-radius:9px;overflow:hidden;>strong{display:block;padding:10px 12px;background:${({theme})=>theme.color.neutral[50]};font-size:13px;}`;
const StageRow = styled.div`display:grid;grid-template-columns:minmax(140px,1fr) minmax(150px,1fr) 110px auto;gap:8px;align-items:center;padding:8px 12px;border-top:1px solid ${({theme})=>theme.color.neutral[100]};input,select{min-height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;font-size:13px;}@media(max-width:680px){grid-template-columns:1fr 1fr;}`;
const ErrorText = styled.p`color:${({theme})=>theme.color.danger}!important;font-weight:700;`;
const ModePanel = styled.div`
  display:flex;align-items:center;justify-content:space-between;gap:14px;padding:14px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:9px;background:${({theme})=>theme.color.neutral[50]};
  strong,small{display:block;}small{margin-top:4px;color:${({theme})=>theme.color.textMuted};font-size:12px;}
  >div:last-child{display:flex;align-items:center;gap:8px;}select{min-height:35px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;background:${({theme})=>theme.color.surface};}
  @media(max-width:760px){align-items:stretch;flex-direction:column;>div:last-child{align-items:stretch;flex-direction:column;}}
`;
const Locked = styled.fieldset`display:contents;&:disabled{opacity:.72;}`;

const categories = stageCategoryOptions;
const emptyWorkflow: ProjectWorkflow = { inheritanceMode:1, isSynchronized:true, statuses: [], transitions: [], stages: [], boards: [] };

export function ProjectWorkflowSettings({ projectId }: { projectId: string }) {
  const { current } = useOrganization();
  const queryClient = useQueryClient();
  const query = useQuery<ProjectWorkflow>({
    queryKey:['project-workflow',projectId], queryFn:()=>api.getProjectWorkflow(projectId),
    enabled:!previewMode, initialData:previewMode?emptyWorkflow:undefined
  });
  const templatesQuery=useQuery<WorkflowTemplate[]>({
    queryKey:['organization-workflow-templates',current?.id],
    queryFn:()=>api.getOrganizationWorkflowTemplates(current!.id),enabled:Boolean(current)&&!previewMode
  });
  const [drafts,setDrafts]=useState<Record<string,WorkflowStatus>>({});
  const [transitions,setTransitions]=useState<Set<string>>(new Set());
  const [newStatus,setNewStatus]=useState({name:'',color:'#3B82F6',category:3});
  const [message,setMessage]=useState('');
  const [selectedTemplateId,setSelectedTemplateId]=useState('');

  useEffect(()=>{
    if(!query.data)return;
    setDrafts(Object.fromEntries(query.data.statuses.map(status=>[status.id,{...status}])));
    setTransitions(new Set(query.data.transitions.map(item=>`${item.sourceStatusId}:${item.targetStatusId}`)));
  },[query.data]);
  useEffect(()=>{
    const available=templatesQuery.data?.filter(template=>template.isActive)??[];
    setSelectedTemplateId(query.data?.workflowTemplateId??available.find(template=>template.isDefault)?.id??available[0]?.id??'');
  },[query.data?.workflowTemplateId,templatesQuery.data]);

  const refresh=()=>queryClient.invalidateQueries({queryKey:['project-workflow',projectId]});
  const run=async(action:()=>Promise<unknown>)=>{setMessage('');try{await action();await refresh();}catch(error){setMessage((error as Error).message);}};
  const create=useMutation({mutationFn:()=>api.createWorkflowStatus(projectId,{
    ...newStatus,position:query.data?.statuses.length?Math.max(...query.data.statuses.map(x=>x.position))+1:0,isInitial:!query.data?.statuses.length,isFinal:newStatus.category===5
  }),onSuccess:async()=>{setNewStatus({name:'',color:'#3B82F6',category:3});await refresh();},onError:error=>setMessage((error as Error).message)});
  const statuses=useMemo(()=>query.data?.statuses??[],[query.data]);
  const byBoard=useMemo(()=>{
    const map=new Map<string,WorkflowStage[]>();
    query.data?.stages.forEach(stage=>map.set(stage.boardId,[...(map.get(stage.boardId)??[]),stage]));
    return map;
  },[query.data]);
  const changeDraft=(id:string,patch:Partial<WorkflowStatus>)=>setDrafts(current=>({...current,[id]:{...current[id],...patch}}));
  const saveStatus=(id:string)=>run(()=>api.updateWorkflowStatus(projectId,id,drafts[id]));
  const moveStatus=async(id:string,delta:number)=>{
    const ids=statuses.map(x=>x.id);const from=ids.indexOf(id);const to=from+delta;if(to<0||to>=ids.length)return;
    [ids[from],ids[to]]=[ids[to],ids[from]];await run(()=>api.reorderWorkflowStatuses(projectId,ids));
  };
  const toggleTransition=(source:string,target:string)=>setTransitions(current=>{
    const next=new Set(current);const key=`${source}:${target}`;
    if(next.has(key))next.delete(key);else next.add(key);
    return next;
  });
  const saveTransitions=()=>run(()=>api.replaceWorkflowTransitions(projectId,[...transitions].map(key=>{
    const [sourceStatusId,targetStatusId]=key.split(':');return {sourceStatusId,targetStatusId};
  })));
  const inherited=query.data?.inheritanceMode===2;
  const setInheritance=(mode:number)=>run(()=>api.setProjectWorkflowInheritance(projectId,mode,mode===2?selectedTemplateId:null));

  if(query.isLoading)return <Shell><header><GitBranch size={16}/><h2>Status e fluxo</h2></header><Content><p>Carregando fluxo...</p></Content></Shell>;
  return <Shell><header><GitBranch size={16}/><h2>Status e fluxo</h2><p>Regras únicas para Kanban, backlog, sprints e relatórios</p></header><Content>
    <ModePanel><div><strong>{inherited?'Herdado da organização':'Personalizado no projeto'}</strong><small>{inherited?`Template: ${query.data?.workflowTemplateName??'organização'}${query.data?.isSynchronized?' · sincronizado':' · atualização pendente'}`:'Este projeto mantém seu próprio fluxo e não recebe mudanças do template.'}</small></div><div>
      {!inherited&&<select aria-label="Template de workflow" value={selectedTemplateId} onChange={event=>setSelectedTemplateId(event.target.value)}>{(templatesQuery.data??[]).filter(template=>template.isActive).map(template=><option key={template.id} value={template.id}>{template.name}{template.isDefault?' (padrão)':''}</option>)}</select>}
      <Primary disabled={!inherited&&!selectedTemplateId} onClick={()=>setInheritance(inherited?1:2)}>{inherited?'Personalizar fluxo':'Herdar template'}</Primary>
    </div></ModePanel>
    <Locked disabled={inherited}>
    <Block><div><h3>Status do projeto</h3><p>Defina significado, ordem, cor e quais estados iniciam ou encerram o trabalho.</p></div>
      {statuses.map((status,index)=>{const draft=drafts[status.id]??status;return <StatusRow key={status.id}>
        <input aria-label={`Cor de ${draft.name}`} type="color" value={draft.color} onChange={e=>changeDraft(status.id,{color:e.target.value})}/>
        <input aria-label="Nome do status" value={draft.name} onChange={e=>changeDraft(status.id,{name:e.target.value})}/>
        <select aria-label="Categoria" value={draft.category} onChange={e=>changeDraft(status.id,{category:Number(e.target.value)})}>{categories.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select>
        <label><input type="radio" name="initial-status" checked={draft.isInitial} onChange={()=>changeDraft(status.id,{isInitial:true})}/>Inicial</label>
        <label><input type="checkbox" checked={draft.isFinal} onChange={e=>changeDraft(status.id,{isFinal:e.target.checked})}/>Final</label>
        <Actions className="actions"><button title="Subir" disabled={index===0} onClick={()=>moveStatus(status.id,-1)}><ArrowUp size={13}/></button><button title="Descer" disabled={index===statuses.length-1} onClick={()=>moveStatus(status.id,1)}><ArrowDown size={13}/></button><button title="Salvar status" onClick={()=>saveStatus(status.id)}><Save size={13}/></button><button title="Excluir status" onClick={()=>window.confirm('Excluir este status não utilizado?')&&run(()=>api.deleteWorkflowStatus(projectId,status.id))}><Trash2 size={13}/></button></Actions>
      </StatusRow>})}
      <AddForm onSubmit={event=>{event.preventDefault();if(newStatus.name.trim())create.mutate();}}><input type="color" value={newStatus.color} onChange={e=>setNewStatus({...newStatus,color:e.target.value})}/><input type="text" required placeholder="Novo status" value={newStatus.name} onChange={e=>setNewStatus({...newStatus,name:e.target.value})}/><select value={newStatus.category} onChange={e=>setNewStatus({...newStatus,category:Number(e.target.value)})}>{categories.map(([id,label])=><option key={id} value={id}>{label}</option>)}</select><Primary disabled={create.isPending}><Plus size={13}/>Adicionar</Primary></AddForm>
    </Block>
    {statuses.length>1&&<Block><div><h3>Transições permitidas</h3><p>Marque os destinos aceitos para cada status de origem. Movimentos inválidos serão bloqueados em todas as telas.</p></div><MatrixWrap><table><thead><tr><th>De \ Para</th>{statuses.map(target=><th key={target.id}>{target.name}</th>)}</tr></thead><tbody>{statuses.map(source=><tr key={source.id}><th><span style={{color:source.color}}>●</span> {source.name}</th>{statuses.map(target=><td key={target.id}>{source.id===target.id?'—':<input aria-label={`${source.name} para ${target.name}`} type="checkbox" checked={transitions.has(`${source.id}:${target.id}`)} onChange={()=>toggleTransition(source.id,target.id)}/>}</td>)}</tr>)}</tbody></table></MatrixWrap><div><Primary onClick={saveTransitions}><Save size={13}/>Salvar transições</Primary></div></Block>}
    <Block><div><h3>Colunas e limites de WIP</h3><p>Cada coluna representa um status. O limite é validado pelo servidor inclusive em ações em lote.</p></div>{query.data?.boards.map(board=><BoardGroup key={board.id}><strong>{board.name}</strong>{(byBoard.get(board.id)??[]).map(stage=><EditableStage key={stage.id} stage={stage} statuses={statuses} onSave={value=>run(()=>api.updateWorkflowStage(projectId,stage.id,value))} onDelete={()=>window.confirm('Excluir esta coluna vazia?')&&run(()=>api.deleteWorkflowStage(projectId,stage.id))}/>)}</BoardGroup>)}</Block>
    </Locked>
    {message&&<ErrorText role="alert">{message}</ErrorText>}
  </Content></Shell>;
}

function EditableStage({stage,statuses,onSave,onDelete}:{
  stage:WorkflowStage;statuses:WorkflowStatus[];
  onSave:(value:{name:string;position:number;wipLimit:number|null;workflowStatusId:string})=>void;
  onDelete:()=>void;
}){
  const [value,setValue]=useState({name:stage.name,wipLimit:stage.wipLimit?.toString()??'',workflowStatusId:stage.workflowStatusId??statuses[0]?.id??''});
  useEffect(()=>setValue({name:stage.name,wipLimit:stage.wipLimit?.toString()??'',workflowStatusId:stage.workflowStatusId??statuses[0]?.id??''}),[stage,statuses]);
  return <StageRow><input aria-label="Nome da coluna" value={value.name} onChange={e=>setValue({...value,name:e.target.value})}/><select aria-label="Status da coluna" value={value.workflowStatusId} onChange={e=>setValue({...value,workflowStatusId:e.target.value})}>{statuses.map(status=><option key={status.id} value={status.id}>{status.name}</option>)}</select><input aria-label="Limite de WIP" type="number" min="1" placeholder="Sem limite" value={value.wipLimit} onChange={e=>setValue({...value,wipLimit:e.target.value})}/><Actions><button title="Salvar coluna" onClick={()=>onSave({name:value.name,position:stage.position,wipLimit:value.wipLimit?Number(value.wipLimit):null,workflowStatusId:value.workflowStatusId})}><Save size={13}/></button><button title="Excluir coluna" onClick={onDelete}><Trash2 size={13}/></button></Actions></StageRow>;
}
