import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Dialog from '@radix-ui/react-dialog';
import { ArrowRight, BarChart3, CalendarRange, FileText, FolderKanban, Inbox, ListChecks, LoaderCircle, Plus, Search, UserRound, Users, X } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import styled from 'styled-components';
import { z } from 'zod';
import { previewMode, previewProject } from '../preview';
import { api } from '../services/api';
import type { ProjectSummary } from '../pages/Projects';
import { searchPlatform } from '../features/search/services';
import type { GlobalSearchHit, GlobalSearchResponse } from '../features/search/types';

const Overlay = styled(Dialog.Overlay)`
  position: fixed;
  inset: 0;
  z-index: 50;
  background: ${({ theme }) => `color-mix(in srgb, ${theme.color.neutral[900]} 42%, transparent)`};
`;

const Palette = styled(Dialog.Content)`
  position: fixed;
  top: 14vh;
  left: 50%;
  z-index: 51;
  width: min(660px, 94vw);
  max-height: min(650px, 76vh);
  overflow: hidden;
  transform: translateX(-50%);
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  outline: none;
`;

const SearchField = styled.div`
  display: flex;
  align-items: center;
  gap: 10px;
  height: 58px;
  padding: 0 16px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};
  color: ${({ theme }) => theme.color.textMuted};

  input {
    min-width: 0;
    flex: 1;
    border: 0;
    outline: 0;
    background: transparent;
    color: ${({ theme }) => theme.color.text};
    font-size: 15px;
  }
`;

const KeyHint = styled.kbd`
  padding: 3px 6px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.sm};
  background: ${({ theme }) => theme.color.neutral[50]};
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 13px;
  font-weight: 700;
`;

const Results = styled.div`
  max-height: calc(76vh - 58px);
  overflow-y: auto;
  padding: 10px;
`;

const ResultGroup = styled.section`
  & + & { margin-top: 10px; }

  h3 {
    padding: 7px 9px;
    color: ${({ theme }) => theme.color.textMuted};
    font-size: 13px;
    font-weight: 800;
    letter-spacing: .06em;
    text-transform: uppercase;
  }
`;

const ResultButton = styled.button`
  display: grid;
  width: 100%;
  grid-template-columns: 34px minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  min-height: 52px;
  padding: 6px 9px;
  border-radius: ${({ theme }) => theme.radius.md};
  text-align: left;

  &:hover,
  &:focus-visible {
    background: ${({ theme }) => theme.color.neutral[100]};
    outline: none;
  }

  > span:first-child {
    display: grid;
    width: 32px;
    height: 32px;
    place-items: center;
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.neutral[50]};
    color: ${({ theme }) => theme.color.brand};
  }

  strong,
  small { display: block; }
  strong { color: ${({ theme }) => theme.color.text}; font-size: 14px; }
  small { margin-top: 3px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
  > svg { color: ${({ theme }) => theme.color.textMuted}; }
`;

const Empty = styled.div`
  display: grid;
  min-height: 170px;
  place-items: center;
  padding: 24px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 14px;
  text-align: center;
`;

interface GlobalSearchDialogProps {
  open: boolean;
  currentProjectId?: string;
  onOpenChange: (open: boolean) => void;
  onCreateTask: () => void;
}

