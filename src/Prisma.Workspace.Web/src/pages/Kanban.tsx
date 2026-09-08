import { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import { Fragment, useMemo } from 'react';
import { TaskFeed } from '../features/task/TaskFeed';
import { TaskTaxonomyPanel } from '../features/task/TaskTaxonomyPanel';
import { TaskChecklistPanel } from '../features/task/TaskChecklistPanel';
import { TaskDescriptionPanel } from '../features/task/TaskDescriptionPanel';
import { BoardCalendar } from '../features/board/BoardCalendar';
import { BoardGantt } from '../features/board/BoardGantt';
import { BoardDashboard } from '../features/board/BoardDashboard';
import { TaskApprovalPanel } from '../features/task/TaskApprovalPanel';
import { TaskDetailDrawer } from '../components/TaskDetailDrawer';
import type { BacklogItem } from '../types/scrum';
import { userDisplayLabel } from '../utils/userDisplayName';
import { KanbanFilterBar } from '../features/board/KanbanFilterBar';
import { compareNewestWorkItems } from '../features/board/kanbanOrdering';
import { AutomationManager } from '../features/board/AutomationManager';
import { KanbanBulkToolbar } from '../features/board/KanbanBulkToolbar';
import {
  applyKanbanFilters, defaultCardSettings, defaultKanbanFilters,
  type KanbanCardSettings, type KanbanFilterState, type SavedFilterOption
} from '../features/board/KanbanFilters';
import { useBoardRealtime } from '../features/board/useBoardRealtime';
import {
  Plus,
  Play,
  Square,
  ChevronLeft,
  ChevronRight,
  Clock,
  FolderPlus,
  AlertTriangle,
  Users,
  UserPlus,
  X,
  Paperclip,
  Download,
  CheckSquare,
  BarChart2,
  Trash2,
  ArrowRight as ArrowRightIcon
} from 'lucide-react';
import {
  AppLayout,
  MainContent,
  BoardHeader,
  SelectorContainer,
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
  DetailSection,
  DetailLabel,
  DetailText,
  SectionHeader,
  SectionTitle,
  ListPanel,
  ListItem,
  MutedText,
  InlineForm,
  SmallButton,
  FileInput,
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
  TaskModal,
  TaskTopbar,
  TaskTimerBtn,
  TaskBody,
  TaskMain,
  TaskSidebar,
  TaskH1,
  TaskMeta,
  SidebarRow,
  SidebarLabel,
  SidebarValue,
  Avatars,
  Avatar,
  ProgressTrack,
  ProgressFill,
  TaskTabs,
  TaskTab
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
  wipLimit?: number | null;
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

interface AssignedUser extends UserDto {
  assignedAt: string;
}

interface Attachment {
  id: string;
  workItemId: string;
  fileName: string;
  fileSize?: number;
  mimeType?: string;
  uploadedBy?: string;
  createdAt: string;
}

interface TimeEntry {
  id: string;
  workItemId: string;
  userId: string;
  startedAt: string;
  endedAt?: string | null;
  durationSeconds?: number;
}

// Mantido temporariamente durante a migração visual; a gaveta canônica é TaskDetailDrawer.
const legacyTaskModalEnabled = false;




























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
  const { boardId: urlBoardId } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [boards, setBoards] = useState<Board[]>([]);
  const [selectedBoardId, setSelectedBoardId] = useState<string>(urlBoardId ?? '');
  // O seletor mostra só os quadros do mesmo projeto do quadro atual (não os da org toda).
  const visibleBoards = useMemo(() => {
    const current = boards.find(b => b.id === selectedBoardId);
    return current?.projectId ? boards.filter(b => b.projectId === current.projectId) : boards;
  }, [boards, selectedBoardId]);
  const [stages, setStages] = useState<Stage[]>([]);
  const [workItems, setWorkItems] = useState<WorkItem[]>([]);
  const [boardView, setBoardView] = useState<'kanban' | 'lista' | 'calendario' | 'gantt' | 'dashboard'>('kanban');
  const [listSort, setListSort] = useState<{ key: string; dir: 1 | -1 }>({ key: 'title', dir: 1 });
  const [filters, setFilters] = useState<KanbanFilterState>(
    searchParams.get('assignee')
      ? { ...defaultKanbanFilters, assigneeId: searchParams.get('assignee')! }
      : defaultKanbanFilters,
  );
  const [groupBy, setGroupBy] = useState('none');
  const [cardSort, setCardSort] = useState('created');
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
  const [taskTab, setTaskTab] = useState<'descricao' | 'comentarios' | 'anexos' | 'subtarefas'>('descricao');
  const [targetStageIdForNewItem, setTargetStageIdForNewItem] = useState<string | null>(null);
  const [draggedItemId, setDraggedItemId] = useState<string | null>(null);
  const [dropTargetStageId, setDropTargetStageId] = useState<string | null>(null);
  const [assignableUsers, setAssignableUsers] = useState<UserDto[]>([]);
  const [selectedItemAssignees, setSelectedItemAssignees] = useState<AssignedUser[]>([]);
  const [selectedItemSubItems, setSelectedItemSubItems] = useState<WorkItem[]>([]);
  const [selectedItemAttachments, setSelectedItemAttachments] = useState<Attachment[]>([]);
  const [selectedAssigneeId, setSelectedAssigneeId] = useState('');
  const [newSubItemTitle, setNewSubItemTitle] = useState('');
  const [uploadingAttachment, setUploadingAttachment] = useState(false);
  const [manualMinutes, setManualMinutes] = useState<number | undefined>(undefined);
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
  const [newStageName, setNewStageName] = useState('');
  // Sem escolha explícita, toda coluna nascia como "em andamento" — inclusive uma
  // chamada "Concluído" — e as tarefas nela nunca eram contadas como concluídas.
  const [newStageCategory, setNewStageCategory] = useState<StageCategoryValue>(StageCategory.InProgress);
  const [newItemTitle, setNewItemTitle] = useState('');
  const [newItemSubtitle, setNewItemSubtitle] = useState('');
  const [newItemDesc, setNewItemDesc] = useState('');
  const [newItemPriority, setNewItemPriority] = useState<number>(0);
  const [newItemHours, setNewItemHours] = useState<number | undefined>(undefined);

  // Timer persistido em TimeEntry.
  const [runningItemId, setRunningItemId] = useState<string | null>(null);
  const [runningStartedAt, setRunningStartedAt] = useState<string | null>(null);
  const [activeTime, setActiveTime] = useState<number>(0);

  const fetchBoards = useCallback(async () => {
    try {
      const data = await api.getBoards();
      setBoards(data);
      if (data.length > 0) {
        const stillValid = selectedBoardId && data.some((b: Board) => b.id === selectedBoardId);
        if (!stillValid) {
          // O quadro da URL manda SEMPRE (mesmo que a listagem não o traga por
          // filtro ou falha momentânea) — nunca abrir silenciosamente outro quadro.
          setSelectedBoardId(urlBoardId || data[0].id);
        }
      }
    } catch (e) {
      console.error(e);
    }
  }, [selectedBoardId]);

  const loadBoardData = useCallback(async (boardId: string) => {
    if (!boardId) return;
    try {
      const [stageData, itemData] = await Promise.all([
        api.getStages(boardId),
        api.getWorkItems(boardId)
      ]);
      setStages(stageData);
      setWorkItems(itemData);
      const currentIds = new Set((itemData as WorkItem[]).map(item => item.id));
      setSelectedItemIds(current => new Set([...current].filter(id => currentIds.has(id))));
    } catch (e) {
      console.error(e);
    }
  }, []);

  const refreshRealtimeBoard = useCallback(() => {
    if (selectedBoardId) loadBoardData(selectedBoardId);
  }, [selectedBoardId, loadBoardData]);
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
    if (!selectedBoardId) {
      setCatalogTags([]);
      setBoardSprints([]);
      return;
    }
    api.getTags().then(setCatalogTags).catch(() => setCatalogTags([]));
    const projectId = boards.find(board => board.id === selectedBoardId)?.projectId;
    if (!projectId) {
      setBoardSprints([]);
      setProjectMethodology(null);
      return;
    }
    api.getProject(projectId)
      .then((project: {methodology?:number;key?:string}) => {
        setProjectMethodology(project.methodology ?? 1);
        setProjectKey(project.key ?? 'ITEM');
      })
      .catch(() => { setProjectMethodology(null); setProjectKey('ITEM'); });
    api.getProjectSprints(projectId)
      .then((items: SprintOptionDto[]) => setBoardSprints(items.filter(item => item.status === 1 || item.status === 2)))
      .catch(() => setBoardSprints([]));
  }, [selectedBoardId, boards]);

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

  const loadSelectedItemDetails = useCallback(async (itemId: string) => {
    try {
      const [users, assignees, subItems, attachments] = await Promise.all([
        api.getAssignableUsers(),
        api.getAssignees(itemId),
        api.getSubItems(itemId),
        api.getAttachments(itemId)
      ]);

      setAssignableUsers(users);
      setSelectedItemAssignees(assignees);
      setSelectedItemSubItems(subItems);
      setSelectedItemAttachments(attachments);

      const assignedIds = new Set(assignees.map((user: AssignedUser) => user.id));
      const firstAvailableUser = users.find((user: UserDto) => !assignedIds.has(user.id));
      setSelectedAssigneeId(firstAvailableUser?.id || '');
    } catch (e) {
      console.error(e);
    }
  }, []);

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
    api.getAssignableUsers().then(setAssignableUsers).catch(() => undefined);
  }, [fetchBoards, loadRunningTimer]);

  useEffect(() => {
    if (selectedBoardId) {
      loadBoardData(selectedBoardId);
    }
  }, [selectedBoardId, loadBoardData]);

  useEffect(() => {
    if (selectedItem) {
      setTaskTab('descricao');
      loadSelectedItemDetails(selectedItem.id);
      return;
    }

    setSelectedItemAssignees([]);
    setSelectedItemSubItems([]);
    setSelectedItemAttachments([]);
    setSelectedAssigneeId('');
    setNewSubItemTitle('');
    setManualMinutes(undefined);
  }, [selectedItem, loadSelectedItemDetails]);

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
      const result = await api.createBoard(newBoardName.trim(), currentProjectId ?? undefined);
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
      await api.deleteBoard(selectedBoardId, deleteBoardDestId || undefined);
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
      const nextPos = stages.length > 0 ? Math.max(...stages.map(s => s.position)) + 100 : 100;
      await api.createStage(selectedBoardId, newStageName, nextPos, undefined, { category: newStageCategory });
      setNewStageName('');
      setNewStageCategory(StageCategory.InProgress);
      setShowStageModal(false);
      await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao criar coluna');
    }
  };

  const handleCreateWorkItem = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newItemTitle.trim() || !selectedBoardId) return;
    try {
      const columnItems = workItems.filter(w => w.stageId === targetStageIdForNewItem);
      const nextPos = columnItems.length > 0 ? Math.max(...columnItems.map(c => c.position)) + 100 : 100;

      await api.createWorkItem({
        boardId: selectedBoardId,
        stageId: targetStageIdForNewItem || undefined,
        title: newItemTitle,
        subtitle: newItemSubtitle || undefined,
        description: newItemDesc || undefined,
        priority: newItemPriority,
        estimatedHours: newItemHours,
        position: nextPos
      });

      // Reset
      setNewItemTitle('');
      setNewItemSubtitle('');
      setNewItemDesc('');
      setNewItemPriority(0);
      setNewItemHours(undefined);
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

  const handleAssignUser = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedItem || !selectedAssigneeId) return;

    try {
      await api.assignUser(selectedItem.id, selectedAssigneeId);
      await loadSelectedItemDetails(selectedItem.id);
      if (selectedBoardId) await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao atribuir responsavel');
    }
  };

  const handleRemoveAssignee = async (userId: string) => {
    if (!selectedItem) return;

    try {
      await api.removeAssignee(selectedItem.id, userId);
      await loadSelectedItemDetails(selectedItem.id);
      if (selectedBoardId) await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao remover responsavel');
    }
  };

  const handleCreateSubItem = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedItem || !newSubItemTitle.trim()) return;

    const nextPos = selectedItemSubItems.length > 0
      ? Math.max(...selectedItemSubItems.map(item => item.position)) + 100
      : 100;

    try {
      await api.createWorkItem({
        boardId: selectedItem.boardId,
        stageId: selectedItem.stageId || undefined,
        parentId: selectedItem.id,
        title: newSubItemTitle,
        priority: 0,
        position: nextPos
      });

      setNewSubItemTitle('');
      await loadSelectedItemDetails(selectedItem.id);
      if (selectedBoardId) await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao criar subtarefa');
    }
  };

  const handleUploadAttachment = async (event: React.ChangeEvent<HTMLInputElement>) => {
    if (!selectedItem || !event.target.files?.[0]) return;

    try {
      setUploadingAttachment(true);
      await api.uploadAttachment(selectedItem.id, event.target.files[0]);
      event.target.value = '';
      await loadSelectedItemDetails(selectedItem.id);
      if (selectedBoardId) await loadBoardData(selectedBoardId);
    } catch {
      alert('Erro ao enviar anexo');
    } finally {
      setUploadingAttachment(false);
    }
  };

  const handleDownloadAttachment = async (attachment: Attachment) => {
    if (!selectedItem) return;

    try {
      const blob = await api.downloadAttachment(selectedItem.id, attachment.id);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = attachment.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      alert('Erro ao baixar anexo');
    }
  };

  const handleCreateManualTimeEntry = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedItem || !manualMinutes || manualMinutes <= 0) return;

    const endedAt = new Date();
    const startedAt = new Date(endedAt.getTime() - manualMinutes * 60 * 1000);

    try {
      await api.createManualTimeEntry({
        workItemId: selectedItem.id,
        startedAt: startedAt.toISOString(),
        endedAt: endedAt.toISOString()
      });

      // Reflete o lançamento imediatamente no "Tempo nesta tarefa"
      // (antes, o total só atualizava ao reabrir o modal).
      const addedSeconds = manualMinutes * 60;
      setSelectedItem(prev => prev
        ? { ...prev, totalTimeSeconds: (prev.totalTimeSeconds || 0) + addedSeconds }
        : prev);

      setManualMinutes(undefined);
      await loadSelectedItemDetails(selectedItem.id);

      if (selectedBoardId) {
        await loadBoardData(selectedBoardId);
      }
    } catch {
      alert('Erro ao lancar tempo manual');
    }
  };

  const toggleTimer = async (itemId: string) => {
    try {
      if (runningItemId === itemId) {
        await api.stopTimer(itemId);
        setRunningItemId(null);
        setRunningStartedAt(null);
        setActiveTime(0);
        window.dispatchEvent(new Event('timer-change'));

        if (selectedItem?.id === itemId) {
          await loadSelectedItemDetails(itemId);
        }

        if (selectedBoardId) {
          await loadBoardData(selectedBoardId);
        }
        return;
      }

      if (runningItemId && runningItemId !== itemId) {
        alert('Pare o timer atual antes de iniciar outro card.');
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

  const formatTotalHours = (seconds?: number) => {
    if (!seconds) return '0.00h';
    return `${(seconds / 3600).toFixed(2)}h`;
  };

  const formatFileSize = (bytes?: number) => {
    if (!bytes) return '0 KB';
    if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
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

  return (
    <AppLayout>
      <MainContent>
        <BoardHeader>
          <SelectorContainer>
            <Select
              value={selectedBoardId}
              onChange={e => setSelectedBoardId(e.target.value)}
              disabled={boards.length === 0}
            >
              {visibleBoards.length === 0 ? (
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

            {visibleBoards.length > 1 && selectedBoardId && (
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

          {selectedBoardId && (
            <div style={{ display: 'flex', gap: '12px' }}>
              <div style={{ display: 'inline-flex', border: '1px solid #cbd5e1', borderRadius: 6, overflow: 'hidden' }}>
                {([['kanban', 'Kanban'], ['lista', 'Lista'], ['calendario', 'Calendário'], ['gantt', 'Gantt'], ['dashboard', 'Dashboard']] as const).map(([key, label]) => (
                  <button
                    key={key}
                    type="button"
                    onClick={() => setBoardView(key)}
                    style={{ padding: '8px 14px', fontSize: 15, fontWeight: 700, background: boardView === key ? '#1E7BD7' : '#fff', color: boardView === key ? '#fff' : '#64748B' }}
                  >{label}</button>
                ))}
              </div>
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
            </div>
          )}
        </BoardHeader>

        {selectedBoardId && boardView !== 'dashboard' && (
          <KanbanFilterBar
            items={workItems}
            resultCount={visibleWorkItems.length}
            users={assignableUsers.map(user => ({ id: user.id, label: getUserLabel(user) }))}
            filter={filters}
            onFilter={setFilters}
            groupBy={groupBy}
            onGroupBy={setGroupBy}
            sortBy={cardSort}
            onSortBy={setCardSort}
            savedFilters={savedFilters}
            onApplySaved={applySavedFilter}
            onSaveFilter={saveCurrentFilter}
            onReset={() => { setFilters(defaultKanbanFilters); setGroupBy('none'); setCardSort('created'); }}
            cardSettings={cardSettings}
            onCardSettings={setCardSettings}
            onSaveCardSettings={saveCardView}
            showPoints={showStoryPoints}
          />
        )}

        {selectedBoardId && (boardView === 'kanban' || boardView === 'lista') && (
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

        {selectedBoardId && boardView === 'calendario' ? (
          <BoardCalendar
            items={visibleWorkItems}
            onOpen={id => { const it = visibleWorkItems.find(w => w.id === id); if (it) handleCardClick(it); }}
          />
        ) : selectedBoardId && boardView === 'gantt' ? (
          <BoardGantt
            items={visibleWorkItems}
            stages={stages}
            onOpen={id => { const it = visibleWorkItems.find(w => w.id === id); if (it) handleCardClick(it); }}
          />
        ) : selectedBoardId && boardView === 'dashboard' ? (
          <BoardDashboard boardId={selectedBoardId} />
        ) : selectedBoardId && boardView === 'lista' ? (
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
                style={{ textAlign: 'left', padding: '10px 12px', fontSize: 14, color: '#64748B', fontWeight: 700, cursor: 'pointer', userSelect: 'none', borderBottom: '2px solid #E2E8F0', whiteSpace: 'nowrap' }}
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
        ) : selectedBoardId ? (
          <KanbanGrid>
            {stages.map(stage => {
              const itemsInStage = visibleWorkItems.filter(w => w.stageId === stage.id);
              return (
                <Column
                  key={stage.id}
                  data-stage-id={stage.id}
                  $isDropTarget={dropTargetStageId === stage.id}
                  $isDraggingAny={draggedItemId !== null}
                >
                  <ColumnHeader>
                    <ColumnTitle><span style={{ color: stage.statusColor || '#64748B', marginRight: 6 }}>●</span>{stage.name}</ColumnTitle>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                      <CardCount style={stage.wipLimit && itemsInStage.length >= stage.wipLimit ? { color: '#B42318', borderColor: '#FCA5A5', background: '#FEF2F2' } : undefined}>
                        {itemsInStage.length}{stage.wipLimit ? ` / ${stage.wipLimit} WIP` : ''}
                      </CardCount>
                      <button
                        type="button"
                        title="Mover coluna para esquerda"
                        disabled={stages.indexOf(stage) === 0}
                        onClick={() => handleMoveStage(stage.id, 'left')}
                        style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 22, height: 22, borderRadius: 4, color: '#94A3B8', background: 'transparent', border: 'none', cursor: 'pointer', opacity: stages.indexOf(stage) === 0 ? 0.3 : 1 }}
                      >
                        <ChevronLeft size={14} />
                      </button>
                      <button
                        type="button"
                        title="Mover coluna para direita"
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
                              onClick={event => {
                                event.stopPropagation();
                                handleMoveItem(item, 'left');
                              }}
                              disabled={stages.indexOf(stage) === 0}
                            >
                              <ChevronLeft size={16} />
                            </ActionIcon>
                            <ActionIcon
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
                />
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
                  {visibleBoards.filter(b => b.id !== selectedBoardId).map(b => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </Select>
              </div>
            )}
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
      />

      {selectedItem && legacyTaskModalEnabled && (() => {
        const boardName = boards.find(b => b.id === selectedItem.boardId)?.name || '—';
        const stageName = stages.find(s => s.id === selectedItem.stageId)?.name || '—';
        const isCurrent = runningItemId === selectedItem.id;
        const totalSecs = (selectedItem.totalTimeSeconds || 0) + (isCurrent ? activeTime : 0);
        const userSecs = (selectedItem.userTimeSeconds || 0) + (isCurrent ? activeTime : 0);
        const estSecs = (selectedItem.estimatedHours || 0) * 3600;
        const timePct = estSecs > 0 ? (totalSecs / estSecs) * 100 : 0;
        return (
        <ModalOverlay onClick={() => setSelectedItem(null)}>
          <TaskModal onClick={event => event.stopPropagation()}>
            <TaskTopbar>
              <TaskTimerBtn $running={isCurrent} onClick={() => toggleTimer(selectedItem.id)}>
                {isCurrent ? <Square size={16} /> : <Play size={16} />}
                <span>{isCurrent ? formatTime(activeTime) : 'Iniciar'}</span>
              </TaskTimerBtn>
              <Avatars>
                {selectedItemAssignees.slice(0, 5).map(user => (
                  <Avatar key={user.id} title={getUserLabel(user)}>
                    {getUserLabel(user).slice(0, 2).toUpperCase()}
                  </Avatar>
                ))}
              </Avatars>
              <div style={{ flex: 1 }} />
              <Tag $priority={selectedItem.priority}>
                {selectedItem.priority === 2 ? 'Alta' : selectedItem.priority === 1 ? 'Media' : 'Baixa'}
              </Tag>
              <ActionIcon onClick={() => setSelectedItem(null)} title="Fechar">
                <X size={18} />
              </ActionIcon>
            </TaskTopbar>

            <TaskBody>
              <TaskMain>
                <TaskH1>{selectedItem.title}</TaskH1>
                <TaskMeta>
                  Criada em {selectedItem.createdAt ? new Date(selectedItem.createdAt).toLocaleDateString('pt-BR') : '—'}
                </TaskMeta>

                <TaskTabs>
                  <TaskTab $active={taskTab === 'descricao'} onClick={() => setTaskTab('descricao')}>Descrição</TaskTab>
                  <TaskTab $active={taskTab === 'comentarios'} onClick={() => setTaskTab('comentarios')}>Comentários</TaskTab>
                  <TaskTab $active={taskTab === 'subtarefas'} onClick={() => setTaskTab('subtarefas')}>Subtarefas</TaskTab>
                  <TaskTab $active={taskTab === 'anexos'} onClick={() => setTaskTab('anexos')}>Anexos</TaskTab>
                </TaskTabs>

                {taskTab === 'comentarios' && <TaskFeed workItemId={selectedItem.id} />}

                {taskTab === 'descricao' && (<>
                {selectedItem.subtitle && (
                  <DetailSection>
                    <DetailLabel>Subtitulo</DetailLabel>
                    <DetailText>{selectedItem.subtitle}</DetailText>
                  </DetailSection>
                )}

                <DetailSection>
                  <DetailLabel>Descrição</DetailLabel>
                  <TaskDescriptionPanel
                    workItemId={selectedItem.id}
                    description={selectedItem.description}
                    onChanged={text => {
                      setSelectedItem(prev => prev ? { ...prev, description: text ?? undefined } : prev);
                      setWorkItems(items => items.map(w => w.id === selectedItem.id ? { ...w, description: text ?? undefined } : w));
                    }}
                  />
                </DetailSection>
                </>)}

                {taskTab === 'subtarefas' && (
                <DetailSection>
                  <SectionHeader>
                    <SectionTitle>
                      <CheckSquare size={16} />
                      <span>Subtarefas</span>
                    </SectionTitle>
                    <InlineForm onSubmit={handleCreateSubItem}>
                      <Input
                        type="text"
                        placeholder="Nova subtarefa"
                        value={newSubItemTitle}
                        onChange={event => setNewSubItemTitle(event.target.value)}
                      />
                      <SmallButton type="submit" disabled={!newSubItemTitle.trim()}>
                        <Plus size={14} />
                        <span>Criar</span>
                      </SmallButton>
                    </InlineForm>
                  </SectionHeader>

                  <ListPanel>
                    {selectedItemSubItems.length === 0 ? (
                      <ListItem>
                        <MutedText>Nenhuma subtarefa criada.</MutedText>
                      </ListItem>
                    ) : (
                      selectedItemSubItems.map(subItem => (
                        <ListItem key={subItem.id}>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                            <span>{subItem.title}</span>
                            {subItem.subtitle && <MutedText>{subItem.subtitle}</MutedText>}
                          </div>
                          <Tag $priority={subItem.priority}>
                            {subItem.priority === 2 ? 'Alta' : subItem.priority === 1 ? 'Media' : 'Baixa'}
                          </Tag>
                        </ListItem>
                      ))
                    )}
                  </ListPanel>
                </DetailSection>
                )}

                {taskTab === 'anexos' && (
                <DetailSection>
                  <SectionHeader>
                    <SectionTitle>
                      <Paperclip size={16} />
                      <span>Anexos</span>
                    </SectionTitle>
                    <FileInput
                      type="file"
                      onChange={handleUploadAttachment}
                      disabled={uploadingAttachment}
                    />
                  </SectionHeader>

                  <ListPanel>
                    {selectedItemAttachments.length === 0 ? (
                      <ListItem>
                        <MutedText>Nenhum anexo enviado.</MutedText>
                      </ListItem>
                    ) : (
                      selectedItemAttachments.map(attachment => (
                        <ListItem key={attachment.id}>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, minWidth: 0 }}>
                            <span style={{ overflowWrap: 'anywhere' }}>{attachment.fileName}</span>
                            <MutedText>{formatFileSize(attachment.fileSize)}</MutedText>
                          </div>
                          <SmallButton type="button" onClick={() => handleDownloadAttachment(attachment)}>
                            <Download size={14} />
                            <span>Baixar</span>
                          </SmallButton>
                        </ListItem>
                      ))
                    )}
                  </ListPanel>
                </DetailSection>
                )}
              </TaskMain>

              <TaskSidebar>
                <SidebarRow>
                  <SidebarLabel>Quadro</SidebarLabel>
                  <SidebarValue>{boardName}</SidebarValue>
                </SidebarRow>
                <SidebarRow>
                  <SidebarLabel>Etapa</SidebarLabel>
                  <SidebarValue>{stageName}</SidebarValue>
                </SidebarRow>
                <SidebarRow>
                  <SidebarLabel>Horas estimadas</SidebarLabel>
                  <SidebarValue>{selectedItem.estimatedHours ? `${selectedItem.estimatedHours}h` : '—'}</SidebarValue>
                </SidebarRow>
                <SidebarRow>
                  <SidebarLabel>Vencimento</SidebarLabel>
                  <SidebarValue>{selectedItem.dueDate || '—'}</SidebarValue>
                </SidebarRow>

                <TaskTaxonomyPanel
                  workItemId={selectedItem.id}
                  taskTypeId={selectedItem.taskTypeId}
                  points={selectedItem.points}
                  tagIds={(selectedItem.tags || []).map(t => t.id)}
                  showPoints={showStoryPoints}
                  onChanged={async () => {
                    if (!selectedBoardId) return;
                    const fresh = await api.getWorkItems(selectedBoardId);
                    setWorkItems(fresh);
                    const updated = fresh.find((w: WorkItem) => w.id === selectedItem.id);
                    if (updated) setSelectedItem(updated);
                  }}
                />

                <TaskApprovalPanel workItemId={selectedItem.id} users={assignableUsers} />

                <TaskChecklistPanel
                  workItemId={selectedItem.id}
                  onChanged={async () => {
                    if (!selectedBoardId) return;
                    const fresh = await api.getWorkItems(selectedBoardId);
                    setWorkItems(fresh);
                    const updated = fresh.find((w: WorkItem) => w.id === selectedItem.id);
                    if (updated) setSelectedItem(updated);
                  }}
                />

                <div style={{ padding: '14px 0', borderBottom: '1px solid #edf2f7' }}>
                  <SidebarLabel><Clock size={14} /> Tempo nesta tarefa</SidebarLabel>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6, fontWeight: 600, fontSize: 15 }}>
                    <span>
                      {formatTotalHours(totalSecs)}
                      {totalSecs !== userSecs ? ` (Você ${formatTotalHours(userSecs)})` : ''}
                    </span>
                    <span>{selectedItem.estimatedHours ? `${selectedItem.estimatedHours}h` : ''}</span>
                  </div>
                  <ProgressTrack><ProgressFill $pct={timePct} /></ProgressTrack>
                  {isCurrent && (
                    <div style={{ marginTop: 8, color: '#008ECF', fontWeight: 'bold' }}>
                      ● {formatTime(activeTime)} agora
                    </div>
                  )}
                  <InlineForm onSubmit={handleCreateManualTimeEntry} style={{ marginTop: 12 }}>
                    <Input
                      type="number"
                      min="1"
                      placeholder="Minutos"
                      value={manualMinutes || ''}
                      onChange={event => setManualMinutes(event.target.value ? Number(event.target.value) : undefined)}
                    />
                    <SmallButton type="submit" disabled={!manualMinutes || manualMinutes <= 0}>
                      <Plus size={14} />
                      <span>Lançar</span>
                    </SmallButton>
                  </InlineForm>
                </div>

                <DetailSection style={{ marginTop: 14 }}>
                  <SectionHeader>
                    <SectionTitle>
                      <Users size={16} />
                      <span>Responsáveis</span>
                    </SectionTitle>
                  </SectionHeader>
                  <InlineForm onSubmit={handleAssignUser}>
                    <Select
                      value={selectedAssigneeId}
                      onChange={event => setSelectedAssigneeId(event.target.value)}
                      disabled={assignableUsers.length === selectedItemAssignees.length}
                    >
                      <option value="">Selecionar usuario</option>
                      {assignableUsers
                        .filter(user => !selectedItemAssignees.some(assignee => assignee.id === user.id))
                        .map(user => (
                          <option key={user.id} value={user.id}>
                            {getUserLabel(user)}
                          </option>
                        ))}
                    </Select>
                    <SmallButton type="submit" disabled={!selectedAssigneeId}>
                      <UserPlus size={14} />
                      <span>Atribuir</span>
                    </SmallButton>
                  </InlineForm>
                  <ListPanel>
                    {selectedItemAssignees.length === 0 ? (
                      <ListItem>
                        <MutedText>Nenhum responsavel atribuido.</MutedText>
                      </ListItem>
                    ) : (
                      selectedItemAssignees.map(user => (
                        <ListItem key={user.id}>
                          <span>{getUserLabel(user)}</span>
                          <ActionIcon onClick={() => handleRemoveAssignee(user.id)} title="Remover">
                            <X size={14} />
                          </ActionIcon>
                        </ListItem>
                      ))
                    )}
                  </ListPanel>
                </DetailSection>
              </TaskSidebar>
            </TaskBody>
          </TaskModal>
        </ModalOverlay>
        );
      })()}

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
                          <button type="button" onClick={() => removeJustification(j.id)} style={{ color: '#CBD5E1' }}><X size={14} /></button>
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
