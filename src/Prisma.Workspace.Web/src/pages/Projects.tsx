import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import styled from 'styled-components';
import { Archive, ArrowRight, FolderKanban, Plus, Search, Users } from 'lucide-react';
import { api } from '../services/api';
import { previewMode, previewProject } from '../preview';
import { useOrganization } from '../features/organizations/OrganizationState';
import {
  HeaderActions,
  HeaderLead,
  Page,
  PageHeader,
  PrimaryAction,
  SearchControl,
  ToolbarSurface,
} from '../components/PageLayout';
import { projectClassificationLabel, workNatureOptions, workTypeOptions } from './projectClassification';

export interface ProjectSummary {
  id: string; key: string; name: string; description?: string;
  ownerId?: string; startDate?: string; dueDate?: string; status?: number; methodology?: number;
  nature?: number; workType?: number;
  isArchived?: boolean; createdAt?: string; updatedAt?: string; settingsJson?: string;
  defaultBoardId?: string;
  boards: Array<{ id: string; name: string; teamId?: string }>;
  teams: Array<{ id: string; name: string }>;
  members?: Array<{ userId: string; role: number }>;
  tags?: Array<{ id: string; name: string; color: string }>;
  customFields?: Array<{
    id: string; name: string; type: number; isRequired: boolean;
    optionsJson?: string; position: number; isActive: boolean;
  }>;
}

/* Cartao compacto a pedido dos devs: cabem mais projetos na primeira tela sem precisar
   rolar. O que sai e espaco vazio, nao informacao — nome, situacao, descricao,
   classificacao e contagens continuam todos ali. */
const Grid = styled.div`display: grid; grid-template-columns: repeat(auto-fill, minmax(234px, 1fr)); gap: 11px;`;
const Card = styled.button`
  position: relative; min-height: 118px; overflow: hidden; text-align: left; padding: 13px 14px 11px;
  border: 1px solid ${({ theme }) => theme.color.border}; border-radius: ${({ theme }) => theme.radius.card};
  background: ${({ theme }) => theme.color.surface}; color: ${({ theme }) => theme.color.text};
  box-shadow: ${({ theme }) => theme.shadow.sm}; transition: .16s;
  &::before { content:''; position:absolute; inset:0 auto 0 0; width:3px; background:${({ theme }) => theme.color.gradient}; opacity:.75; }
  &:hover { border-color: ${({ theme }) => theme.color.borderStrong}; transform: translateY(-2px); box-shadow: ${({ theme }) => theme.shadow.md}; }
`;
const CardTitle = styled.div`display:flex;align-items:flex-start;justify-content:space-between;gap:7px;margin-bottom:6px;h2{font-size:14px;line-height:1.3;letter-spacing:-.01em;}`;
const Meta = styled.div`display:flex;gap:12px;margin-top:11px;padding-top:8px;border-top:1px solid ${({ theme }) => theme.color.border};color:${({ theme }) => theme.color.textMuted};font-size:12px;span{display:flex;align-items:center;gap:5px;}`;
const Classifications = styled.div`display:flex;flex-wrap:wrap;gap:5px;margin:8px 0 2px;`;
const Classification = styled.span`padding:3px 7px;border:1px solid color-mix(in srgb, ${({ theme }) => theme.color.brand} 22%, ${({ theme }) => theme.color.border});border-radius:999px;background:color-mix(in srgb, ${({ theme }) => theme.color.brand} 7%, ${({ theme }) => theme.color.surface});color:${({ theme }) => theme.color.textMuted};font-size:11px;font-weight:750;`;
const Empty = styled.div`grid-column:1/-1;padding:64px 24px;text-align:center;border:1px dashed ${({ theme }) => theme.color.borderStrong};border-radius:${({ theme }) => theme.radius.card};background:${({ theme }) => theme.color.surface};color:${({ theme }) => theme.color.textMuted};`;
const Overlay = styled.div`position: fixed; inset: 0; z-index: 20; display: grid; place-items: center; background: rgba(15,23,42,.35);`;
const Dialog = styled.form`width: min(480px, 92vw); display: grid; gap: 14px; padding: 24px; border-radius: 10px; background: ${({ theme }) => theme.color.surface}; box-shadow: ${({ theme }) => theme.shadow.lg}; input, textarea, select { width: 100%; padding: 10px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 6px; background: ${({ theme }) => theme.color.bg}; color: ${({ theme }) => theme.color.text}; } label { display:grid; gap:5px; color:${({ theme }) => theme.color.textMuted}; font-size:13.5px; font-weight:700; } textarea { min-height: 90px; resize: vertical; } footer { display: flex; justify-content: flex-end; gap: 8px; }`;
const Secondary = styled.button`padding: 9px 14px; border: 1px solid ${({ theme }) => theme.color.border}; border-radius: 6px; color: ${({ theme }) => theme.color.text};`;
const ErrorText = styled.div`color: ${({ theme }) => theme.color.danger}; font-size: 14px;`;
const Filter = styled.label`display:inline-flex;min-height:40px;align-items:center;gap:7px;padding:0 8px;color:${({ theme }) => theme.color.textMuted};font-size:13px;white-space:nowrap;input{accent-color:${({ theme }) => theme.color.brand};}`;
const FilterSelect = styled.select`min-height:36px;padding:0 28px 0 10px;border:1px solid ${({ theme }) => theme.color.border};border-radius:${({ theme }) => theme.radius.md};background:${({ theme }) => theme.color.surface};color:${({ theme }) => theme.color.text};font:inherit;font-size:12.5px;`;
const Badge = styled.span<{ $archived?: boolean }>`display: inline-flex; align-items: center; gap: 4px; padding: 2px 7px; border-radius: 999px; background: ${({ theme, $archived }) => $archived ? theme.color.neutral[100] : `color-mix(in srgb, ${theme.color.success} 14%, white)`}; color: ${({ theme, $archived }) => $archived ? theme.color.textMuted : theme.color.success}; font-size: 11px; font-weight: 800;`;
const Description = styled.p`margin: 0; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; line-height: 1.4; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;`;

