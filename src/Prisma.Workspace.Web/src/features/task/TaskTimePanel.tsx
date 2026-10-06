import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Clock3, FileClock, Play, Plus, Square } from 'lucide-react';
import styled from 'styled-components';
import { api } from '../../services/api';
import { userDisplayLabel } from '../../utils/userDisplayName';

interface TimeEntry {
  id: string;
  workItemId: string;
  userId: string;
  startedAt: string;
  endedAt?: string;
  note?: string;
  durationSeconds?: number;
}

interface UserOption { id: string; displayName?: string; email?: string; userName?: string; }

const Actions = styled.div`display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:12px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};`;
const Button = styled.button<{ $running?:boolean }>`display:inline-flex;align-items:center;gap:6px;min-height:32px;padding:0 10px;border:0;border-radius:${({theme})=>theme.radius.md};background:${({theme,$running})=>$running?theme.color.danger:theme.color.brand};color:white;font-size:13px;font-weight:800;&:disabled{opacity:.5;}`;
const Summary = styled.span`font-size:13px;color:${({theme})=>theme.color.textMuted};`;
const Manual = styled.form`display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:8px;padding:12px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};input{min-width:0;min-height:32px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13px}.wide{grid-column:1/-1}.submit{grid-column:1/-1;justify-self:end;max-width:100%;}@media(max-width:560px){grid-template-columns:1fr;.submit{justify-self:stretch}}`;
const PeopleSummary = styled.section`display:grid;gap:8px;padding:14px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};h3{display:flex;align-items:center;gap:6px;margin:0;font-size:13px;color:${({theme})=>theme.color.text}}`;
const PersonRow = styled.article`display:grid;grid-template-columns:minmax(0,1fr) auto;gap:10px;padding:10px 12px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surfaceSubtle};strong{font-size:13px}p{margin:3px 0 0;color:${({theme})=>theme.color.textMuted};font-size:12px}.total{align-self:center;color:${({theme})=>theme.color.text};font-size:13px;white-space:nowrap}`;
const Log = styled.details`border-bottom:1px solid ${({theme})=>theme.color.border};>summary{display:flex;align-items:center;gap:7px;padding:12px 16px;color:${({theme})=>theme.color.textMuted};font-size:13px;font-weight:800;cursor:pointer;list-style:none}summary::-webkit-details-marker{display:none}summary::after{content:'+';margin-left:auto;font-size:16px} &[open]>summary::after{content:'−'}`;
const History = styled.div`display:grid;max-height:240px;overflow:auto;`;
const Row = styled.article`display:grid;grid-template-columns:minmax(0,1fr) auto;gap:8px;padding:10px 16px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};strong{display:block;font-size:13px}p,small{margin:2px 0 0;color:${({theme})=>theme.color.textMuted};font-size:12px}`;
const Empty = styled.p`padding:16px;text-align:center;color:${({theme})=>theme.color.textMuted};font-size:13px;`;
const Error = styled.p`padding:8px 16px;color:${({theme})=>theme.color.danger};font-size:13px;`;
const TimerStatus = styled.p<{ $active?: boolean }>`margin:0;padding:8px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme,$active})=>$active?`color-mix(in srgb, ${theme.color.success} 9%, ${theme.color.surface})`:theme.color.neutral[50]};color:${({theme})=>theme.color.text};font-size:12.5px;line-height:1.45;`;

