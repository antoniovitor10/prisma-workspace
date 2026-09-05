import { useQuery } from '@tanstack/react-query';
import { History, Search } from 'lucide-react';
import { useState } from 'react';
import styled from 'styled-components';
import { searchAudit } from './services';
import type { AuditPage } from './types';

const Section=styled.section`overflow:hidden;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surface};`;
const Header=styled.header`display:flex;align-items:center;gap:9px;padding:16px 18px;border-bottom:1px solid ${({theme})=>theme.color.border};h2{font-size:15px;}p{margin-left:auto;color:${({theme})=>theme.color.textMuted};font-size:13px;}`;
const Filters=styled.div`display:grid;grid-template-columns:1fr 1fr 1fr auto;gap:8px;padding:12px 18px;border-bottom:1px solid ${({theme})=>theme.color.border};input,select{min-height:34px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13px;}button{display:flex;align-items:center;gap:5px;padding:0 11px;border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.brand};color:white;font-size:13px;font-weight:800;}@media(max-width:720px){grid-template-columns:1fr;}`;
const Row=styled.details`border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};summary{display:grid;grid-template-columns:145px 100px 160px minmax(0,1fr) 100px;gap:8px;padding:11px 18px;cursor:pointer;font-size:13px;span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}}pre{max-height:260px;overflow:auto;margin:0;padding:12px 18px;background:${({theme})=>theme.color.neutral[50]};font-size:12px;white-space:pre-wrap;}@media(max-width:760px){summary{grid-template-columns:1fr 1fr;}}`;
const Empty=styled.p`padding:28px;text-align:center;color:${({theme})=>theme.color.textMuted};font-size:13.5px;`;

const pretty=(value?:string|null)=>{if(!value)return '—';try{return JSON.stringify(JSON.parse(value),null,2);}catch{return value;}};

export function AuditLogPanel(){
  const [draft,setDraft]=useState({entityType:'',action:'',userId:''});
  const [filters,setFilters]=useState(draft);
  const query=useQuery<AuditPage>({queryKey:['audit',filters],queryFn:()=>searchAudit({...filters,page:'1',pageSize:'50'}),retry:false});
  return <Section><Header><History size={16}/><h2>Auditoria</h2><p>{query.data?.total??0} evento(s)</p></Header>
    <Filters><input placeholder="Entidade (ex.: WorkItem)" value={draft.entityType} onChange={e=>setDraft({...draft,entityType:e.target.value})}/><select value={draft.action} onChange={e=>setDraft({...draft,action:e.target.value})}><option value="">Todas as ações</option><option value="created">Criação</option><option value="updated">Alteração</option><option value="deleted">Exclusão</option></select><input placeholder="ID do usuário" value={draft.userId} onChange={e=>setDraft({...draft,userId:e.target.value})}/><button onClick={()=>setFilters(draft)}><Search size={12}/>Filtrar</button></Filters>
    {query.data?.items.map(item=><Row key={item.id}><summary><span>{new Date(item.occurredAt).toLocaleString('pt-BR')}</span><strong>{item.action}</strong><span>{item.entityType}</span><span title={item.entityId}>{item.entityId}</span><span>{item.origin}{item.ipAddress?` · ${item.ipAddress}`:''}</span></summary><pre>{`Anterior\n${pretty(item.previousValuesJson)}\n\nNovo\n${pretty(item.newValuesJson)}\n\nUsuário: ${item.userId??'sistema'}\nCorrelação: ${item.correlationId??'—'}`}</pre></Row>)}
    {!query.isLoading&&!query.data?.items.length&&<Empty>{query.error?(query.error as Error).message:'Nenhum evento encontrado.'}</Empty>}
  </Section>;
}
