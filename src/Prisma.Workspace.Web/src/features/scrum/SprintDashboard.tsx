import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Dialog from '@radix-ui/react-dialog';
import {
  AlertCircle,
  BarChart3,
  Ban,
  CalendarDays,
  CheckCircle2,
  Clock3,
  Columns3,
  Gauge,
  History,
  ListTree,
  LoaderCircle,
  Pencil,
  Plus,
  Target,
  Trash2,
  Users,
  X,
} from 'lucide-react';
import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import styled, { useTheme } from 'styled-components';
import { z } from 'zod';
import { TaskDetailDrawer } from '../../components/TaskDetailDrawer';
import { previewBacklog, previewMode, previewSprints, previewTeams } from '../../preview';
import { api } from '../../services/api';
import type { BacklogItem, Sprint, Team, TeamMember } from '../../types/scrum';
import { buildBurndown, buildVelocity, calculateNetCapacity, calculateSprintProgress } from '../../utils/scrumAnalytics';
import type { ProjectSummary } from '../../pages/Projects';

const SprintBacklogPanel = lazy(() => import('./SprintBacklogPanel'));
const SprintHistoryPanel = lazy(() => import('./SprintHistoryPanel'));
const SprintKanbanBoard = lazy(() => import('./SprintKanbanBoard'));

const Page = styled.section`
  padding: 20px 28px 36px;
  @media (max-width: 760px) { padding: 16px; }
`;

const Toolbar = styled.div`
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 14px;
  margin-bottom: 14px;

  h2 { font-size: 18px; }
  p { margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
`;

const Primary = styled.button`
  display: inline-flex;
  min-height: 36px;
  align-items: center;
  gap: 6px;
  padding: 0 12px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.brand};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 13.5px;
  font-weight: 800;
  &:disabled { cursor: not-allowed; opacity: .55; }
`;

const Secondary = styled.button`
  display: inline-flex;
  min-height: 34px;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.surface};
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 13px;
  font-weight: 750;
  &:hover { background: ${({ theme }) => theme.color.neutral[50]}; }
  &:disabled { cursor: not-allowed; opacity: .55; }
`;

const SprintRail = styled.div`
  display: flex;
  gap: 8px;
  margin-bottom: 14px;
  overflow-x: auto;
  padding-bottom: 3px;
`;

const SprintTab = styled.button<{ $active: boolean }>`
  min-width: 190px;
  padding: 11px 12px;
  border: 1px solid ${({ $active, theme }) => $active ? theme.color.brand : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ $active, theme }) => $active ? `color-mix(in srgb, ${theme.color.brand} 5%, ${theme.color.surface})` : theme.color.surface};
  color: ${({ theme }) => theme.color.text};
  text-align: left;

  strong, small { display: block; }
  strong { font-size: 13.5px; }
  small { margin-top: 4px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const StatusDot = styled.span<{ $status: number }>`
  display: inline-block;
  width: 7px;
  height: 7px;
  margin-right: 6px;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ $status, theme }) => $status === 2 ? theme.color.success : $status === 3 ? theme.color.neutral[400] : $status === 4 ? theme.color.danger : theme.color.warning};
`;

const GoalCard = styled.section`
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 18px;
  align-items: center;
  padding: 18px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};

  h3 { display: flex; align-items: center; gap: 7px; color: ${({ theme }) => theme.color.brand}; font-size: 14px; }
  p { margin-top: 6px; color: ${({ theme }) => theme.color.neutral[700]}; font-size: 15px; font-weight: 650; }
  @media (max-width: 620px) { grid-template-columns: 1fr; }
`;

const GoalProgress = styled.div`
  min-width: 190px;
  small { display: flex; justify-content: space-between; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const GoalSide = styled.div`
  display: grid;
  gap: 9px;
  min-width: 210px;
  > div:last-child { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
  @media (max-width: 620px) { min-width: 0; > div:last-child { justify-content: flex-start; } }
`;

const ProgressTrack = styled.div`
  height: 7px;
  margin-top: 7px;
  overflow: hidden;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[100]};
`;

const ProgressFill = styled.div<{ $value: number }>`
  width: ${({ $value }) => Math.max(0, Math.min(100, $value))}%;
  height: 100%;
  border-radius: inherit;
  background: ${({ theme }) => theme.color.success};
`;

const KpiGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(5, minmax(140px, 1fr));
  gap: 10px;
  margin-top: 12px;
  @media (max-width: 940px) { grid-template-columns: repeat(2, 1fr); }
  @media (max-width: 520px) { grid-template-columns: 1fr; }
`;

const Kpi = styled.article`
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr);
  gap: 10px;
  align-items: center;
  padding: 14px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};

  > span {
    display: grid;
    width: 34px;
    height: 34px;
    place-items: center;
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.neutral[100]};
    color: ${({ theme }) => theme.color.brand};
  }
  small, strong { display: block; }
  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 700; text-transform: uppercase; }
  strong { margin-top: 3px; color: ${({ theme }) => theme.color.text}; font-size: 18px; }
