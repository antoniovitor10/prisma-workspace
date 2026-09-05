import { Pencil, Plus, Settings2, Trash2, X } from 'lucide-react';
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import styled from 'styled-components';
import { api } from '../../services/api';
import type { BulkOption } from './KanbanBulkToolbar';

const Button = styled.button<{ $primary?: boolean; $danger?: boolean }>`
  height:32px;display:inline-flex;align-items:center;justify-content:center;gap:5px;padding:0 10px;
  border:1px solid ${({theme,$primary,$danger})=>$primary||$danger?'transparent':theme.color.border};border-radius:7px;
  background:${({theme,$primary,$danger})=>$danger?'#fee4e2':$primary?theme.color.brand:theme.color.surface};
  color:${({theme,$primary,$danger})=>$danger?'#b42318':$primary?theme.color.onBrand:theme.color.textMuted};
  font-size:13px;font-weight:800;&:disabled{opacity:.5;cursor:not-allowed;}
`;
const Overlay = styled.div`
  position:fixed;inset:0;z-index:1000;display:grid;place-items:center;padding:20px;
  background:rgba(15,23,42,.48);
`;
const Dialog = styled.section`
  width:min(760px,100%);max-height:min(760px,90vh);overflow:auto;padding:18px;
  border-radius:14px;background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.lg};
`;
const Header = styled.header`
  display:flex;align-items:flex-start;justify-content:space-between;gap:12px;margin-bottom:15px;
  h2{margin:0;color:${({theme})=>theme.color.text};font-size:18px;}
  p{margin:4px 0 0;color:${({theme})=>theme.color.textMuted};font-size:13.5px;}
`;
const Form = styled.form`
  display:grid;grid-template-columns:1.1fr 1fr 1.2fr auto;align-items:end;gap:9px;padding:12px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:10px;background:#f8fafc;
  @media(max-width:720px){grid-template-columns:1fr;}
`;
const Field = styled.label`
  display:grid;gap:5px;font-size:13px;font-weight:800;color:${({theme})=>theme.color.textMuted};
  select{height:34px;padding:0 8px;border:1px solid ${({theme})=>theme.color.border};border-radius:7px;background:white;color:${({theme})=>theme.color.text};font-size:13.5px;}
`;
const Active = styled.label`
  display:flex;align-items:center;gap:6px;height:34px;font-size:13px;font-weight:800;color:${({theme})=>theme.color.textMuted};
  input{width:15px;height:15px;accent-color:${({theme})=>theme.color.brand};}
`;
const List = styled.div`display:grid;gap:8px;margin-top:14px;`;
const Rule = styled.article`
  display:grid;grid-template-columns:1fr auto;align-items:center;gap:10px;padding:10px 12px;
  border:1px solid ${({theme})=>theme.color.border};border-radius:9px;
  strong{display:block;color:${({theme})=>theme.color.text};font-size:13.5px;}
  span{color:${({theme})=>theme.color.textMuted};font-size:13px;}
`;
const Actions = styled.div`display:flex;align-items:center;gap:5px;`;
const Feedback = styled.p<{ $error?: boolean }>`font-size:13.5px;color:${({$error})=>$error?'#b42318':'#067647'};`;

interface AutomationRule {
  id: string;
  triggerStageId: string;
  triggerStageName: string;
  actionType: number;
  actionValue: string;
  isActive: boolean;
}

const ACTION = { assign: 1, move: 2, priority: 3, addTag: 4 } as const;
const priorityOptions: BulkOption[] = [
  {id:'Low',label:'Baixa'}, {id:'Medium',label:'Média'},
  {id:'High',label:'Alta'}, {id:'Critical',label:'Crítica'}
];

