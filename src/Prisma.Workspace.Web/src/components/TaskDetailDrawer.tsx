import * as Dialog from '@radix-ui/react-dialog';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Archive,
  CalendarDays,
  Check,
  CheckCircle2,
  CircleDot,
  Clock3,
  Copy,
  Download,
  Eye,
  EyeOff,
  FileUp,
  FolderKanban,
  GitBranch,
  Link2,
  LoaderCircle,
  MessageSquareText,
  Paperclip,
  Plus,
  RotateCcw,
  Save,
  Search,
  Send,
  Target,
  Trash2,
  UserPlus,
  UserRound,
  X,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent, type FormEvent, type KeyboardEvent } from 'react';
import styled from 'styled-components';
import { TaskChecklistPanel } from '../features/task/TaskChecklistPanel';
import { DependencyAutocomplete } from '../features/task/DependencyAutocomplete';
import { TaskFeed } from '../features/task/TaskFeed';
import { TaskStateGraph } from '../features/task/TaskStateGraph';
import { TaskTaxonomyPanel } from '../features/task/TaskTaxonomyPanel';
import { TaskTimePanel } from '../features/task/TaskTimePanel';
import { TaskWikiPages } from '../features/task/TaskWikiPages';
import { previewMode } from '../preview';
import { api } from '../services/api';
import type { BacklogItem, WorkItemCustomField, WorkItemDetails } from '../types/scrum';
import { linkTypeNames, originNames, priorityNames } from '../types/scrum';
import { WorkItemKind, kindDisplayOrder, kindMeta, workItemKinds } from '../features/workItems/workItemKinds';
import { WorkItemKindSelector } from './WorkItemKindSelector';
import { userDisplayLabel } from '../utils/userDisplayName';
import { RichTaskDescriptionEditor } from './RichTaskDescriptionEditor';

interface StageOption {
  id: string;
  name: string;
  workflowStatusId?: string;
  statusName?: string;
  statusColor?: string;
}
interface AssignableUser { id: string; displayName?: string; email?: string; userName?: string; }
interface Attachment { id: string; fileName: string; fileSize?: number; mimeType?: string; createdAt: string; }
interface ProjectOption { id: string; methodology?: number; teams: Array<{id: string; name: string}>; }

interface Draft {
  title: string;
  description: string;
  kind: number;
  stageId: string;
  priority: number;
  responsibleId: string;
  teamId: string;
  origin: number;
  requesterId: string;
  requesterName: string;
  requesterEmail: string;
  startDate: string;
  dueDate: string;
  estimatedHours: string;
  remainingHours: string;
  points: string;
  acceptanceCriteria: string;
}