export function GlobalSearchDialog({ open, currentProjectId, onOpenChange, onCreateTask }: GlobalSearchDialogProps) {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [debounced,setDebounced]=useState('');
  useEffect(()=>{const id=window.setTimeout(()=>setDebounced(search.trim()),250);return()=>window.clearTimeout(id);},[search]);
  const { data, isLoading } = useQuery<GlobalSearchResponse>({
    queryKey: ['global-search',debounced],
    enabled: open&&debounced.length>=2&&!previewMode,
    retry: false,
    staleTime: 30_000,
    queryFn:()=>searchPlatform(debounced),
  });

  useEffect(() => { if (!open) setSearch(''); }, [open]);

  const go = (path: string) => {
    onOpenChange(false);
    navigate(path);
  };

  const createTask=()=>{onOpenChange(false);onCreateTask();};
  const groupConfig:Array<[GlobalSearchHit['kind'],string,typeof FileText]>=[
    ['tasks','Tarefas',FileText],['requests','Solicitações',Inbox],['projects','Projetos',FolderKanban],
    ['users','Usuários',UserRound],['teams','Equipes',Users],['sprints','Sprints',CalendarRange],
    ['wiki','Wiki',FileText],
  ];
  const commands=[
    {label:'Criar tarefa',detail:'Abrir criação rápida',icon:Plus,run:createTask},
    {label:'Criar projeto',detail:'Cadastrar um novo projeto',icon:FolderKanban,run:()=>go('/projects?create=1')},
    {label:'Abrir backlog',detail:currentProjectId?'Projeto atual':'Selecionar projeto',icon:ListChecks,run:()=>go(currentProjectId?`/projects/${currentProjectId}/backlog`:'/projects')},
    {label:'Abrir sprint',detail:currentProjectId?'Projeto atual':'Selecionar projeto',icon:CalendarRange,run:()=>go(currentProjectId?`/projects/${currentProjectId}/sprints`:'/projects')},
    {label:'Abrir solicitações',detail:'Fila de triagem e atendimento',icon:Inbox,run:()=>go('/requests')},
    {label:'Abrir relatórios',detail:'Relatórios e construtor',icon:BarChart3,run:()=>go('/reports')},
  ];

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Overlay />
        <Palette aria-describedby={undefined}>
          <Dialog.Title style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0 0 0 0)' }}>
            Pesquisa global
          </Dialog.Title>
          <SearchField>
            {isLoading ? <LoaderCircle size={18} /> : <Search size={18} />}
            <input
              autoFocus
              aria-label="Pesquisar em toda a plataforma"
              placeholder="Busque projetos, tarefas e protocolos..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <KeyHint>ESC</KeyHint>
          </SearchField>
          <Results>
            {debounced.length>=2&&groupConfig.map(([kind,label,Icon])=>Boolean(data?.groups[kind]?.length)&&<ResultGroup key={kind}>
              <h3>{label}</h3>{data!.groups[kind]!.map(item=><ResultButton key={`${kind}-${item.id}`} onClick={()=>go(item.path)}>
                <span><Icon size={16}/></span><span><strong>{item.title}</strong><small>{item.subtitle}</small></span><ArrowRight size={14}/>
              </ResultButton>)}
            </ResultGroup>)}
            {debounced.length>=2&&!isLoading&&data?.total===0&&<Empty>Nenhum resultado encontrado.<br/>Tente outro termo.</Empty>}
            {debounced.length<2&&<Empty>Digite ao menos 2 caracteres ou escolha um comando rápido.</Empty>}
            <ResultGroup><h3>Comandos rápidos</h3>{commands.map(command=><ResultButton key={command.label} onClick={command.run}>
              <span><command.icon size={16}/></span><span><strong>{command.label}</strong><small>{command.detail}</small></span><ArrowRight size={14}/>
            </ResultButton>)}</ResultGroup>
          </Results>
        </Palette>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

const QuickDialog = styled(Dialog.Content)`
  position: fixed;
  top: 22vh;
  left: 50%;
  z-index: 51;
  width: min(560px, 94vw);
  transform: translateX(-50%);
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.lg};
  outline: none;
`;

const QuickHeader = styled.header`
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 18px;
  border-bottom: 1px solid ${({ theme }) => theme.color.border};

  h2 { font-size: 16px; }
  p { margin-top: 2px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
`;

const CloseButton = styled(Dialog.Close)`
  display: grid;
  width: 32px;
  height: 32px;
  place-items: center;
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.textMuted};
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
`;

