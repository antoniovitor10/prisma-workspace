import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  Bookmark,
  Bug,
  Check,
  ChevronRight,
  CircleDot,
  Filter,
  Layers3,
  ListChecks,
  Search,
  Sparkles,
  Trash2,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import styled from 'styled-components';
import { TaskDetailDrawer } from '../components/TaskDetailDrawer';
import { previewBacklog, previewMode } from '../preview';
import { api } from '../services/api';
import type { BacklogItem } from '../types/scrum';
import { kindNames, priorityNames } from '../types/scrum';
import type { ProjectSummary } from './Projects';
import {
  defaultProjectQuery as defaults,
  definitionFromParams,
  definitionToParams,
  isProjectQuery,
  runProjectQuery,
  type GroupBy,
  type MatchMode,
  type ProjectQueryDefinition,
  type Segment,
  type SortBy,
} from './ProjectItemsQuery.logic';

interface SavedFilterOption { id: string; name: string; filterJson: string; }
const EMPTY_ITEMS: BacklogItem[] = [];

const segments: Array<{
  id: Segment;
  label: string;
  singular: string;
  description: string;
  icon: typeof Layers3;
}> = [
  { id: 'all', label: 'Todos', singular: 'item', description: 'Visão completa do projeto', icon: ListChecks },
  { id: '1', label: 'Épicos', singular: 'épico', description: 'Objetivos e iniciativas amplas', icon: Layers3 },
  { id: '2', label: 'Features', singular: 'feature', description: 'Capacidades que geram valor', icon: Sparkles },
  { id: '3', label: 'Product backlog', singular: 'item de backlog', description: 'Histórias prontas para priorização', icon: Bookmark },
  { id: '4', label: 'Bugs', singular: 'bug', description: 'Falhas e correções necessárias', icon: Bug },
  { id: '5', label: 'Tarefas', singular: 'tarefa', description: 'Trabalho executável da equipe', icon: Check },
];