export function AutomationManager({
  boardId, stages, users, tags
}: {
  boardId: string;
  stages: BulkOption[];
  users: BulkOption[];
  tags: BulkOption[];
}) {
  const [open, setOpen] = useState(false);
  const [rules, setRules] = useState<AutomationRule[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [triggerStageId, setTriggerStageId] = useState('');
  const [actionType, setActionType] = useState<number>(ACTION.move);
  const [actionValue, setActionValue] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<{message:string;error?:boolean}|null>(null);

  const reset = useCallback(() => {
    setEditingId(null);
    setTriggerStageId(stages[0]?.id ?? '');
    setActionType(ACTION.move);
    setActionValue('');
    setIsActive(true);
  }, [stages]);

  const load = useCallback(async () => {
    setBusy(true);
    setFeedback(null);
    try { setRules(await api.getAutomations(boardId)); }
    catch(error){ setFeedback({message:(error as Error).message,error:true}); }
    finally { setBusy(false); }
  }, [boardId]);

  useEffect(() => {
    if (!open) return;
    reset();
    void load();
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false); };
    window.addEventListener('keydown', close);
    return () => window.removeEventListener('keydown', close);
  }, [open, load, reset]);

  const options = actionType === ACTION.assign
    ? users
    : actionType === ACTION.move
      ? stages.filter(stage=>stage.id!==triggerStageId)
      : actionType === ACTION.priority
        ? priorityOptions
        : tags;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!triggerStageId || !actionValue) return;
    setBusy(true);setFeedback(null);
    const data={triggerStageId,actionType,actionValue,isActive};
    try {
      if(editingId) await api.updateAutomation(boardId,editingId,data);
      else await api.createAutomation(boardId,data);
      setFeedback({message:editingId?'Automação atualizada.':'Automação criada.'});
      reset();await load();
    } catch(error){setFeedback({message:(error as Error).message,error:true});}
    finally{setBusy(false);}
  };

  const edit = (rule: AutomationRule) => {
    setEditingId(rule.id);setTriggerStageId(rule.triggerStageId);
    setActionType(rule.actionType);setActionValue(rule.actionValue);setIsActive(rule.isActive);
  };

  const toggle = async (rule: AutomationRule) => {
    setBusy(true);setFeedback(null);
    try {
      await api.updateAutomation(boardId,rule.id,{...rule,isActive:!rule.isActive});
      await load();
    } catch(error){setFeedback({message:(error as Error).message,error:true});}
    finally{setBusy(false);}
  };

  const remove = async (rule: AutomationRule) => {
    if(!window.confirm('Excluir esta automação?'))return;
    setBusy(true);setFeedback(null);
    try{await api.deleteAutomation(boardId,rule.id);await load();}
    catch(error){setFeedback({message:(error as Error).message,error:true});}
    finally{setBusy(false);}
  };

  const targetLabel=(rule:AutomationRule)=>{
    const source=rule.actionType===ACTION.assign?users:rule.actionType===ACTION.move?stages:rule.actionType===ACTION.priority?priorityOptions:tags;
    return source.find(option=>option.id.toLowerCase()===rule.actionValue.toLowerCase())?.label??rule.actionValue;
  };
  const actionLabel=(type:number)=>({1:'atribuir a',2:'mover para',3:'definir prioridade',4:'adicionar tag'}[type]??'executar');

  return <>
    <Button type="button" onClick={()=>setOpen(true)}><Settings2 size={14}/>Automações</Button>
    {open&&<Overlay onMouseDown={event=>{if(event.target===event.currentTarget)setOpen(false);}}>
      <Dialog role="dialog" aria-modal="true" aria-labelledby="automation-title">
        <Header><div><h2 id="automation-title">Automações do quadro</h2><p>Execute até cinco ações seguras quando uma tarefa entrar em uma etapa.</p></div><Button aria-label="Fechar" onClick={()=>setOpen(false)}><X size={15}/></Button></Header>
        <Form onSubmit={submit}>
          <Field>Quando entrar em<select autoFocus required value={triggerStageId} onChange={e=>{setTriggerStageId(e.target.value);if(actionType===ACTION.move&&actionValue===e.target.value)setActionValue('');}}><option value="">Selecione</option>{stages.map(stage=><option key={stage.id} value={stage.id}>{stage.label}</option>)}</select></Field>
          <Field>Ação<select value={actionType} onChange={e=>{setActionType(Number(e.target.value));setActionValue('');}}><option value={ACTION.move}>Mover para etapa</option><option value={ACTION.assign}>Atribuir usuário</option><option value={ACTION.priority}>Definir prioridade</option><option value={ACTION.addTag}>Adicionar tag</option></select></Field>
          <Field>Destino<select required value={actionValue} onChange={e=>setActionValue(e.target.value)}><option value="">Selecione</option>{options.map(option=><option key={option.id} value={option.id}>{option.label}</option>)}</select></Field>
          <div><Active><input type="checkbox" checked={isActive} onChange={e=>setIsActive(e.target.checked)}/>Ativa</Active><Button $primary type="submit" disabled={busy||!triggerStageId||!actionValue}><Plus size={13}/>{editingId?'Salvar':'Criar'}</Button>{editingId&&<Button type="button" onClick={reset}>Cancelar</Button>}</div>
        </Form>
        {feedback&&<Feedback role="status" aria-live="polite" $error={feedback.error}>{feedback.message}</Feedback>}
        <List aria-label="Regras configuradas">
          {rules.map(rule=><Rule key={rule.id}><div><strong>{rule.triggerStageName} → {actionLabel(rule.actionType)} {targetLabel(rule)}</strong><span>{rule.isActive?'Ativa':'Pausada'}</span></div><Actions><Button type="button" onClick={()=>toggle(rule)} disabled={busy}>{rule.isActive?'Pausar':'Ativar'}</Button><Button type="button" aria-label="Editar automação" onClick={()=>edit(rule)}><Pencil size={13}/></Button><Button $danger type="button" aria-label="Excluir automação" onClick={()=>remove(rule)}><Trash2 size={13}/></Button></Actions></Rule>)}
          {!busy&&rules.length===0&&<Rule><div><strong>Nenhuma automação configurada</strong><span>Crie a primeira regra acima.</span></div></Rule>}
        </List>
      </Dialog>
    </Overlay>}
  </>;
}