const QuickForm = styled.form`
  padding: 18px;

  > input,
  select {
    width: 100%;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
  }

  > input {
    min-height: 48px;
    padding: 0 13px;
    font-size: 15px;
    outline: none;
    &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; }
  }

  select { min-height: 36px; padding: 0 10px; font-size: 13.5px; }
`;

const ContextRow = styled.div`
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 8px;
  margin-top: 10px;

  > div { min-width: 0; }

  @media (max-width: 520px) {
    grid-template-columns: 1fr;
  }
`;

const ErrorText = styled.p`
  margin-top: 8px;
  color: ${({ theme }) => theme.color.danger};
  font-size: 13.5px;
`;

const QuickFooter = styled.footer`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 16px;

  small { color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; }
`;

const CreateButton = styled.button`
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 36px;
  padding: 0 13px;
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme }) => theme.color.brand};
  color: ${({ theme }) => theme.color.onBrand};
  font-size: 13.5px;
  font-weight: 800;
  &:disabled { cursor: not-allowed; opacity: .55; }
`;

const BoardCheckboxList = styled.div`
  max-height: 120px;
  overflow-y: auto;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  padding: 5px;
  display: flex;
  flex-direction: column;
  gap: 2px;
`;

const BoardCheckboxItem = styled.label`
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 7px;
  border-radius: ${({ theme }) => theme.radius.sm};
  font-size: 13.5px;
  cursor: pointer;
  color: ${({ theme }) => theme.color.text};
  &:hover { background: ${({ theme }) => theme.color.neutral[100]}; }
  span { min-width: 0; overflow-wrap: anywhere; }
  input {
    width: 16px;
    height: 16px;
    min-height: 16px;
    flex: 0 0 16px;
    margin: 0;
    padding: 0;
    cursor: pointer;
    accent-color: ${({ theme }) => theme.color.brand};
  }
`;

const FieldLabel = styled.p`
  margin-bottom: 5px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 700;
  letter-spacing: .04em;
  text-transform: uppercase;
`;

const quickItemSchema = z.object({
  title: z.string().trim().min(1, 'Digite um título.').max(500, 'Use no máximo 500 caracteres.'),
  projectId: z.string().min(1, 'Selecione um projeto.'),
  boardIds: z.array(z.string()).min(1, 'Selecione pelo menos um quadro.'),
});

type QuickItemForm = z.infer<typeof quickItemSchema>;

interface QuickCreateDialogProps {
  open: boolean;
  currentProjectId?: string;
  onOpenChange: (open: boolean) => void;
  onCreated: (message: string) => void;
}