const Page = styled.section`padding:20px 28px 42px;@media(max-width:760px){padding:16px;}`;
const Intro = styled.div`display:flex;align-items:flex-start;justify-content:space-between;gap:18px;margin-bottom:18px;h2{font-size:20px;letter-spacing:-.02em;}p{margin-top:5px;color:${({theme})=>theme.color.textMuted};font-size:13.5px;}`;
const SegmentGrid = styled.div`display:grid;grid-template-columns:repeat(6,minmax(132px,1fr));gap:9px;margin-bottom:14px;overflow-x:auto;padding-bottom:2px;@media(max-width:900px){grid-template-columns:repeat(6,150px);}`;
const SegmentCard = styled.button<{$active:boolean}>`
  position:relative;display:grid;gap:9px;min-height:112px;overflow:hidden;padding:14px 14px 14px 17px;cursor:pointer;text-align:left;border:1px solid ${({theme,$active})=>$active?theme.color.brand:theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme,$active})=>$active?`color-mix(in srgb, ${theme.color.brand} 7%, ${theme.color.surface})`:theme.color.surface};color:${({theme})=>theme.color.text};box-shadow:${({theme,$active})=>$active?theme.shadow.md:theme.shadow.sm};transition:border-color .16s,background .16s,box-shadow .16s,transform .16s;
  &::before{content:'';position:absolute;inset:8px auto 8px 4px;width:3px;border-radius:999px;background:${({theme})=>theme.color.gradient};opacity:${({$active})=>$active?1:.26};}
  &:hover{transform:translateY(-1px);border-color:${({theme,$active})=>$active?theme.color.brand:theme.color.borderStrong};}
  &:focus-visible{outline:2px solid color-mix(in srgb, ${({theme})=>theme.color.brand} 45%, transparent);outline-offset:2px;}
  span{display:flex;align-items:center;justify-content:space-between;color:${({theme,$active})=>$active?theme.color.brand:theme.color.textMuted};}b{font-size:22px;}strong{font-size:13px;}small{font-size:11px;line-height:1.35;color:${({theme})=>theme.color.textMuted};}
`;
const Composer = styled.section`padding:15px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};box-shadow:${({theme})=>theme.shadow.sm};`;
const ComposerTop = styled.div`display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:12px;strong{display:flex;align-items:center;gap:7px;font-size:13px;}span{color:${({theme})=>theme.color.textMuted};font-size:12px;}`;
const Fields = styled.div`display:grid;grid-template-columns:minmax(220px,1.5fr) repeat(5,minmax(120px,.72fr));gap:8px;@media(max-width:1180px){grid-template-columns:repeat(3,minmax(0,1fr));}@media(max-width:640px){grid-template-columns:1fr;}`;
const Field = styled.label`display:grid;gap:4px;color:${({theme})=>theme.color.textMuted};font-size:10.5px;font-weight:800;letter-spacing:.05em;text-transform:uppercase;input,select{width:100%;min-height:38px;padding:0 10px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surfaceSubtle};color:${({theme})=>theme.color.text};font:inherit;font-size:13px;font-weight:600;text-transform:none;letter-spacing:0;&:focus{outline:none;border-color:${({theme})=>theme.color.brand};}}`;
const SearchField = styled(Field)`position:relative;svg{position:absolute;left:10px;bottom:11px;color:${({theme})=>theme.color.textMuted};}input{padding-left:32px;}`;
const ComposerActions = styled.div`display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:10px;margin-top:12px;padding-top:12px;border-top:1px solid ${({theme})=>theme.color.border};`;
const QuerySummary = styled.div`display:flex;flex-wrap:wrap;align-items:center;gap:6px;color:${({theme})=>theme.color.textMuted};font-size:12px;span{padding:4px 7px;border-radius:${({theme})=>theme.radius.pill};background:${({theme})=>theme.color.surfaceSubtle};color:${({theme})=>theme.color.text};font-weight:700;}`;
const Buttons = styled.div`display:flex;flex-wrap:wrap;gap:7px;`;
const Button = styled.button<{$primary?:boolean}>`display:inline-flex;min-height:36px;align-items:center;justify-content:center;gap:6px;padding:0 12px;border:1px solid ${({theme,$primary})=>$primary?theme.color.brand:theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme,$primary})=>$primary?theme.color.gradient:theme.color.surface};color:${({theme,$primary})=>$primary?theme.color.onBrand:theme.color.text};font-size:12.5px;font-weight:800;&:disabled{opacity:.5;cursor:not-allowed;}`;
const SavedPanel = styled.div`display:flex;flex-wrap:wrap;align-items:center;gap:7px;margin:12px 0 14px;padding:10px 12px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.lg};background:${({theme})=>theme.color.surfaceSubtle};>strong{display:flex;align-items:center;gap:6px;margin-right:3px;font-size:12px;}input{min-height:34px;padding:0 9px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.md};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.text};}`;
const SavedChip = styled.div`display:flex;align-items:center;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.pill};background:${({theme})=>theme.color.surface};overflow:hidden;button{min-height:32px;padding:0 10px;color:${({theme})=>theme.color.text};font-size:12px;font-weight:700;}button:last-child{padding:0 8px;color:${({theme})=>theme.color.textMuted};border-left:1px solid ${({theme})=>theme.color.border};}`;
const ResultsHeader = styled.div`display:flex;align-items:center;justify-content:space-between;gap:12px;margin:18px 0 9px;h3{font-size:15px;}p{color:${({theme})=>theme.color.textMuted};font-size:12.5px;}`;
const Group = styled.section`margin-bottom:14px;border:1px solid ${({theme})=>theme.color.border};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};overflow:hidden;box-shadow:${({theme})=>theme.shadow.sm};`;
const GroupTitle = styled.h4`display:flex;align-items:center;gap:7px;padding:10px 13px;border-bottom:1px solid ${({theme})=>theme.color.border};background:${({theme})=>theme.color.surfaceSubtle};font-size:12px;text-transform:uppercase;letter-spacing:.05em;span{color:${({theme})=>theme.color.textMuted};}`;
const TableWrap = styled.div`overflow-x:auto;`;
const Table = styled.table`width:100%;min-width:850px;border-collapse:collapse;th{padding:9px 12px;text-align:left;color:${({theme})=>theme.color.textMuted};font-size:10px;text-transform:uppercase;letter-spacing:.06em;}td{padding:10px 12px;border-top:1px solid ${({theme})=>theme.color.border};font-size:12.5px;}tbody tr:hover{background:${({theme})=>theme.color.surfaceSubtle};}`;
const ItemButton = styled.button`display:flex;align-items:center;gap:8px;max-width:420px;color:${({theme})=>theme.color.text};text-align:left;font-weight:750;span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}svg{color:${({theme})=>theme.color.textMuted};flex:0 0 auto;}`;
const KindBadge = styled.span<{$kind:number}>`display:inline-flex;padding:3px 7px;border-radius:${({theme})=>theme.radius.sm};background:${({theme,$kind})=>$kind===4?`color-mix(in srgb, ${theme.color.danger} 12%, ${theme.color.surface})`:$kind<=2?`color-mix(in srgb, ${theme.color.brand} 11%, ${theme.color.surface})`:`color-mix(in srgb, ${theme.color.accentBlue} 10%, ${theme.color.surface})`};color:${({theme,$kind})=>$kind===4?theme.color.danger:$kind<=2?theme.color.brand:theme.color.accentBlue};font-size:10.5px;font-weight:800;text-transform:uppercase;`;
const Empty = styled.div`display:grid;place-items:center;gap:8px;min-height:220px;padding:30px;border:1px dashed ${({theme})=>theme.color.borderStrong};border-radius:${({theme})=>theme.radius.card};background:${({theme})=>theme.color.surface};color:${({theme})=>theme.color.textMuted};text-align:center;`;
const ErrorBanner = styled.div`display:flex;align-items:center;gap:7px;margin:12px 0;padding:10px 12px;border:1px solid ${({theme})=>theme.color.danger};border-radius:${({theme})=>theme.radius.md};color:${({theme})=>theme.color.danger};font-size:13px;`;

