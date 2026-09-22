import { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router-dom';
import { useTheme } from 'styled-components';
import { api } from '../services/api';
import { Fragment, useMemo } from 'react';
import { BoardCalendar } from '../features/board/BoardCalendar';
import { BoardGantt } from '../features/board/BoardGantt';
import { BoardDashboard } from '../features/board/BoardDashboard';
import { TaskDetailDrawer } from '../components/TaskDetailDrawer';
import { WorkItemKind } from '../features/workItems/workItemKinds';
import { WorkItemKindSelector } from '../components/WorkItemKindSelector';
import type { BacklogItem } from '../types/scrum';
import { userDisplayLabel } from '../utils/userDisplayName';
import { KanbanFilterBar } from '../features/board/KanbanFilterBar';
import { compareKanbanWorkItems, compareNewestWorkItems, type KanbanCardSort } from '../features/board/kanbanOrdering';
import { AutomationManager } from '../features/board/AutomationManager';
import { KanbanBulkToolbar } from '../features/board/KanbanBulkToolbar';
import {
  applyKanbanFilters, defaultCardSettings, defaultKanbanFilters,
  type KanbanCardSettings, type KanbanFilterState, type SavedFilterOption
} from '../features/board/KanbanFilters';
import { useBoardRealtime } from '../features/board/useBoardRealtime';
import {
  Plus,
  Square,
  ChevronLeft,
  ChevronRight,
  Clock,
  FolderPlus,
  AlertTriangle,
  Users,
  X,
  Paperclip,
  CheckSquare,
  BarChart2,
  Trash2,
  Pencil,
  ArrowRight as ArrowRightIcon
} from 'lucide-react';
import {
  AppLayout,
  MainContent,
  BoardHeader,
  BoardHelp,
  SelectorContainer,
  BoardActions,
  ViewSwitcher,
  ViewSwitcherButton,
  Select,
  ActionButton,
  AddCardButton,
  KanbanGrid,
  Column,
  ColumnHeader,
  ColumnTitle,
  CardCount,
  CardList,
  Card,
  CardTitle,
  CardSubtitle,
  CardEstimate,
  DragPreview,
  DragPreviewTitle,
  DragPreviewSubtitle,
  CardMeta,
  Tag,
  CardActions,
  ActionIcon,
  ModalOverlay,
  Modal,
  ModalTitle,
  DetailText,
  SmallButton,
  ModalForm,
  FormRow,
  Input,
  Textarea,
  ModalActions,
  SubmitButton,
  CancelButton,
  TimerWrapper,
  PlayIcon,
  LeadTimeTable,
  LeadTimeTh,
  LeadTimeTd,
  TempoGrid,
  TempoDay,
  TempoDayLabel,
  TempoBarTrack,
  TempoBarFill,
  TempoEmptyHours,
  TempoOverBadge,
  TempoPct,
  TempoAdjust,
  TempoTodayBadge,
} from './Kanban.styles';
import { StageCategory, stageCategoryOptions, type StageCategoryValue } from '../features/workflow/stageCategories';





















































interface Board {
  id: string;
  name: string;
  projectId?: string | null;
  teamId?: string | null;
  cardSettingsJson?: string | null;
}

interface Stage {
  id: string;
  boardId: string;
  name: string;
  position: number;
  category?: StageCategoryValue;
  workflowStatusId?: string | null;
  statusName?: string | null;
  statusColor?: string | null;
  isInitial?: boolean;
  isFinal?: boolean;
}

interface WorkItem {
  id: string;
  number?: number;
  boardId: string;
  stageId: string | null;
  parentId?: string | null;
  title: string;
  subtitle?: string;
  description?: string;
  priority: number;
  origin?: number;
  responsibleId?: string | null;
  teamId?: string | null;
  teamName?: string | null;
  requesterId?: string | null;
  requesterName?: string | null;
  requesterEmail?: string | null;
  estimatedHours?: number;
  dueDate?: string;
  position: number;
  assignees?: WorkItemAssignee[];
  subItemsCount?: number;
  attachmentsCount?: number;
  createdAt: string;
  totalTimeSeconds?: number;
  userTimeSeconds?: number;
  taskTypeId?: string | null;
  taskTypeName?: string | null;
  taskTypeColor?: string | null;
  points?: number | null;
  tags?: TagChipDto[];
  checklistTotal?: number;
  checklistDone?: number;
  isBlocked?: boolean;
  customFields?: { fieldId: string; value?: string | null }[];
}

interface TagChipDto {
  id: string;
  name: string;
  color: string;
}

interface WorkItemAssignee {
  userId: string;
  assignedAt: string;
}

interface UserDto {
  id: string;
  displayName?: string;
  email?: string;
  userName?: string;
}

interface CatalogTagDto {
  id: string;
  name: string;
  color: string;
}

interface SprintOptionDto {
  id: string;
  name: string;
  status: number;
}



interface TimeEntry {
  id: string;
  workItemId: string;
  userId: string;
  startedAt: string;
  endedAt?: string | null;
  durationSeconds?: number;
}


interface DailyTime {
  date: string;
  totalSeconds: number;
  totalHours: number;
}

interface WeeklyTime {
  weekStart: string;
  days: DailyTime[];
}

interface StageLeadTime {
  stageId: string;
  stageName: string;
  averageSeconds: number;
  averageHours: number;
  averageDays: number;
  itemsCount: number;
}

export const Kanban: React.FC = () => {
  /**
   * Duas entradas para a mesma tela:
   *   /boards/:boardId            — um quadro específico
   *   /projects/:projectId/boards — o Kanban do projeto, que abre direto
   *
   * No modo projeto ninguém escolhe quadro antes: o fluxo é do projeto (D83) e os cartões
   * vêm do projeto inteiro, então nenhuma tarefa fica escondida por estar em outro quadro.
   * O quadro continua sendo resolvido em segundo plano porque criar tarefa, salvar filtro
   * e as configurações de cartão ainda são por quadro.
   */
  const { boardId: urlBoardId, projectId: urlProjectId } = useParams();
  const modoProjeto = Boolean(urlProjectId);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const theme = useTheme();
  const [boards, setBoards] = useState<Board[]>([]);
  const [selectedBoardId, setSelectedBoardId] = useState<string>(urlBoardId ?? '');
  const [boardsStatus, setBoardsStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [boardsError, setBoardsError] = useState('');
  // O seletor mostra só os quadros do mesmo projeto do quadro atual (não os da org toda).
  const visibleBoards = useMemo(() => {
    const current = boards.find(b => b.id === selectedBoardId);
    return current?.projectId ? boards.filter(b => b.projectId === current.projectId) : boards;
  }, [boards, selectedBoardId]);
  const [stages, setStages] = useState<Stage[]>([]);
  const [workItems, setWorkItems] = useState<WorkItem[]>([]);
  const [boardDataStatus, setBoardDataStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle');
  const [boardDataError, setBoardDataError] = useState('');
  const [boardView, setBoardView] = useState<'kanban' | 'lista' | 'calendario' | 'gantt' | 'dashboard'>('kanban');
  const [listSort, setListSort] = useState<{ key: string; dir: 1 | -1 }>({ key: 'title', dir: 1 });
  const [filters, setFilters] = useState<KanbanFilterState>(
    searchParams.get('assignee')
      ? { ...defaultKanbanFilters, assigneeId: searchParams.get('assignee')! }
      : defaultKanbanFilters,
  );
  const [groupBy, setGroupBy] = useState('none');
  const [cardSort, setCardSort] = useState<KanbanCardSort>('position');
  const [columnSorts, setColumnSorts] = useState<Record<string, KanbanCardSort>>({});
  const [savedFilters, setSavedFilters] = useState<SavedFilterOption[]>([]);
  const [cardSettings, setCardSettings] = useState<KanbanCardSettings>(defaultCardSettings);
  const [projectMethodology, setProjectMethodology] = useState<number | null>(null);
  const [projectKey, setProjectKey] = useState('ITEM');
  const showStoryPoints = projectMethodology !== null && projectMethodology !== 1;
  const [selectedItemIds, setSelectedItemIds] = useState<Set<string>>(new Set());
  const [catalogTags, setCatalogTags] = useState<CatalogTagDto[]>([]);
  const [boardSprints, setBoardSprints] = useState<SprintOptionDto[]>([]);
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkFeedback, setBulkFeedback] = useState<{message:string;error?:boolean}|null>(null);

  // Modais
  const [showBoardModal, setShowBoardModal] = useState(false);
  const [showStageModal, setShowStageModal] = useState(false);
  const [showEditStageModal, setShowEditStageModal] = useState(false);
  const [editingStage, setEditingStage] = useState<Stage | null>(null);
  const [deleteStageDestination, setDeleteStageDestination] = useState('');
  const [confirmCategoryChange, setConfirmCategoryChange] = useState(false);
  const [confirmDescendants, setConfirmDescendants] = useState(false);
  const [stageImpact, setStageImpact] = useState<Awaited<ReturnType<typeof api.getStageImpact>> | null>(null);
  const [editStageName, setEditStageName] = useState('');
  const [editStageCategory, setEditStageCategory] = useState<StageCategoryValue>(StageCategory.InProgress);
  const [editStagePending, setEditStagePending] = useState(false);
  const [editStageError, setEditStageError] = useState('');
  const [showItemModal, setShowItemModal] = useState(false);
  const [showLeadTimeModal, setShowLeadTimeModal] = useState(false);
  const [showTempoModal, setShowTempoModal] = useState(false);
  const [weeklyTime, setWeeklyTime] = useState<WeeklyTime | null>(null);
  const [weekStart, setWeekStart] = useState<string | undefined>(undefined);
  const [loadingWeekly, setLoadingWeekly] = useState(false);
  const [adjustDay, setAdjustDay] = useState<string | null>(null);
  const [adjustTaskId, setAdjustTaskId] = useState('');
  const [adjustMinutes, setAdjustMinutes] = useState<number | undefined>(undefined);
  const [adjustTasks, setAdjustTasks] = useState<{ id: string; title: string; boardName: string }[]>([]);
  const [adjustTab, setAdjustTab] = useState<'tarefas' | 'justificar'>('tarefas');
  const [dailyByTask, setDailyByTask] = useState<{ workItemId: string; title: string; seconds: number }[]>([]);
  const [justifications, setJustifications] = useState<{ id: string; reason: string; hours: number }[]>([]);
  const [justReason, setJustReason] = useState('');
  const [justHours, setJustHours] = useState<number | undefined>(undefined);

  const loadAdjustData = useCallback(async (date: string) => {
    try {
      const [daily, just] = await Promise.all([api.getDailyByTask(date), api.getJustifications(date)]);
      setDailyByTask(daily);
      setJustifications(just);
    } catch { /* silencioso */ }
  }, []);

  const submitJustification = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!adjustDay || !justReason.trim() || !justHours) return;
    try {
      await api.addJustification(adjustDay, justReason.trim(), justHours);
      setJustReason('');
      setJustHours(undefined);
      await loadAdjustData(adjustDay);
    } catch (e) {
      console.error(e);
      alert('Erro ao justificar o dia.');
    }
  };

  const removeJustification = async (id: string) => {
    if (!adjustDay) return;
    await api.deleteJustification(id);
    await loadAdjustData(adjustDay);
  };
  const [selectedItem, setSelectedItem] = useState<WorkItem | null>(null);
  const [targetStageIdForNewItem, setTargetStageIdForNewItem] = useState<string | null>(null);
  const [draggedItemId, setDraggedItemId] = useState<string | null>(null);
  const [dropTargetStageId, setDropTargetStageId] = useState<string | null>(null);
  const [assignableUsers, setAssignableUsers] = useState<UserDto[]>([]);
  const [leadTimeData, setLeadTimeData] = useState<StageLeadTime[]>([]);
  const [loadingLeadTime, setLoadingLeadTime] = useState(false);
  const [dragPreview, setDragPreview] = useState<{
    title: string;
    subtitle?: string;
    priority: number;
    x: number;
    y: number;
  } | null>(null);
  const activeDragRef = useRef<{
    item: WorkItem;
    pointerId: number;
    startX: number;
    startY: number;
    isDragging: boolean;
  } | null>(null);
  const suppressNextCardClickRef = useRef(false);
  const boardDataRequestRef = useRef(0);
  const loadedBoardContextRef = useRef<string | null>(null);

  // TASK-026: estado do modal de criar quadro
  const [createBoardPending, setCreateBoardPending] = useState(false);
  const [createBoardError, setCreateBoardError] = useState('');
  const [createBoardSuccessId, setCreateBoardSuccessId] = useState<string | null>(null);

  // TASK-028: estado do modal de excluir quadro
  const [showDeleteBoardModal, setShowDeleteBoardModal] = useState(false);
  const [deleteBoardPending, setDeleteBoardPending] = useState(false);
  const [deleteBoardError, setDeleteBoardError] = useState('');
  const [deleteBoardDestId, setDeleteBoardDestId] = useState('');

  // Form values
  const [newBoardName, setNewBoardName] = useState('');
  const [copyBoardId, setCopyBoardId] = useState('');
  const [deleteBoardStageId, setDeleteBoardStageId] = useState('');
  const [destinationStages, setDestinationStages] = useState<Stage[]>([]);
  useEffect(() => {
    setDeleteBoardStageId(''); setDestinationStages([]);
    let active = true;
    if (deleteBoardDestId) api.getBoardStages(deleteBoardDestId).then(data => { if (active) setDestinationStages(data); }).catch(() => { if (active) setDeleteBoardError('Não foi possível carregar as colunas de destino.'); });
    return () => { active = false; };
  }, [deleteBoardDestId]);
  // Equipe do quadro: existia na tela que apenas listava quadros e foi trazida para cá
  // junto com a criação, para a capacidade não se perder ao remover aquela tela.
  const [newBoardTeamId, setNewBoardTeamId] = useState('');
  const [projectTeams, setProjectTeams] = useState<Array<{ id: string; name: string }>>([]);
  const [newStageName, setNewStageName] = useState('');
  // Sem escolha explícita, toda coluna nascia como "em andamento" — inclusive uma
  // chamada "Concluído" — e as tarefas nela nunca eram contadas como concluídas.
  const [newStageCategory, setNewStageCategory] = useState<StageCategoryValue>(StageCategory.InProgress);
  const [newItemTitle, setNewItemTitle] = useState('');
  const [newItemSubtitle, setNewItemSubtitle] = useState('');
  const [newItemDesc, setNewItemDesc] = useState('');
  const [newItemPriority, setNewItemPriority] = useState<number>(0);
  const [newItemHours, setNewItemHours] = useState<number | undefined>(undefined);
  const [newItemKind, setNewItemKind] = useState<number>(WorkItemKind.Task);
  const [newItemResponsibleId, setNewItemResponsibleId] = useState('');
  const [newItemParticipantIds, setNewItemParticipantIds] = useState<string[]>([]);

  // Timer persistido em TimeEntry.
  const [runningItemId, setRunningItemId] = useState<string | null>(null);
  const [runningStartedAt, setRunningStartedAt] = useState<string | null>(null);
  const [activeTime, setActiveTime] = useState<number>(0);

  const fetchBoards = useCallback(async () => {
    setBoardsStatus('loading');
    setBoardsError('');
    try {
      const data = await api.getBoards();
      setBoards(data);
      setBoardsStatus('ready');
      if (modoProjeto) {
        // Escolhe sozinho um quadro do projeto, em vez de pedir que a pessoa escolha.
        const doProjeto = (data as Board[]).filter((b) => b.projectId === urlProjectId);
        const aindaVale = selectedBoardId && doProjeto.some((b) => b.id === selectedBoardId);
        if (!aindaVale) setSelectedBoardId(doProjeto[0]?.id ?? '');
        return;
      }
      if (data.length > 0) {
        const stillValid = selectedBoardId && data.some((b: Board) => b.id === selectedBoardId);
        if (!stillValid) {
          // O quadro da URL manda SEMPRE (mesmo que a listagem não o traga por
          // filtro ou falha momentânea) — nunca abrir silenciosamente outro quadro.
          setSelectedBoardId(urlBoardId || data[0].id);
        }
      }
    } catch (e) {
      setBoards([]);
      setSelectedBoardId('');
      setStages([]);
      setWorkItems([]);
      setSelectedItemIds(new Set());
      setBoardsStatus('error');
      setBoardsError('Não foi possível carregar os quadros disponíveis para seu acesso. Tente novamente.');
    }
  }, [selectedBoardId, modoProjeto, urlProjectId]);

  const loadBoardData = useCallback(async (boardId: string) => {
    if (!boardId) return;
    const contextKey = `board:${boardId}`;
    const contextChanged = loadedBoardContextRef.current !== contextKey;
    const requestId = ++boardDataRequestRef.current;
    if (contextChanged) {
      setBoardDataStatus('loading');
      setBoardDataError('');
      setStages([]);
      setWorkItems([]);
      setSelectedItemIds(new Set());
    }
    try {
      const board = boards.find(b => b.id === boardId);
      const projectId = urlProjectId ?? board?.projectId;
      if (!projectId) {
        if (boardsStatus === 'loading') { setBoardDataStatus('idle'); return; }
        throw new Error('O quadro selecionado não está disponível para seu acesso.');
      }
      const [stageData, itemData] = await Promise.all([
        api.getBoardStages(boardId),
        api.getWorkItems(boardId)
      ]);
      if (requestId !== boardDataRequestRef.current) return;
      setStages(stageData);
      setWorkItems(itemData);
      const currentIds = new Set((itemData as WorkItem[]).map(item => item.id));
      if (contextChanged) setSelectedItemIds(new Set());
      else setSelectedItemIds(current => new Set([...current].filter(id => currentIds.has(id))));
      loadedBoardContextRef.current = contextKey;
      setBoardDataStatus('ready');
    } catch (e) {
      if (requestId !== boardDataRequestRef.current) return;
      setStages([]);
      setWorkItems([]);
      setSelectedItemIds(new Set());
      setBoardDataStatus('error');
      setBoardDataError((e as Error).message || 'Não foi possível carregar este quadro. Tente novamente.');
    }
  }, [boards, boardsStatus, modoProjeto, urlProjectId]);

  const refreshRealtimeBoard = useCallback(() => {
    if (selectedBoardId || modoProjeto) loadBoardData(selectedBoardId);
  }, [selectedBoardId, modoProjeto, loadBoardData]);
  useBoardRealtime(selectedBoardId, refreshRealtimeBoard);

  useEffect(() => {
    if (!selectedBoardId) return;
    api.getSavedFilters(selectedBoardId).then(filters => {
      setSavedFilters((filters as SavedFilterOption[]).filter((filter: SavedFilterOption) => {
        try { return JSON.parse(filter.filterJson)?.scope !== 'project-items-v1'; }
        catch { return true; }
      }));
    }).catch(() => setSavedFilters([]));
    const board = boards.find(item => item.id === selectedBoardId);
    if (board?.cardSettingsJson) {
      try { setCardSettings({ ...defaultCardSettings, ...JSON.parse(board.cardSettingsJson) }); }
      catch { setCardSettings(defaultCardSettings); }
    } else setCardSettings(defaultCardSettings);
  }, [selectedBoardId, boards]);

  useEffect(() => {
    setSelectedItemIds(new Set());
    setBulkFeedback(null);
    if (!selectedBoardId && !modoProjeto) {
      setCatalogTags([]);
      setBoardSprints([]);
      return;
    }
    api.getTags().then(setCatalogTags).catch(() => setCatalogTags([]));
    const projectId = urlProjectId ?? boards.find(board => board.id === selectedBoardId)?.projectId;
    if (!projectId) {
      setBoardSprints([]);
      setProjectMethodology(null);
      return;
    }
    api.getProject(projectId)
      .then((project: {methodology?:number;key?:string;teams?:Array<{id:string;name:string}>}) => {
        setProjectMethodology(project.methodology ?? 1);
        setProjectKey(project.key ?? 'ITEM');
        setProjectTeams(project.teams ?? []);
      })
      .catch(() => { setProjectMethodology(null); setProjectKey('ITEM'); setProjectTeams([]); });
    api.getProjectSprints(projectId)
      .then((items: SprintOptionDto[]) => setBoardSprints(items.filter(item => item.status === 1 || item.status === 2)))
      .catch(() => setBoardSprints([]));
    api.getAssignableUsers(projectId).then(setAssignableUsers).catch(() => setAssignableUsers([]));
  }, [selectedBoardId, boards, modoProjeto, urlProjectId]);

  const loadLeadTime = useCallback(async () => {
    if (!selectedBoardId) return;
    setLoadingLeadTime(true);
    try {
      const data = await api.getBoardLeadTime(selectedBoardId);
      setLeadTimeData(data);
    } catch (e) {
      console.error(e);
      alert('Erro ao carregar métricas de tempo.');
    } finally {
      setLoadingLeadTime(false);
    }
  }, [selectedBoardId]);

  const loadWeekly = useCallback(async (ws?: string) => {
    setLoadingWeekly(true);
    try {
      const data = await api.getMyWeeklyTime(ws) as WeeklyTime;
      setWeeklyTime(data);
      setWeekStart(data?.weekStart);
    } catch (e) {
      console.error(e);
      alert('Erro ao carregar seu tempo semanal.');
    } finally {
      setLoadingWeekly(false);
    }
  }, []);

  const shiftWeek = (deltaDays: number) => {
    if (!weekStart) return;
    const d = new Date(`${weekStart}T12:00:00`);
    d.setDate(d.getDate() + deltaDays);
    loadWeekly(d.toISOString().slice(0, 10));
  };

  const localDateStr = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  const todayStr = () => localDateStr(new Date());
  const formatDayLabel = (dateStr: string) => {
    const d = new Date(`${dateStr}T12:00:00`);
    const wd = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'][d.getDay()];
    return `${wd} ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
  };
  const formatWeekLabel = (ws: string) => {
    const d = new Date(`${ws}T12:00:00`);
    return `Semana de ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
  };
  const fmtHm = (secs: number) => {
    const total = Math.max(0, Math.round(secs));
    const h = Math.floor(total / 3600);
    const m = Math.round((total % 3600) / 60);
    return `${h}h${String(m).padStart(2, '0')}`;
  };

  const loadAllTasks = useCallback(async () => {
    try {
      const bs = await api.getBoards() as { id: string; name: string }[];
      const lists = await Promise.all(
        bs.map(b => api.getWorkItems(b.id).then((items: WorkItem[]) =>
          items.map(it => ({ id: it.id, title: it.title, boardName: b.name }))
        ))
      );
      setAdjustTasks(lists.flat());
    } catch (e) {
      console.error(e);
    }
  }, []);

  const submitAdjust = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!adjustDay || !adjustTaskId || !adjustMinutes) return;
    try {
      const start = new Date(`${adjustDay}T09:00:00-03:00`);
      const end = new Date(start.getTime() + adjustMinutes * 60000);
      await api.createManualTimeEntry({
        workItemId: adjustTaskId,
        startedAt: start.toISOString(),
        endedAt: end.toISOString(),
        note: 'Ajuste manual'
      });
      setAdjustMinutes(undefined);
      setAdjustTaskId('');
      setAdjustDay(null);
      await loadWeekly(weeklyTime?.weekStart);
    } catch (e) {
      console.error(e);
      alert('Erro ao ajustar o tempo.');
    }
  };

  const loadRunningTimer = useCallback(async () => {
    try {
      const running = await api.getRunningTimeEntry() as TimeEntry | null;
      if (!running) {
        setRunningItemId(null);
        setRunningStartedAt(null);
        setActiveTime(0);
        return;
      }

      setRunningItemId(running.workItemId);
      setRunningStartedAt(running.startedAt);
    } catch (e) {
      console.error(e);
    }
  }, []);

  useEffect(() => {
    fetchBoards();
    loadRunningTimer();
  }, [fetchBoards, loadRunningTimer]);

  useEffect(() => {
    if (selectedBoardId || modoProjeto) {
      loadBoardData(selectedBoardId);
    }
  }, [selectedBoardId, modoProjeto, loadBoardData]);

  // Contagem visual baseada no timer persistido.
  useEffect(() => {
    let interval: any;
    if (runningItemId && runningStartedAt) {
      const updateActiveTime = () => {
        const startedAt = new Date(runningStartedAt).getTime();
        setActiveTime(Math.max(0, Math.floor((Date.now() - startedAt) / 1000)));
      };

      updateActiveTime();
      interval = setInterval(updateActiveTime, 1000);
    } else {
      setActiveTime(0);
    }
    return () => clearInterval(interval);
  }, [runningItemId, runningStartedAt]);

  const handleCreateBoard = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newBoardName.trim() || createBoardPending) return;
    const currentProjectId = boards.find(b => b.id === selectedBoardId)?.projectId ?? undefined;
    setCreateBoardPending(true);
    setCreateBoardError('');
    try {
      const result = await api.createBoard(
        newBoardName.trim(), currentProjectId ?? undefined, newBoardTeamId || undefined, copyBoardId || undefined);
      const newBoardId: string = typeof result === 'string' ? result : (result as { id?: string })?.id ?? String(result);
      setCreateBoardSuccessId(newBoardId);
      await fetchBoards();
    } catch (err) {
      setCreateBoardError((err as Error).message || 'Erro ao criar quadro.');
    } finally {
      setCreateBoardPending(false);
    }
  };

  const handleDeleteBoard = async () => {
    if (!selectedBoardId || deleteBoardPending) return;
    setDeleteBoardPending(true);
    setDeleteBoardError('');
    try {
      await api.deleteBoard(selectedBoardId, deleteBoardDestId || undefined, deleteBoardStageId || undefined);
      setShowDeleteBoardModal(false);
      setDeleteBoardDestId('');
      await fetchBoards();
      if (deleteBoardDestId) {
        setSelectedBoardId(deleteBoardDestId);
      }
    } catch (err) {
      setDeleteBoardError((err as Error).message || 'Erro ao excluir quadro.');
    } finally {
      setDeleteBoardPending(false);
    }
  };

  const handleMoveStage = async (stageId: string, direction: 'left' | 'right') => {
    const idx = stages.findIndex(s => s.id === stageId);
    if (idx === -1) return;
    const newIdx = direction === 'left' ? idx - 1 : idx + 1;
    if (newIdx < 0 || newIdx >= stages.length) return;

    const newStages = [...stages];
    [newStages[idx], newStages[newIdx]] = [newStages[newIdx], newStages[idx]];
    setStages(newStages);

    try {
      const projectId = boards.find(b => b.id === selectedBoardId)?.projectId;
      if (!projectId) throw new Error('Projeto do quadro não encontrado.');
      await api.reorderStages(selectedBoardId, newStages.map(s => s.id));
    } catch (err) {
      setStages(stages);
      alert((err as Error).message || 'Erro ao reordenar colunas.');
    }
  };

  const handleCreateStage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newStageName.trim() || !selectedBoardId) return;
    try {
      const projectId = boards.find(b => b.id === selectedBoardId)?.projectId;
      if (!projectId) throw new Error('Projeto do quadro não encontrado.');
      const nextPos = stages.length > 0 ? Math.max(...stages.map(s => s.position)) + 100 : 100;
      await api.createStage(projectId, newStageName, nextPos, { category: newStageCategory, boardId: selectedBoardId });
      setNewStageName('');
      setNewStageCategory(StageCategory.InProgress);
      setShowStageModal(false);
      await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao criar coluna');
    }
  };

  useEffect(() => {
    let active = true;
    setStageImpact(null);setConfirmCategoryChange(false);setConfirmDescendants(false);
    if (showEditStageModal && editingStage && editingStage.category !== editStageCategory) {
      api.getStageImpact(editingStage.id, editStageCategory).then(impact => {
        if (active) setStageImpact(impact);
      }).catch(error => { if (active) setEditStageError((error as Error).message || 'Erro ao carregar impacto'); });
    }
    return () => { active = false; };
  }, [showEditStageModal, editingStage, editStageCategory]);

  const handleOpenEditStage = (stage: Stage) => {
    setEditingStage(stage);
    setConfirmCategoryChange(false);
    setDeleteStageDestination('');
    setEditStageName(stage.name);
    setEditStageCategory(stage.category ?? StageCategory.InProgress);
    setEditStageError('');
    setShowEditStageModal(true);
  };

  const handleUpdateStage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingStage || !editStageName.trim()) return;
    if (editingStage.category !== editStageCategory
      && (!stageImpact || (stageImpact.totalItems > 0 && !confirmCategoryChange)
        || (stageImpact.openDescendants > 0 && !confirmDescendants))) {
      setEditStageError('Revise o impacto e confirme explicitamente as alterações.');return;
    }
    setEditStagePending(true);
    setEditStageError('');
    try {
      await api.updateStage(editingStage.id, {
        name: editStageName.trim(),
        category: editStageCategory,
        confirmCategoryChange,
        confirmDescendants,
        impactToken: stageImpact?.snapshotToken,
      });
      setShowEditStageModal(false);
      setEditingStage(null);
      await loadBoardData(selectedBoardId);
    } catch (err: unknown) {
      console.error(err);
      setEditStageError((err as Error)?.message || 'Erro ao atualizar coluna');
      setConfirmCategoryChange(false);setConfirmDescendants(false);
      if (editingStage.category !== editStageCategory) {
        try { setStageImpact(await api.getStageImpact(editingStage.id, editStageCategory)); }
        catch { setStageImpact(null); }
      }
    } finally {
      setEditStagePending(false);
    }
  };

  const handleCreateWorkItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newItemTitle.trim() || !selectedBoardId) return;
    try {
      const columnItems = workItems.filter(w => w.stageId === targetStageIdForNewItem);
      const nextPos = columnItems.length > 0
        ? Math.max(0, Math.min(...columnItems.map(c => c.position)) - 100)
        : 100;

      await api.createWorkItem({
        boardId: selectedBoardId,
        stageId: targetStageIdForNewItem || undefined,
        title: newItemTitle,
        subtitle: newItemSubtitle || undefined,
        description: newItemDesc || undefined,
        priority: newItemPriority,
        estimatedHours: newItemHours,
        position: nextPos,
        kind: newItemKind,
        responsibleId: newItemResponsibleId || undefined,
        participantIds: newItemParticipantIds.filter(id => id !== newItemResponsibleId),
      });

      // Reset
      setNewItemTitle('');
      setNewItemSubtitle('');
      setNewItemDesc('');
      setNewItemPriority(0);
      setNewItemHours(undefined);
      setNewItemKind(WorkItemKind.Task);
      setNewItemResponsibleId('');
      setNewItemParticipantIds([]);
      setShowItemModal(false);
      await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao criar cartão');
    }
  };

  const handleMoveItem = async (item: WorkItem, direction: 'left' | 'right') => {
    if (!selectedBoardId) return;
    const currentIdx = stages.findIndex(s => s.id === item.stageId);
    if (currentIdx === -1) return;

    let targetStage: Stage | null = null;
    if (direction === 'left' && currentIdx > 0) {
      targetStage = stages[currentIdx - 1];
    } else if (direction === 'right' && currentIdx < stages.length - 1) {
      targetStage = stages[currentIdx + 1];
    }

    if (targetStage) {
      const targetItems = workItems.filter(w => w.stageId === targetStage?.id);
      const nextPos = targetItems.length > 0 ? Math.max(...targetItems.map(c => c.position)) + 100 : 100;

      try {
        await api.moveWorkItem(item.id, targetStage.id, nextPos);
        await loadBoardData(selectedBoardId);
      } catch (err) {
        alert((err as Error).message || 'Erro ao mover cartão');
      }
    }
  };

  const moveItemToStage = async (item: WorkItem, destinationStageId: string) => {
    if (!selectedBoardId || item.stageId === destinationStageId) return;

    const targetItems = workItems.filter(w => w.stageId === destinationStageId && w.id !== item.id);
    const nextPos = targetItems.length > 0 ? Math.max(...targetItems.map(c => c.position)) + 100 : 100;

    try {
      await api.moveWorkItem(item.id, destinationStageId, nextPos);
      await loadBoardData(selectedBoardId);
    } catch (err) {
      alert((err as Error).message || 'Erro ao mover cartão');
    }
  };

  const getStageIdAtPoint = (clientX: number, clientY: number) => {
    const element = document.elementFromPoint(clientX, clientY);
    return element?.closest<HTMLElement>('[data-stage-id]')?.dataset.stageId || null;
  };

  const clearPointerDrag = () => {
    activeDragRef.current = null;
    setDraggedItemId(null);
    setDropTargetStageId(null);
    setDragPreview(null);
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  };

  const handleCardPointerDown = (event: React.PointerEvent<HTMLDivElement>, item: WorkItem) => {
    if (event.button !== 0) return;

    const target = event.target as HTMLElement;
    if (target.closest('button, a, input, select, textarea, label')) return;

    activeDragRef.current = {
      item,
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      isDragging: false
    };

    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const handleCardPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    const drag = activeDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;

    const distanceX = event.clientX - drag.startX;
    const distanceY = event.clientY - drag.startY;
    const movedFarEnough = Math.hypot(distanceX, distanceY) >= 8;

    if (!drag.isDragging && movedFarEnough) {
      drag.isDragging = true;
      setDraggedItemId(drag.item.id);
      document.body.style.cursor = 'grabbing';
      document.body.style.userSelect = 'none';
    }

    if (!drag.isDragging) return;

    event.preventDefault();
    setDragPreview({
      title: drag.item.title,
      subtitle: drag.item.subtitle,
      priority: drag.item.priority,
      x: event.clientX,
      y: event.clientY
    });
    setDropTargetStageId(getStageIdAtPoint(event.clientX, event.clientY));
  };

  const handleCardPointerUp = async (event: React.PointerEvent<HTMLDivElement>) => {
    const drag = activeDragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) return;

    if (!drag.isDragging) {
      clearPointerDrag();
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    suppressNextCardClickRef.current = true;
    const targetStageId = getStageIdAtPoint(event.clientX, event.clientY);
    const item = drag.item;
    clearPointerDrag();

    if (targetStageId) {
      await moveItemToStage(item, targetStageId);
    }
  };

  const handleCardPointerCancel = () => {
    clearPointerDrag();
  };

  const handleCardClick = (item: WorkItem) => {
    if (suppressNextCardClickRef.current) {
      suppressNextCardClickRef.current = false;
      return;
    }

    setSelectedItem(item);
  };

  const toggleTimer = async (itemId: string) => {
    try {
      if (runningItemId === itemId) {
        await api.stopTimer(itemId);
        setRunningItemId(null);
        setRunningStartedAt(null);
        setActiveTime(0);
        window.dispatchEvent(new Event('timer-change'));

        if (selectedBoardId) {
          await loadBoardData(selectedBoardId);
        }
        return;
      }

      const started = await api.startTimer(itemId) as TimeEntry;
      setRunningItemId(started.workItemId);
      setRunningStartedAt(started.startedAt);
      window.dispatchEvent(new Event('timer-change'));

      if (selectedBoardId) {
        await loadBoardData(selectedBoardId);
      }
    } catch {
      alert('Erro ao atualizar timer');
    }
  };

  const formatTime = (secs: number) => {
    const h = Math.floor(secs / 3600);
    const m = Math.floor((secs % 3600) / 60);
    const s = secs % 60;
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  const getUserLabel = (user: UserDto) => userDisplayLabel(user);

  const getCardAssigneeLabel = (item: WorkItem) => {
    const userIds = [
      ...(item.responsibleId ? [item.responsibleId] : []),
      ...(item.assignees?.map(assignee => assignee.userId) ?? [])
    ].filter((userId, index, values) => values.indexOf(userId) === index);

    if (userIds.length === 0) return 'Sem responsável';

    const labels = userIds.map(userId => {
      const user = assignableUsers.find(candidate => candidate.id === userId);
      return user ? getUserLabel(user) : userId;
    });

    if (labels.length <= 2) return labels.join(', ');
    return `${labels.slice(0, 2).join(', ')} +${labels.length - 2}`;
  };

  const getGroupLabel = useCallback((item: WorkItem) => {
    if (groupBy === 'priority') return item.priority === 3 ? 'Crítica' : item.priority === 2 ? 'Alta' : item.priority === 1 ? 'Média' : 'Baixa';
    if (groupBy === 'type') return item.taskTypeName || 'Sem tipo';
    if (groupBy === 'team') return item.teamName || 'Sem equipe';
    if (groupBy === 'origin') return ({1:'Criação interna',2:'Portal externo',3:'Formulário',4:'Integração',5:'Importação'} as Record<number,string>)[item.origin || 1] || 'Outra origem';
    if (groupBy === 'responsible') {
      const id = item.responsibleId || item.assignees?.[0]?.userId;
      const user = assignableUsers.find(candidate => candidate.id === id);
      return user ? getUserLabel(user) : 'Sem responsável';
    }
    return '';
  }, [groupBy, assignableUsers]);

  const visibleWorkItems = useMemo(() => {
    const result = applyKanbanFilters(workItems, filters);
    return [...result].sort((a, b) => {
      if (groupBy !== 'none') {
        const grouped = getGroupLabel(a).localeCompare(getGroupLabel(b), 'pt-BR');
        if (grouped) return grouped;
      }
      if (cardSort === 'priority') return b.priority - a.priority;
      if (cardSort === 'due') return (a.dueDate || '9999-12-31').localeCompare(b.dueDate || '9999-12-31');
      if (cardSort === 'title') return a.title.localeCompare(b.title, 'pt-BR');
      if (cardSort === 'created') return compareNewestWorkItems(a, b);
      return a.position - b.position;
    });
  }, [workItems, filters, groupBy, cardSort, getGroupLabel]);

  const stageOptions = useMemo(
    () => stages.map(stage => ({ id: stage.id, label: stage.name })),
    [stages]);
  const userOptions = useMemo(
    () => assignableUsers.map(user => ({ id: user.id, label: getUserLabel(user) })),
    [assignableUsers]);
  const tagOptions = useMemo(
    () => catalogTags.map(tag => ({ id: tag.id, label: tag.name })),
    [catalogTags]);
  const sprintOptions = useMemo(
    () => boardSprints.map(sprint => ({ id: sprint.id, label: sprint.name })),
    [boardSprints]);
  const selectableVisibleIds = useMemo(
    () => visibleWorkItems.slice(0, 200).map(item => item.id),
    [visibleWorkItems]);
  const allVisibleSelected = selectableVisibleIds.length > 0
    && selectableVisibleIds.every(id => selectedItemIds.has(id));

  const toggleItemSelection = (itemId: string) => {
    setBulkFeedback(null);
    if (!selectedItemIds.has(itemId) && selectedItemIds.size >= 200) {
      setBulkFeedback({message:'O limite é de 200 tarefas por ação.',error:true});
      return;
    }
    setSelectedItemIds(current => {
      const next = new Set(current);
      if (next.has(itemId)) next.delete(itemId);
      else if (next.size < 200) next.add(itemId);
      return next;
    });
  };

  const toggleVisibleSelection = () => {
    setBulkFeedback(null);
    if (!allVisibleSelected && visibleWorkItems.length > selectableVisibleIds.length)
      setBulkFeedback({message:'Foram selecionadas as primeiras 200 tarefas visíveis.'});
    setSelectedItemIds(current => {
      const next = new Set(current);
      if (allVisibleSelected) selectableVisibleIds.forEach(id => next.delete(id));
      else {
        for (const id of selectableVisibleIds) {
          if (next.size >= 200) break;
          next.add(id);
        }
      }
      return next;
    });
  };

  const applyBulkAction = async (action: number, targetValue?: string, priority?: number) => {
    if (!selectedBoardId || selectedItemIds.size === 0) return;
    const count = selectedItemIds.size;
    setBulkBusy(true);
    setBulkFeedback({message:'Aplicando alterações…'});
    try {
      await api.bulkWorkItems(selectedBoardId, {
        workItemIds: [...selectedItemIds], action, targetValue, priority
      });
      setSelectedItemIds(new Set());
      setBulkFeedback({message:`${count} tarefa(s) atualizada(s) com sucesso.`});
      await loadBoardData(selectedBoardId);
    } catch (error) {
      setBulkFeedback({message:(error as Error).message,error:true});
    } finally {
      setBulkBusy(false);
    }
  };

  const applySavedFilter = (saved: SavedFilterOption) => {
    try {
      const parsed = JSON.parse(saved.filterJson);
      setFilters({ ...defaultKanbanFilters, ...(parsed.filters || parsed) });
      if (parsed.groupBy) setGroupBy(parsed.groupBy);
      if (parsed.sortBy) setCardSort(parsed.sortBy);
    } catch { alert('O filtro salvo está inválido.'); }
  };

  const saveCurrentFilter = async () => {
    if (!selectedBoardId) return;
    const name = window.prompt('Nome do filtro:')?.trim();
    if (!name) return;
    try {
      await api.createSavedFilter(selectedBoardId, name, { filters, groupBy, sortBy: cardSort });
      setSavedFilters(await api.getSavedFilters(selectedBoardId));
    } catch (error) { alert((error as Error).message); }
  };

  const saveCardView = async () => {
    const board = boards.find(item => item.id === selectedBoardId);
    if (!board?.projectId) return alert('Este quadro ainda não está vinculado a um projeto.');
    try {
      await api.updateBoardCardSettings(board.projectId, board.id, cardSettings);
      setBoards(current => current.map(item => item.id === board.id
        ? { ...item, cardSettingsJson: JSON.stringify(cardSettings) } : item));
    } catch (error) { alert((error as Error).message); }
  };

  const canRenderBoard = Boolean(selectedBoardId) && boardDataStatus === 'ready';

  return (
    <AppLayout>
      <MainContent>
        <BoardHeader>
          <SelectorContainer>
            <Select
              aria-label="Selecionar quadro"
              value={selectedBoardId}
              onChange={e => setSelectedBoardId(e.target.value)}
              disabled={boardsStatus !== 'ready' || boards.length === 0}
            >
              {boardsStatus === 'loading' ? (
                <option>Carregando quadros...</option>
              ) : boardsStatus === 'error' ? (
                <option>Quadros indisponíveis</option>
              ) : visibleBoards.length === 0 ? (
                <option>Nenhum quadro disponível</option>
              ) : (
                visibleBoards.map(b => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))
              )}
            </Select>

            <ActionButton onClick={() => {
              setShowBoardModal(true);
              setNewBoardName('');
              setCreateBoardError('');
              setCreateBoardSuccessId(null);
            }}>
              <FolderPlus size={16} />
              <span>Novo Quadro</span>
            </ActionButton>

            {canRenderBoard && visibleBoards.length > 1 && selectedBoardId && (
              <ActionButton
                onClick={() => {
                  const others = visibleBoards.filter(b => b.id !== selectedBoardId);
                  setDeleteBoardDestId(others[0]?.id ?? '');
                  setDeleteBoardError('');
                  setShowDeleteBoardModal(true);
                }}
                style={{ color: '#D92D20', borderColor: '#FECACA' }}
              >
                <Trash2 size={16} />
                <span>Excluir Quadro</span>
              </ActionButton>
            )}

            <ActionButton onClick={() => { setShowTempoModal(true); loadWeekly(); }}>
              <Clock size={16} />
              <span>Meu Tempo</span>
            </ActionButton>
          </SelectorContainer>

          {canRenderBoard && (
            <BoardActions>
              <ViewSwitcher role="group" aria-label="Visão do quadro">
                {([['kanban', 'Kanban'], ['lista', 'Lista'], ['calendario', 'Calendário'], ['gantt', 'Gantt'], ['dashboard', 'Dashboard']] as const).map(([key, label]) => (
                  <ViewSwitcherButton
                    key={key}
                    type="button"
                    $active={boardView === key}
                    aria-pressed={boardView === key}
                    onClick={() => setBoardView(key)}
                  >{label}</ViewSwitcherButton>
                ))}
              </ViewSwitcher>
              <ActionButton onClick={() => {
                loadLeadTime();
                setShowLeadTimeModal(true);
              }}>
                <BarChart2 size={16} />
                <span>Lead Time</span>
              </ActionButton>
              <AutomationManager
                boardId={selectedBoardId}
                stages={stageOptions}
                users={userOptions}
                tags={tagOptions}
              />
              <ActionButton onClick={() => setShowStageModal(true)}>
                <Plus size={16} />
                <span>Nova Coluna</span>
              </ActionButton>
            </BoardActions>
          )}
        </BoardHeader>
        {canRenderBoard && boardView === 'kanban' && (
          <BoardHelp>Arraste um cartão para outra coluna. Use o seletor no cabeçalho de cada coluna para ordenar somente aquela coluna; a ordem manual é a padrão compartilhada.</BoardHelp>
        )}

        {canRenderBoard && boardView !== 'dashboard' && (
          <KanbanFilterBar
            items={workItems}
            resultCount={visibleWorkItems.length}
            users={assignableUsers.map(user => ({ id: user.id, label: getUserLabel(user) }))}
            filter={filters}
            onFilter={setFilters}
            groupBy={groupBy}
            onGroupBy={setGroupBy}
            sortBy={cardSort}
            onSortBy={value => {
              setCardSort(value as KanbanCardSort);
              setColumnSorts({});
            }}
            savedFilters={savedFilters}
            onApplySaved={applySavedFilter}
            onSaveFilter={saveCurrentFilter}
            onReset={() => { setFilters(defaultKanbanFilters); setGroupBy('none'); setCardSort('position'); setColumnSorts({}); }}
            cardSettings={cardSettings}
            onCardSettings={setCardSettings}
            onSaveCardSettings={saveCardView}
            showPoints={showStoryPoints}
          />
        )}

        {canRenderBoard && (boardView === 'kanban' || boardView === 'lista') && (
          <KanbanBulkToolbar
            selectedCount={selectedItemIds.size}
            visibleCount={visibleWorkItems.length}
            allVisibleSelected={allVisibleSelected}
            stages={stageOptions}
            users={userOptions}
            tags={tagOptions}
            sprints={sprintOptions}
            busy={bulkBusy}
            feedback={bulkFeedback}
            onToggleVisible={toggleVisibleSelection}
            onClear={() => { setSelectedItemIds(new Set()); setBulkFeedback(null); }}
            onApply={applyBulkAction}
          />
        )}

        {boardsStatus === 'loading' ? (
          <div role="status" aria-live="polite" style={{ padding: '48px 16px', textAlign: 'center', color: theme.color.textMuted }}>Carregando quadros disponíveis...</div>
        ) : boardsStatus === 'error' ? (
          <div role="alert" style={{ padding: '16px', border: `1px solid ${theme.color.danger}`, borderRadius: 8, background: theme.color.surface, color: theme.color.text }}>
            <p>{boardsError}</p>
            <button type="button" onClick={() => void fetchBoards()}>Tentar carregar os quadros novamente</button>
          </div>
        ) : boardDataStatus === 'loading' ? (
          <div role="status" aria-live="polite" style={{ padding: '48px 16px', textAlign: 'center', color: theme.color.textMuted }}>Carregando quadro...</div>
        ) : boardDataStatus === 'error' ? (
          <div role="alert" style={{ padding: '16px', border: `1px solid ${theme.color.danger}`, borderRadius: 8, background: theme.color.surface, color: theme.color.text }}>
            <p>{boardDataError}</p>
            <button type="button" onClick={() => void loadBoardData(selectedBoardId)}>Tentar carregar o quadro novamente</button>
          </div>
        ) : canRenderBoard && boardView === 'calendario' ? (
          <BoardCalendar
            items={visibleWorkItems}
            onOpen={id => { const it = visibleWorkItems.find(w => w.id === id); if (it) handleCardClick(it); }}
          />
        ) : canRenderBoard && boardView === 'gantt' ? (
          <BoardGantt
            items={visibleWorkItems}
            stages={stages}
            onOpen={id => { const it = visibleWorkItems.find(w => w.id === id); if (it) handleCardClick(it); }}
          />
        ) : canRenderBoard && boardView === 'dashboard' ? (
          <BoardDashboard boardId={selectedBoardId} />
        ) : canRenderBoard && boardView === 'lista' ? (
          (() => {
            const stageName = (id: string | null) => stages.find(s => s.id === id)?.name || '—';
            const sorted = [...visibleWorkItems].sort((a, b) => {
              const k = listSort.key;
              let va: string | number = '', vb: string | number = '';
              if (k === 'title') { va = a.title.toLowerCase(); vb = b.title.toLowerCase(); }
              else if (k === 'assignee') { va = getCardAssigneeLabel(a); vb = getCardAssigneeLabel(b); }
              else if (k === 'stage') { va = stageName(a.stageId); vb = stageName(b.stageId); }
              else if (k === 'due') { va = a.dueDate || '9999'; vb = b.dueDate || '9999'; }
              else if (k === 'time') { va = a.totalTimeSeconds || 0; vb = b.totalTimeSeconds || 0; }
              return va < vb ? -listSort.dir : va > vb ? listSort.dir : 0;
            });
            const th = (key: string, label: string) => (
              <th
                onClick={() => setListSort(s => ({ key, dir: s.key === key ? (s.dir === 1 ? -1 : 1) : 1 }))}
                style={{ textAlign: 'left', padding: '10px 12px', fontSize: 14, color: theme.color.textMutedAccessible, fontWeight: 700, cursor: 'pointer', userSelect: 'none', borderBottom: '2px solid #E2E8F0', whiteSpace: 'nowrap' }}
              >{label}{listSort.key === key ? (listSort.dir === 1 ? ' ▲' : ' ▼') : ''}</th>
            );
            return (
              <div style={{ overflowX: 'auto', background: '#fff', borderRadius: 10, border: '1px solid #E2E8F0' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <thead>
                    <tr>
                      <th style={{width:42,padding:'10px 12px',borderBottom:'2px solid #E2E8F0'}}>
                        <span aria-hidden="true">✓</span>
                      </th>
                      {th('title', 'Tarefa')}{th('assignee', 'Responsável')}{th('stage', 'Etapa')}{th('due', 'Entrega')}{th('time', 'Tempo')}
                    </tr>
                  </thead>
                  <tbody>
                    {sorted.map(item => {
                      const late = !!item.dueDate && item.dueDate < todayStr();
                      return (
                        <tr key={item.id} onClick={() => handleCardClick(item)} style={{ cursor: 'pointer', background: selectedItemIds.has(item.id) ? '#E8F3FF' : late ? '#FDEEEE' : undefined, borderBottom: '1px solid #EDF2F7' }}>
                          <td style={{padding:'10px 12px'}}>
                            <input
                              type="checkbox"
                              checked={selectedItemIds.has(item.id)}
                              aria-label={`Selecionar #${item.number ?? ''} ${item.title}`}
                              onClick={event=>event.stopPropagation()}
                              onChange={()=>toggleItemSelection(item.id)}
                              style={{width:15,height:15,accentColor:'#1E7BD7'}}
                            />
                          </td>
                          <td style={{ padding: '10px 12px', fontSize: 15, fontWeight: 600, color: '#1D2D3E' }}>
                            {item.taskTypeName && <span style={{ fontSize: 13, fontWeight: 700, padding: '1px 6px', borderRadius: 3, color: '#fff', background: item.taskTypeColor || '#1E7BD7', marginRight: 8 }}>{item.taskTypeName}</span>}
                            {item.title}
                          </td>
                          <td style={{ padding: '10px 12px', fontSize: 15, color: '#64748B' }}>{getCardAssigneeLabel(item)}</td>
                          <td style={{ padding: '10px 12px', fontSize: 15, color: '#64748B' }}>{stageName(item.stageId)}</td>
                          <td style={{ padding: '10px 12px', fontSize: 15, color: late ? '#D92D20' : '#64748B', fontWeight: late ? 700 : 400 }}>{item.dueDate ? `${item.dueDate.slice(8,10)}/${item.dueDate.slice(5,7)}` : '—'}</td>
                          <td style={{ padding: '10px 12px', fontSize: 15, color: '#64748B' }}>{formatTime(item.totalTimeSeconds || 0)}</td>
                        </tr>
                      );
                    })}
                    {sorted.length === 0 && (
                      <tr><td colSpan={6} style={{ padding: '30px', textAlign: 'center', color: '#94A3B8' }}>Nenhuma tarefa neste quadro.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            );
          })()
        ) : canRenderBoard ? (
          <KanbanGrid>
            {stages.map(stage => {
              const columnSort = columnSorts[stage.id] || cardSort;
              const itemsInStage = visibleWorkItems
                .filter(w => w.stageId === stage.id)
                .sort((a, b) => compareKanbanWorkItems(a, b, columnSort));
              return (
                <Column
                  key={stage.id}
                  data-stage-id={stage.id}
                  $isDropTarget={dropTargetStageId === stage.id}
                  $isDraggingAny={draggedItemId !== null}
                >
                  <ColumnHeader>
                    <ColumnTitle as="h2"><span style={{ color: stage.statusColor || '#64748B', marginRight: 6 }}>●</span>{stage.name}</ColumnTitle>
                      <select
                        aria-label={`Ordenar cartões da coluna ${stage.name}`}
                        value={columnSort}
                        onChange={event => setColumnSorts(current => ({ ...current, [stage.id]: event.target.value as KanbanCardSort }))}
                        style={{ maxWidth: 120, minHeight: 26, padding: '0 4px', border: '1px solid #CBD5E1', borderRadius: 5, color: '#64748B', fontSize: 11 }}
                      >
                        <option value="position">Ordem manual</option>
                        <option value="priority">Prioridade</option>
                        <option value="due">Prazo</option>
                        <option value="title">Título</option>
                        <option value="created">Mais recentes</option>
                      </select>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                      <CardCount>
                        {itemsInStage.length}
                      </CardCount>
                      <button
                        type="button"
                        title={`Editar coluna ${stage.name}`}
                        aria-label={`Editar coluna ${stage.name}`}
                        onClick={() => handleOpenEditStage(stage)}
                        style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 22, height: 22, borderRadius: 4, color: '#94A3B8', background: 'transparent', border: 'none', cursor: 'pointer' }}
                      >
                        <Pencil size={13} />
                      </button>
                      <button
                        type="button"
                        title={`Mover coluna ${stage.name} para a esquerda`}
                        aria-label={`Mover coluna ${stage.name} para a esquerda`}
                        disabled={stages.indexOf(stage) === 0}
                        onClick={() => handleMoveStage(stage.id, 'left')}
                        style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 22, height: 22, borderRadius: 4, color: '#94A3B8', background: 'transparent', border: 'none', cursor: 'pointer', opacity: stages.indexOf(stage) === 0 ? 0.3 : 1 }}
                      >
                        <ChevronLeft size={14} />
                      </button>
                      <button
                        type="button"
                        title={`Mover coluna ${stage.name} para a direita`}
                        aria-label={`Mover coluna ${stage.name} para a direita`}
                        disabled={stages.indexOf(stage) === stages.length - 1}
                        onClick={() => handleMoveStage(stage.id, 'right')}
                        style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 22, height: 22, borderRadius: 4, color: '#94A3B8', background: 'transparent', border: 'none', cursor: 'pointer', opacity: stages.indexOf(stage) === stages.length - 1 ? 0.3 : 1 }}
                      >
                        <ChevronRight size={14} />
                      </button>
                    </div>
                  </ColumnHeader>

                  <CardList>
                    {itemsInStage.map((item, itemIndex) => (
                      <Fragment key={item.id}>
                        {groupBy !== 'none' && (itemIndex === 0 || getGroupLabel(itemsInStage[itemIndex - 1]) !== getGroupLabel(item)) && (
                          <div style={{ padding: '5px 2px 1px', fontSize: 13, fontWeight: 800, color: '#64748B', textTransform: 'uppercase', letterSpacing: '.04em' }}>
                            {getGroupLabel(item)}
                          </div>
                        )}
                      <Card
                        $isRunning={runningItemId === item.id}
                        $isDragging={draggedItemId === item.id}
                        $priority={item.priority}
                        $late={!!item.dueDate && item.dueDate < todayStr()}
                        onClick={() => handleCardClick(item)}
                        onPointerDown={event => handleCardPointerDown(event, item)}
                        onPointerMove={handleCardPointerMove}
                        onPointerUp={handleCardPointerUp}
                        onPointerCancel={handleCardPointerCancel}
                        style={selectedItemIds.has(item.id) ? {outline:'2px solid #1E7BD7',outlineOffset:1} : undefined}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                          <div style={{display:'flex',alignItems:'center',gap:7}}>
                            <input
                              type="checkbox"
                              checked={selectedItemIds.has(item.id)}
                              aria-label={`Selecionar #${item.number ?? ''} ${item.title}`}
                              onPointerDown={event=>event.stopPropagation()}
                              onClick={event=>event.stopPropagation()}
                              onChange={()=>toggleItemSelection(item.id)}
                              style={{width:15,height:15,accentColor:'#1E7BD7'}}
                            />
                            {cardSettings.showPriority && <Tag $priority={item.priority}>
                              {item.priority === 3 ? 'Crítica' : item.priority === 2 ? 'Alta' : item.priority === 1 ? 'Média' : 'Baixa'}
                            </Tag>}
                          </div>

                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            {cardSettings.showDueDate && item.dueDate && (
                              <CardEstimate style={{ color: item.dueDate < todayStr() ? '#D92D20' : undefined, fontWeight: item.dueDate < todayStr() ? 700 : undefined }}>
                                <AlertTriangle size={12} />
                                <span>{item.dueDate.slice(8, 10)}/{item.dueDate.slice(5, 7)}</span>
                              </CardEstimate>
                            )}
                            {cardSettings.showEstimate && item.estimatedHours && (
                              <CardEstimate>
                                <Clock size={12} />
                                <span>{item.estimatedHours}h est.</span>
                              </CardEstimate>
                            )}
                          </div>
                        </div>

                        <CardTitle>{item.number ? `#${item.number} · ` : ''}{item.title}</CardTitle>
                        {(item.isBlocked || item.origin && item.origin !== 1) && <div style={{ display:'flex', gap:5, flexWrap:'wrap' }}>
                          {item.isBlocked && <span style={{fontSize:13,fontWeight:800,color:'#B45309',background:'#FEF3C7',padding:'2px 7px',borderRadius:999}}>Bloqueada</span>}
                          {item.origin && item.origin !== 1 && <span style={{fontSize:13,fontWeight:800,color:'#1D4ED8',background:'#DBEAFE',padding:'2px 7px',borderRadius:999}}>{item.origin === 2 ? 'Portal externo' : 'Origem externa'}</span>}
                        </div>}

                        {item.subtitle && (
                          <CardSubtitle>
                            {item.subtitle}
                          </CardSubtitle>
                        )}

                        {((cardSettings.showType && item.taskTypeName) || (cardSettings.showTags && item.tags && item.tags.length > 0) || (showStoryPoints && cardSettings.showPoints && item.points != null)) && (
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 6 }}>
                            {cardSettings.showType && item.taskTypeName && (
                              <span style={{ fontSize: 13.5, fontWeight: 700, padding: '2px 8px', borderRadius: 4, color: '#fff', background: item.taskTypeColor || '#1E7BD7' }}>
                                {item.taskTypeName}
                              </span>
                            )}
                            {cardSettings.showTags && (item.tags || []).map(tag => (
                              <span key={tag.id} style={{ fontSize: 13.5, fontWeight: 600, padding: '2px 8px', borderRadius: 999, color: '#fff', background: tag.color || '#64748B' }}>
                                {tag.name}
                              </span>
                            ))}
                            {showStoryPoints && cardSettings.showPoints && item.points != null && (
                              <span style={{ fontSize: 13.5, fontWeight: 700, padding: '2px 8px', borderRadius: 4, color: '#334155', background: '#E2E8F0' }}>
                                {item.points} pts
                              </span>
                            )}
                          </div>
                        )}

                        <CardMeta>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                            {cardSettings.showAssignees && <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                              <Users size={12} />
                              <span>{getCardAssigneeLabel(item)}</span>
                            </span>}
                            {(item.subItemsCount || 0) > 0 && (
                              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                                <CheckSquare size={12} />
                                <span>{item.subItemsCount}</span>
                              </span>
                            )}
                            {cardSettings.showAttachments && (item.attachmentsCount || 0) > 0 && (
                              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                                <Paperclip size={12} />
                                <span>{item.attachmentsCount}</span>
                              </span>
                            )}
                            {cardSettings.showChecklist && (item.checklistTotal || 0) > 0 && (
                              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, color: (item.checklistDone || 0) === item.checklistTotal ? '#10B981' : undefined }}>
                                <CheckSquare size={12} />
                                <span>{item.checklistDone || 0}/{item.checklistTotal}</span>
                              </span>
                            )}
                          </div>

                          {cardSettings.showTime && ((item.totalTimeSeconds || 0) > 0 || runningItemId === item.id) && (
                            <TimerWrapper $isRunning={runningItemId === item.id}>
                              {runningItemId === item.id ? <span style={{ color: '#008ECF' }}>●</span> : <Clock size={12} />}
                              <span>
                                {(() => {
                                  const isCurrent = runningItemId === item.id;
                                  const userSecs = (item.userTimeSeconds || 0) + (isCurrent ? activeTime : 0);
                                  const totalSecs = (item.totalTimeSeconds || 0) + (isCurrent ? activeTime : 0);

                                  if (totalSecs === userSecs) {
                                    return formatTime(totalSecs);
                                  } else {
                                    return `${formatTime(totalSecs)} (Você: ${formatTime(userSecs)})`;
                                  }
                                })()}
                              </span>
                            </TimerWrapper>
                          )}
                        </CardMeta>

                        <CardActions>
                          <div style={{ display: 'flex', gap: 4 }}>
                            <ActionIcon
                              aria-label="Mover tarefa para a coluna anterior"
                              title="Mover tarefa para a coluna anterior"
                              onClick={event => {
                                event.stopPropagation();
                                handleMoveItem(item, 'left');
                              }}
                              disabled={stages.indexOf(stage) === 0}
                            >
                              <ChevronLeft size={16} />
                            </ActionIcon>
                            <ActionIcon
                              aria-label="Mover tarefa para a próxima coluna"
                              title="Mover tarefa para a próxima coluna"
                              onClick={event => {
                                event.stopPropagation();
                                handleMoveItem(item, 'right');
                              }}
                              disabled={stages.indexOf(stage) === stages.length - 1}
                            >
                              <ChevronRight size={16} />
                            </ActionIcon>
                          </div>

                          <ActionIcon
                            aria-label={runningItemId === item.id ? 'Parar cronômetro da tarefa' : 'Iniciar cronômetro da tarefa'}
                            title={runningItemId === item.id ? 'Parar cronômetro' : 'Iniciar cronômetro'}
                            $color={runningItemId === item.id ? '#EF4444' : '#10B981'}
                            onClick={event => {
                              event.stopPropagation();
                              toggleTimer(item.id);
                            }}
                          >
                            {runningItemId === item.id ? <Square size={16} fill="#EF4444" /> : <PlayIcon size={16} />}
                          </ActionIcon>
                        </CardActions>
                      </Card>
                      </Fragment>
                    ))}

                    <AddCardButton
                      onClick={() => {
                        setTargetStageIdForNewItem(stage.id);
                        setShowItemModal(true);
                      }}
                    >
                      <Plus size={14} />
                      <span>Novo Card</span>
                    </AddCardButton>
                  </CardList>
                </Column>
              );
            })}
          </KanbanGrid>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '80px 0', gap: 20 }}>
            <AlertTriangle size={48} style={{ color: '#00D2FF' }} />
            <p style={{ color: '#9CA3AF' }}>Nenhum quadro ativo encontrado. Crie um quadro para iniciar.</p>
          </div>
        )}

        {dragPreview && (
          <DragPreview
            $priority={dragPreview.priority}
            style={{ left: dragPreview.x, top: dragPreview.y }}
          >
            <DragPreviewTitle>{dragPreview.title}</DragPreviewTitle>
            {dragPreview.subtitle && (
              <DragPreviewSubtitle>{dragPreview.subtitle}</DragPreviewSubtitle>
            )}
          </DragPreview>
        )}
      </MainContent>

      {/* Modais */}
      {showBoardModal && (
        <ModalOverlay onClick={() => { if (!createBoardPending && !createBoardSuccessId) setShowBoardModal(false); }}>
          <Modal onClick={e => e.stopPropagation()}>
            <ModalTitle>Criar Novo Quadro</ModalTitle>
            {createBoardSuccessId ? (
              <>
                <p style={{ color: '#10B981', fontWeight: 600, fontSize: 15, margin: '8px 0 16px' }}>
                  Quadro criado com sucesso!
                </p>
                <ModalActions>
                  <CancelButton type="button" onClick={() => {
                    setShowBoardModal(false);
                    setCreateBoardSuccessId(null);
                    setNewBoardName('');
                  }}>Fechar</CancelButton>
                  <SubmitButton
                    type="button"
                    onClick={() => {
                      setShowBoardModal(false);
                      setCreateBoardSuccessId(null);
                      setNewBoardName('');
                      setSelectedBoardId(createBoardSuccessId);
                      navigate(`/boards/${createBoardSuccessId}`);
                    }}
                    style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                  >
                    <ArrowRightIcon size={14} />
                    Ir para o quadro
                  </SubmitButton>
                </ModalActions>
              </>
            ) : (
              <ModalForm onSubmit={handleCreateBoard}>
                <Input
                  type="text"
                  placeholder="Nome do Quadro"
                  value={newBoardName}
                  onChange={e => setNewBoardName(e.target.value)}
                  required
                  autoFocus
                  disabled={createBoardPending}
                  aria-label="Nome do quadro"
                />
                {projectTeams.length > 0 && (
                  <Select
                    aria-label="Equipe do quadro"
                    value={newBoardTeamId}
                    onChange={e => setNewBoardTeamId(e.target.value)}
                    disabled={createBoardPending}
                  >
                    <option value="">Sem equipe específica</option>
                    {projectTeams.map(team => (
                      <option key={team.id} value={team.id}>{team.name}</option>
                    ))}
                  </Select>
                )}
                <label htmlFor="board-structure">Estrutura de colunas</label>
                <Select id="board-structure" value={copyBoardId} onChange={e => setCopyBoardId(e.target.value)}>
                  <option value="">Estrutura básica</option>
                  {visibleBoards.filter(b => b.projectId === (urlProjectId ?? boards.find(board => board.id === selectedBoardId)?.projectId)).map(b => <option key={b.id} value={b.id}>Copiar colunas de {b.name}</option>)}
                </Select>
                {createBoardError && (
                  <p style={{ color: '#D92D20', fontSize: 13.5, marginTop: 4 }}>{createBoardError}</p>
                )}
                <ModalActions>
                  <CancelButton type="button" onClick={() => setShowBoardModal(false)} disabled={createBoardPending}>Cancelar</CancelButton>
                  <SubmitButton type="submit" disabled={createBoardPending || !newBoardName.trim()}>
                    {createBoardPending ? 'Criando…' : 'Criar'}
                  </SubmitButton>
                </ModalActions>
              </ModalForm>
            )}
          </Modal>
        </ModalOverlay>
      )}

      {showDeleteBoardModal && (
        <ModalOverlay onClick={() => { if (!deleteBoardPending) setShowDeleteBoardModal(false); }}>
          <Modal onClick={e => e.stopPropagation()}>
            <ModalTitle>Excluir Quadro</ModalTitle>
            <p style={{ fontSize: 15, color: '#334155', margin: '8px 0 12px' }}>
              Tem certeza que deseja excluir o quadro{' '}
              <strong>"{boards.find(b => b.id === selectedBoardId)?.name}"</strong>?
              Esta ação não pode ser desfeita.
            </p>
            {visibleBoards.filter(b => b.id !== selectedBoardId).length > 0 && (
              <div style={{ marginBottom: 14 }}>
                <label style={{ fontSize: 13.5, fontWeight: 700, color: '#64748B', display: 'block', marginBottom: 5 }}>
                  Mover tarefas para:
                </label>
                <Select
                  value={deleteBoardDestId}
                  onChange={e => setDeleteBoardDestId(e.target.value)}
                  disabled={deleteBoardPending}
                  style={{ width: '100%' }}
                >
                  <option value="">Escolha o quadro de destino</option>
                  {visibleBoards.filter(b => b.id !== selectedBoardId && b.projectId === (urlProjectId ?? boards.find(board => board.id === selectedBoardId)?.projectId)).map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </Select>
              </div>
            )}
            {deleteBoardDestId && <Select aria-label="Coluna de destino" value={deleteBoardStageId} onChange={e => setDeleteBoardStageId(e.target.value)}>
              <option value="">Escolha a coluna de destino</option>
              {destinationStages.map(stage => <option key={stage.id} value={stage.id}>{stage.name}</option>)}
            </Select>}
            {deleteBoardError && (
              <p style={{ color: '#D92D20', fontSize: 13.5, marginBottom: 8 }}>{deleteBoardError}</p>
            )}
            <ModalActions>
              <CancelButton type="button" onClick={() => setShowDeleteBoardModal(false)} disabled={deleteBoardPending}>Cancelar</CancelButton>
              <SubmitButton
                type="button"
                onClick={handleDeleteBoard}
                disabled={deleteBoardPending}
                style={{ background: '#D92D20' }}
              >
                {deleteBoardPending ? 'Excluindo…' : 'Excluir Quadro'}
              </SubmitButton>
            </ModalActions>
          </Modal>
        </ModalOverlay>
      )}

      {showStageModal && (
        <ModalOverlay>
          <Modal>
            <ModalTitle>Criar Nova Coluna</ModalTitle>
            <ModalForm onSubmit={handleCreateStage}>
              <Input
                type="text"
                placeholder="Nome da Coluna"
                value={newStageName}
                onChange={e => setNewStageName(e.target.value)}
                required
                autoFocus
              />
              <Select
                aria-label="Classificação da coluna"
                value={newStageCategory}
                onChange={e => setNewStageCategory(Number(e.target.value) as StageCategoryValue)}
              >
                {stageCategoryOptions.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </Select>
              <ModalActions>
                <CancelButton type="button" onClick={() => setShowStageModal(false)}>Cancelar</CancelButton>
                <SubmitButton type="submit">Adicionar</SubmitButton>
              </ModalActions>
            </ModalForm>
          </Modal>
        </ModalOverlay>
      )}

      {showEditStageModal && editingStage && (
        <ModalOverlay>
          <Modal>
            <ModalTitle>Editar Coluna</ModalTitle>
            <ModalForm onSubmit={handleUpdateStage}>
              <Input
                type="text"
                placeholder="Nome da Coluna"
                value={editStageName}
                onChange={e => setEditStageName(e.target.value)}
                required
                autoFocus
              />
              <Select
                aria-label="Classificação da coluna"
                value={editStageCategory}
                onChange={e => setEditStageCategory(Number(e.target.value) as StageCategoryValue)}
              >
                {stageCategoryOptions.map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </Select>
              {editingStage.category !== editStageCategory && <div aria-label="Impacto da reclassificação">
                {stageImpact ? <>
                  <p>{editingStage.name}: {editStageCategory === StageCategory.Done ? 'concluir' : 'reabrir'}. {stageImpact.totalItems} tarefa(s) na coluna; {stageImpact.changedItems} terão o estado alterado; {stageImpact.openDescendants} descendente(s) aberto(s) fora da coluna.</p>
                  {stageImpact.totalItems > 0 && <label><input type="checkbox" checked={confirmCategoryChange} onChange={e=>setConfirmCategoryChange(e.target.checked)}/>Confirmo alterar o estado das tarefas desta coluna</label>}
                  {stageImpact.openDescendants > 0 && <label><input type="checkbox" checked={confirmDescendants} onChange={e=>setConfirmDescendants(e.target.checked)}/>Aceito concluir também todos os {stageImpact.openDescendants} descendentes abertos, sem movê-los</label>}
                </> : <p>Carregando impacto da alteração...</p>}
              </div>}
              <label htmlFor="remove-stage-destination">Ao excluir, transferir tarefas para</label>
              <Select id="remove-stage-destination" value={deleteStageDestination} onChange={e=>setDeleteStageDestination(e.target.value)}>
                <option value="">Escolha uma coluna de destino</option>
                {stages.filter(stage=>stage.id!==editingStage.id).map(stage=><option key={stage.id} value={stage.id}>{stage.name}</option>)}
              </Select>
              <button type="button" disabled={editStagePending} onClick={async()=>{
                setEditStagePending(true);setEditStageError('');
                try { await api.deleteStage(editingStage.id,deleteStageDestination||undefined);setShowEditStageModal(false);setEditingStage(null);setDeleteStageDestination('');await loadBoardData(selectedBoardId); }
                catch(error) { setEditStageError((error as Error).message); }
                finally { setEditStagePending(false); }
              }}>Excluir coluna</button>
              {editStageError && (
                <div style={{ color: '#EF4444', fontSize: '13px', marginTop: '4px' }}>
                  {editStageError}
                </div>
              )}
              <ModalActions>
                <CancelButton
                  type="button"
                  onClick={() => { setShowEditStageModal(false); setEditingStage(null); }}
                  disabled={editStagePending}
                >
                  Cancelar
                </CancelButton>
                <SubmitButton type="submit" disabled={editStagePending}>
                  {editStagePending ? 'Salvando…' : 'Salvar'}
                </SubmitButton>
              </ModalActions>
            </ModalForm>
          </Modal>
        </ModalOverlay>
      )}

      {showItemModal && (
        <ModalOverlay>
          <Modal>
            <ModalTitle>Novo Card / Tarefa</ModalTitle>
            <ModalForm onSubmit={handleCreateWorkItem}>
              <Input
                type="text"
                placeholder="Título do Card"
                value={newItemTitle}
                onChange={e => setNewItemTitle(e.target.value)}
                required
                autoFocus
              />
              <Input
                type="text"
                placeholder="Subtítulo (Opcional)"
                value={newItemSubtitle}
                onChange={e => setNewItemSubtitle(e.target.value)}
              />
              <Textarea
                placeholder="Descrição em Markdown (Opcional)"
                value={newItemDesc}
                onChange={e => setNewItemDesc(e.target.value)}
              />
              <div>
                <label style={{ fontSize: '14px', display: 'block', marginBottom: 4 }}>Tipo do Card</label>
                <WorkItemKindSelector
                  value={newItemKind}
                  onChange={setNewItemKind}
                />
              </div>
              <FormRow>
                <div>
                  <label style={{ fontSize: '14px', display: 'block', marginBottom: 4 }}>Prioridade</label>
                  <Select
                    style={{ width: '100%' }}
                    value={newItemPriority}
                    onChange={e => setNewItemPriority(Number(e.target.value))}
                  >
                    <option value={0}>Baixa</option>
                    <option value={1}>Média</option>
                    <option value={2}>Alta</option>
                  </Select>
                </div>
                <div>
                  <label style={{ fontSize: '14px', display: 'block', marginBottom: 4 }}>Horas Est.</label>
                  <Input
                    style={{ width: '100%' }}
                    type="number"
                    placeholder="Horas"
                    value={newItemHours || ''}
                    onChange={e => setNewItemHours(e.target.value ? Number(e.target.value) : undefined)}
                  />
                </div>
              </FormRow>
              <FormRow>
                <div>
                  <label style={{ fontSize: '14px', display: 'block', marginBottom: 4 }}>Responsável principal</label>
                  <Select
                    aria-label='Responsável principal da nova tarefa'
                    style={{ width: '100%' }}
                    value={newItemResponsibleId}
                    onChange={e => setNewItemResponsibleId(e.target.value)}
                    required
                  >
                    <option value=''>Selecione uma pessoa...</option>
                    {assignableUsers.map(user => <option key={user.id} value={user.id}>{getUserLabel(user)}</option>)}
                  </Select>
                </div>
                <div>
                  <label style={{ fontSize: '14px', display: 'block', marginBottom: 4 }}>Responsáveis adicionais</label>
                  <Select
                    aria-label='Responsáveis adicionais da nova tarefa'
                    style={{ width: '100%', minHeight: 86 }}
                    multiple
                    value={newItemParticipantIds}
                    onChange={e => setNewItemParticipantIds(Array.from(e.currentTarget.selectedOptions, option => option.value))}
                  >
                    {assignableUsers.filter(user => user.id !== newItemResponsibleId).map(user => <option key={user.id} value={user.id}>{getUserLabel(user)}</option>)}
                  </Select>
                </div>
              </FormRow>
              <p style={{ margin: '0', color: '#64748B', fontSize: '12px' }}>Somente pessoas com acesso ao projeto aparecem nesta lista.</p>
              <ModalActions>
                <CancelButton type="button" onClick={() => setShowItemModal(false)}>Cancelar</CancelButton>
                <SubmitButton type="submit">Criar Card</SubmitButton>
              </ModalActions>
            </ModalForm>
          </Modal>
        </ModalOverlay>
      )}

      <TaskDetailDrawer
        item={selectedItem ? ({
          id:selectedItem.id,
          number:selectedItem.number,
          boardId:selectedItem.boardId,
          boardName:boards.find(board=>board.id===selectedItem.boardId)?.name??'Quadro',
          stageId:selectedItem.stageId??undefined,
          stageName:stages.find(stage=>stage.id===selectedItem.stageId)?.name,
          parentId:selectedItem.parentId??undefined,
          teamId:selectedItem.teamId??undefined,
          kind:5,
          title:selectedItem.title,
          description:selectedItem.description,
          priority:selectedItem.priority,
          origin:selectedItem.origin,
          responsibleId:selectedItem.responsibleId??undefined,
          requesterId:selectedItem.requesterId??undefined,
          requesterName:selectedItem.requesterName??undefined,
          requesterEmail:selectedItem.requesterEmail??undefined,
          points:selectedItem.points??undefined,
          estimatedHours:selectedItem.estimatedHours,
          dueDate:selectedItem.dueDate,
          rank:selectedItem.position,
          position:selectedItem.position,
          createdAt:selectedItem.createdAt,
          assigneeIds:selectedItem.assignees?.map(assignee=>assignee.userId),
        } satisfies BacklogItem) : null}
        projectKey={projectKey}
        onOpenChange={open=>{if(!open){setSelectedItem(null);if(selectedBoardId)void loadBoardData(selectedBoardId);}}}
        onItemUpdated={()=>{if(selectedBoardId)void loadBoardData(selectedBoardId);}}
        onOpenSubtask={workItemId=>{void api.getWorkItemDetails(workItemId).then(details=>setSelectedItem(details as unknown as WorkItem));}}
      />

      {showLeadTimeModal && (
        <ModalOverlay onClick={() => setShowLeadTimeModal(false)}>
          <Modal wide onClick={event => event.stopPropagation()}>
            <ModalTitle>Métricas de Lead Time (Tempo Médio por Etapa)</ModalTitle>

            {loadingLeadTime ? (
              <div style={{ padding: '40px 0', textAlign: 'center', color: '#94A3B8' }}>
                Carregando métricas de tempo...
              </div>
            ) : leadTimeData.length === 0 ? (
              <div style={{ padding: '40px 0', textAlign: 'center', color: '#94A3B8' }}>
                Nenhum histórico de transição registrado neste quadro.
              </div>
            ) : (
              <LeadTimeTable>
                <thead>
                  <tr>
                    <LeadTimeTh>Etapa / Coluna</LeadTimeTh>
                    <LeadTimeTh>Cards Finalizados</LeadTimeTh>
                    <LeadTimeTh>Média (Horas)</LeadTimeTh>
                    <LeadTimeTh>Média (Dias)</LeadTimeTh>
                  </tr>
                </thead>
                <tbody>
                  {leadTimeData.map(data => (
                    <tr key={data.stageId}>
                      <LeadTimeTd><strong>{data.stageName}</strong></LeadTimeTd>
                      <LeadTimeTd>{data.itemsCount}</LeadTimeTd>
                      <LeadTimeTd>{data.averageHours}h</LeadTimeTd>
                      <LeadTimeTd>{data.averageDays} dias</LeadTimeTd>
                    </tr>
                  ))}
                </tbody>
              </LeadTimeTable>
            )}

            <ModalActions>
              <CancelButton type="button" onClick={() => setShowLeadTimeModal(false)}>Fechar</CancelButton>
            </ModalActions>
          </Modal>
        </ModalOverlay>
      )}
      {showTempoModal && (
        <ModalOverlay onClick={() => setShowTempoModal(false)}>
          <Modal wide onClick={event => event.stopPropagation()}>
            <ModalTitle>Meu Tempo</ModalTitle>

            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '12px', marginBottom: '8px' }}>
              <SmallButton type="button" onClick={() => shiftWeek(-7)}>
                <ChevronLeft size={14} />
                <span>Anterior</span>
              </SmallButton>
              <strong>{weeklyTime ? formatWeekLabel(weeklyTime.weekStart) : '—'}</strong>
              <SmallButton type="button" onClick={() => shiftWeek(7)}>
                <span>Próxima</span>
                <ChevronRight size={14} />
              </SmallButton>
            </div>

            {loadingWeekly ? (
              <DetailText>Carregando...</DetailText>
            ) : (
              <>
                <TempoGrid>
                  {weeklyTime?.days.map(day => {
                    const goalSecs = 8 * 3600;
                    const pctRaw = (day.totalSeconds / goalSecs) * 100;
                    const pct = Math.round(pctRaw);
                    const heightPct = day.totalSeconds > 0 ? Math.min(100, Math.max(14, pctRaw)) : 0;
                    const isToday = day.date === todayStr();
                    const overSecs = day.totalSeconds - goalSecs;
                    return (
                      <TempoDay key={day.date}>
                        <TempoBarTrack>
                          {isToday && <TempoTodayBadge>HOJE</TempoTodayBadge>}
                          {overSecs > 0 && <TempoOverBadge>+{fmtHm(overSecs)}</TempoOverBadge>}
                          {day.totalSeconds > 0 ? (
                            <TempoBarFill $pct={heightPct} $today={isToday}>
                              <span>{fmtHm(day.totalSeconds)}</span>
                            </TempoBarFill>
                          ) : (
                            <TempoEmptyHours>0h00</TempoEmptyHours>
                          )}
                        </TempoBarTrack>
                        <TempoPct $over={pct >= 100}>{day.totalSeconds > 0 ? `${pct}%` : '0%'}</TempoPct>
                        <TempoAdjust
                          type="button"
                          onClick={() => { setAdjustDay(day.date); setAdjustTaskId(''); setAdjustMinutes(undefined); setAdjustTab('tarefas'); loadAllTasks(); loadAdjustData(day.date); }}
                        >
                          Ajustar
                        </TempoAdjust>
                        <TempoDayLabel>{formatDayLabel(day.date)}</TempoDayLabel>
                      </TempoDay>
                    );
                  })}
                </TempoGrid>
                <DetailText style={{ textAlign: 'center', marginTop: '8px' }}>
                  Meta diária: 8h. Total da semana:{' '}
                  <strong>
                    {fmtHm((weeklyTime?.days || []).reduce((acc, d) => acc + d.totalSeconds, 0))}
                  </strong>
                </DetailText>
              </>
            )}

            <ModalActions>
              <CancelButton type="button" onClick={() => setShowTempoModal(false)}>Fechar</CancelButton>
            </ModalActions>
          </Modal>
        </ModalOverlay>
      )}
      {adjustDay && (
        <ModalOverlay onClick={() => setAdjustDay(null)}>
          <Modal onClick={event => event.stopPropagation()}>
            <ModalTitle>Ajustar: {formatDayLabel(adjustDay)}</ModalTitle>

            <div style={{ display: 'flex', gap: 6, borderBottom: '1px solid #E2E8F0', marginBottom: 16 }}>
              <button type="button" onClick={() => setAdjustTab('tarefas')} style={{ padding: '8px 14px', fontSize: 15, fontWeight: 700, color: adjustTab === 'tarefas' ? '#1E7BD7' : '#64748B', borderBottom: `2px solid ${adjustTab === 'tarefas' ? '#1E7BD7' : 'transparent'}`, marginBottom: -1 }}>Ajustar tarefas</button>
              <button type="button" onClick={() => setAdjustTab('justificar')} style={{ padding: '8px 14px', fontSize: 15, fontWeight: 700, color: adjustTab === 'justificar' ? '#1E7BD7' : '#64748B', borderBottom: `2px solid ${adjustTab === 'justificar' ? '#1E7BD7' : 'transparent'}`, marginBottom: -1 }}>Justificar o dia</button>
            </div>

            {adjustTab === 'tarefas' && (
              <>
                {dailyByTask.length > 0 && (
                  <div style={{ marginBottom: 14 }}>
                    <DetailText style={{ fontWeight: 700, marginBottom: 6 }}>Horas neste dia por tarefa</DetailText>
                    {dailyByTask.map(d => (
                      <div key={d.workItemId} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 15, padding: '4px 0', color: '#334155' }}>
                        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 260 }}>{d.title}</span>
                        <strong>{fmtHm(d.seconds)}</strong>
                      </div>
                    ))}
                  </div>
                )}
                <DetailText style={{ marginBottom: '12px' }}>
                  Adicione tempo a uma tarefa neste dia (lançamento manual).
                </DetailText>
                {adjustTasks.length === 0 ? (
                  <DetailText>Nenhuma tarefa encontrada.</DetailText>
                ) : (
                  <ModalForm onSubmit={async e => { await submitAdjust(e); if (adjustDay) await loadAdjustData(adjustDay); }}>
                    <Select value={adjustTaskId} onChange={e => setAdjustTaskId(e.target.value)}>
                      <option value="">Selecionar tarefa</option>
                      {adjustTasks.map(t => (
                        <option key={t.id} value={t.id}>{t.boardName} — {t.title}</option>
                      ))}
                    </Select>
                    <Input
                      type="number"
                      min="1"
                      placeholder="Minutos a adicionar"
                      value={adjustMinutes || ''}
                      onChange={e => setAdjustMinutes(e.target.value ? Number(e.target.value) : undefined)}
                    />
                    <ModalActions>
                      <CancelButton type="button" onClick={() => setAdjustDay(null)}>Fechar</CancelButton>
                      <SubmitButton type="submit" disabled={!adjustTaskId || !adjustMinutes}>Adicionar</SubmitButton>
                    </ModalActions>
                  </ModalForm>
                )}
              </>
            )}

            {adjustTab === 'justificar' && (
              <>
                <DetailText style={{ marginBottom: '12px' }}>
                  Justifique o dia (férias, atestado, feriado). Abate da meta de 8h.
                </DetailText>
                {justifications.length > 0 && (
                  <div style={{ marginBottom: 14 }}>
                    {justifications.map(j => (
                      <div key={j.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 15, padding: '6px 0', color: '#334155' }}>
                        <span>{j.reason}</span>
                        <span style={{ display: 'inline-flex', gap: 10, alignItems: 'center' }}>
                          <strong>{j.hours}h</strong>
                          <button type="button" aria-label={`Remover justificativa ${j.reason}`} onClick={() => removeJustification(j.id)} style={{ color: '#CBD5E1' }}><X size={14} /></button>
                        </span>
                      </div>
                    ))}
                  </div>
                )}
                <ModalForm onSubmit={submitJustification}>
                  <Select value={justReason} onChange={e => setJustReason(e.target.value)}>
                    <option value="">Motivo</option>
                    <option value="Férias">Férias</option>
                    <option value="Atestado">Atestado</option>
                    <option value="Feriado">Feriado</option>
                    <option value="Folga">Folga</option>
                    <option value="Outro">Outro</option>
                  </Select>
                  <Input
                    type="number"
                    min="1"
                    max="24"
                    placeholder="Horas"
                    value={justHours || ''}
                    onChange={e => setJustHours(e.target.value ? Number(e.target.value) : undefined)}
                  />
                  <ModalActions>
                    <CancelButton type="button" onClick={() => setAdjustDay(null)}>Fechar</CancelButton>
                    <SubmitButton type="submit" disabled={!justReason || !justHours}>Justificar</SubmitButton>
                  </ModalActions>
                </ModalForm>
              </>
            )}
          </Modal>
        </ModalOverlay>
      )}
    </AppLayout>
  );
};