const Overlay = styled(Dialog.Overlay)`
  position:fixed;inset:0;z-index:40;
  background:${({theme})=>`color-mix(in srgb, ${theme.color.neutral[900]} 40%, transparent)`};
`;
/* centered modal — wide, max 94vh height, internal scroll */
const Sheet = styled(Dialog.Content)`
  position:fixed;
  left:50%;top:50%;
  transform:translate(-50%,-50%);
  z-index:41;
  width:min(1200px,95vw);
  max-height:94vh;
  display:flex;flex-direction:column;
  border-radius:${({theme})=>theme.radius.card};
  background:${({theme})=>theme.color.surface};
  box-shadow:${({theme})=>theme.shadow.lg};
  outline:none;
  overflow:hidden;
  @media(max-width:640px){
    inset:auto 0 0 0;left:0;top:auto;
    transform:none;
    width:100%;max-height:96vh;
    border-radius:${({theme})=>theme.radius.card} ${({theme})=>theme.radius.card} 0 0;
  }
  &:has([data-description-expanded=true]){
    inset:0;transform:none;width:100vw;height:100dvh;max-height:100dvh;border-radius:0;
    >header,>[role=alert]{display:none;}
  }
`;
const Header = styled.header`flex:0 0 auto;display:flex;align-items:center;justify-content:space-between;min-height:56px;padding:0 22px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.surface};`;
const HeaderGroup = styled.div`display:flex;align-items:center;gap:9px;color:${({theme})=>theme.color.textMuted};font-size:13px;font-weight:750;`;
const AssigneeWrap = styled.div`position:relative;`;
/* chip do header: botao que abre o popover de responsaveis */
const HeaderAssignee = styled.button`
  display:inline-flex;align-items:center;gap:6px;min-height:34px;padding:0 8px 0 4px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.pill};
  background:${({theme})=>theme.color.neutral[50]};color:${({theme})=>theme.color.textMuted};cursor:pointer;
  &:hover,&[aria-expanded="true"]{border-color:${({theme})=>theme.color.accentBlue};background:${({theme})=>theme.color.surface};}
  >span.count{color:${({theme})=>theme.color.text};font-size:12.5px;font-weight:700;white-space:nowrap;}
`;
const AvatarStack = styled.span`display:inline-flex;align-items:center;>span{box-shadow:0 0 0 2px ${({theme})=>theme.color.surface};}>span+span{margin-left:-8px;}`;
const AssigneePopover = styled.div`
  position:absolute;right:0;top:calc(100% + 6px);z-index:5;width:min(320px,calc(100vw - 32px));
  display:grid;gap:8px;padding:12px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};
  background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.lg};
  text-align:left;
`;
const PopoverTitle = styled.p`display:flex;align-items:center;justify-content:space-between;color:${({theme})=>theme.color.neutral[700]};font-size:12px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;small{color:${({theme})=>theme.color.textMuted};font-weight:700;text-transform:none;}`;
const PopoverList = styled.div<{ $scroll?:boolean }>`display:grid;gap:2px;${({$scroll})=>$scroll?'max-height:220px;overflow-y:auto;':''}`;
const PopoverRow = styled.div`display:flex;align-items:center;gap:8px;min-height:36px;padding:2px 4px;border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.text};font-size:13px;font-weight:600;>span.name{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}`;
const PopoverOption = styled.button`display:flex;width:100%;align-items:center;gap:8px;min-height:36px;padding:2px 6px;border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.text};font-size:13px;font-weight:600;text-align:left;>span.name{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}>svg{color:${({theme})=>theme.color.textMuted};}&:hover{background:${({theme})=>theme.color.neutral[50]};}&:disabled{opacity:.5;}`;
const PopoverSearch = styled.label`display:flex;align-items:center;gap:6px;min-height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.textMuted};input{flex:1;min-width:0;border:0;background:transparent;color:${({theme})=>theme.color.text};font:inherit;font-size:13px;outline:none;}&:focus-within{border-color:${({theme})=>theme.color.accentBlue};}`;
const SaveState = styled.span<{ $error?: boolean }>`display:inline-flex;align-items:center;gap:4px;color:${({theme,$error})=>$error?theme.color.danger:theme.color.success};font-size:12px;`;
const IconButton = styled.button<{ $danger?: boolean }>`display:grid;width:32px;height:32px;place-items:center;border-radius:${({theme})=>theme.radius.md};color:${({theme,$danger})=>$danger?theme.color.danger:theme.color.textMuted};&:hover{background:${({theme})=>theme.color.neutral[100]};}`;
const Close = styled(Dialog.Close)`display:grid;width:34px;height:34px;place-items:center;border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.textMuted};&:hover{background:${({theme})=>theme.color.neutral[100]};}`;
/* scrollable body — fills remaining modal height */
const Body = styled.div`flex:1;overflow-y:auto;padding:20px 26px 40px;@media(max-width:600px){padding:16px 16px 36px;}&:has([data-description-expanded=true]){padding:0;overflow:hidden;> *:not(:has([data-description-expanded=true])){display:none;} >section{margin:0;padding:0;>h2{display:none;}}}`;
const Tabs = styled.nav`position:sticky;top:0;z-index:2;flex:0 0 auto;display:flex;gap:4px;overflow-x:auto;margin:16px -26px 0;padding:8px 26px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.surface};@media(max-width:600px){margin-left:-16px;margin-right:-16px;padding-left:16px;padding-right:16px;}`;
const Tab = styled.button<{ $active:boolean }>`flex:0 0 auto;min-height:34px;padding:0 11px;border-radius:${({theme})=>theme.radius.md};background:${({theme,$active})=>$active?theme.color.brand:theme.color.neutral[50]};color:${({theme,$active})=>$active?theme.color.onBrand:theme.color.textMuted};font-size:13px;font-weight:800;`;
const TypeLine = styled.div`display:flex;flex-wrap:wrap;align-items:center;gap:8px;margin-bottom:10px;`;
// Cada tipo tem cor propria (ver workItemKinds.ts): antes eram tres cores para dez tipos.
const TypeBadge = styled.span<{ $kind:number }>`display:inline-flex;align-items:center;min-height:23px;padding:0 8px;border-radius:${({theme})=>theme.radius.sm};background:${({$kind})=>kindMeta($kind).color};color:white;font-size:12px;font-weight:800;text-transform:uppercase;`;
const OriginBadge = styled.span<{ $external?:boolean }>`display:inline-flex;align-items:center;gap:4px;padding:4px 7px;border-radius:999px;background:${({theme,$external})=>`color-mix(in srgb, ${$external?theme.color.warning:theme.color.neutral[400]} 14%, white)`};color:${({theme,$external})=>$external?theme.color.neutral[800]:theme.color.textMuted};font-size:12px;font-weight:750;`;
const ItemCode = styled.span`color:${({theme})=>theme.color.textMuted};font-size:13px;font-weight:750;`;
/* titulo: duplo clique abre edicao inline */
const Title = styled(Dialog.Title)`margin:0 -6px;padding:2px 6px;border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.text};font-family:${({theme})=>theme.font.display};font-size:clamp(20px,3vw,27px);font-weight:800;line-height:1.2;overflow-wrap:anywhere;cursor:text;&:hover{background:${({theme})=>theme.color.neutral[50]};}`;
const TitleInput = styled.input`display:block;width:calc(100% + 12px);margin:0 -6px;padding:2px 6px;border:1px solid ${({theme})=>theme.color.accentBlue};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};font-family:${({theme})=>theme.font.display};font-size:clamp(20px,3vw,27px);font-weight:800;line-height:1.2;outline:none;box-shadow:0 0 0 2px color-mix(in srgb, ${({theme})=>theme.color.accentBlue} 12%, transparent);`;
const Meta = styled.div`display:flex;flex-wrap:wrap;gap:7px;margin-top:14px;`;
const MetaChip = styled.span`display:inline-flex;align-items:center;gap:5px;min-height:28px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.neutral[50]};color:${({theme})=>theme.color.neutral[700]};font-size:13px;font-weight:700;`;
const Section = styled.section`margin-top:24px;padding-top:4px;>h2{display:flex;align-items:center;gap:7px;margin-bottom:10px;color:${({theme})=>theme.color.neutral[700]};font-size:13.5px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;}`;
const FormGrid = styled.div`display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;@media(max-width:540px){grid-template-columns:1fr;}`;
const Field = styled.label<{ $wide?:boolean }>`display:grid;grid-column:${({$wide})=>$wide?'1/-1':'auto'};gap:5px;color:${({theme})=>theme.color.textMuted};font-size:12px;font-weight:800;text-transform:uppercase;input,textarea,select{width:100%;min-height:37px;padding:8px 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};font-family:${({theme})=>theme.font.body};font-size:14px;font-weight:500;text-transform:none;outline:none;&:focus{border-color:${({theme})=>theme.color.accentBlue};box-shadow:0 0 0 2px color-mix(in srgb, ${({theme})=>theme.color.accentBlue} 12%, transparent);}}textarea{min-height:86px;resize:vertical;line-height:1.5;}select[multiple]{min-height:92px;}`;
const Button = styled.button<{ $secondary?:boolean; $danger?:boolean }>`display:inline-flex;min-height:34px;align-items:center;justify-content:center;gap:6px;padding:0 11px;border:1px solid ${({theme,$danger,$secondary})=>$danger?theme.color.danger:$secondary?theme.color.border:'transparent'};border-radius:${({theme})=>theme.radius.md};background:${({theme,$danger,$secondary})=>$danger?'transparent':$secondary?theme.color.surface:theme.color.brand};color:${({theme,$danger,$secondary})=>$danger?theme.color.danger:$secondary?theme.color.text:theme.color.onBrand};font-size:13px;font-weight:800;&:disabled{opacity:.5;}`;
const InlineForm = styled.form`display:flex;position:relative;flex-wrap:wrap;gap:7px;align-items:center;margin-top:9px;input,select{min-height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};font-size:13px;}input{flex:1 1 220px;min-width:min(220px,100%);}.kind-selector{position:relative;z-index:1;flex:0 1 140px;min-width:140px;}button{flex:0 0 auto;max-width:100%;}@media(max-width:560px){> *{flex:1 1 100%;min-width:0;}.kind-selector{min-width:0;}}`;
const List = styled.div`display:grid;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};overflow:hidden;`;
const Row = styled.div`display:grid;grid-template-columns:minmax(0,1fr) auto;gap:9px;align-items:center;min-height:48px;padding:8px 11px;border-bottom:1px solid ${({theme})=>theme.color.neutral[100]};&:last-child{border-bottom:0;}strong{display:block;font-size:13.5px;}small{display:block;margin-top:2px;color:${({theme})=>theme.color.textMuted};font-size:12px;}`;
const AvatarRow = styled.div`display:flex;flex-wrap:wrap;gap:7px;align-items:center;`;
const Person = styled.span`display:inline-flex;align-items:center;gap:6px;padding:5px 7px;border:1px solid ${({theme})=>theme.color.border};border-radius:999px;color:${({theme})=>theme.color.text};font-size:13px;button{display:grid;place-items:center;color:${({theme})=>theme.color.textMuted};}`;
const PrimaryLabel = styled.small`padding:2px 5px;border-radius:999px;background:${({theme})=>theme.color.neutral[100]};color:${({theme})=>theme.color.textMuted};font-size:10px;font-weight:800;text-transform:uppercase;`;
const SectionHelp = styled.p`margin:-3px 0 10px;color:${({theme})=>theme.color.textMuted};font-size:13px;line-height:1.45;`;
const ActionFeedback = styled.p`margin:8px 0 0;color:${({theme})=>theme.color.success};font-size:12.5px;font-weight:700;`;
const Avatar = styled.span`display:grid;width:24px;height:24px;place-items:center;border-radius:999px;background:${({theme})=>theme.color.brand};color:white;font-size:11px;font-weight:800;`;
const Empty = styled.p`padding:14px;color:${({theme})=>theme.color.textMuted};font-size:13px;text-align:center;`;
const ActionBar = styled.div`display:flex;flex-wrap:wrap;gap:8px;margin-top:26px;padding-top:18px;border-top:1px solid ${({theme})=>theme.color.border};`;
const Progress = styled.div`display:grid;min-height:230px;place-items:center;color:${({theme})=>theme.color.textMuted};font-size:13.5px;`;
const ErrorBox = styled.div.attrs({role:'alert'})`margin-top:12px;padding:12px 14px;border:1px solid color-mix(in srgb, ${({theme})=>theme.color.danger} 50%, ${({theme})=>theme.color.surface});border-radius:${({theme})=>theme.radius.md};background:color-mix(in srgb, ${({theme})=>theme.color.danger} 10%, ${({theme})=>theme.color.surface});color:${({theme})=>theme.color.text};font-size:13px;line-height:1.5;`;
const TaskError = styled(ErrorBox)`flex:0 0 auto;margin:12px 22px 0;`;
const VisibilityNote = styled.p`margin-bottom:9px;padding:9px 11px;border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.neutral[50]};color:${({theme})=>theme.color.textMuted};font-size:13px;line-height:1.5;`;
const PublicConversation = styled.div`display:grid;gap:7px;max-height:250px;overflow:auto;margin-bottom:9px;`;
const PublicMessage = styled.article<{ $requester?:boolean }>`justify-self:${({$requester})=>$requester?'start':'end'};max-width:88%;padding:9px 11px;border-radius:${({theme})=>theme.radius.lg};background:${({theme,$requester})=>$requester?theme.color.neutral[100]:theme.color.brand};color:${({theme,$requester})=>$requester?theme.color.text:theme.color.onBrand};strong{display:block;font-size:12px}p{margin:3px 0;font-size:13.5px;line-height:1.45;white-space:pre-wrap}small{font-size:11px;opacity:.72}`;
const PublicReply = styled.form`display:grid;gap:7px;textarea{min-height:74px;padding:8px 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};resize:vertical;font:inherit;font-size:13.5px}button{justify-self:end}`;