export function TaskTimePanel({workItemId,realizedHours}:{workItemId:string;realizedHours:number}){
  const client=useQueryClient();
  const defaults=useMemo(()=>{
    const end=new Date();end.setSeconds(0,0);const start=new Date(end.getTime()-60*60*1000);
    return {startedAt:localInput(start),endedAt:localInput(end),note:''};
  },[]);
  const [manual,setManual]=useState(defaults);
  const [elapsed,setElapsed]=useState(0);
  const entries=useQuery<TimeEntry[]>({queryKey:['time-entries',workItemId],queryFn:()=>api.getTimeEntriesByWorkItem(workItemId) as Promise<TimeEntry[]>});
  const running=useQuery<TimeEntry|null>({queryKey:['active-timer'],queryFn:()=>api.getRunningTimeEntry() as Promise<TimeEntry|null>});
  const users=useQuery<UserOption[]>({queryKey:['assignable-users'],queryFn:()=>api.getAssignableUsers() as Promise<UserOption[]>});
  const refresh=async()=>{window.dispatchEvent(new Event('timer-change'));await Promise.all([
    client.invalidateQueries({queryKey:['time-entries']}),
    client.invalidateQueries({queryKey:['active-timer']}),
    client.invalidateQueries({queryKey:['work-item']})
  ]);};
  const toggle=useMutation({mutationFn:()=>running.data?.workItemId===workItemId?api.stopTimer(workItemId):api.startTimer(workItemId),onSuccess:refresh});
  const add=useMutation({mutationFn:()=>api.createManualTimeEntry({workItemId,startedAt:new Date(manual.startedAt).toISOString(),endedAt:new Date(manual.endedAt).toISOString(),note:manual.note||undefined}),onSuccess:async()=>{setManual({...defaults,note:''});await refresh();}});
  const currentHere=running.data?.workItemId===workItemId;
  const anotherRunning=Boolean(running.data&&!currentHere);
  useEffect(()=>{
    if(!running.data?.startedAt){setElapsed(0);return;}
    const update=()=>setElapsed(Math.max(0,Math.floor((Date.now()-new Date(running.data!.startedAt).getTime())/1000)));
    update();
    const interval=window.setInterval(update,1000);
    return()=>window.clearInterval(interval);
  },[running.data?.startedAt]);
  const names=useMemo(()=>new Map(users.data?.map(user=>[user.id,userDisplayLabel(user)])??[]),[users.data]);
  const timeByUser=useMemo(()=>{
    const grouped=new Map<string,{userId:string;name:string;startedAt:string;endedAt?:string;running:boolean;totalSeconds:number}>();
    for(const entry of entries.data??[]){
      const current=grouped.get(entry.userId);
      const runningEntry=!entry.endedAt;
      const duration=runningEntry&&entry.id===running.data?.id?elapsed:(entry.durationSeconds??0);
      grouped.set(entry.userId,{
        userId:entry.userId,
        name:names.get(entry.userId)??entry.userId,
        startedAt:!current||new Date(entry.startedAt).getTime()<new Date(current.startedAt).getTime()?entry.startedAt:current.startedAt,
        endedAt:runningEntry?current?.endedAt:(!current?.endedAt||new Date(entry.endedAt!).getTime()>new Date(current.endedAt).getTime()?entry.endedAt:current.endedAt),
        running:Boolean(current?.running||runningEntry),
        totalSeconds:(current?.totalSeconds??0)+duration,
      });
    }
    return [...grouped.values()].sort((left,right)=>right.totalSeconds-left.totalSeconds||left.name.localeCompare(right.name,'pt-BR'));
  },[elapsed,entries.data,names,running.data?.id]);
  return <div>
    <Actions><Button type="button" $running={currentHere} disabled={toggle.isPending||running.isLoading||running.isError} onClick={()=>toggle.mutate()}>{currentHere?<><Square size={11}/>Parar cronômetro</>:<><Play size={11}/>Iniciar cronômetro</>}</Button><Summary><Clock3 size={11}/> {realizedHours.toFixed(2)} h realizadas</Summary></Actions>
    <TimerStatus role="status" aria-live="polite" $active={currentHere}>{currentHere?`Cronômetro desta tarefa em andamento — ${formatSeconds(elapsed)} desde o início.`:anotherRunning?'Há um cronômetro em outra tarefa. Ao iniciar aqui, ele será encerrado automaticamente.':'Nenhum cronômetro ativo nesta sessão.'}</TimerStatus>
    <Manual onSubmit={event=>{event.preventDefault();add.mutate();}}><input aria-label="Início do apontamento" type="datetime-local" required value={manual.startedAt} onChange={event=>setManual({...manual,startedAt:event.target.value})}/><input aria-label="Fim do apontamento" type="datetime-local" required min={manual.startedAt} value={manual.endedAt} onChange={event=>setManual({...manual,endedAt:event.target.value})}/><input className="wide" maxLength={2000} placeholder="Observação do apontamento (opcional)" value={manual.note} onChange={event=>setManual({...manual,note:event.target.value})}/><Button className="submit" disabled={add.isPending}><Plus size={11}/>Registrar manualmente</Button></Manual>
    {(toggle.error||add.error||entries.error||running.error)&&<Error role="alert">{((toggle.error||add.error||entries.error||running.error) as Error).message}</Error>}
    <PeopleSummary aria-label="Tempo geral por responsável">
      <h3><Clock3 size={14}/>Tempo geral por responsável</h3>
      {timeByUser.map(person=><PersonRow key={person.userId}><div><strong>{person.name}</strong><p>Início: {formatDateTime(person.startedAt)} · {person.running?'Em andamento':`Fim: ${formatDateTime(person.endedAt!)}`}</p></div><strong className="total">{formatSeconds(person.totalSeconds)}</strong></PersonRow>)}
      {!entries.isLoading&&timeByUser.length===0&&<Empty>Nenhum apontamento registrado.</Empty>}
    </PeopleSummary>
    <Log><summary><FileClock size={14}/>Log de apontamentos ({entries.data?.length??0})</summary><History>{entries.data?.map(entry=><Row key={entry.id}><div><strong>{names.get(entry.userId)??entry.userId}</strong><p>{formatDateTime(entry.startedAt)} — {entry.endedAt?formatDateTime(entry.endedAt):'em andamento'}</p>{entry.note&&<small>{entry.note}</small>}</div><strong>{formatSeconds(entry.durationSeconds??0)}</strong></Row>)}{!entries.isLoading&&!entries.data?.length&&<Empty>Nenhum apontamento registrado.</Empty>}</History></Log>
  </div>;
}

function localInput(date:Date){const offset=date.getTimezoneOffset()*60_000;return new Date(date.getTime()-offset).toISOString().slice(0,16);}
function formatSeconds(seconds:number){const hours=Math.floor(seconds/3600);const minutes=Math.floor((seconds%3600)/60);return `${hours}h ${String(minutes).padStart(2,'0')}min`;}
function formatDateTime(value:string){return new Date(value).toLocaleString('pt-BR');}