function formatDate(value?: string) {
  if (!value) return '—';
  return new Intl.DateTimeFormat('pt-BR', { timeZone: 'UTC' }).format(new Date(value));
}

function groupLabel(item: BacklogItem, groupBy: GroupBy) {
  if (groupBy === 'kind') return kindNames[item.kind] ?? 'Outro tipo';
  if (groupBy === 'priority') return priorityNames[item.priority] ?? 'Sem prioridade';
  if (groupBy === 'board') return item.boardName;
  if (groupBy === 'stage') return item.stageName ?? 'Sem etapa';
  return 'Resultados';
}

export function ProjectItemsQuery({ project }: { project: ProjectSummary }) {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const initial = definitionFromParams(searchParams);
  const [draft, setDraft] = useState<ProjectQueryDefinition>(initial);
  const [applied, setApplied] = useState<ProjectQueryDefinition>(initial);
  const [saving, setSaving] = useState(false);
  const [saveName, setSaveName] = useState('');
  const [selectedItem, setSelectedItem] = useState<BacklogItem | null>(null);
  const anchorBoardId = project.defaultBoardId ?? project.boards[0]?.id;

  useEffect(() => {
    const next = definitionFromParams(searchParams);
    setDraft(next);
    setApplied(next);
  }, [searchParams]);

  const backlogQuery = useQuery<BacklogItem[]>({
    queryKey: ['project-backlog', project.id],
    retry: false,
    queryFn: async () => {
      try { return await api.getProjectBacklog(project.id) as BacklogItem[]; }
      catch { if (previewMode) return previewBacklog; throw new Error('Não foi possível carregar os itens do projeto.'); }
    },
  });
  const savedQuery = useQuery<SavedFilterOption[]>({
    queryKey: ['project-item-queries', anchorBoardId], enabled: Boolean(anchorBoardId) && !previewMode,
    queryFn: async () => {
      const filters = await api.getSavedFilters(anchorBoardId!) as SavedFilterOption[];
      return filters.filter(filter => { try { return isProjectQuery(JSON.parse(filter.filterJson)); } catch { return false; } });
    },
  });
  const saveMutation = useMutation({
    mutationFn: () => api.createSavedFilter(anchorBoardId!, saveName.trim(), applied),
    onSuccess: async () => { setSaveName(''); setSaving(false); await queryClient.invalidateQueries({ queryKey: ['project-item-queries', anchorBoardId] }); },
  });
  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteSavedFilter(anchorBoardId!, id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['project-item-queries', anchorBoardId] }),
  });

  const items = backlogQuery.data ?? EMPTY_ITEMS;
  const counts = useMemo(() => new Map(segments.map(segment => [segment.id, segment.id === 'all' ? items.length : items.filter(item => item.kind === Number(segment.id)).length])), [items]);
  const boards = useMemo(() => Array.from(new Map(items.map(item => [item.boardId, item.boardName])).entries()), [items]);
  const stages = useMemo(() => Array.from(new Map(items.filter(item => item.stageId).map(item => [item.stageId!, item.stageName ?? 'Sem nome'])).entries()), [items]);
  const results = useMemo(() => runProjectQuery(items, applied), [items, applied]);
  const grouped = useMemo(() => {
    const groups = new Map<string, BacklogItem[]>();
    results.forEach(item => { const label = groupLabel(item, applied.groupBy); groups.set(label, [...(groups.get(label) ?? []), item]); });
    return groups;
  }, [results, applied.groupBy]);
  const activeCriteria = [applied.search && `Busca: ${applied.search}`, applied.priority !== 'all' && `Prioridade: ${priorityNames[Number(applied.priority)]}`, applied.boardId !== 'all' && `Quadro: ${boards.find(([id]) => id === applied.boardId)?.[1] ?? 'selecionado'}`, applied.stageId !== 'all' && `Etapa: ${stages.find(([id]) => id === applied.stageId)?.[1] ?? 'selecionada'}`].filter(Boolean) as string[];

  const apply = (next = draft) => {
    setApplied(next);
    setDraft(next);
    setSearchParams(definitionToParams(next, searchParams), { replace: false });
  };
  const selectSegment = (segment: Segment) => apply({ ...draft, segment });
  const loadSaved = (saved: SavedFilterOption) => {
    try {
      const parsed = JSON.parse(saved.filterJson);
      if (!isProjectQuery(parsed)) return;
      apply({ ...defaults, ...parsed });
    } catch { /* definições inválidas não são aplicadas */ }
  };

  return (
    <Page>
      <Intro><div><h2>Itens do projeto</h2><p>Explore o trabalho por tipo e componha consultas pessoais sem sair do contexto do projeto.</p></div></Intro>
      <SegmentGrid aria-label="Segmentos de itens">
        {segments.map(({ id, label, description, icon: Icon }) => <SegmentCard key={id} $active={draft.segment === id} aria-pressed={draft.segment === id} onClick={() => selectSegment(id)} data-testid={`item-segment-${id}`}><span><Icon size={17}/><b>{counts.get(id) ?? 0}</b></span><strong>{label}</strong><small>{description}</small></SegmentCard>)}
      </SegmentGrid>

      <Composer aria-label="Compositor de consulta">
        <ComposerTop><strong><Filter size={15}/>Compositor visual</strong><span>Os critérios são aplicados somente ao clicar em Aplicar.</span></ComposerTop>
        <Fields>
          <SearchField>Buscar<Search size={14}/><input value={draft.search} onChange={event=>setDraft({...draft,search:event.target.value})} placeholder="Título, número ou descrição"/></SearchField>
          <Field>Combinação<select value={draft.match} onChange={event=>setDraft({...draft,match:event.target.value as MatchMode})}><option value="all">Todos os critérios (E)</option><option value="any">Qualquer critério (OU)</option></select></Field>
          <Field>Prioridade<select value={draft.priority} onChange={event=>setDraft({...draft,priority:event.target.value})}><option value="all">Todas</option>{[0,1,2,3].map(value=><option key={value} value={value}>{priorityNames[value]}</option>)}</select></Field>
          <Field>Quadro<select value={draft.boardId} onChange={event=>setDraft({...draft,boardId:event.target.value,stageId:'all'})}><option value="all">Todos</option>{boards.map(([id,name])=><option key={id} value={id}>{name}</option>)}</select></Field>
          <Field>Etapa<select value={draft.stageId} onChange={event=>setDraft({...draft,stageId:event.target.value})}><option value="all">Todas</option>{stages.map(([id,name])=><option key={id} value={id}>{name}</option>)}</select></Field>
          <Field>Agrupar<select value={draft.groupBy} onChange={event=>setDraft({...draft,groupBy:event.target.value as GroupBy})}><option value="none">Sem agrupamento</option><option value="kind">Tipo</option><option value="priority">Prioridade</option><option value="board">Quadro</option><option value="stage">Etapa</option></select></Field>
          <Field>Ordenar<select value={draft.sortBy} onChange={event=>setDraft({...draft,sortBy:event.target.value as SortBy})}><option value="rank">Prioridade do backlog</option><option value="newest">Mais recentes</option><option value="title">Título</option><option value="due">Prazo</option></select></Field>
        </Fields>
        <ComposerActions>
          <QuerySummary><strong>Consulta:</strong><span>{segments.find(segment=>segment.id===applied.segment)?.label}</span>{activeCriteria.map(value=><span key={value}>{value}</span>)}{activeCriteria.length===0&&<em>sem critérios adicionais</em>}</QuerySummary>
          <Buttons><Button onClick={()=>apply(defaults)}><X size={13}/>Limpar</Button><Button $primary onClick={()=>apply()}><Sparkles size={13}/>Aplicar consulta</Button><Button disabled={!anchorBoardId} onClick={()=>setSaving(value=>!value)}><Bookmark size={13}/>Salvar</Button></Buttons>
        </ComposerActions>
      </Composer>

      <SavedPanel aria-label="Consultas pessoais salvas">
        <strong><Bookmark size={13}/>Minhas consultas</strong>
        {!anchorBoardId && <span>Crie um quadro no projeto para salvar consultas pessoais.</span>}
        {saving && <><input autoFocus aria-label="Nome da consulta" placeholder="Nome da consulta" value={saveName} onChange={event=>setSaveName(event.target.value)} onKeyDown={event=>{if(event.key==='Enter'&&saveName.trim())saveMutation.mutate();}}/><Button $primary disabled={!saveName.trim()||saveMutation.isPending} onClick={()=>saveMutation.mutate()}>Salvar agora</Button></>}
        {(savedQuery.data??[]).map(saved=><SavedChip key={saved.id}><button onClick={()=>loadSaved(saved)}>{saved.name}</button><button aria-label={`Excluir consulta ${saved.name}`} onClick={()=>deleteMutation.mutate(saved.id)}><Trash2 size={12}/></button></SavedChip>)}
        {anchorBoardId&&!savedQuery.isLoading&&(savedQuery.data?.length??0)===0&&!saving&&<span>Nenhuma consulta salva ainda.</span>}
      </SavedPanel>

      {backlogQuery.error && <ErrorBanner role="alert"><AlertCircle size={15}/>{(backlogQuery.error as Error).message}</ErrorBanner>}
      <ResultsHeader><div><h3>{results.length} resultado(s)</h3><p>Somente itens autorizados do projeto atual.</p></div></ResultsHeader>
      {backlogQuery.isLoading ? <Empty>Carregando itens do projeto...</Empty> : results.length===0 ? <Empty><Search size={20}/><strong>Nenhum item corresponde à consulta.</strong><span>Ajuste os critérios ou escolha outro segmento.</span></Empty> : Array.from(grouped.entries()).map(([label,groupItems])=><Group key={label}><GroupTitle><CircleDot size={12}/>{label}<span>{groupItems.length}</span></GroupTitle><TableWrap><Table><thead><tr><th>Item</th><th>Tipo</th><th>Etapa</th><th>Prioridade</th><th>Quadro</th><th>Esforço</th><th>Prazo</th></tr></thead><tbody>{groupItems.map(item=><tr key={item.id}><td><ItemButton onClick={()=>setSelectedItem(item)} aria-label={`Abrir detalhes de ${item.title}`}><span>{project.key}-{item.number ?? '—'} · {item.title}</span><ChevronRight size={13}/></ItemButton></td><td><KindBadge $kind={item.kind}>{item.kind===3?'Product backlog':kindNames[item.kind]??'Outro'}</KindBadge></td><td>{item.stageName??'Sem etapa'}</td><td>{priorityNames[item.priority]??'—'}</td><td>{item.boardName}</td><td>{item.kind<=3?(item.points!=null?`${item.points} pts`:'—'):(item.estimatedHours!=null?`${item.estimatedHours} h`:'—')}</td><td>{formatDate(item.dueDate)}</td></tr>)}</tbody></Table></TableWrap></Group>)}
      <TaskDetailDrawer item={selectedItem} projectKey={project.key} onOpenChange={open=>{if(!open)setSelectedItem(null);}} onItemUpdated={updated=>{setSelectedItem(updated);void queryClient.invalidateQueries({queryKey:['project-backlog',project.id]});}} onOpenSubtask={workItemId=>{void api.getWorkItemDetails(workItemId).then(details=>setSelectedItem(details as unknown as BacklogItem));}}/>
    </Page>
  );
}