`;

const ViewTabs = styled.div`
  display: flex;
  gap: 5px;
  margin-top: 12px;
  overflow-x: auto;
  padding: 4px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.neutral[50]};
`;

const ViewTab = styled.button<{ $active: boolean }>`
  display: inline-flex;
  min-height: 34px;
  flex: 1 0 auto;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 0 12px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ $active, theme }) => $active ? theme.color.surface : 'transparent'};
  color: ${({ $active, theme }) => $active ? theme.color.brand : theme.color.textMuted};
  font-size: 13px;
  font-weight: 800;
  box-shadow: ${({ $active, theme }) => $active ? theme.shadow.sm : 'none'};
`;

const AnalyticsGrid = styled.div`
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 12px;
  @media (max-width: 980px) { grid-template-columns: 1fr; }
`;

const Panel = styled.section`
  min-width: 0;
  padding: 16px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};

  > header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    margin-bottom: 14px;
  }
  h3 { display: flex; align-items: center; gap: 7px; font-size: 14px; }
  header p { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const ChartFrame = styled.div`
  width: 100%;
  height: 230px;
  font-size: 12px;
`;

const CapacityTable = styled.div`
  overflow: hidden;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
`;

const CapacityRow = styled.div`
  display: grid;
  grid-template-columns: minmax(130px, 1fr) 86px 86px minmax(100px, 1fr) 64px;
  align-items: center;
  min-height: 48px;
  border-bottom: 1px solid ${({ theme }) => theme.color.neutral[100]};
  color: ${({ theme }) => theme.color.neutral[700]};
  font-size: 13px;

  &:last-child { border-bottom: 0; }
  > * { min-width: 0; padding: 7px 9px; }
  input {
    width: 100%;
    min-height: 30px;
    padding: 0 6px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.sm};
    font-size: 13px;
  }
  @media (max-width: 720px) { grid-template-columns: minmax(120px, 1fr) 70px 70px; > *:nth-child(4), > *:nth-child(5) { display: none; } }
`;

const CapacityHead = styled(CapacityRow)`
  min-height: 34px;
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 11px;
  font-weight: 800;
  letter-spacing: .04em;
  text-transform: uppercase;
`;

const Person = styled.div`
  display: flex;
  align-items: center;
  gap: 7px;
  font-weight: 700;
  span { display: grid; width: 26px; height: 26px; place-items: center; border-radius: ${({ theme }) => theme.radius.pill}; background: ${({ theme }) => theme.color.brand}; color: ${({ theme }) => theme.color.onBrand}; font-size: 11px; }
`;

const LoadBar = styled.div<{ $ratio: number }>`
  height: 6px;
  overflow: hidden;
  border-radius: ${({ theme }) => theme.radius.pill};
  background: ${({ theme }) => theme.color.neutral[100]};
  &::after { content: ''; display: block; width: ${({ $ratio }) => Math.min(100, Math.max(0, $ratio * 100))}%; height: 100%; background: ${({ $ratio, theme }) => $ratio > 1 ? theme.color.danger : theme.color.success}; }
`;

const ErrorBanner = styled.div`
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
  padding: 10px 12px;
  border-left: 3px solid ${({ theme }) => theme.color.danger};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.danger} 6%, ${theme.color.surface})`};
  color: ${({ theme }) => theme.color.danger};
  font-size: 13px;
  font-weight: 700;
`;

const Overlay = styled(Dialog.Overlay)`
  position: fixed;
  inset: 0;
  z-index: 55;
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.neutral[900]} 38%, transparent)`};
`;

const DialogContent = styled(Dialog.Content)`
  position: fixed;
  top: 50%;
  left: 50%;
  z-index: 56;
  width: min(500px, 94vw);
  transform: translate(-50%, -50%);
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  outline: none;
`;

const DialogHeader = styled.header`
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 18px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  h2 { font-size: 16px; }
`;

