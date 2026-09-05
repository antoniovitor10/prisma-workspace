import { Filter, RotateCcw, Save, Search, SlidersHorizontal, X } from 'lucide-react';
import { useState } from 'react';
import styled from 'styled-components';
import type {
  FilterableWorkItem, KanbanCardSettings, KanbanFilterState, SavedFilterOption
} from './KanbanFilters';

/* ─── primitives ─── */

const Wrap = styled.section`
  display: grid;
  gap: 0;
  padding: 8px 12px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 10px;
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
`;

const PrimaryRow = styled.div`
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: nowrap;
`;

const SearchBox = styled.label`
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
  padding: 0 9px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 7px;
  background: ${({ theme }) => theme.color.neutral[50]};

  input {
    height: 32px;
    flex: 1;
    min-width: 0;
    border: 0;
    outline: 0;
    background: transparent;
    font-size: 13px;
    color: ${({ theme }) => theme.color.text};
  }

  &:focus-within {
    border-color: ${({ theme }) => theme.color.accentBlue};
  }
`;

const SearchClear = styled.button`
  display: grid;
  place-items: center;
  color: ${({ theme }) => theme.color.textMuted};
  &:hover { color: ${({ theme }) => theme.color.text}; }
`;

const Btn = styled.button<{ $active?: boolean; $primary?: boolean }>`
  height: 32px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 0 9px;
  border: 1px solid ${({ theme, $primary, $active }) =>
    $primary ? 'transparent' : $active ? theme.color.accentBlue : theme.color.border};
  border-radius: 7px;
  background: ${({ theme, $primary, $active }) =>
    $primary
      ? theme.color.brand
      : $active
      ? `color-mix(in srgb, ${theme.color.accentBlue} 10%, ${theme.color.surface})`
      : theme.color.surface};
  color: ${({ theme, $primary, $active }) =>
    $primary
      ? theme.color.onBrand
      : $active
      ? theme.color.accentBlue
      : theme.color.textMuted};
  font-size: 12px;
  font-weight: 800;
  white-space: nowrap;
  flex: 0 0 auto;
`;

const Select = styled.select`
  height: 32px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 7px;
  background: ${({ theme }) => theme.color.surface};
  font-size: 12px;
  color: ${({ theme }) => theme.color.text};
  flex: 0 0 auto;
`;

const Count = styled.span`
  font-size: 12px;
  color: ${({ theme }) => theme.color.textMuted};
  white-space: nowrap;
  margin-left: auto;
  flex: 0 0 auto;
`;

/* ─── expanded filter panel ─── */

const Panel = styled.div`
  padding-top: 10px;
  margin-top: 8px;
  border-top: 1px solid ${({ theme }) => theme.color.border};
  display: grid;
  gap: 8px;
`;

const PanelRow = styled.div`
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
`;

const PanelLabel = styled.span`
  font-size: 11px;
  font-weight: 800;
  color: ${({ theme }) => theme.color.textMuted};
  text-transform: uppercase;
  letter-spacing: .06em;
  white-space: nowrap;
`;

const PanelSelect = styled.select`
  height: 30px;
  max-width: 155px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 7px;
  background: ${({ theme }) => theme.color.surface};
  font-size: 12px;
  color: ${({ theme }) => theme.color.text};
`;

const PanelInput = styled.input`
  height: 30px;
  max-width: 140px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: 7px;
  background: ${({ theme }) => theme.color.surface};
  font-size: 12px;
  color: ${({ theme }) => theme.color.text};
  outline: none;
  &:focus { border-color: ${({ theme }) => theme.color.accentBlue}; }
`;

const CardSettingsDetails = styled.details`
  position: relative;
  summary {
    list-style: none;
    height: 30px;
    display: flex;
    align-items: center;
    gap: 5px;
    padding: 0 9px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: 7px;
    font-size: 12px;
    font-weight: 800;
    color: ${({ theme }) => theme.color.textMuted};
    cursor: pointer;
  }
  [data-popover] {
    position: absolute;
    z-index: 20;
    right: 0;
    top: 36px;
    width: 240px;
    padding: 12px;
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 9px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: 9px;
    background: ${({ theme }) => theme.color.surface};
    box-shadow: ${({ theme }) => theme.shadow.lg};
    label {
      display: flex;
      gap: 6px;
      align-items: center;
      font-size: 12px;
    }
  }
`;

/* ─── active filter count ─── */

function countActive(filter: KanbanFilterState): number {
  return [filter.search, filter.assigneeId, filter.teamId, filter.priority,
    filter.taskTypeId, filter.tagId, filter.due, filter.origin,
    filter.blocked, filter.requester, filter.customField,
  ].filter(Boolean).length;
}