export function Projects() {
  const navigate = useNavigate();
  const { current } = useOrganization();
  const [searchParams, setSearchParams] = useSearchParams();
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [query, setQuery] = useState('');
  const [natureFilter, setNatureFilter] = useState(0);
  const [workTypeFilter, setWorkTypeFilter] = useState(0);
  const [open, setOpen] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({ key: '', name: '', description: '', nature: 0, workType: 0, startDate: '', dueDate: '' });

  useEffect(() => {
    let active = true;
    setLoading(true);
    api.getProjects(includeArchived)
      .then((result) => { if (active) setProjects(result); })
      .catch(() => { if (active && previewMode) setProjects([previewProject]); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [current.id, includeArchived]);

  useEffect(() => {
    if (searchParams.get('create') !== '1') return;
    setOpen(true);
    const next = new URLSearchParams(searchParams);
    next.delete('create');
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return projects.filter((project) => {
      if (natureFilter && project.nature !== natureFilter) return false;
      if (workTypeFilter && project.workType !== workTypeFilter) return false;
      if (!q) return true;
      return project.name.toLowerCase().includes(q)
        || (project.description ?? '').toLowerCase().includes(q)
        || projectClassificationLabel(workNatureOptions, project.nature).toLowerCase().includes(q)
        || projectClassificationLabel(workTypeOptions, project.workType).toLowerCase().includes(q);
    });
  }, [projects, query, natureFilter, workTypeFilter]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    try {
      const id = await api.createProject({
        ...form,
        methodology: 1,
        startDate: form.startDate || undefined,
        dueDate: form.dueDate || undefined,
      });
      setOpen(false);
      navigate(`/projects/${id}/backlog`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível criar o projeto.');
    }
  };

  return (
    <Page>
      <PageHeader>
        <HeaderLead>
          <span className="eyebrow">Portfólio de trabalho</span>
          <h1>Projetos</h1>
          <p>Centralize objetivos, equipes e todas as visões de execução em um só lugar.</p>
        </HeaderLead>
        <HeaderActions>
          <ToolbarSurface>
          <SearchControl>
            <Search size={14} />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Buscar projetos"
              aria-label="Buscar projetos"
            />
          </SearchControl>
          <Filter>
            <input type="checkbox" checked={includeArchived} onChange={(event) => setIncludeArchived(event.target.checked)} />
            Exibir arquivados
          </Filter>
          <FilterSelect aria-label="Filtrar por natureza" value={natureFilter} onChange={(event) => setNatureFilter(Number(event.target.value))}>
            <option value={0}>Todas as naturezas</option>
            {workNatureOptions.map(([id, label]) => <option key={id} value={id}>{label}</option>)}
          </FilterSelect>
          <FilterSelect aria-label="Filtrar por tipo de trabalho" value={workTypeFilter} onChange={(event) => setWorkTypeFilter(Number(event.target.value))}>
            <option value={0}>Todos os tipos</option>
            {workTypeOptions.map(([id, label]) => <option key={id} value={id}>{label}</option>)}
          </FilterSelect>
          </ToolbarSurface>
          <PrimaryAction onClick={() => setOpen(true)}><Plus size={16} />Novo projeto</PrimaryAction>
        </HeaderActions>
      </PageHeader>
      <Grid>
        {!loading && filtered.length === 0 && <Empty>Nenhum projeto disponível.</Empty>}
        {filtered.map((project) => (
          <Card key={project.id} onClick={() => navigate(`/projects/${project.id}/backlog`)}>
            <CardTitle>
              <h2>{project.name}</h2>
              {project.isArchived
                ? <Badge $archived><Archive size={10} />Arquivado</Badge>
                : <Badge>Ativo</Badge>}
            </CardTitle>
            <Description>{project.description || 'Sem descrição.'}</Description>
            <Classifications>
              <Classification>{projectClassificationLabel(workNatureOptions, project.nature)}</Classification>
              <Classification>{projectClassificationLabel(workTypeOptions, project.workType)}</Classification>
            </Classifications>
            <Meta>
              <span><FolderKanban size={13} />{project.boards.length} quadro(s)</span>
              <span><Users size={13} />{project.teams.length} time(s)</span>
              <ArrowRight size={14} style={{ marginLeft: 'auto' }} />
            </Meta>
          </Card>
        ))}
      </Grid>
      {open && (
        <Overlay onMouseDown={() => setOpen(false)}>
          <Dialog onSubmit={submit} onMouseDown={(e) => e.stopPropagation()}>
            <h2>Novo projeto</h2>
            <input required maxLength={200} placeholder="Nome do projeto" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <textarea placeholder="Objetivo e contexto" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            <label>Natureza<select required aria-label="Natureza" value={form.nature} onChange={(e) => setForm({ ...form, nature: Number(e.target.value) })}><option value={0} disabled>Selecione a natureza</option>{workNatureOptions.map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
            <label>Tipo de Trabalho<select required aria-label="Tipo de Trabalho" value={form.workType} onChange={(e) => setForm({ ...form, workType: Number(e.target.value) })}><option value={0} disabled>Selecione o tipo</option>{workTypeOptions.map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
            <label>Data de início<input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></label>
            <label>Prazo<input type="date" min={form.startDate || undefined} value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} /></label>
            {error && <ErrorText>{error}</ErrorText>}
            <footer>
              <Secondary type="button" onClick={() => setOpen(false)}>Cancelar</Secondary>
              <PrimaryAction type="submit">Criar projeto</PrimaryAction>
            </footer>
          </Dialog>
        </Overlay>
      )}
    </Page>
  );
}