const initials=(value:string)=>value.split(/[-_.@\s]+/).filter(Boolean).slice(0,2).map(part=>part[0]?.toUpperCase()).join('');
const toDraft=(details:WorkItemDetails):Draft=>({
  title:details.title,description:details.description??'',kind:details.kind,stageId:details.stageId??'',priority:details.priority,
  responsibleId:details.responsibleId??'',teamId:details.teamId??'',origin:details.origin??1,requesterId:details.requesterId??'',requesterName:details.requesterName??'',requesterEmail:details.requesterEmail??'',
  startDate:details.startDate??'',dueDate:details.dueDate??'',estimatedHours:details.estimatedHours?.toString()??'',remainingHours:details.remainingHours?.toString()??'',points:details.points?.toString()??'',acceptanceCriteria:details.acceptanceCriteria??'',
});
const numberOrNull=(value:string)=>value.trim()===''?null:Number(value);
const formatBytes=(value?:number)=>!value?'0 B':value<1024?`${value} B`:value<1048576?`${(value/1024).toFixed(1)} KB`:`${(value/1048576).toFixed(1)} MB`;
const parseOptions=(value?:string)=>{try{return JSON.parse(value??'[]') as string[];}catch{return [];}};
const parseSelected=(value?:string)=>{try{return JSON.parse(value??'[]') as string[];}catch{return [];}};

interface TaskDetailDrawerProps {
  item: BacklogItem | null;
  projectKey: string;
  sprintName?: string;
  onOpenChange: (open:boolean)=>void;
  onItemUpdated?: (item:BacklogItem)=>void;
  onOpenSubtask?: (workItemId:string)=>void;
}

