export interface KanbanFilterState {
  search:string;assigneeId:string;teamId:string;priority:string;taskTypeId:string;
  tagId:string;due:string;origin:string;blocked:string;requester:string;customField:string;
}
export interface KanbanCardSettings {
  showPriority:boolean;showDueDate:boolean;showEstimate:boolean;showAssignees:boolean;
  showType:boolean;showTags:boolean;showPoints:boolean;showChecklist:boolean;
  showAttachments:boolean;showTime:boolean;
}
export interface FilterableWorkItem {
  id:string;title:string;subtitle?:string;description?:string;priority:number;origin?:number;
  responsibleId?:string|null;teamId?:string|null;taskTypeId?:string|null;dueDate?:string|null;
  requesterId?:string|null;requesterName?:string|null;requesterEmail?:string|null;isBlocked?:boolean;
  assignees?:{userId:string}[];tags?:{id:string;name:string}[];
  customFields?:{fieldId:string;value?:string|null}[];
}
export interface SavedFilterOption {id:string;name:string;filterJson:string;}

export const defaultKanbanFilters:KanbanFilterState={search:'',assigneeId:'',teamId:'',priority:'',taskTypeId:'',tagId:'',due:'',origin:'',blocked:'',requester:'',customField:''};
export const defaultCardSettings:KanbanCardSettings={showPriority:true,showDueDate:true,showEstimate:true,showAssignees:true,showType:true,showTags:true,showPoints:true,showChecklist:true,showAttachments:true,showTime:true};

const localDate=(date=new Date())=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
export function applyKanbanFilters<T extends FilterableWorkItem>(items:T[],filter:KanbanFilterState):T[]{
  const today=localDate();const week=new Date();week.setDate(week.getDate()+7);const weekEnd=localDate(week);const q=filter.search.trim().toLowerCase();
  return items.filter(item=>{
    if(q&&!`${item.title} ${item.subtitle??''} ${item.description??''}`.toLowerCase().includes(q))return false;
    if(filter.assigneeId&&item.responsibleId!==filter.assigneeId&&!item.assignees?.some(x=>x.userId===filter.assigneeId))return false;
    if(filter.teamId&&item.teamId!==filter.teamId)return false;
    if(filter.priority&&item.priority!==Number(filter.priority))return false;
    if(filter.taskTypeId&&item.taskTypeId!==filter.taskTypeId)return false;
    if(filter.tagId&&!item.tags?.some(x=>x.id===filter.tagId))return false;
    if(filter.origin&&item.origin!==Number(filter.origin))return false;
    if(filter.blocked==='yes'&&!item.isBlocked)return false;
    if(filter.blocked==='no'&&item.isBlocked)return false;
    if(filter.requester&&!`${item.requesterId??''} ${item.requesterName??''} ${item.requesterEmail??''}`.toLowerCase().includes(filter.requester.toLowerCase()))return false;
    if(filter.customField&&!item.customFields?.some(x=>`${x.value??''}`.toLowerCase().includes(filter.customField.toLowerCase())))return false;
    if(filter.due==='overdue'&&(!item.dueDate||item.dueDate>=today))return false;
    if(filter.due==='today'&&item.dueDate!==today)return false;
    if(filter.due==='week'&&(!item.dueDate||item.dueDate<today||item.dueDate>weekEnd))return false;
    if(filter.due==='none'&&item.dueDate)return false;
    return true;
  });
}
