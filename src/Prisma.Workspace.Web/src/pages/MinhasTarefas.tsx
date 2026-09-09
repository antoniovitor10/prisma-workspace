import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import {
  AlertCircle, Bell, CalendarDays, Clock3, Columns3, Flag, Link2,
  ListChecks, Play, Search, Square, UserCheck, Users
} from 'lucide-react';
import { api } from '../services/api';
import { HeaderLead, Page, PageHeader, SearchControl } from '../components/PageLayout';

interface MyWorkTask {
  id:string;number:number;title:string;boardId:string;boardName:string;projectKey?:string|null;
  stageName?:string|null;statusColor?:string|null;priority:number;origin:number;
  teamName?:string|null;requesterName?:string|null;dueDate?:string|null;completedAt?:string|null;
  assigned:boolean;created:boolean;following:boolean;today:boolean;thisWeek:boolean;
  overdue:boolean;blocked:boolean;externalRequest:boolean;upcomingDeadline:boolean;tags:string[];
  userTimeSeconds?:number;
}
interface MyWorkComment {id:string;workItemId:string;workItemNumber:number;workItemTitle:string;author:string;content:string;createdAt:string;isMention:boolean;}
interface MyWorkApproval {id:string;workItemId:string;workItemNumber:number;workItemTitle:string;boardName:string;createdAt:string;}
interface MyWorkNotification {type:string;severity:string;title:string;message:string;workItemId?:string|null;occurredAt:string;}
interface MyWorkProject {
  id:string;key:string;name:string;openTasks:number;overdueTasks:number;completedTasks:number;
  progressPercentage:number;currentSprintId?:string|null;currentSprintName?:string|null;currentSprintDaysLeft?:number|null;
}
interface MyWorkSprint {
  id:string;name:string;projectId:string;projectKey:string;projectName:string;
  startDate:string;endDate:string;daysLeft:number;endingSoon:boolean;
  totalItems:number;totalCompleted:number;totalProgressPercentage:number;
  myItems:number;myCompleted:number;myProgressPercentage:number;
}
interface MyWorkHours {weekHours:number;plannedHours:number;daysWithoutEntry:number;}
interface MyWorkDashboard {
  summary:{assigned:number;today:number;thisWeek:number;overdue:number;blocked:number;externalRequests:number;pendingApprovals:number;mentions:number};
  tasks:MyWorkTask[];comments:MyWorkComment[];mentions:MyWorkComment[];
  pendingApprovals:MyWorkApproval[];upcomingDeadlines:MyWorkTask[];importantNotifications:MyWorkNotification[];
  projects:MyWorkProject[];sprints:MyWorkSprint[];hours:MyWorkHours;
}
interface ActiveTimer {workItemId:string;startedAt:string;}
type FilterKey='assigned'|'created'|'following'|'today'|'week'|'overdue'|'blocked'|'requests'|'all';