/* ─── component ─── */

export function KanbanFilterBar({
  items, resultCount, users, filter, onFilter, groupBy, onGroupBy,
  sortBy, onSortBy, savedFilters, onApplySaved, onSaveFilter, onReset,
  cardSettings, onCardSettings, onSaveCardSettings, showPoints = true,
}: {
  items: FilterableWorkItem[];
  resultCount: number;
  users: { id: string; label: string }[];
  filter: KanbanFilterState;
  onFilter: (value: KanbanFilterState) => void;
  groupBy: string;
  onGroupBy: (value: string) => void;
  sortBy: string;
  onSortBy: (value: string) => void;
  savedFilters: SavedFilterOption[];
  onApplySaved: (filter: SavedFilterOption) => void;
  onSaveFilter: () => void;
  onReset: () => void;
  cardSettings: KanbanCardSettings;
  onCardSettings: (value: KanbanCardSettings) => void;
  onSaveCardSettings: () => void;
  showPoints?: boolean;
}) {
  const [panelOpen, setPanelOpen] = useState(false);

  const teams = [...new Set(items.map((x) => x.teamId).filter(Boolean))] as string[];
  const types = [
    ...new Map(
      items
        .filter((x) => x.taskTypeId)
        .map((x) => [
          x.taskTypeId!,
          { id: x.taskTypeId!, name: (x as FilterableWorkItem & { taskTypeName?: string }).taskTypeName ?? 'Tipo' },
        ]),
    ).values(),
  ];
  const tags = [...new Map(items.flatMap((x) => x.tags ?? []).map((x) => [x.id, x])).values()];
  const set = (patch: Partial<KanbanFilterState>) => onFilter({ ...filter, ...patch });

  const activeCount = countActive(filter);
  const hasFilters = activeCount > 0;

  const cardLabels: Record<string, string> = {
    showPriority: 'Prioridade', showDueDate: 'Prazo', showEstimate: 'Estimativa',
    showAssignees: 'Responsáveis', showType: 'Tipo', showTags: 'Tags',
    showPoints: 'Pontos', showChecklist: 'Checklist', showAttachments: 'Anexos', showTime: 'Tempo',
  };

  return (
    <Wrap aria-label="Filtros do Kanban">
      {/* ── primary compact row ── */}
      <PrimaryRow>
        <SearchBox aria-label="Buscar nos cartões">
          <Search size={12} />
          <input
            placeholder="Buscar nos cartões"
            value={filter.search}
            onChange={(e) => set({ search: e.target.value })}
          />
          {filter.search && (
            <SearchClear type="button" aria-label="Limpar busca" onClick={() => set({ search: '' })}>
              <X size={11} />
            </SearchClear>
          )}
        </SearchBox>

        <Btn
          $active={panelOpen || hasFilters}
          onClick={() => setPanelOpen((v) => !v)}
          aria-expanded={panelOpen}
          aria-controls="kanban-filter-panel"
        >
          <Filter size={12} />
          {activeCount > 0 ? `Filtros (${activeCount})` : 'Filtros'}
        </Btn>

        <Select
          aria-label="Ordenar cartões"
          value={sortBy}
          onChange={(e) => onSortBy(e.target.value)}
        >
          <option value="position">Ordem do quadro</option>
          <option value="priority">Prioridade</option>
          <option value="due">Prazo</option>
          <option value="title">Título</option>
          <option value="created">Mais recentes</option>
        </Select>

        <Count>{resultCount} de {items.length} cartão(ões)</Count>
      </PrimaryRow>

      {/* ── expanded filter panel ── */}
      {panelOpen && (
        <Panel id="kanban-filter-panel" role="region" aria-label="Painel de filtros">

          <PanelRow>
            <PanelLabel>Pessoas e equipes</PanelLabel>
            <PanelSelect
              value={filter.assigneeId}
              onChange={(e) => set({ assigneeId: e.target.value })}
              aria-label="Responsável"
            >
              <option value="">Responsável: todos</option>
              {users.map((u) => <option key={u.id} value={u.id}>{u.label}</option>)}
            </PanelSelect>
            <PanelSelect
              value={filter.teamId}
              onChange={(e) => set({ teamId: e.target.value })}
              aria-label="Equipe"
            >
              <option value="">Equipe: todas</option>
              {teams.map((t) => (
                <option key={t} value={t}>
                  {(items.find((x) => x.teamId === t) as FilterableWorkItem & { teamName?: string })?.teamName ?? t}
                </option>
              ))}
            </PanelSelect>
          </PanelRow>

          <PanelRow>
            <PanelLabel>Classificação</PanelLabel>
            <PanelSelect
              value={filter.priority}
              onChange={(e) => set({ priority: e.target.value })}
              aria-label="Prioridade"
            >
              <option value="">Prioridade: todas</option>
              <option value="3">Crítica</option>
              <option value="2">Alta</option>
              <option value="1">Média</option>
              <option value="0">Baixa</option>
            </PanelSelect>
            <PanelSelect
              value={filter.taskTypeId}
              onChange={(e) => set({ taskTypeId: e.target.value })}
              aria-label="Tipo"
            >
              <option value="">Tipo: todos</option>
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </PanelSelect>
            <PanelSelect
              value={filter.tagId}
              onChange={(e) => set({ tagId: e.target.value })}
              aria-label="Tag"
            >
              <option value="">Tag: todas</option>
              {tags.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </PanelSelect>
          </PanelRow>

          <PanelRow>
            <PanelLabel>Prazo e origem</PanelLabel>
            <PanelSelect
              value={filter.due}
              onChange={(e) => set({ due: e.target.value })}
              aria-label="Prazo"
            >
              <option value="">Prazo: todos</option>
              <option value="overdue">Atrasadas</option>
              <option value="today">Hoje</option>
              <option value="week">Próximos 7 dias</option>
              <option value="none">Sem prazo</option>
            </PanelSelect>
            <PanelSelect
              value={filter.origin}
              onChange={(e) => set({ origin: e.target.value })}
              aria-label="Origem"
            >
              <option value="">Origem: todas</option>
              <option value="1">Interna</option>
              <option value="2">Portal externo</option>
              <option value="3">Formulário</option>
              <option value="4">Integração</option>
              <option value="5">Importação</option>
            </PanelSelect>
            <PanelSelect
              value={filter.blocked}
              onChange={(e) => set({ blocked: e.target.value })}
              aria-label="Bloqueio"
            >
              <option value="">Bloqueio: todos</option>
              <option value="yes">Bloqueadas</option>
              <option value="no">Não bloqueadas</option>
            </PanelSelect>
          </PanelRow>

          <PanelRow>
            <PanelLabel>Outros</PanelLabel>
            <PanelInput
              placeholder="Solicitante"
              value={filter.requester}
              onChange={(e) => set({ requester: e.target.value })}
              aria-label="Solicitante"
            />
            <PanelInput
              placeholder="Campo personalizado"
              value={filter.customField}
              onChange={(e) => set({ customField: e.target.value })}
              aria-label="Campo personalizado"
            />
          </PanelRow>

          <PanelRow>
            <PanelLabel>Agrupamento</PanelLabel>
            <PanelSelect
              value={groupBy}
              onChange={(e) => onGroupBy(e.target.value)}
              aria-label="Agrupar cartões"
            >
              <option value="none">Sem agrupamento</option>
              <option value="responsible">Por responsável</option>
              <option value="priority">Por prioridade</option>
              <option value="type">Por tipo</option>
              <option value="team">Por equipe</option>
              <option value="origin">Por origem</option>
            </PanelSelect>

            <CardSettingsDetails>
              <summary><SlidersHorizontal size={12} />Conteúdo do cartão</summary>
              <div data-popover>
                {Object.entries(cardSettings)
                  .filter(([key]) => showPoints || key !== 'showPoints')
                  .map(([key, value]) => (
                    <label key={key}>
                      <input
                        type="checkbox"
                        checked={value}
                        onChange={(e) => onCardSettings({ ...cardSettings, [key]: e.target.checked })}
                      />
                      {cardLabels[key] ?? key}
                    </label>
                  ))}
                <Btn $primary onClick={onSaveCardSettings} style={{ gridColumn: '1/-1' }}>
                  <Save size={11} />Salvar visual
                </Btn>
              </div>
            </CardSettingsDetails>
          </PanelRow>

          <PanelRow>
            <PanelLabel>Filtros salvos</PanelLabel>
            <PanelSelect
              aria-label="Aplicar filtro salvo"
              defaultValue=""
              onChange={(e) => {
                const found = savedFilters.find((x) => x.id === e.target.value);
                if (found) onApplySaved(found);
              }}
            >
              <option value="">Selecionar filtro salvo...</option>
              {savedFilters.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </PanelSelect>
            <Btn onClick={onSaveFilter}><Save size={11} />Salvar filtro atual</Btn>
            {hasFilters && (
              <Btn onClick={() => { onReset(); setPanelOpen(false); }}>
                <RotateCcw size={11} />Limpar filtros
              </Btn>
            )}
          </PanelRow>
        </Panel>
      )}
    </Wrap>
  );
}