export function TaskDetailDrawer({item,projectKey,sprintName,onOpenChange,onItemUpdated,onOpenSubtask}:TaskDetailDrawerProps){
  const queryClient=useQueryClient();
  const realMode=Boolean(api.getToken())&&!previewMode;
  const [draft,setDraft]=useState<Draft|null>(null);
  const [saveState,setSaveState]=useState<'idle'|'saving'|'saved'|'error'>('idle');
  const [assigneeId,setAssigneeId]=useState('');
  const [transferBoardId,setTransferBoardId]=useState('');
  const [transferStageId,setTransferStageId]=useState('');
  const [assigneeOpen,setAssigneeOpen]=useState(false);
  const [assigneeSearch,setAssigneeSearch]=useState('');
  const [assigneeFeedback,setAssigneeFeedback]=useState('');
  const assigneeWrapRef=useRef<HTMLDivElement>(null);
  const [editingTitle,setEditingTitle]=useState(false);
  const titleBeforeEditRef=useRef('');
  const editingTitleRef=useRef(false);
  const [subtaskTitle,setSubtaskTitle]=useState('');
  const [subtaskKind,setSubtaskKind]=useState<number>(WorkItemKind.Subtask);
  const [linkForm,setLinkForm]=useState({targetWorkItemId:'',type:1});
  const [customValues,setCustomValues]=useState<Record<string,string>>({});
  const [publicReply,setPublicReply]=useState('');
  const [activeTab,setActiveTab]=useState<'description'|'comments'|'subtasks'|'attachments'|'history'|'graph'>('description');

  const fallback=useMemo<WorkItemDetails|null>(()=>item?{
    id:item.id,number:item.number??0,reference:item.number?`${projectKey}-${item.number}`:`${projectKey}-${item.id.slice(-4).toUpperCase()}`,
    boardId:item.boardId,boardName:item.boardName,projectKey,teamId:item.teamId,stageId:item.stageId,stageName:item.stageName,
    workflowStatusId:item.workflowStatusId,workflowStatusName:item.workflowStatusName,parentId:item.parentId,sprintId:item.sprintId,sprintName,
    kind:item.kind,origin:item.origin??1,title:item.title,description:item.description,priority:item.priority,responsibleId:item.responsibleId,
    participants:(item.assigneeIds??[]).map(userId=>({userId})),requesterId:item.requesterId,requesterName:item.requesterName,requesterEmail:item.requesterEmail,
    startDate:item.startDate,dueDate:item.dueDate,createdAt:item.createdAt??new Date().toISOString(),updatedAt:item.createdAt??new Date().toISOString(),completedAt:item.completedAt,
    estimatedHours:item.estimatedHours,remainingHours:item.remainingHours,realizedHours:0,points:item.points,tags:[],acceptanceCriteria:item.acceptanceCriteria,isArchived:false,
    checklist:[],subtasks:[],attachmentsCount:0,commentsCount:0,followerIds:[],isFollowing:false,links:[],customFields:[],version:item.version??'',
  }:null,[item,projectKey,sprintName]);

  const detailsQuery=useQuery<WorkItemDetails>({queryKey:['work-item',item?.id],queryFn:()=>api.getWorkItemDetails(item!.id),enabled:Boolean(item)&&realMode,retry:false});
  const details=detailsQuery.data??fallback;
  const stagesQuery=useQuery<StageOption[]>({queryKey:['stages',details?.boardId],queryFn:()=>api.getBoardStages(details!.boardId!),enabled:Boolean(details?.boardId)&&realMode});
  const transferBoards=useQuery<Array<{id:string;name:string;projectId:string}>>({queryKey:['boards'],queryFn:()=>api.getBoards(),enabled:Boolean(details)&&realMode});
  const transferStages=useQuery<StageOption[]>({queryKey:['stages',transferBoardId],queryFn:()=>api.getBoardStages(transferBoardId),enabled:Boolean(transferBoardId)&&realMode});
  const usersQuery=useQuery<AssignableUser[]>({queryKey:['assignable-users',details?.projectId],queryFn:()=>api.getAssignableUsers(details!.projectId!),enabled:Boolean(details?.projectId)&&realMode});
  const projectQuery=useQuery<ProjectOption>({queryKey:['project',details?.projectId],queryFn:()=>api.getProject(details!.projectId!),enabled:Boolean(details?.projectId)&&realMode});
  const sprintsQuery=useQuery<Array<{id:string;name:string;status:number}>>({queryKey:['project-sprints',details?.projectId],queryFn:()=>api.getProjectSprints(details!.projectId!),enabled:Boolean(details?.projectId)&&realMode});
  const attachmentsQuery=useQuery<Attachment[]>({queryKey:['work-item-attachments',details?.id],queryFn:()=>api.getAttachments(details!.id),enabled:Boolean(details)&&realMode});
  const showStoryPoints=Boolean(projectQuery.data)&&projectQuery.data?.methodology!==1;

  useEffect(()=>{if(details){setDraft(toDraft(details));setCustomValues(Object.fromEntries(details.customFields.map(field=>[field.fieldId,field.value??''])));}},[details]);
  useEffect(()=>{setActiveTab('description');setAssigneeOpen(false);setAssigneeSearch('');setEditingTitle(false);},[item?.id]);
  // fecha o popover de responsaveis ao clicar fora dele
  useEffect(()=>{
    if(!assigneeOpen)return;
    const onPointerDown=(event:PointerEvent)=>{if(!assigneeWrapRef.current?.contains(event.target as Node))setAssigneeOpen(false);};
    document.addEventListener('pointerdown',onPointerDown);
    return()=>document.removeEventListener('pointerdown',onPointerDown);
  },[assigneeOpen]);

  const invalidate=async()=>Promise.all([
    queryClient.invalidateQueries({queryKey:['work-item',item?.id]}),
    queryClient.invalidateQueries({queryKey:['project-backlog']}),
  ]);
  const update=useMutation({
    mutationFn:async(next:Draft)=>{if(realMode&&details)await api.updateWorkItem(details.id,{title:next.title,description:next.description||null,kind:next.kind,stageId:next.stageId||null,priority:next.priority,responsibleId:next.responsibleId||null,teamId:next.teamId||null,origin:next.origin,requesterId:next.requesterId||null,requesterName:next.requesterName||null,requesterEmail:next.requesterEmail||null,startDate:next.startDate||null,dueDate:next.dueDate||null,estimatedHours:numberOrNull(next.estimatedHours),remainingHours:numberOrNull(next.remainingHours),points:numberOrNull(next.points),acceptanceCriteria:next.acceptanceCriteria||null});return next;},
    onMutate:async(next)=>{
      setSaveState('saving');
      if(!details)return {previous:undefined};
      const key=['work-item',details.id] as const;
      await queryClient.cancelQueries({queryKey:key});
      const previous=queryClient.getQueryData<WorkItemDetails>(key);
      const selectedStage=stagesQuery.data?.find(stage=>stage.id===next.stageId);
      queryClient.setQueryData<WorkItemDetails>(key,current=>current?{
        ...current,...next,
        stageId:next.stageId||undefined,
        stageName:selectedStage?.name,
        workflowStatusId:selectedStage?.workflowStatusId,
        workflowStatusName:selectedStage?.statusName,
        teamId:next.teamId||undefined,
        teamName:projectQuery.data?.teams.find(team=>team.id===next.teamId)?.name,
        estimatedHours:numberOrNull(next.estimatedHours)??undefined,
        remainingHours:numberOrNull(next.remainingHours)??undefined,
        points:numberOrNull(next.points)??undefined,
      }:current);
      return {previous};
    },
    onSuccess:async(next)=>{setSaveState('saved');if(item)onItemUpdated?.({...item,title:next.title,description:next.description,kind:next.kind,stageId:next.stageId||undefined,stageName:stagesQuery.data?.find(stage=>stage.id===next.stageId)?.name,priority:next.priority,responsibleId:next.responsibleId||undefined,teamId:next.teamId||undefined,origin:next.origin,requesterId:next.requesterId||undefined,requesterName:next.requesterName||undefined,requesterEmail:next.requesterEmail||undefined,startDate:next.startDate||undefined,dueDate:next.dueDate||undefined,estimatedHours:numberOrNull(next.estimatedHours)??undefined,remainingHours:numberOrNull(next.remainingHours)??undefined,points:numberOrNull(next.points)??undefined,acceptanceCriteria:next.acceptanceCriteria||undefined});if(realMode)await invalidate();},
    onError:(_error,_next,context)=>{
      if(details&&context?.previous)queryClient.setQueryData(['work-item',details.id],context.previous);
      setSaveState('error');
    },
  });
  const commit=(next:Draft)=>{setDraft(next);update.mutate(next);};
  const change=<K extends keyof Draft>(key:K,value:Draft[K],immediate=false)=>{if(!draft)return;const next={...draft,[key]:value};setDraft(next);if(immediate)commit(next);};
  const saveDescription=useCallback(async(description:string)=>{
    if(!draft)return;
    const next={...draft,description};
    setDraft(next);
    await update.mutateAsync(next);
  },[draft,update]);

  const assignees=useMutation({mutationFn:({userId,add}:{userId:string;add:boolean})=>add?api.assignUser(details!.id,userId):api.removeAssignee(details!.id,userId),onMutate:()=>setAssigneeFeedback(''),onSuccess:async(_result,variables)=>{setAssigneeId('');setAssigneeFeedback(variables.add?'Responsável adicionado.':'Responsável removido.');await invalidate();}});
  const subtask=useMutation({
    mutationFn:async()=>({result:await api.createWorkItem({boardId:details!.boardId,stageId:details!.stageId,parentId:details!.id,title:subtaskTitle,kind:subtaskKind,priority:1,position:(details!.subtasks.length+1)*100}),title:subtaskTitle}),
    onSuccess:async({result,title})=>{
      setSubtaskTitle('');setSubtaskKind(WorkItemKind.Subtask);await invalidate();
      const workItemId=typeof result==='string'?result:(result as {id?:string})?.id;
      if(workItemId){onOpenSubtask?.(workItemId);return;}
      if(onOpenSubtask){
        const created=(await api.getSubItems(details!.id) as Array<{id:string;title:string}>).find(child=>child.title===title);
        if(created)onOpenSubtask(created.id);
      }
    },
  });
  const sprint=useMutation({mutationFn:(sprintId:string|null)=>api.planSprint(details!.projectId!,sprintId,[details!.id]),onSuccess:invalidate});
  const link=useMutation({mutationFn:()=>api.addWorkItemLink(details!.id,linkForm.targetWorkItemId,linkForm.type),onSuccess:async()=>{setLinkForm({targetWorkItemId:'',type:1});await invalidate();}});
  const removeLink=useMutation({mutationFn:(linkId:string)=>api.removeWorkItemLink(details!.id,linkId),onSuccess:invalidate});
  const following=useMutation({mutationFn:()=>api.setWorkItemFollowing(details!.id,!details!.isFollowing),onMutate:()=>queryClient.setQueryData<WorkItemDetails>(['work-item',details!.id],current=>current?{...current,isFollowing:!current.isFollowing}:current),onSuccess:invalidate});
  const customFields=useMutation({mutationFn:()=>api.setWorkItemCustomFields(details!.id,customValues),onMutate:()=>setSaveState('saving'),onSuccess:async()=>{setSaveState('saved');await invalidate();},onError:()=>setSaveState('error')});
  const publicResponse=useMutation({mutationFn:()=>api.replyToExternalRequest(details!.externalCommunication!.protocol,publicReply.trim()),onSuccess:async()=>{setPublicReply('');await queryClient.invalidateQueries({queryKey:['work-item',details!.id]});}});
  const upload=useMutation({mutationFn:(file:File)=>api.uploadAttachment(details!.id,file),onSuccess:()=>queryClient.invalidateQueries({queryKey:['work-item-attachments',details?.id]})});
  const removeAttachment=useMutation({
    mutationFn:(attachmentId:string)=>api.deleteAttachment(details!.id,attachmentId),
    onSuccess:()=>{
      queryClient.invalidateQueries({queryKey:['work-item-attachments',details?.id]});
      queryClient.invalidateQueries({queryKey:['work-item',details?.id]});
    },
  });
  const archive=useMutation({mutationFn:()=>details!.isArchived?api.reactivateWorkItem(details!.id):api.archiveWorkItem(details!.id),onSuccess:async()=>{await invalidate();onOpenChange(false);}});
  const transfer=useMutation({mutationFn:()=>api.transferWorkItem(details!.id,transferBoardId,transferStageId),onSuccess:async()=>{setTransferBoardId('');setTransferStageId('');await invalidate();},onError:()=>setSaveState('error')});
  const duplicate=useMutation({mutationFn:()=>api.duplicateWorkItem(details!.id),onSuccess:async()=>{setSaveState('saved');await invalidate();}});

  const onUpload=(event:ChangeEvent<HTMLInputElement>)=>{const file=event.target.files?.[0];if(file)upload.mutate(file);event.target.value='';};
  const uploadDescriptionImage=useCallback(async(file:File)=>{
    if(!details)throw new Error('Tarefa indisponível.');
    const extension=file.type.split('/')[1]?.replace('jpeg','jpg')||'png';
    const normalized=file.name&&file.name.includes('.')
      ? file
      : new File([file],`imagem-colada-${Date.now()}.${extension}`,{type:file.type||'image/png'});
    const attachment=await upload.mutateAsync(normalized) as Attachment;
    await queryClient.invalidateQueries({queryKey:['work-item',details.id]});
    return {attachmentId:attachment.id,alt:attachment.fileName||normalized.name};
  },[details,queryClient,upload]);
  const downloadAttachment=async(attachment:Attachment)=>{const blob=await api.downloadAttachment(details!.id,attachment.id);const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download=attachment.fileName;anchor.click();URL.revokeObjectURL(url);};
  const submitSubtask=(event:FormEvent)=>{event.preventDefault();if(subtaskTitle.trim()&&realMode)subtask.mutate();};
  const submitLink=(event:FormEvent)=>{event.preventDefault();if(linkForm.targetWorkItemId&&realMode)link.mutate();};
  const availableUsers=usersQuery.data?.filter(user=>!details?.participants.some(person=>person.userId===user.id))??[];
  const currentResponsibleIsEligible=Boolean(draft?.responsibleId&&usersQuery.data?.some(user=>user.id===draft.responsibleId));
  const currentResponsibleLabel=details?.responsibleName
    || details?.participants.find(person=>person.userId===draft?.responsibleId)?.displayName
    || (draft?.responsibleId ? `Usuário ${draft.responsibleId.slice(0,8)}` : '');
  const userLabel=(id:string)=>{const user=usersQuery.data?.find(current=>current.id===id);return user?userDisplayLabel(user):`Usuário ${id.slice(0,8)}`;};
  const participantLabel=(person:{userId:string;displayName?:string})=>person.displayName||userLabel(person.userId);
  const searchTerm=assigneeSearch.trim().toLocaleLowerCase('pt-BR');
  const searchableUsers=searchTerm?availableUsers.filter(user=>userDisplayLabel(user).toLocaleLowerCase('pt-BR').includes(searchTerm)):availableUsers;
  // responsavel principal primeiro na pilha de avatares do header
  const stackedAssignees=details?[...details.participants].sort((a,b)=>Number(b.userId===draft?.responsibleId)-Number(a.userId===draft?.responsibleId)).slice(0,3):[];

  // ref espelha o estado para blur/Escape nao dispararem duas vezes no mesmo ciclo
  const startTitleEdit=()=>{if(!draft)return;titleBeforeEditRef.current=draft.title;editingTitleRef.current=true;setEditingTitle(true);};
  const finishTitleEdit=()=>{
    if(!editingTitleRef.current||!draft)return;
    editingTitleRef.current=false;setEditingTitle(false);
    const trimmed=draft.title.trim();
    if(!trimmed){change('title',titleBeforeEditRef.current);return;}
    if(trimmed!==titleBeforeEditRef.current)commit({...draft,title:trimmed});else if(trimmed!==draft.title)change('title',trimmed);
  };
  const cancelTitleEdit=()=>{if(!editingTitleRef.current)return;editingTitleRef.current=false;setEditingTitle(false);change('title',titleBeforeEditRef.current);};
  const onTitleKeyDown=(event:KeyboardEvent<HTMLInputElement>)=>{if(event.key==='Enter'){event.preventDefault();finishTitleEdit();}};

  // Escape fecha primeiro o popover / a edicao do titulo; so depois o modal
  const onEscapeKeyDown=(event:globalThis.KeyboardEvent)=>{
    if(assigneeOpen){event.preventDefault();setAssigneeOpen(false);return;}
    if(editingTitleRef.current){event.preventDefault();cancelTitleEdit();}
  };

  return <Dialog.Root open={Boolean(item)} onOpenChange={onOpenChange}><Dialog.Portal><Overlay/>{item&&<Sheet aria-describedby={`task-description-${item.id}`} onEscapeKeyDown={onEscapeKeyDown}>
    <Header>
      <HeaderGroup>
        <GitBranch size={13}/>Item de trabalho
        {saveState==='saving'&&<SaveState><LoaderCircle size={11}/>Salvando...</SaveState>}
        {saveState==='saved'&&<SaveState><Check size={11}/>Salvo</SaveState>}
        {saveState==='error'&&<SaveState $error>Falha ao salvar</SaveState>}
      </HeaderGroup>
      <div style={{display:'flex',gap:4,alignItems:'center'}}>
        {details&&draft&&(
          <AssigneeWrap ref={assigneeWrapRef}>
            <HeaderAssignee type="button" aria-haspopup="dialog" aria-expanded={assigneeOpen} aria-controls={`task-assignees-${item.id}`} title="Clique para adicionar ou remover responsáveis" onClick={()=>setAssigneeOpen(open=>!open)}>
              <AvatarStack aria-hidden>{stackedAssignees.length===0?<Avatar>—</Avatar>:stackedAssignees.map(person=><Avatar key={person.userId}>{initials(participantLabel(person))||'—'}</Avatar>)}</AvatarStack>
              <span className="count">{details.participants.length} {details.participants.length===1?'responsável':'responsáveis'}</span>
              <UserPlus size={14}/>
            </HeaderAssignee>
            {assigneeOpen&&<AssigneePopover id={`task-assignees-${item.id}`} role="dialog" aria-label="Gerenciar responsáveis">
              <PopoverTitle>Responsáveis<small>{details.participants.length} alocado(s)</small></PopoverTitle>
              <PopoverList>
                {details.participants.map(person=>{const label=participantLabel(person);const primary=person.userId===draft.responsibleId;return <PopoverRow key={person.userId} data-user-id={person.userId}><Avatar>{initials(label)||'—'}</Avatar><span className="name">{label}</span>{primary?<PrimaryLabel>Principal</PrimaryLabel>:realMode&&<IconButton $danger aria-label={`Remover ${label} dos responsáveis`} disabled={assignees.isPending} onClick={()=>assignees.mutate({userId:person.userId,add:false})}><X size={13}/></IconButton>}</PopoverRow>;})}
                {details.participants.length===0&&<Empty>Nenhum responsável alocado.</Empty>}
              </PopoverList>
              {realMode&&<>
                <PopoverSearch><Search size={13}/><input autoFocus aria-label="Buscar pessoa para adicionar" placeholder="Buscar pessoa para adicionar..." value={assigneeSearch} onChange={e=>setAssigneeSearch(e.target.value)}/></PopoverSearch>
                <PopoverList $scroll aria-label="Pessoas disponíveis">
                  {searchableUsers.map(user=>{const label=userDisplayLabel(user);return <PopoverOption key={user.id} data-user-id={user.id} type="button" disabled={assignees.isPending} aria-label={`Adicionar ${label} como responsável`} onClick={()=>assignees.mutate({userId:user.id,add:true})}><Avatar>{initials(label)||'—'}</Avatar><span className="name">{label}</span><Plus size={13}/></PopoverOption>;})}
                  {searchableUsers.length===0&&<Empty>{availableUsers.length===0?'Todas as pessoas já estão alocadas.':'Nenhuma pessoa encontrada.'}</Empty>}
                </PopoverList>
              </>}
            </AssigneePopover>}
          </AssigneeWrap>
        )}
        {details&&<IconButton title={details.isFollowing?'Deixar de seguir':'Seguir tarefa'} onClick={()=>realMode&&following.mutate()}>{details.isFollowing?<EyeOff size={16}/>:<Eye size={16}/>}</IconButton>}
        <Close aria-label="Fechar modal"><X size={18}/></Close>
      </div>
    </Header>
    {(update.error||assignees.error||link.error||customFields.error)&&<TaskError>{((update.error||assignees.error||link.error||customFields.error) as Error).message}</TaskError>}
    {!details||!draft?<Progress>{detailsQuery.isLoading?'Carregando detalhes...':'Preparando tarefa...'}</Progress>:<Body>
      <TypeLine><TypeBadge $kind={draft.kind} title={kindMeta(draft.kind).description}>{kindMeta(draft.kind).label}</TypeBadge><ItemCode>{details.reference}</ItemCode><OriginBadge $external={draft.origin===2}>{originNames[draft.origin]??'Origem não informada'}</OriginBadge>{details.isArchived&&<OriginBadge>Arquivada</OriginBadge>}</TypeLine>
      {editingTitle
        ?<><Dialog.Title style={{position:'absolute',width:1,height:1,overflow:'hidden'}}>{draft.title}</Dialog.Title><TitleInput autoFocus aria-label="Editar título" maxLength={500} value={draft.title} onChange={e=>change('title',e.target.value)} onBlur={finishTitleEdit} onKeyDown={onTitleKeyDown} onFocus={e=>e.currentTarget.select()}/></>
        :<Title title="Clique duas vezes para editar o título" onDoubleClick={startTitleEdit}>{draft.title}</Title>}
      <span id={`task-description-${item.id}`} style={{position:'absolute',width:1,height:1,overflow:'hidden'}}>Detalhes editáveis da tarefa {draft.title}.</span>
      <Meta><MetaChip aria-label="Coluna atual"><CircleDot size={12}/>{stagesQuery.data?.find(stage=>stage.id===draft.stageId)?.name||details.stageName||'Sem coluna'}</MetaChip><MetaChip><Target size={12}/>{priorityNames[draft.priority]??'Média'}</MetaChip><MetaChip><FolderKanban size={12}/>{details.boardName}</MetaChip>{(details.sprintName||sprintName)&&<MetaChip><GitBranch size={12}/>{details.sprintName||sprintName}</MetaChip>}</Meta>
      {detailsQuery.error&&<ErrorBox>{(detailsQuery.error as Error).message}. Exibindo os dados disponíveis na tela atual.</ErrorBox>}

      {realMode && <details>
        <summary>Transferir tarefa para outro quadro</summary>
        <select aria-label="Quadro de destino" value={transferBoardId} onChange={e=>{setTransferBoardId(e.target.value);setTransferStageId('');}}>
          <option value="">Escolha o quadro de destino</option>
          {(transferBoards.data??[]).filter(board=>board.projectId===details.projectId&&board.id!==details.boardId).map(board=><option key={board.id} value={board.id}>{board.name}</option>)}
        </select>
        <select aria-label="Coluna de destino" value={transferStageId} onChange={e=>setTransferStageId(e.target.value)}>
          <option value="">Escolha a coluna de destino</option>
          {(transferStages.data??[]).map(stage=><option key={stage.id} value={stage.id}>{stage.name}</option>)}
        </select>
        <button type="button" disabled={!transferBoardId||!transferStageId||transfer.isPending} onClick={()=>transfer.mutate()}>Transferir tarefa</button>
        {transfer.isError&&<p role="alert">{(transfer.error as Error).message}</p>}
      </details>}
      <Tabs aria-label="Seções da tarefa">
        <Tab $active={activeTab==='description'} onClick={()=>setActiveTab('description')}>Descrição</Tab>
        <Tab $active={activeTab==='comments'} onClick={()=>setActiveTab('comments')}>Comentários</Tab>
        <Tab $active={activeTab==='subtasks'} onClick={()=>setActiveTab('subtasks')}>Subtarefas</Tab>
        <Tab $active={activeTab==='attachments'} onClick={()=>setActiveTab('attachments')}>Anexos</Tab>
        <Tab $active={activeTab==='history'} onClick={()=>setActiveTab('history')}>Histórico</Tab>
        <Tab $active={activeTab==='graph'} onClick={()=>setActiveTab('graph')}>Grafo de estados</Tab>
      </Tabs>

      {activeTab==='description'&&<>
      <Section><h2><Save size={14}/>Descrição</h2><RichTaskDescriptionEditor value={draft.description} workItemId={details.id} onSave={saveDescription} onOpenAttachments={()=>setActiveTab('attachments')} onUploadImage={realMode?uploadDescriptionImage:undefined}/></Section>
      <Section><h2><Save size={14}/>Dados principais</h2><FormGrid>
        <Field $wide>Título<input value={draft.title} maxLength={500} onChange={e=>change('title',e.target.value)} onBlur={()=>draft.title.trim()&&commit({...draft,title:draft.title.trim()})}/></Field>
        <Field>Tipo<select value={draft.kind} onChange={e=>change('kind',Number(e.target.value),true)} title={kindMeta(draft.kind).description}>{kindDisplayOrder.map(id=><option key={id} value={id} title={workItemKinds[id].description}>{workItemKinds[id].label} — {workItemKinds[id].description}</option>)}</select></Field>
        <Field>Status<select value={draft.stageId} onChange={e=>change('stageId',e.target.value,true)}><option value="" disabled>Escolha uma coluna</option>{stagesQuery.data?.map(stage=><option key={stage.id} value={stage.id}>{stage.name}</option>)}</select></Field>
        <Field>Prioridade<select value={draft.priority} onChange={e=>change('priority',Number(e.target.value),true)}><option value={0}>Baixa</option><option value={1}>Média</option><option value={2}>Alta</option><option value={3}>Crítica</option></select></Field>
        <Field>Responsável principal<select aria-label="Responsável principal" value={draft.responsibleId} onChange={e=>change('responsibleId',e.target.value,true)}><option value="">Não atribuído</option>{draft.responsibleId&&!currentResponsibleIsEligible&&<option value={draft.responsibleId} disabled>{currentResponsibleLabel} (sem acesso atual)</option>}{usersQuery.data?.map(user=><option key={user.id} value={user.id}>{userDisplayLabel(user)}</option>)}</select></Field>
        <Field>Equipe<select value={draft.teamId} onChange={e=>change('teamId',e.target.value,true)}><option value="">Herdar do quadro</option>{projectQuery.data?.teams.map(team=><option key={team.id} value={team.id}>{team.name}</option>)}</select></Field>
        <Field>Origem<select value={draft.origin} onChange={e=>change('origin',Number(e.target.value),true)}>{Object.entries(originNames).map(([id,label])=><option key={id} value={id}>{label}</option>)}</select></Field>
      </FormGrid></Section>

      <Section><h2><CalendarDays size={14}/>Planejamento</h2><FormGrid>
        <Field>Data de início<input type="date" value={draft.startDate} onChange={e=>change('startDate',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        <Field>Prazo<input type="date" min={draft.startDate||undefined} value={draft.dueDate} onChange={e=>change('dueDate',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        <Field>Horas previstas<input type="number" min="0.01" step="0.25" value={draft.estimatedHours} onChange={e=>change('estimatedHours',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        <Field>Horas restantes<input type="number" min="0" step="0.25" value={draft.remainingHours} onChange={e=>change('remainingHours',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        {showStoryPoints&&<Field>Story points<input type="number" min="0" value={draft.points} onChange={e=>change('points',e.target.value)} onBlur={()=>commit(draft)}/></Field>}
        {details.projectId&&<Field>Sprint<select aria-label='Sprint da tarefa' value={details.sprintId??''} disabled={sprint.isPending} onChange={event=>sprint.mutate(event.target.value||null)}><option value=''>Product Backlog</option>{sprintsQuery.data?.filter(current=>current.status===1||current.status===2||current.id===details.sprintId).map(current=><option key={current.id} value={current.id}>{current.name}</option>)}</select></Field>}
        <Field>Horas realizadas<input value={`${details.realizedHours.toFixed(2)} h`} readOnly/></Field>
        <Field $wide>Critérios de aceite<textarea value={draft.acceptanceCriteria} onChange={e=>change('acceptanceCriteria',e.target.value)} onBlur={()=>commit(draft)} placeholder="Condições objetivas para concluir esta tarefa"/></Field>
      </FormGrid></Section>

      {(draft.origin!==1||details.requesterName||details.requesterEmail)&&<Section><h2><UserRound size={14}/>Solicitante e origem</h2><FormGrid>
        <Field>Nome do solicitante<input value={draft.requesterName} onChange={e=>change('requesterName',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        <Field>E-mail<input type="email" value={draft.requesterEmail} onChange={e=>change('requesterEmail',e.target.value)} onBlur={()=>commit(draft)}/></Field>
        <Field $wide>ID interno do solicitante<input value={draft.requesterId} onChange={e=>change('requesterId',e.target.value)} onBlur={()=>commit(draft)} placeholder="Opcional para usuários internos"/></Field>
      </FormGrid></Section>}

      <Section><h2><UserRound size={14}/>Responsáveis da tarefa</h2><SectionHelp>Adicione quantas pessoas forem necessárias. O responsável principal continua identificado como accountable da entrega.</SectionHelp><AvatarRow>{details.participants.map(person=>{const label=person.displayName||userLabel(person.userId);const primary=person.userId===draft.responsibleId;return <Person key={person.userId}><Avatar>{initials(label)}</Avatar>{label}{primary&&<PrimaryLabel>Principal</PrimaryLabel>}{!primary&&realMode&&<button aria-label={`Remover responsável ${label}`} onClick={()=>assignees.mutate({userId:person.userId,add:false})}><X size={11}/></button>}</Person>;})}{details.participants.length===0&&<Empty>Nenhum responsável alocado.</Empty>}</AvatarRow>{realMode&&<InlineForm aria-label="Adicionar responsável" onSubmit={event=>{event.preventDefault();if(assigneeId)assignees.mutate({userId:assigneeId,add:true});}}><select aria-label="Novo responsável" value={assigneeId} onChange={e=>setAssigneeId(e.target.value)}><option value="">Selecionar pessoa...</option>{availableUsers.map(user=><option key={user.id} value={user.id}>{userDisplayLabel(user)}</option>)}</select><Button disabled={!assigneeId||assignees.isPending}><UserPlus size={12}/>Adicionar responsável</Button></InlineForm>}{assigneeFeedback&&<ActionFeedback role="status">{assigneeFeedback}</ActionFeedback>}</Section>

      {realMode&&<Section><h2><TagsIcon/>Classificação</h2><TaskTaxonomyPanel workItemId={details.id} taskTypeId={details.taskTypeId} points={details.points} tagIds={details.tags.map(tag=>tag.id)} showPoints={showStoryPoints} onChanged={invalidate}/></Section>}
      </>}
      {activeTab==='subtasks'&&<Section><h2><CheckCircle2 size={14}/>Checklist e subtarefas</h2><SectionHelp>Use o checklist para passos simples e o seletor abaixo para criar uma subtarefa ligada a esta tarefa. Selecione uma subtarefa para abrir todos os detalhes.</SectionHelp>{realMode?<TaskChecklistPanel workItemId={details.id} onChanged={invalidate}/>:<Empty>Checklist disponível com dados reais.</Empty>}<List>{details.subtasks.map(child=><Row key={child.id} role='button' tabIndex={0} aria-label={'Abrir detalhes da subtarefa ' + child.title} onClick={()=>onOpenSubtask?.(child.id)} onKeyDown={event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();onOpenSubtask?.(child.id);}}} style={{cursor:onOpenSubtask?'pointer':'default'}}><div><strong>{child.number?`${projectKey}-${child.number} · `:''}{child.title}</strong><small>{child.completedAt?'Concluída':'Em andamento'}</small></div></Row>)}{details.subtasks.length===0&&<Empty>Nenhuma subtarefa.</Empty>}</List>{realMode&&<InlineForm onSubmit={submitSubtask}><WorkItemKindSelector className="kind-selector" ariaLabel="Tipo da subtarefa" value={subtaskKind} onChange={setSubtaskKind} allowedKinds={[WorkItemKind.Subtask, WorkItemKind.Task, WorkItemKind.Bug, WorkItemKind.UserStory]}/><input value={subtaskTitle} onChange={e=>setSubtaskTitle(e.target.value)} placeholder="Nova subtarefa (somente título)"/><Button disabled={!subtaskTitle.trim()||subtask.isPending}><Plus size={12}/>Adicionar</Button></InlineForm>}</Section>}

      {activeTab==='attachments'&&<Section><h2><Paperclip size={14}/>Anexos</h2><List>{attachmentsQuery.data?.map(attachment=><Row key={attachment.id}><div><strong>{attachment.fileName}</strong><small>{formatBytes(attachment.fileSize)} · {attachment.mimeType||'arquivo'}</small></div><div style={{display:'flex',gap:4}}><IconButton aria-label={`Baixar ${attachment.fileName}`} onClick={()=>downloadAttachment(attachment)}><Download size={14}/></IconButton>{realMode&&<IconButton $danger aria-label={`Remover ${attachment.fileName}`} disabled={removeAttachment.isPending} onClick={()=>{if(window.confirm(`Remover o anexo "${attachment.fileName}"?`))removeAttachment.mutate(attachment.id);}}><Trash2 size={14}/></IconButton>}</div></Row>)}{!attachmentsQuery.data?.length&&<Empty>Nenhum anexo.</Empty>}</List>{realMode&&<label style={{display:'inline-flex',marginTop:9}}><input type="file" hidden onChange={onUpload}/><Button as="span"><FileUp size={12}/>{upload.isPending?'Enviando...':'Adicionar anexo'}</Button></label>}</Section>}

      {activeTab==='description'&&<>
      <Section><h2><Link2 size={14}/>Dependências e bloqueios</h2><List>{details.links.map(current=><Row key={current.id}><div><strong>{current.isIncoming?'Recebido: ':''}{linkTypeNames[current.type]??'Relacionado'} · {current.relatedProjectKey&&current.relatedNumber?`${current.relatedProjectKey}-${current.relatedNumber} · `:''}{current.relatedTitle}</strong><small>{current.relatedWorkItemId}</small></div>{realMode&&<IconButton $danger aria-label="Remover relacionamento" onClick={()=>removeLink.mutate(current.id)}><Trash2 size={13}/></IconButton>}</Row>)}{details.links.length===0&&<Empty>Nenhuma dependência ou bloqueio.</Empty>}</List>{realMode&&details.projectId&&<InlineForm onSubmit={submitLink}><DependencyAutocomplete projectId={details.projectId} excludeWorkItemId={details.id} disabled={link.isPending} onSelect={selected=>setLinkForm({...linkForm,targetWorkItemId:selected?.id??''})}/><select value={linkForm.type} onChange={e=>setLinkForm({...linkForm,type:Number(e.target.value)})}>{Object.entries(linkTypeNames).map(([id,label])=><option key={id} value={id}>{label}</option>)}</select><Button disabled={!linkForm.targetWorkItemId||link.isPending}><Link2 size={12}/>Vincular</Button></InlineForm>}</Section>

      {details.customFields.length>0&&<Section><h2><FolderKanban size={14}/>Campos personalizados</h2><FormGrid>{details.customFields.map(field=><CustomFieldInput key={field.fieldId} field={field} value={customValues[field.fieldId]??''} users={usersQuery.data??[]} teams={projectQuery.data?.teams??[]} onChange={value=>setCustomValues(current=>({...current,[field.fieldId]:value}))}/>)}</FormGrid><div style={{display:'flex',justifyContent:'flex-end',marginTop:9}}><Button onClick={()=>customFields.mutate()} disabled={!realMode||customFields.isPending}><Save size={12}/>Salvar campos</Button></div></Section>}

      {realMode&&<Section><h2><Clock3 size={14}/>Apontamento de horas</h2><TaskTimePanel workItemId={details.id} realizedHours={details.realizedHours}/></Section>}

      {details.externalCommunication&&<Section><h2><MessageSquareText size={14}/>Resposta pública ao solicitante</h2><VisibilityNote>Esta conversa é visível no acompanhamento do protocolo {details.externalCommunication.protocol}. Comentários internos e apontamentos de horas nunca aparecem aqui.</VisibilityNote><PublicConversation>{details.externalCommunication.messages.map(message=><PublicMessage key={message.id} $requester={message.authorType===1}><strong>{message.authorName}</strong><p>{message.content}</p><small>{new Date(message.createdAt).toLocaleString('pt-BR')}</small></PublicMessage>)}{details.externalCommunication.messages.length===0&&<Empty>Nenhuma mensagem pública.</Empty>}</PublicConversation>{realMode&&<PublicReply onSubmit={event=>{event.preventDefault();if(publicReply.trim())publicResponse.mutate();}}><textarea maxLength={4000} required value={publicReply} onChange={event=>setPublicReply(event.target.value)} placeholder="Escreva uma resposta que ficará visível ao solicitante."/><Button disabled={!publicReply.trim()||publicResponse.isPending}><Send size={12}/>Enviar resposta pública</Button></PublicReply>}{publicResponse.error&&<ErrorBox>{(publicResponse.error as Error).message}</ErrorBox>}</Section>}

      <ActionBar><Button $secondary onClick={()=>realMode&&following.mutate()}>{details.isFollowing?<EyeOff size={13}/>:<Eye size={13}/>} {details.isFollowing?'Deixar de seguir':'Seguir tarefa'}</Button><Button $secondary onClick={()=>realMode&&duplicate.mutate()} disabled={duplicate.isPending}><Copy size={13}/>Duplicar</Button><Button $danger onClick={()=>realMode&&archive.mutate()} disabled={archive.isPending}>{details.isArchived?<RotateCcw size={13}/>:<Archive size={13}/>} {details.isArchived?'Reativar':'Arquivar'}</Button></ActionBar>
      {realMode&&details.projectId&&<TaskWikiPages projectId={details.projectId} workItemId={details.id} onNavigate={()=>onOpenChange(false)}/>}
      <Section><h2><Clock3 size={14}/>Auditoria</h2><Meta><MetaChip>Criada em {new Date(details.createdAt).toLocaleString('pt-BR')}</MetaChip><MetaChip>Atualizada em {new Date(details.updatedAt).toLocaleString('pt-BR')}</MetaChip><MetaChip>{details.commentsCount} comentário(s)</MetaChip><MetaChip>{details.attachmentsCount} anexo(s)</MetaChip></Meta></Section>
      </>}
      {activeTab==='comments'&&<Section><h2><MessageSquareText size={14}/>Comentários internos</h2><VisibilityNote>Visível somente para a equipe interna. Este conteúdo não é enviado nem exibido ao solicitante.</VisibilityNote>{realMode?<TaskFeed workItemId={details.id} mode="comments"/>:<Empty>Entre com dados reais para comentar.</Empty>}</Section>}
      {activeTab==='history'&&<Section><h2><Clock3 size={14}/>Histórico imutável</h2><VisibilityNote>Registro cronológico das alterações e movimentações desta tarefa.</VisibilityNote>{realMode?<TaskFeed workItemId={details.id} mode="history"/>:<Empty>Histórico disponível com dados reais.</Empty>}</Section>}
      {activeTab==='graph'&&<Section><h2><GitBranch size={14}/>Grafo de estados</h2><VisibilityNote>Caminho real percorrido pela tarefa entre etapas, com responsáveis e datas de cada transição.</VisibilityNote>{realMode?<TaskStateGraph workItemId={details.id} createdAt={details.createdAt} createdByName={details.requesterName || details.responsibleName}/>:<Empty>Grafo disponível com dados reais.</Empty>}</Section>}
    </Body>}
  </Sheet>}</Dialog.Portal></Dialog.Root>;
}

function TagsIcon(){return <Target size={14}/>;}

function CustomFieldInput({field,value,users,teams,onChange}:{field:WorkItemCustomField;value:string;users:AssignableUser[];teams:Array<{id:string;name:string}>;onChange:(value:string)=>void}){
  const options=parseOptions(field.optionsJson);
  if(field.type===4)return <Field>{field.name}{field.isRequired?' *':''}<select value={value} onChange={event=>onChange(event.target.value)}><option value="">Selecione...</option><option value="true">Sim</option><option value="false">Não</option></select></Field>;
  if(field.type===5)return <Field>{field.name}{field.isRequired?' *':''}<select value={value} onChange={event=>onChange(event.target.value)}><option value="">Selecione...</option>{options.map(option=><option key={option} value={option}>{option}</option>)}</select></Field>;
  if(field.type===6)return <Field>{field.name}{field.isRequired?' *':''}<select multiple value={parseSelected(value)} onChange={event=>onChange(JSON.stringify(Array.from(event.currentTarget.selectedOptions,option=>option.value)))}>{options.map(option=><option key={option} value={option}>{option}</option>)}</select></Field>;
  if(field.type===7)return <Field $wide>{field.name}{field.isRequired?' *':''}<textarea value={value} onChange={event=>onChange(event.target.value)}/></Field>;
  if(field.type===10)return <Field>{field.name}{field.isRequired?' *':''}<select value={value} onChange={event=>onChange(event.target.value)}><option value="">Selecione...</option>{users.map(user=><option key={user.id} value={user.id}>{userDisplayLabel(user)}</option>)}</select></Field>;
  if(field.type===11)return <Field>{field.name}{field.isRequired?' *':''}<select value={value} onChange={event=>onChange(event.target.value)}><option value="">Selecione...</option>{teams.map(team=><option key={team.id} value={team.id}>{team.name}</option>)}</select></Field>;
  const inputType=field.type===2||field.type===8?'number':field.type===3?'date':field.type===9?'datetime-local':field.type===12?'url':'text';
  return <Field>{field.name}{field.isRequired?' *':''}<input type={inputType} min={field.type===8?'0':undefined} max={field.type===8?'100':undefined} step={field.type===2||field.type===8?'any':undefined} value={field.type===9&&value?value.slice(0,16):value} onChange={event=>onChange(event.target.value)}/></Field>;
}