const WorkPage=styled(Page)`display:grid;gap:18px;`;
const Metrics=styled.div`display:grid;grid-template-columns:repeat(6,minmax(132px,1fr));gap:11px;overflow:auto;padding:1px 1px 4px;`;
const Metric=styled.button<{ $active?:boolean;$tone?:string }>`position:relative;min-width:132px;padding:15px 15px 14px;overflow:hidden;text-align:left;border:1px solid ${({theme,$active,$tone})=>$active?($tone||theme.color.brand):theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};transition:transform .15s ease,box-shadow .15s ease;&::after{content:'';position:absolute;inset:auto 0 0;height:3px;background:${({theme,$tone})=>$tone||theme.color.gradient};opacity:${({$active})=>$active?1:.15};}span{display:block;font-size:10.5px;color:${({theme})=>theme.color.textMuted};font-weight:800;letter-spacing:.06em;text-transform:uppercase;}strong{display:block;margin-top:6px;font-size:24px;letter-spacing:-.04em;color:${({$tone,theme})=>$tone||theme.color.text};}&:hover{transform:translateY(-1px);box-shadow:${({theme})=>theme.shadow.md};}`;
const Layout=styled.div`display:grid;grid-template-columns:minmax(0,1fr) 330px;gap:16px;align-items:start;@media(max-width:1050px){grid-template-columns:1fr;}`;
const Panel=styled.section`border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};overflow:hidden;box-shadow:${({theme})=>theme.shadow.sm};`;
const PanelHeader=styled.header`min-height:56px;padding:11px 16px;display:flex;align-items:center;gap:9px;border-bottom:1px solid ${({theme})=>theme.color.border};h2{font-size:14px;letter-spacing:-.01em;}small{margin-left:auto;color:${({theme})=>theme.color.textMuted};font-size:12px;}`;
const Tabs=styled.div`display:flex;gap:5px;overflow:auto;padding:10px 12px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};`;
const Tab=styled.button<{ $active?:boolean }>`white-space:nowrap;padding:6px 9px;border-radius:7px;background:${({$active,theme})=>$active?theme.color.brand:theme.color.neutral[50]};color:${({$active,theme})=>$active?theme.color.onBrand:theme.color.textMuted};font-size:12px;font-weight:800;`;
const TaskList=styled.div`display:grid;`;
const Task=styled.article<{ $late?:boolean;$blocked?:boolean }>`display:grid;grid-template-columns:4px minmax(0,1fr) auto;gap:12px;padding:14px 16px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({$late,theme})=>$late?`color-mix(in srgb, ${theme.color.danger} 4%, ${theme.color.surface})`:'transparent'};&:hover{background:${({theme})=>theme.color.surfaceSubtle};}`;
const StatusBar=styled.span<{ $color?:string }>`width:5px;border-radius:999px;background:${({$color})=>$color||'#94A3B8'};`;
const TaskCenter=styled.div`min-width:0;display:grid;gap:5px;h3{font-size:14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}p{font-size:12px;color:${({theme})=>theme.color.textMuted};}.meta{display:flex;align-items:center;gap:6px;flex-wrap:wrap;}`;
const Badge=styled.span<{ $tone?:string }>`display:inline-flex;align-items:center;gap:3px;padding:3px 6px;border-radius:999px;background:${({$tone})=>$tone?`${$tone}18`:'#F1F5F9'};color:${({$tone,theme})=>$tone||theme.color.textMuted};font-size:11px;font-weight:800;`;
const TaskRight=styled.div`display:flex;align-items:center;gap:9px;small{font-size:12px;color:${({theme})=>theme.color.textMuted};white-space:nowrap;}`;
const TimerButton=styled.button<{ $running?:boolean }>`width:31px;height:31px;border-radius:50%;display:grid;place-items:center;color:white;background:${({$running})=>$running?'#E5484D':'#16A36A'};`;
const KanbanLink=styled.button`display:inline-flex;align-items:center;gap:5px;height:31px;padding:0 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:8px;background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.textMuted};font-size:12px;font-weight:750;white-space:nowrap;&:hover{color:${({theme})=>theme.color.brand};border-color:${({theme})=>theme.color.brand};}`;
const Rail=styled.aside`display:grid;gap:12px;`;
const RailList=styled.div`display:grid;max-height:320px;overflow:auto;`;
const RailItem=styled.div`padding:10px 13px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};display:grid;gap:4px;strong{font-size:13px;}p{font-size:12px;line-height:1.45;color:${({theme})=>theme.color.textMuted};overflow-wrap:anywhere;}small{font-size:11px;color:${({theme})=>theme.color.textMuted};}`;
const Empty=styled.div`padding:42px 16px;text-align:center;color:${({theme})=>theme.color.textMuted};font-size:13px;`;
const ErrorBox=styled.div`padding:12px;border:1px solid #FCA5A5;border-radius:9px;background:#FEF2F2;color:#B42318;font-size:13px;`;

const filters:{id:FilterKey;label:string}[]=[
  {id:'assigned',label:'Atribuídas'},{id:'created',label:'Criadas por mim'},
  {id:'following',label:'Acompanhando'},{id:'today',label:'Hoje'},
  {id:'week',label:'Esta semana'},{id:'overdue',label:'Atrasadas'},
  {id:'blocked',label:'Bloqueadas'},{id:'requests',label:'Solicitações atribuídas'},{id:'all',label:'Todas'}
];
const matches=(task:MyWorkTask,filter:FilterKey)=>filter==='all'||({
  assigned:task.assigned,created:task.created,following:task.following,today:task.today,
  week:task.thisWeek,overdue:task.overdue,blocked:task.blocked,requests:task.externalRequest&&task.assigned
} as Record<string,boolean>)[filter];
const dueLabel=(date?:string|null)=>date?new Date(`${date}T12:00:00`).toLocaleDateString('pt-BR',{day:'2-digit',month:'short'}):'';