const Close = styled(Dialog.Close)`
  display: grid;
  width: 32px;
  height: 32px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
`;

const SprintForm = styled.form`
  display: grid;
  gap: 10px;
  padding: 18px;
  input, textarea, select {
    width: 100%;
    min-height: 38px;
    padding: 0 10px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 13.5px;
  }
  textarea { min-height: 80px; padding-block: 9px; resize: vertical; }
  label { display: grid; gap: 5px; color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; font-weight: 700; }
  footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 4px; }
`;

const DateGrid = styled.div`display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px;`;
const FormError = styled.p`color: ${({ theme }) => theme.color.danger}; font-size: 13px;`;
const Empty = styled.div`padding: 40px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; text-align: center;`;

const sprintSchema = z.object({
  teamId: z.string().optional(),
  name: z.string().trim().min(1, 'Informe o nome.').max(200),
  goal: z.string().max(1000).optional(),
  startDate: z.string().min(1, 'Informe o início.'),
  endDate: z.string().min(1, 'Informe o fim.'),
}).refine((data) => data.endDate >= data.startDate, { message: 'O fim deve ser posterior ao início.', path: ['endDate'] });

type SprintFormData = z.infer<typeof sprintSchema>;
type SprintView = 'planning' | 'board' | 'metrics' | 'history';

const initials = (name: string) => name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase();
const formatDate = (value: string) => new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(new Date(`${value}T12:00:00`));
const sprintStatusNames: Record<number, string> = { 1: 'Planejada', 2: 'Ativa', 3: 'Concluída', 4: 'Cancelada' };

interface CapacityEditorProps {
  sprint: Sprint;
  member: TeamMember;
  workload: number;
  readOnly: boolean;
  onSave: (userId: string, availableHours: number, daysOffHours: number) => Promise<void>;
}

function CapacityEditor({ sprint, member, workload, readOnly, onSave }: CapacityEditorProps) {
  const capacity = sprint.capacities.find((item) => item.userId === member.userId);
  const [available, setAvailable] = useState(capacity?.availableHours ?? member.weeklyCapacityHours * 2);
  const [daysOff, setDaysOff] = useState(capacity?.daysOffHours ?? 0);
  const net = Math.max(0, available - daysOff);

  useEffect(() => {
    setAvailable(capacity?.availableHours ?? member.weeklyCapacityHours * 2);
    setDaysOff(capacity?.daysOffHours ?? 0);
  }, [capacity?.availableHours, capacity?.daysOffHours, member.weeklyCapacityHours, sprint.id]);

  return (
    <CapacityRow>
      <Person><span>{initials(member.name)}</span>{member.name}</Person>
      <input disabled={readOnly} aria-label={`Capacidade de ${member.name}`} type="number" min="0" max="500" value={available} onChange={(event) => setAvailable(Number(event.target.value))} />
      <input disabled={readOnly} aria-label={`Ausências de ${member.name}`} type="number" min="0" max={available} value={daysOff} onChange={(event) => setDaysOff(Number(event.target.value))} />
      <LoadBar $ratio={net > 0 ? workload / net : workload > 0 ? 2 : 0} title={`${workload}h alocadas de ${net}h líquidas`} />
      <Secondary disabled={readOnly} onClick={() => onSave(member.userId, available, daysOff)}>Salvar</Secondary>
    </CapacityRow>
  );
}

interface SprintDashboardProps { project: ProjectSummary; }