export function QuickCreateDialog({ open, currentProjectId, onOpenChange, onCreated }: QuickCreateDialogProps) {
  const queryClient = useQueryClient();
  const { data: projects = [] } = useQuery<ProjectSummary[]>({
    queryKey: ['projects'],
    enabled: open,
    retry: false,
    queryFn: async () => {
      try { return await api.getProjects(); }
      catch { return previewMode ? [previewProject] : []; }
    },
  });
  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm<QuickItemForm>({
    resolver: zodResolver(quickItemSchema),
    defaultValues: { title: '', projectId: currentProjectId ?? '', boardIds: [] },
  });
  const projectId = watch('projectId');
  const title = watch('title') ?? '';
  const boardIds = watch('boardIds') ?? [];
  const selectedProject = useMemo(() => projects.find((project) => project.id === projectId), [projectId, projects]);

  const getDefaultBoardId = (project: ProjectSummary | undefined) =>
    project?.defaultBoardId ?? project?.boards[0]?.id ?? '';

  useEffect(() => {
    if (!open || projects.length === 0) return;
    const nextProject = projects.find((project) => project.id === currentProjectId) ?? projects[0];
    setValue('projectId', nextProject.id);
    const defaultId = getDefaultBoardId(nextProject);
    setValue('boardIds', defaultId ? [defaultId] : []);
  }, [currentProjectId, open, projects, setValue]);

  const updateProject = (nextProjectId: string) => {
    const project = projects.find((candidate) => candidate.id === nextProjectId);
    setValue('projectId', nextProjectId);
    const defaultId = getDefaultBoardId(project);
    setValue('boardIds', defaultId ? [defaultId] : [], { shouldValidate: true });
  };

  const toggleBoard = (boardId: string) => {
    const next = boardIds.includes(boardId)
      ? boardIds.filter((id) => id !== boardId)
      : [...boardIds, boardId];
    setValue('boardIds', next, { shouldValidate: true });
  };

  const mutation = useMutation({
    mutationFn: async (data: QuickItemForm) => {
      if (previewMode) return `preview-${Date.now()}`;
      return api.createWorkItem({
        boardId: data.boardIds[0],
        boardIds: data.boardIds,
        projectId: data.projectId,
        title: data.title,
        priority: 1,
        position: Date.now(),
      });
    },
    onSuccess: async () => {
      const title = watch('title');
      const currentProjectId2 = watch('projectId');
      await queryClient.invalidateQueries({ queryKey: ['project-backlog', currentProjectId2] });
      await queryClient.invalidateQueries({ queryKey: ['workItems'] });
      const defaultId = getDefaultBoardId(selectedProject);
      reset({ title: '', projectId: currentProjectId2, boardIds: defaultId ? [defaultId] : [] });
      onOpenChange(false);
      onCreated(`"${title}" foi adicionado ao quadro.`);
    },
  });

  const canSubmit = !mutation.isPending && title.trim().length > 0 && projects.length > 0 && boardIds.length > 0;

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Overlay />
        <QuickDialog aria-describedby={undefined}>
          <QuickHeader>
            <div><Dialog.Title asChild><h2>Criar item rapidamente</h2></Dialog.Title><p>Só o título é necessário para começar.</p></div>
            <CloseButton aria-label="Fechar"><X size={17} /></CloseButton>
          </QuickHeader>
          <QuickForm onSubmit={handleSubmit((data) => mutation.mutate(data))}>
            <input autoFocus aria-label="Título da tarefa" placeholder="O que precisa ser feito?" {...register('title')} />
            <ContextRow>
              <div>
                <FieldLabel>Projeto</FieldLabel>
                <select aria-label="Projeto" value={projectId} style={{ width: '100%', minHeight: 36, padding: '0 10px', fontSize: 13.5 }} onChange={(event) => updateProject(event.target.value)}>
                  {projects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
                </select>
              </div>
              <div>
                <FieldLabel>Quadros</FieldLabel>
                {(selectedProject?.boards ?? []).length === 0 ? (
                  <p style={{ fontSize: 13, color: '#94A3B8', padding: '8px 4px' }}>Nenhum quadro disponível.</p>
                ) : (
                  <BoardCheckboxList>
                    {(selectedProject?.boards ?? []).map((board) => (
                      <BoardCheckboxItem key={board.id} htmlFor={`qb-${board.id}`}>
                        <input
                          id={`qb-${board.id}`}
                          type="checkbox"
                          checked={boardIds.includes(board.id)}
                          onChange={() => toggleBoard(board.id)}
                        />
                        <span>{board.name}</span>
                      </BoardCheckboxItem>
                    ))}
                  </BoardCheckboxList>
                )}
              </div>
            </ContextRow>
            {(errors.title || errors.projectId || errors.boardIds || mutation.error) && (
              <ErrorText>
                {errors.title?.message
                  || errors.projectId?.message
                  || errors.boardIds?.message
                  || (mutation.error as Error | null)?.message}
              </ErrorText>
            )}
            <QuickFooter>
              <small>Os demais campos podem ser preenchidos no detalhe.</small>
              <CreateButton type="submit" disabled={!canSubmit}>
                {mutation.isPending ? <LoaderCircle size={14} /> : <Plus size={14} />}Criar item
              </CreateButton>
            </QuickFooter>
          </QuickForm>
        </QuickDialog>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
