import { useEffect, useRef, useState } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';

export interface DayEntry {
  entry: { id:string; workItemId:string; startedAt:string; endedAt?:string|null; note?:string|null; durationSeconds:number; isManual:boolean };
  title:string;
}
interface TaskOption {id:string;number:number;title:string;boardName:string;assigned:boolean;}
const Container=styled.div`display:grid;gap:10px;min-width:0;input,textarea{width:100%;min-width:0;padding:9px;border:1px solid ${({theme})=>theme.color.border};border-radius:6px;font:inherit;}button{min-height:38px;padding:6px 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:6px;}button:disabled{opacity:.5;}article{padding:12px;border:1px solid ${({theme})=>theme.color.border};border-radius:8px;overflow-wrap:anywhere;}form,label{display:grid;gap:6px;}form{gap:10px;}[role=listbox]{max-height:50dvh;overflow:auto;display:flex;flex-direction:column;gap:4px;}[role=option]{flex-shrink:0;min-width:0;text-align:left;white-space:normal;overflow-wrap:anywhere;}[aria-selected=true]{outline:2px solid ${({theme})=>theme.color.brand};} .actions{display:flex;gap:8px;flex-wrap:wrap;}small{display:block;margin:6px 0;}[role=alert]{color:${({theme})=>theme.color.danger};}`;
export function TimeTaskSearch({value,onChange}:{value:string;onChange:(id:string)=>void}){
  const [query,setQuery]=useState('');
  const [tasks,setTasks]=useState<TaskOption[]>([]);
  const [open,setOpen]=useState(true);
  const [active,setActive]=useState(0);
  const [loading,setLoading]=useState(false);
  const [error,setError]=useState('');
  const optionsRef=useRef<HTMLDivElement>(null);
  useEffect(()=>{
    let current=true;setLoading(true);setError('');
    api.getTimeAdjustmentTasks(query).then(rows=>{if(current){setTasks(rows);setActive(0);}})
      .catch(e=>{if(current){setTasks([]);setError((e as Error).message);}})
      .finally(()=>{if(current)setLoading(false);});
    return()=>{current=false;};
  },[query]);
  useEffect(()=>{
    const list=optionsRef.current;
    const option=list?.querySelector<HTMLButtonElement>('[aria-selected="true"]');
    if(!open||loading||!list||!option)return;
    const bounds=list.getBoundingClientRect();const target=option.getBoundingClientRect();
    const top=bounds.top+list.clientTop;const bottom=top+list.clientHeight;
    if(target.top<top||target.height>list.clientHeight)list.scrollTop+=target.top-top;
    else if(target.bottom>bottom)list.scrollTop+=target.bottom-bottom;
    // Revela também no modal e nos demais ancestrais, mantendo o foco no campo.
    option.scrollIntoView?.({block:'nearest',inline:'nearest',behavior:'instant'});
  },[active,open,loading,tasks]);
  const choose=(task:TaskOption)=>{onChange(task.id);setQuery(`#${task.number}`);setOpen(false);};
  return <Container><label htmlFor="time-task-search">Buscar tarefa por título ou número</label>
    <input id="time-task-search" role="combobox" autoComplete="off" aria-expanded={open} aria-controls="time-task-options" aria-autocomplete="list"
      aria-activedescendant={open&&!loading&&tasks[active]?`time-task-${tasks[active].id}`:undefined}
      value={query} onFocus={()=>setOpen(true)} onClick={()=>setOpen(true)} onChange={e=>{setQuery(e.target.value);onChange('');setOpen(true);}}
      onKeyDown={e=>{if(e.key==='ArrowDown'){e.preventDefault();setOpen(true);setActive(i=>Math.min(i+1,tasks.length-1));}
        else if(e.key==='ArrowUp'){e.preventDefault();setActive(i=>Math.max(0,i-1));}
        else if(e.key==='Enter'&&open){e.preventDefault();if(tasks[active]&&!loading)choose(tasks[active]);}
        else if(e.key==='Escape'){e.preventDefault();setOpen(false);}}}/>
    {open&&<div ref={optionsRef} id="time-task-options" role="listbox" aria-label="Tarefas para apontamento" aria-busy={loading}>
      {!loading&&tasks.map((task,index)=><button id={`time-task-${task.id}`} type="button" role="option" key={task.id} aria-selected={index===active} onClick={()=>choose(task)}>#{task.number} · {task.title} — {task.boardName}{task.assigned?' · Atribuída a você':''}</button>)}
    </div>}
    {loading&&<small role="status">Buscando tarefas…</small>}
    {open&&!loading&&!error&&tasks.length===0&&<small>Nenhuma tarefa encontrada.</small>}
    {value&&<small role="status">Tarefa selecionada.</small>}
    {error&&<p role="alert">{error}</p>}
  </Container>;
}

const inputTime=(value:string)=>new Date(Date.parse(value)-3*3600_000).toISOString().slice(0,19);
const instant=(value:string)=>new Date(`${value}-03:00`).toISOString();
// O editor mostra segundos inteiros; campos não alterados mantêm toda a precisão da API.
const editedInstant=(value:string,original:string)=>instant(value)===instant(inputTime(original))?original:instant(value);
const displayTime=(value:string)=>new Date(value).toLocaleTimeString('pt-BR',{timeZone:'America/Sao_Paulo'});
const durationLabel=(seconds:number)=>`${Math.floor(seconds/3600)}h ${String(Math.floor(seconds%3600/60)).padStart(2,'0')}min ${String(seconds%60).padStart(2,'0')}s`;
export function DayTimeEntries({entries,onChanged}:{entries:DayEntry[];onChanged:()=>Promise<void>}){
  const [editing,setEditing]=useState<string|null>(null);
  const [deleting,setDeleting]=useState<string|null>(null);
  const [draft,setDraft]=useState({startedAt:'',endedAt:'',note:''});
  const [pending,setPending]=useState(false);
  const [error,setError]=useState('');
  const mutate=async(action:()=>Promise<unknown>)=>{setPending(true);setError('');try{await action();setEditing(null);setDeleting(null);await onChanged();}catch(e){setError((e as Error).message);}finally{setPending(false);}};
  return <Container><h3>Lançamentos do dia</h3><small>Horários de Brasília. Pare o cronômetro antes de alterar ou excluir.</small>
    {error&&<p role="alert">{error}</p>}
    {entries.length===0&&<p>Nenhum lançamento neste dia.</p>}
    {entries.map(({entry,title})=><article key={entry.id} aria-label={`Lançamento ${title}`}>
      <strong>{title}</strong><small>{displayTime(entry.startedAt)} — {entry.endedAt?displayTime(entry.endedAt):'Em andamento'} · {durationLabel(entry.durationSeconds)} · {entry.isManual?'Manual':'Cronômetro'}</small>
      {entry.note&&<p>{entry.note}</p>}
      {editing===entry.id?<form onSubmit={e=>{e.preventDefault();void mutate(()=>api.updateTimeEntry(entry.id,{startedAt:editedInstant(draft.startedAt,entry.startedAt),endedAt:editedInstant(draft.endedAt,entry.endedAt!),note:draft.note}));}}>
        <label>Início (Brasília)<input type="datetime-local" step="1" required value={draft.startedAt} onChange={e=>setDraft({...draft,startedAt:e.target.value})}/></label>
        <label>Fim (Brasília)<input type="datetime-local" step="1" required value={draft.endedAt} onChange={e=>setDraft({...draft,endedAt:e.target.value})}/></label>
        <label>Observação<textarea maxLength={2000} value={draft.note} onChange={e=>setDraft({...draft,note:e.target.value})}/></label>
        <div className="actions"><button type="submit" disabled={pending}>Salvar lançamento</button><button type="button" disabled={pending} onClick={()=>setEditing(null)}>Cancelar edição</button></div>
      </form>:deleting===entry.id?<div role="alertdialog" aria-label="Confirmar exclusão do lançamento"><p>Excluir este lançamento de {durationLabel(entry.durationSeconds)}? O total do dia será reduzido. A exclusão ficará na auditoria.</p>
        <div className="actions"><button type="button" disabled={pending} onClick={()=>void mutate(()=>api.deleteTimeEntry(entry.id))}>Confirmar exclusão</button><button type="button" disabled={pending} onClick={()=>setDeleting(null)}>Cancelar exclusão</button></div>
      </div>:<div className="actions"><button type="button" disabled={pending||!entry.endedAt} onClick={()=>{setEditing(entry.id);setDeleting(null);setError('');setDraft({startedAt:inputTime(entry.startedAt),endedAt:inputTime(entry.endedAt!),note:entry.note??''});}}>Editar lançamento</button>
        <button type="button" disabled={pending||!entry.endedAt} onClick={()=>{setDeleting(entry.id);setEditing(null);setError('');}}>Excluir lançamento</button></div>}
    </article>)}
  </Container>;
}