export function SprintDashboard({ project }: SprintDashboardProps) {
  const showStoryPoints = project.methodology !== 1;
  const theme = useTheme();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [selectedSprintId, setSelectedSprintId] = useState('');
  const [view, setView] = useState<SprintView>('planning');
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [lifecycleStatus, setLifecycleStatus] = useState<3 | 4 | null>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [incompleteItemsAction, setIncompleteItemsAction] = useState(1);
  const [targetSprintId, setTargetSprintId] = useState('');
  const [editForm, setEditForm] = useState({ name: '', goal: '', startDate: '', endDate: '' });
  const [selectedItem, setSelectedItem] = useState<BacklogItem | null>(null);
  const [error, setError] = useState('');
  const sprintQuery = useQuery<Sprint[]>({
    queryKey: ['project-sprints', project.id],
    retry: false,
    queryFn: async () => {
      try { return await api.getProjectSprints(project.id); }
      catch { return previewMode ? previewSprints : []; }
    },
  });
  const backlogQuery = useQuery<BacklogItem[]>({
    queryKey: ['project-backlog', project.id],
    retry: false,
    queryFn: async () => {
      try { return await api.getProjectBacklog(project.id); }
      catch { return previewMode ? previewBacklog : []; }
    },
  });
  const teamsQuery = useQuery<Team[]>({
    queryKey: ['teams'],
    retry: false,
    queryFn: async () => {
      try { return await api.getTeams(); }
      catch { return previewMode ? previewTeams : []; }
    },
  });

  useEffect(() => {
    if (selectedSprintId || !sprintQuery.data?.length) return;
    setSelectedSprintId(sprintQuery.data.find((sprint) => sprint.status === 2)?.id ?? sprintQuery.data[0].id);
  }, [selectedSprintId, sprintQuery.data]);

  const selectedSprint = sprintQuery.data?.find((sprint) => sprint.id === selectedSprintId);
  const sprintItems = useMemo(() => (backlogQuery.data ?? []).filter((item) => item.sprintId === selectedSprintId), [backlogQuery.data, selectedSprintId]);
  const unfinishedItemCount = sprintItems.filter((item) => !item.completedAt).length;
  const selectedTeam = teamsQuery.data?.find((team) => team.id === selectedSprint?.teamId);
  const progress = selectedSprint ? selectedSprint.progressPercentage ?? calculateSprintProgress(selectedSprint) : 0;
  const burndown = selectedSprint ? buildBurndown(selectedSprint, backlogQuery.data ?? []) : [];
  const velocity = buildVelocity(sprintQuery.data ?? []);
  const netCapacity = selectedSprint ? selectedSprint.capacityHours ?? calculateNetCapacity(selectedSprint) : 0;
  const statusMutation = useMutation({
    mutationFn: ({ sprintId, status, action = 1, targetId }: { sprintId: string; status: number; action?: number; targetId?: string }) => previewMode ? Promise.resolve() : api.changeSprintStatus(sprintId, status, action, targetId || null),
    onMutate: () => setError(''),
    onSuccess: async (_, variables) => {
      queryClient.setQueryData<Sprint[]>(['project-sprints', project.id], (current = []) => current.map((sprint) => sprint.id === variables.sprintId ? { ...sprint, status: variables.status } : sprint));
      setLifecycleStatus(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['project-sprints', project.id] }),
        queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] }),
      ]);
    },
    onError: (caught) => setError(caught instanceof Error ? caught.message : 'Não foi possível alterar a sprint.'),
  });

  // Excluir remove só o vínculo das tarefas: elas voltam ao Product Backlog no mesmo
  // quadro, coluna e posição (SPEC-S-003 v3).
  const deleteMutation = useMutation({
    mutationFn: (sprintId: string) => previewMode ? Promise.resolve() : api.deleteSprint(sprintId),
    onMutate: () => setError(''),
    onSuccess: async () => {
      setDeleteOpen(false);
      setSelectedSprintId('');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['project-sprints', project.id] }),
        queryClient.invalidateQueries({ queryKey: ['project-backlog', project.id] }),
      ]);
    },
    onError: (caught) => setError(caught instanceof Error ? caught.message : 'Não foi possível excluir a sprint.'),
  });

  const saveCapacity = async (userId: string, availableHours: number, daysOffHours: number) => {
    if (!selectedSprint) return;
    setError('');
    try {
      if (!previewMode) await api.setSprintCapacity(selectedSprint.id, userId, availableHours, daysOffHours);
      queryClient.setQueryData<Sprint[]>(['project-sprints', project.id], (current = []) => current.map((sprint) => sprint.id !== selectedSprint.id ? sprint : {
        ...sprint,
        capacities: [...sprint.capacities.filter((capacity) => capacity.userId !== userId), { userId, availableHours, daysOffHours }],
      }));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Não foi possível salvar a capacidade.');
    }
  };

  const { register, handleSubmit, reset, formState: { errors }, setValue } = useForm<SprintFormData>({
    resolver: zodResolver(sprintSchema),
    defaultValues: { teamId: project.teams[0]?.id ?? '', name: '', goal: '', startDate: '', endDate: '' },
  });
  useEffect(() => { if (project.teams[0]) setValue('teamId', project.teams[0].id); }, [project.teams, setValue]);
  const createMutation = useMutation({
    mutationFn: (data: SprintFormData) => previewMode ? Promise.resolve(`preview-sprint-${Date.now()}`) : api.createSprint(project.id, data),
    onSuccess: async () => {
      reset({ teamId: project.teams[0]?.id ?? '', name: '', goal: '', startDate: '', endDate: '' });
      setCreateOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['project-sprints', project.id] });
    },
    onError: (caught) => setError(caught instanceof Error ? caught.message : 'Não foi possível criar a sprint.'),
  });
  const updateMutation = useMutation({
    mutationFn: () => {
      if (!selectedSprint) return Promise.resolve();
      return previewMode ? Promise.resolve() : api.updateSprint(selectedSprint.id, editForm);
    },
    onSuccess: async () => {
      setEditOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['project-sprints', project.id] });
    },
    onError: (caught) => setError(caught instanceof Error ? caught.message : 'Não foi possível editar a sprint.'),
  });

  const openEdit = () => {
    if (!selectedSprint) return;
    setEditForm({
      name: selectedSprint.name,
      goal: selectedSprint.goal ?? '',
      startDate: selectedSprint.startDate,
      endDate: selectedSprint.endDate,
    });
    setEditOpen(true);
  };

  const openLifecycle = (status: 3 | 4) => {
    setIncompleteItemsAction(1);
    setTargetSprintId('');
    setLifecycleStatus(status);
  };

  if (sprintQuery.isLoading) return <Page><Empty>Carregando sprints...</Empty></Page>;

  return (
    <Page>
      <Toolbar>
        <div><h2>Sprints</h2><p>Meta, capacidade e entrega do time em uma única visão.</p></div>
        <Primary onClick={() => setCreateOpen(true)}><Plus size={14} />Nova sprint</Primary>
      </Toolbar>
      {error && <ErrorBanner role="alert"><AlertCircle size={14} />{error}</ErrorBanner>}
      {!sprintQuery.data?.length && <Empty>Nenhuma sprint planejada para este projeto.</Empty>}
      <SprintRail>
        {(sprintQuery.data ?? []).map((sprint) => (
          <SprintTab key={sprint.id} $active={sprint.id === selectedSprintId} onClick={() => setSelectedSprintId(sprint.id)}>
            <strong><StatusDot $status={sprint.status} />{sprint.name}</strong>
            <small>{sprintStatusNames[sprint.status]} · {formatDate(sprint.startDate)} — {formatDate(sprint.endDate)} · {sprint.teamName}</small>
          </SprintTab>
        ))}
      </SprintRail>
      {selectedSprint && (
        <>
          <GoalCard>
            <div><h3><Target size={15} />Meta da sprint</h3><p>{selectedSprint.goal || 'Meta ainda não definida.'}</p></div>
            <GoalSide>
              <GoalProgress><small><span>Progresso da meta</span><b>{progress}%</b></small><ProgressTrack><ProgressFill $value={progress} /></ProgressTrack></GoalProgress>
              <div>
                {/* D84: não existe "Iniciar sprint". O estado Ativa vem das datas. */}
                {selectedSprint.status < 3 && <Secondary onClick={openEdit}><Pencil size={13} />Editar</Secondary>}
                {selectedSprint.status === 2 && <Secondary disabled={statusMutation.isPending} onClick={() => openLifecycle(3)}><CheckCircle2 size={13} />Concluir sprint</Secondary>}
                {selectedSprint.status < 3 && <Secondary disabled={statusMutation.isPending} onClick={() => openLifecycle(4)}><Ban size={13} />Cancelar sprint</Secondary>}
                <Secondary disabled={deleteMutation.isPending} onClick={() => setDeleteOpen(true)}><Trash2 size={13} />Excluir sprint</Secondary>
              </div>
            </GoalSide>
          </GoalCard>
          <KpiGrid>
            <Kpi><span><CheckCircle2 size={17} /></span><div><small>Itens concluídos</small><strong>{selectedSprint.completedItemCount ?? 0}/{selectedSprint.itemCount}</strong></div></Kpi>
            {showStoryPoints&&<Kpi><span><BarChart3 size={17} /></span><div><small>Pontos entregues</small><strong>{selectedSprint.completedStoryPoints ?? 0}/{selectedSprint.storyPoints}</strong></div></Kpi>}
            <Kpi><span><CalendarDays size={17} /></span><div><small>Horas previstas</small><strong>{selectedSprint.plannedHours ?? sprintItems.reduce((sum, item) => sum + (item.estimatedHours ?? 0), 0)}h</strong></div></Kpi>
            <Kpi><span><Clock3 size={17} /></span><div><small>Trabalho restante</small><strong>{selectedSprint.remainingHours ?? sprintItems.reduce((sum, item) => sum + (item.remainingHours ?? 0), 0)}h</strong></div></Kpi>
            <Kpi><span><Gauge size={17} /></span><div><small>Capacidade líquida</small><strong>{netCapacity}h</strong></div></Kpi>
          </KpiGrid>
          <ViewTabs role="tablist" aria-label="Visões da sprint">
            <ViewTab role="tab" aria-selected={view === 'planning'} $active={view === 'planning'} onClick={() => setView('planning')}><ListTree size={13} />Planejamento</ViewTab>
            <ViewTab role="tab" aria-selected={view === 'board'} $active={view === 'board'} onClick={() => setView('board')}><Columns3 size={13} />Quadro</ViewTab>
            <ViewTab role="tab" aria-selected={view === 'metrics'} $active={view === 'metrics'} onClick={() => setView('metrics')}><BarChart3 size={13} />Métricas</ViewTab>
            <ViewTab role="tab" aria-selected={view === 'history'} $active={view === 'history'} onClick={() => setView('history')}><History size={13} />Histórico</ViewTab>
          </ViewTabs>

          {view === 'planning' && (
            <>
              <Suspense fallback={<Empty>Carregando Sprint Backlog...</Empty>}>
                <SprintBacklogPanel
                  sprint={selectedSprint}
                  items={sprintItems}
                  allItems={backlogQuery.data ?? []}
                  projectKey={project.key}
                  showPoints={showStoryPoints}
                  onOpenItem={setSelectedItem}
                  onPlan={() => navigate(`/projects/${project.id}/backlog`)}
                />
              </Suspense>
              <Panel style={{ marginTop: 12 }}>
                <header>
                  <div><h3><Users size={15} />Capacidade da equipe</h3><p>Horas disponíveis, ausências e carga planejada.</p></div>
                </header>
                <CapacityTable>
                  <CapacityHead><span>Pessoa</span><span>Disponível</span><span>Ausências</span><span>Alocação</span><span>Ação</span></CapacityHead>
                  {(selectedTeam?.members ?? []).map((member) => (
                    <CapacityEditor
                      key={member.userId}
                      sprint={selectedSprint}
                      member={member}
                      workload={sprintItems.filter((item) => item.assigneeIds?.includes(member.userId)).reduce((sum, item) => sum + (item.remainingHours ?? 0), 0)}
                      readOnly={selectedSprint.status >= 3}
                      onSave={saveCapacity}
                    />
                  ))}
                  {!selectedTeam?.members.length && <Empty>Nenhum membro encontrado para o time da sprint.</Empty>}
                </CapacityTable>
              </Panel>
            </>
          )}

          {view === 'board' && (
            <Suspense fallback={<Empty>Carregando quadro da sprint...</Empty>}>
              <SprintKanbanBoard
                projectId={project.id}
                sprint={selectedSprint}
                items={sprintItems}
                onOpenItem={setSelectedItem}
                onError={setError}
                showPoints={showStoryPoints}
              />
            </Suspense>
          )}

          {view === 'metrics' && (
            <AnalyticsGrid>
              {showStoryPoints&&<Panel>
                <header><div><h3><BarChart3 size={15} />Burndown</h3><p>Trabalho ideal e restante ao longo da sprint.</p></div></header>
                <ChartFrame>
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={burndown} margin={{ top: 8, right: 12, bottom: 4, left: -20 }}>
                      <CartesianGrid stroke={theme.color.neutral[100]} vertical={false} />
                      <XAxis dataKey="label" tick={{ fill: theme.color.textMuted, fontSize: 12 }} axisLine={false} tickLine={false} />
                      <YAxis tick={{ fill: theme.color.textMuted, fontSize: 12 }} axisLine={false} tickLine={false} />
                      <Tooltip contentStyle={{ border: `1px solid ${theme.color.border}`, borderRadius: theme.radius.md, fontSize: 13 }} />
                      <Legend wrapperStyle={{ fontSize: 12 }} />
                      <Line name="Ideal" type="monotone" dataKey="ideal" stroke={theme.color.neutral[400]} strokeDasharray="5 4" dot={false} strokeWidth={2} />
                      <Line name="Restante" type="stepAfter" dataKey="actual" stroke={theme.color.brand} dot={{ r: 2 }} strokeWidth={2} connectNulls={false} />
                    </LineChart>
                  </ResponsiveContainer>
                </ChartFrame>
              </Panel>}
              <Panel>
                <header><div><h3><Gauge size={15} />Velocity</h3><p>Planejado versus entregue nas últimas sprints.</p></div></header>
                <ChartFrame>
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={velocity} margin={{ top: 8, right: 12, bottom: 4, left: -20 }}>
                      <CartesianGrid stroke={theme.color.neutral[100]} vertical={false} />
                      <XAxis dataKey="name" tick={{ fill: theme.color.textMuted, fontSize: 12 }} axisLine={false} tickLine={false} />
                      <YAxis tick={{ fill: theme.color.textMuted, fontSize: 12 }} axisLine={false} tickLine={false} />
                      <Tooltip contentStyle={{ border: `1px solid ${theme.color.border}`, borderRadius: theme.radius.md, fontSize: 13 }} />
                      <Legend wrapperStyle={{ fontSize: 12 }} />
                      <Bar name="Planejado" dataKey="planned" fill={theme.color.neutral[300]} radius={[3, 3, 0, 0]} />
                      <Bar name="Entregue" dataKey="delivered" fill={theme.color.accentGreen} radius={[3, 3, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </ChartFrame>
              </Panel>
            </AnalyticsGrid>
          )}

          {view === 'history' && (
            <Suspense fallback={<Empty>Carregando histórico...</Empty>}>
              <SprintHistoryPanel
                sprints={sprintQuery.data ?? []}
                selectedSprintId={selectedSprint.id}
                onSelectSprint={setSelectedSprintId}
                showPoints={showStoryPoints}
              />
            </Suspense>
          )}

          <TaskDetailDrawer item={selectedItem} projectKey={project.key} sprintName={selectedSprint.name} onOpenChange={(open) => { if (!open) setSelectedItem(null); }} onOpenSubtask={(workItemId) => { void api.getWorkItemDetails(workItemId).then((details) => setSelectedItem(details as unknown as BacklogItem)); }} />
        </>
      )}
      <Dialog.Root open={createOpen} onOpenChange={setCreateOpen}>
        <Dialog.Portal>
          <Overlay />
          <DialogContent aria-describedby={undefined}>
            <DialogHeader><Dialog.Title asChild><h2>Planejar nova sprint</h2></Dialog.Title><Close aria-label="Fechar"><X size={17} /></Close></DialogHeader>
            <SprintForm onSubmit={handleSubmit((data) => createMutation.mutate(data))}>
              <label>Equipe (opcional)<select {...register('teamId')}><option value="">Sem equipe</option>{project.teams.map((team) => <option key={team.id} value={team.id}>{team.name}</option>)}</select></label>
              <label>Nome<input placeholder="Ex.: Sprint 26" {...register('name')} /></label>
              <label>Meta<textarea placeholder="Qual resultado a equipe pretende alcançar?" {...register('goal')} /></label>
              <DateGrid><label>Início<input type="date" {...register('startDate')} /></label><label>Fim<input type="date" {...register('endDate')} /></label></DateGrid>
              {(errors.teamId || errors.name || errors.startDate || errors.endDate) && <FormError>{errors.teamId?.message || errors.name?.message || errors.startDate?.message || errors.endDate?.message}</FormError>}
              <footer><Secondary type="button" onClick={() => setCreateOpen(false)}>Cancelar</Secondary><Primary type="submit" disabled={createMutation.isPending}>{createMutation.isPending ? <LoaderCircle size={14} /> : <CalendarDays size={14} />}Criar sprint</Primary></footer>
            </SprintForm>
          </DialogContent>
        </Dialog.Portal>
      </Dialog.Root>
      <Dialog.Root open={editOpen} onOpenChange={setEditOpen}>
        <Dialog.Portal>
          <Overlay />
          <DialogContent aria-describedby={undefined}>
            <DialogHeader><Dialog.Title asChild><h2>Editar sprint</h2></Dialog.Title><Close aria-label="Fechar"><X size={17} /></Close></DialogHeader>
            <SprintForm onSubmit={(event) => {
              event.preventDefault();
              if (!editForm.name.trim() || !editForm.startDate || editForm.endDate < editForm.startDate) return;
              updateMutation.mutate();
            }}>
              <label>Nome<input required maxLength={200} value={editForm.name} onChange={(event) => setEditForm({ ...editForm, name: event.target.value })} /></label>
              <label>Meta<textarea maxLength={1000} value={editForm.goal} onChange={(event) => setEditForm({ ...editForm, goal: event.target.value })} /></label>
              <DateGrid><label>Início<input required type="date" value={editForm.startDate} onChange={(event) => setEditForm({ ...editForm, startDate: event.target.value })} /></label><label>Fim<input required type="date" min={editForm.startDate} value={editForm.endDate} onChange={(event) => setEditForm({ ...editForm, endDate: event.target.value })} /></label></DateGrid>
              <footer><Secondary type="button" onClick={() => setEditOpen(false)}>Voltar</Secondary><Primary type="submit" disabled={updateMutation.isPending}><Pencil size={13} />Salvar sprint</Primary></footer>
            </SprintForm>
          </DialogContent>
        </Dialog.Portal>
      </Dialog.Root>
      <Dialog.Root open={lifecycleStatus !== null} onOpenChange={(open) => { if (!open) setLifecycleStatus(null); }}>
        <Dialog.Portal>
          <Overlay />
          <DialogContent aria-describedby={undefined}>
            <DialogHeader><Dialog.Title asChild><h2>{lifecycleStatus === 3 ? 'Concluir sprint' : 'Cancelar sprint'}</h2></Dialog.Title><Close aria-label="Fechar"><X size={17} /></Close></DialogHeader>
            <SprintForm onSubmit={(event) => {
              event.preventDefault();
              if (!selectedSprint || lifecycleStatus === null) return;
              statusMutation.mutate({
                sprintId: selectedSprint.id,
                status: lifecycleStatus,
                action: incompleteItemsAction,
                targetId: incompleteItemsAction === 2 ? targetSprintId : undefined,
              });
            }}>
              <p>{unfinishedItemCount === 0
                ? 'Todos os itens estão concluídos. O escopo e as métricas serão preservados no histórico.'
                : `${unfinishedItemCount} item(ns) não concluído(s) precisam de um destino.`}</p>
              {unfinishedItemCount > 0 && <>
                <label><span><input type="radio" checked={incompleteItemsAction === 1} onChange={() => setIncompleteItemsAction(1)} /> Retornar ao Product Backlog</span></label>
                <label><span><input type="radio" checked={incompleteItemsAction === 2} onChange={() => setIncompleteItemsAction(2)} /> Enviar para outra sprint</span></label>
                {incompleteItemsAction === 2 && <label>Sprint de destino<select required value={targetSprintId} onChange={(event) => setTargetSprintId(event.target.value)}><option value="">Selecione...</option>{(sprintQuery.data ?? []).filter((sprint) => sprint.id !== selectedSprint?.id && sprint.status < 3).map((sprint) => <option key={sprint.id} value={sprint.id}>{sprint.name}</option>)}</select></label>}
              </>}
              <footer><Secondary type="button" onClick={() => setLifecycleStatus(null)}>Voltar</Secondary><Primary type="submit" disabled={statusMutation.isPending || (unfinishedItemCount > 0 && incompleteItemsAction === 2 && !targetSprintId)}>{lifecycleStatus === 3 ? <CheckCircle2 size={13} /> : <Ban size={13} />}{lifecycleStatus === 3 ? 'Concluir' : 'Cancelar'}</Primary></footer>
            </SprintForm>
          </DialogContent>
        </Dialog.Portal>
      </Dialog.Root>
      <Dialog.Root open={deleteOpen} onOpenChange={setDeleteOpen}>
        <Dialog.Portal>
          <Overlay />
          <DialogContent aria-describedby={undefined}>
            <DialogHeader><Dialog.Title asChild><h2>Excluir sprint</h2></Dialog.Title><Close aria-label="Fechar"><X size={17} /></Close></DialogHeader>
            <SprintForm onSubmit={(event) => {
              event.preventDefault();
              if (selectedSprint) deleteMutation.mutate(selectedSprint.id);
            }}>
              <p>
                A sprint <strong>{selectedSprint?.name}</strong> será excluída.
                {(selectedSprint?.itemCount ?? 0) > 0
                  ? ` As ${selectedSprint?.itemCount} tarefa(s) vinculadas voltam ao Product Backlog, no mesmo quadro e coluna. Nenhuma tarefa é excluída ou arquivada.`
                  : ' Nenhuma tarefa está vinculada a ela.'}
              </p>
              <footer>
                <Secondary type="button" onClick={() => setDeleteOpen(false)}>Voltar</Secondary>
                <Primary type="submit" disabled={deleteMutation.isPending}><Trash2 size={13} />Excluir</Primary>
              </footer>
            </SprintForm>
          </DialogContent>
        </Dialog.Portal>
      </Dialog.Root>
    </Page>
  );
}
