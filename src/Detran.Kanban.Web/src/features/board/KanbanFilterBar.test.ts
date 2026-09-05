import { describe, expect, it } from 'vitest';
import { applyKanbanFilters, defaultKanbanFilters, type FilterableWorkItem } from './KanbanFilters';

const items:FilterableWorkItem[]=[
  {id:'1',title:'Corrigir login',priority:2,origin:1,teamId:'team-a',responsibleId:'user-a',
    taskTypeId:'bug',isBlocked:true,requesterName:'Maria',assignees:[{userId:'user-a'}],
    tags:[{id:'urgent',name:'Urgente'}],customFields:[{fieldId:'area',value:'Segurança'}]},
  {id:'2',title:'Novo relatório',priority:1,origin:2,teamId:'team-b',responsibleId:'user-b',
    taskTypeId:'feature',isBlocked:false,requesterName:'João',assignees:[{userId:'user-b'}],
    tags:[{id:'finance',name:'Financeiro'}],customFields:[{fieldId:'area',value:'BI'}]}
];

describe('applyKanbanFilters',()=>{
  it('combina responsável, equipe, prioridade, tipo, tag e origem',()=>{
    const result=applyKanbanFilters(items,{...defaultKanbanFilters,
      assigneeId:'user-b',teamId:'team-b',priority:'1',taskTypeId:'feature',tagId:'finance',origin:'2'});
    expect(result.map(item=>item.id)).toEqual(['2']);
  });

  it('filtra bloqueio, solicitante e campo personalizado',()=>{
    const result=applyKanbanFilters(items,{...defaultKanbanFilters,
      blocked:'yes',requester:'maria',customField:'segurança'});
    expect(result.map(item=>item.id)).toEqual(['1']);
  });
});