export const MinhasTarefas:React.FC=()=>{
  const navigate=useNavigate();
  const userId=api.getUserId();
  const [data,setData]=useState<MyWorkDashboard|null>(null);
  const [filter,setFilter]=useState<FilterKey>('assigned');
  const [search,setSearch]=useState('');
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState('');
  const [timer,setTimer]=useState<ActiveTimer|null>(null);
  const [elapsed,setElapsed]=useState(0);

  const load=useCallback(async()=>{setLoading(true);setError('');try{const [dashboard,active]=await Promise.all([api.getMyWork(),api.getMyActiveTimer()]);setData(dashboard);setTimer(active);}catch(e){setError((e as Error).message);}finally{setLoading(false);}},[]);
  useEffect(()=>{load();},[load]);
  useEffect(()=>{if(!timer){setElapsed(0);return;}const start=new Date(timer.startedAt).getTime();const tick=()=>setElapsed(Math.max(0,Math.floor((Date.now()-start)/1000)));tick();const id=setInterval(tick,1000);return()=>clearInterval(id);},[timer]);
  const tasks=useMemo(()=>data?.tasks.filter(task=>matches(task,filter)&&(!search.trim()||`${task.number} ${task.title} ${task.boardName} ${task.projectKey??''}`.toLowerCase().includes(search.toLowerCase())))??[],[data,filter,search]);
  const toggleTimer=async(taskId:string)=>{try{if(timer?.workItemId===taskId){await api.stopTimer(taskId);setTimer(null);}else{setTimer(await api.startTimer(taskId));}window.dispatchEvent(new Event('timer-change'));await load();}catch(e){setError((e as Error).message);}};

  return <WorkPage><PageHeader><HeaderLead><span className="eyebrow">Central de atenção</span><h1>Meu trabalho</h1><p>Prioridades, prazos e atividades que pedem uma decisão sua agora.</p></HeaderLead><SearchControl><Search size={14}/><input aria-label="Pesquisar meu trabalho" placeholder="Pesquisar tarefa ou projeto" value={search} onChange={e=>setSearch(e.target.value)}/></SearchControl></PageHeader>
    {error&&<ErrorBox role="alert">{error}</ErrorBox>}
    <Metrics>
      <Metric $active={filter==='assigned'} onClick={()=>setFilter('assigned')}><span>Atribuídas</span><strong>{data?.summary.assigned??0}</strong></Metric>
      <Metric $active={filter==='today'} onClick={()=>setFilter('today')}><span>Para hoje</span><strong>{data?.summary.today??0}</strong></Metric>
      <Metric $active={filter==='week'} onClick={()=>setFilter('week')}><span>Esta semana</span><strong>{data?.summary.thisWeek??0}</strong></Metric>
      <Metric $active={filter==='overdue'} $tone="#D92D20" onClick={()=>setFilter('overdue')}><span>Atrasadas</span><strong>{data?.summary.overdue??0}</strong></Metric>
      <Metric $active={filter==='blocked'} $tone="#D97706" onClick={()=>setFilter('blocked')}><span>Bloqueadas</span><strong>{data?.summary.blocked??0}</strong></Metric>
      <Metric $active={filter==='requests'} $tone="#2563EB" onClick={()=>setFilter('requests')}><span>Solicitações atribuídas</span><strong>{data?.summary.externalRequests??0}</strong></Metric>
    </Metrics>
    <Layout><Panel><PanelHeader><ListChecks size={15}/><h2>Fila de atenção</h2><small>{tasks.length} item(ns)</small></PanelHeader><Tabs>{filters.map(item=><Tab key={item.id} $active={filter===item.id} onClick={()=>setFilter(item.id)}>{item.label}</Tab>)}</Tabs>
      {loading?<Empty>Carregando seu trabalho...</Empty>:tasks.length===0?<Empty>Nenhuma tarefa neste recorte.</Empty>:<TaskList>{tasks.map(task=>{
        const running=timer?.workItemId===task.id;return <Task key={task.id} $late={task.overdue} $blocked={task.blocked}><StatusBar $color={task.statusColor??undefined}/><TaskCenter><p>{task.projectKey?`${task.projectKey} · `:''}{task.boardName}{task.stageName?` / ${task.stageName}`:''}</p><h3>#{task.number} · {task.title}</h3><div className="meta">{task.priority>=2&&<Badge $tone="#D92D20"><Flag size={9}/>{task.priority===3?'Crítica':'Alta'}</Badge>}{task.blocked&&<Badge $tone="#D97706"><Link2 size={9}/>Bloqueada</Badge>}{task.externalRequest&&<Badge $tone="#2563EB"><Users size={9}/>Portal externo</Badge>}{task.teamName&&<Badge><UserCheck size={9}/>{task.teamName}</Badge>}{task.tags.slice(0,3).map(tag=><Badge key={tag}>{tag}</Badge>)}</div></TaskCenter><TaskRight><KanbanLink title="Ver no Kanban do projeto, filtrado nas suas tarefas" onClick={()=>navigate(`/boards/${task.boardId}?assignee=${userId??''}`)}><Columns3 size={12}/>Kanban</KanbanLink>{task.dueDate&&<small style={{color:task.overdue?'#D92D20':undefined}}><CalendarDays size={11}/> {dueLabel(task.dueDate)}</small>}{(()=>{const total=(task.userTimeSeconds??0)+(running?elapsed:0);const fmt=`${String(Math.floor(total/3600)).padStart(2,'0')}:${String(Math.floor((total%3600)/60)).padStart(2,'0')}:${String(total%60).padStart(2,'0')}`;return running?<small>{fmt}</small>:total>0?<small style={{opacity:.65}} title="Tempo que você já gastou nesta tarefa">{fmt}</small>:null;})()}{task.assigned&&<TimerButton $running={running} title={running?'Parar timer':'Iniciar timer'} onClick={()=>toggleTimer(task.id)}>{running?<Square size={12} fill="white"/>:<Play size={12} fill="white"/>}</TimerButton>}</TaskRight></Task>})}</TaskList>}
    </Panel><Rail>
      <Panel><PanelHeader><Bell size={14}/><h2>Importante</h2><small>{data?.importantNotifications.length??0}</small></PanelHeader><RailList>{data?.importantNotifications.slice(0,10).map((item,index)=><RailItem key={`${item.type}-${item.workItemId}-${index}`}><strong>{item.severity==='danger'?<AlertCircle size={11} color="#D92D20"/>:<Bell size={11}/>} {item.title}</strong><p>{item.message}</p></RailItem>)}{!data?.importantNotifications.length&&<Empty>Sem alertas importantes.</Empty>}</RailList></Panel>

      <Panel><PanelHeader><CalendarDays size={14}/><h2>Sprints em curso</h2><small>{data?.sprints.length??0}</small></PanelHeader><RailList>
        {data?.sprints.map(sprint=><RailItem key={sprint.id}>
          <strong>{sprint.endingSoon&&<AlertCircle size={11} color="#D97706"/>} {sprint.name}</strong>
          <p>{sprint.projectKey} · {sprint.daysLeft === 0 ? 'encerra hoje' : `${sprint.daysLeft} dia(s) restante(s)`}</p>
          {/* Progresso da sprint e o seu, lado a lado: são perguntas diferentes. */}
          <p>Sprint {sprint.totalProgressPercentage}% ({sprint.totalCompleted}/{sprint.totalItems})
            {sprint.myItems>0&&<> · Você {sprint.myProgressPercentage}% ({sprint.myCompleted}/{sprint.myItems})</>}</p>
        </RailItem>)}
        {!data?.sprints.length&&<Empty>Nenhuma sprint em curso nos seus projetos.</Empty>}
      </RailList></Panel>

      <Panel><PanelHeader><Columns3 size={14}/><h2>Projetos vigentes</h2><small>{data?.projects.length??0}</small></PanelHeader><RailList>
        {data?.projects.map(projeto=><RailItem key={projeto.id}>
          <strong>{projeto.overdueTasks>0&&<AlertCircle size={11} color="#D92D20"/>} {projeto.key} · {projeto.name}</strong>
          <p>{projeto.openTasks} aberta(s){projeto.overdueTasks>0&&<> · {projeto.overdueTasks} atrasada(s)</>}
            {projeto.currentSprintName&&<> · {projeto.currentSprintName}</>}</p>
        </RailItem>)}
        {!data?.projects.length&&<Empty>Você ainda não participa de nenhum projeto ativo.</Empty>}
      </RailList></Panel>

      <Panel><PanelHeader><Clock3 size={14}/><h2>Minhas horas na semana</h2></PanelHeader><RailList>
        <RailItem>
          <strong>{(data?.hours.weekHours??0).toLocaleString('pt-BR')}h apontadas</strong>
          <p>{(data?.hours.plannedHours??0).toLocaleString('pt-BR')}h previstas nas suas tarefas abertas</p>
          {(data?.hours.daysWithoutEntry??0)>0&&<p>{data?.hours.daysWithoutEntry} dia(s) sem apontamento nesta semana</p>}
        </RailItem>
      </RailList></Panel>
    </Rail></Layout>
  </WorkPage>;
};
