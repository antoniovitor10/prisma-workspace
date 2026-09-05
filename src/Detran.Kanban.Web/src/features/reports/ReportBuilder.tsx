import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Copy, Download, Eye, Plus, Save, Trash2 } from 'lucide-react';
import styled from 'styled-components';
import { api } from '../../services/api';
import type {
  ReportCatalog,
  ReportDefinition,
  ReportExecution,
  ReportFilter,
  ReportMetric,
  SavedReport,
} from '../../types/reports';

const Layout = styled.div`
  display: grid;
  grid-template-columns: minmax(250px, 310px) minmax(0, 1fr);
  gap: 12px;
  @media (max-width: 940px) { grid-template-columns: 1fr; }
`;

const Panel = styled.section`
  min-width: 0;
  padding: 15px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.lg};
  background: ${({ theme }) => theme.color.surface};
  box-shadow: ${({ theme }) => theme.shadow.sm};
  h3 { font-size: 15px; }
  > p { margin-top: 5px; color: ${({ theme }) => theme.color.textMuted}; font-size: 13px; line-height: 1.5; }
`;

const Field = styled.label`
  display: grid;
  gap: 5px;
  margin-top: 12px;
  color: ${({ theme }) => theme.color.textMuted};
  font-size: 12px;
  font-weight: 750;
  input, select {
    width: 100%;
    min-height: 34px;
    padding: 0 9px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.md};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font: inherit;
    font-size: 13px;
  }
`;

const Inline = styled.div`
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
`;

const Columns = styled.div`
  display: grid;
  max-height: 210px;
  gap: 5px;
  overflow: auto;
  margin-top: 6px;
  padding: 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  label { display: flex; align-items: center; gap: 7px; color: ${({ theme }) => theme.color.text}; font-size: 13px; }
`;

const SectionTitle = styled.div`
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-top: 15px;
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
  font-weight: 800;
`;

const Mini = styled.button`
  display: inline-flex;
  min-height: 28px;
  align-items: center;
  gap: 4px;
  padding: 0 8px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  color: ${({ theme }) => theme.color.text};
  font-size: 12px;
  font-weight: 750;
`;

const RowEditor = styled.div`
  display: grid;
  grid-template-columns: minmax(90px, 1fr) minmax(78px, .75fr) minmax(75px, 1fr) minmax(75px, .8fr) auto;
  gap: 5px;
  margin-top: 6px;
  select, input {
    min-width: 0;
    min-height: 31px;
    padding: 0 7px;
    border: 1px solid ${({ theme }) => theme.color.border};
    border-radius: ${({ theme }) => theme.radius.sm};
    background: ${({ theme }) => theme.color.surface};
    color: ${({ theme }) => theme.color.text};
    font-size: 12px;
  }
  button { color: ${({ theme }) => theme.color.danger}; }
  @media (max-width: 560px) { grid-template-columns: 1fr 1fr; }
`;

const Toggle = styled.label`
  display: flex;
  align-items: center;
  gap: 7px;
  margin-top: 12px;
  color: ${({ theme }) => theme.color.text};
  font-size: 13px;
`;

const Actions = styled.div`
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
  margin-top: 14px;
`;

const Action = styled.button<{ $primary?: boolean }>`
  display: inline-flex;
  min-height: 34px;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  border: 1px solid ${({ theme, $primary }) => $primary ? theme.color.brand : theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  background: ${({ theme, $primary }) => $primary ? theme.color.brand : 'transparent'};
  color: ${({ theme, $primary }) => $primary ? theme.color.onBrand : theme.color.text};
  font-size: 13px;
  font-weight: 800;
  &:disabled { opacity: .5; }
`;

const Message = styled.p<{ $error?: boolean }>`
  margin-top: 10px;
  color: ${({ theme, $error }) => $error ? theme.color.danger : theme.color.textMuted} !important;
  font-size: 13px !important;
`;

const SavedList = styled.div`display: grid; gap: 7px; margin-top: 10px;`;
const Saved = styled.article`
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 8px;
  padding: 10px;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  b { display: block; overflow: hidden; font-size: 13px; text-overflow: ellipsis; white-space: nowrap; }
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; }
  div:last-child { display: flex; gap: 5px; }
  button { color: ${({ theme }) => theme.color.textMuted}; &:hover { color: ${({ theme }) => theme.color.brand}; }
`;

const ResultHeader = styled.div`
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 12px;
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 12px; }
`;

const TableWrap = styled.div`
  overflow: auto;
  border: 1px solid ${({ theme }) => theme.color.border};
  border-radius: ${({ theme }) => theme.radius.md};
  table { width: 100%; border-collapse: collapse; font-size: 12px; }
  th, td { min-width: 105px; padding: 8px; border-bottom: 1px solid ${({ theme }) => theme.color.border}; text-align: left; }
  th { position: sticky; top: 0; background: ${({ theme }) => theme.color.neutral[50]}; color: ${({ theme }) => theme.color.textMuted}; }
`;

const ResultBars = styled.div`display: grid; gap: 8px;`;
const ResultBar = styled.div`
  display: grid;
  grid-template-columns: minmax(90px, 150px) 1fr 55px;
  gap: 8px;
  align-items: center;
  font-size: 12px;
  > span:first-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  i { display: block; height: 8px; border-radius: 9px; background: ${({ theme }) => theme.color.accentBlue}; }
  b { text-align: right; }
`;
const ColumnChart = styled.div`
  display: flex;
  min-height: 240px;
  align-items: end;
  gap: 8px;
  overflow-x: auto;
  padding-top: 20px;
  article { display: grid; width: 52px; flex: 1 0 44px; justify-items: center; gap: 5px; }
  i { display: block; width: min(30px, 70%); min-height: 3px; border-radius: 5px 5px 0 0; background: ${({ theme }) => theme.color.accentBlue}; }
  b { font-size: 11px; }
  span { width: 100%; overflow: hidden; color: ${({ theme }) => theme.color.textMuted}; font-size: 11px; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
`;
const ResultLine = styled.svg`
  width: 100%;
  height: 240px;
  overflow: visible;
  polyline { fill: none; stroke: ${({ theme }) => theme.color.accentBlue}; stroke-width: 3; vector-effect: non-scaling-stroke; }
  circle { fill: ${({ theme }) => theme.color.surface}; stroke: ${({ theme }) => theme.color.accentBlue}; stroke-width: 2; vector-effect: non-scaling-stroke; }
`;
const PieLayout = styled.div`
  display: grid;
  grid-template-columns: minmax(160px, 220px) 1fr;
  align-items: center;
  gap: 20px;
  @media (max-width: 620px) { grid-template-columns: 1fr; justify-items: center; }
`;
const Pie = styled.div<{ $gradient:string; $donut:boolean }>`
  position: relative;
  width: 190px;
  aspect-ratio: 1;
  border-radius: 50%;
  background: ${({ $gradient }) => $gradient};
  &::after { content: ''; position: absolute; inset: ${({ $donut }) => $donut ? '28%' : '50%'}; border-radius: 50%; background: ${({ theme }) => theme.color.surface}; }
`;
const Legend = styled.div`
  display: grid;
  gap: 7px;
  div { display: grid; grid-template-columns: 9px 1fr auto; align-items: center; gap: 6px; font-size: 12px; }
  i { width: 8px; height: 8px; border-radius: 2px; }
  span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
`;

const visualizationNames: Record<number, string> = {
  1: 'Tabela', 2: 'Indicador', 3: 'Barras', 4: 'Colunas',
  5: 'Linhas', 6: 'Pizza', 7: 'Rosca',
};
const metricNames: Record<number, string> = {
  1: 'Contagem', 2: 'Soma', 3: 'Média', 4: 'Percentual',
  5: 'Mínimo', 6: 'Máximo', 7: 'Previsto x realizado',
};
const operatorNames: Record<string, string> = {
  eq: 'igual', neq: 'diferente', contains: 'contém', startsWith: 'começa com',
  gt: 'maior', gte: 'maior ou igual', lt: 'menor', lte: 'menor ou igual',
  between: 'entre', in: 'está em', isEmpty: 'vazio', isNotEmpty: 'preenchido',
};

const emptyDefinition: ReportDefinition = {
  columns: [], metrics: [], filters: [], descending: false,
};

function display(value: unknown) {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não';
  if (typeof value === 'number') return value.toLocaleString('pt-BR', { maximumFractionDigits: 2 });
  return String(value);
}

function VisualResult({ result }: { result: ReportExecution }) {
  if (!result.rows.length) return <Message>Nenhum registro corresponde aos filtros.</Message>;
  if (result.visualization === 1) return <TableWrap><table><thead><tr>{result.columns.map(column => <th key={column.key}>{column.label}</th>)}</tr></thead><tbody>
    {result.rows.slice(0, 200).map((row, index) => <tr key={index}>{result.columns.map(column => <td key={column.key}>{display(row[column.key])}</td>)}</tr>)}
  </tbody></table></TableWrap>;

  const numeric = result.columns.find(column => column.numeric) ?? result.columns[1];
  const label = result.columns.find(column => column.key !== numeric?.key) ?? result.columns[0];
  if (!numeric) return <Message>Escolha uma métrica numérica para esta visualização.</Message>;
  const values = result.rows.slice(0, 20).map(row => ({
    label: display(row[label?.key]), value: Number(row[numeric.key] ?? 0),
  }));
  const max = Math.max(...values.map(value => Math.abs(value.value)), 1);
  if (result.visualization === 2) return <KpiResult><div><strong>{display(values[0]?.value ?? 0)}</strong><span>{numeric.label}</span></div></KpiResult>;
  if (result.visualization === 4) return <ColumnChart>{values.map((value, index) => <article key={`${value.label}-${index}`} title={value.label}><b>{display(value.value)}</b><i style={{ height: `${Math.max(3, Math.abs(value.value) * 180 / max)}px` }} /><span>{value.label}</span></article>)}</ColumnChart>;
  if (result.visualization === 5) {
    const points = values.map((value, index) => `${values.length === 1 ? 300 : index * 600 / (values.length - 1)},${225 - Math.abs(value.value) * 205 / max}`).join(' ');
    return <><ResultLine viewBox="0 0 600 240" preserveAspectRatio="none" role="img" aria-label={`Gráfico de ${numeric.label}`}><polyline points={points} />{points.split(' ').map((point, index) => { const [cx, cy] = point.split(','); return <circle key={index} cx={cx} cy={cy} r="4" />; })}</ResultLine></>;
  }
  if (result.visualization === 6 || result.visualization === 7) {
    const colors = ['#2563EB','#06B6D4','#10B981','#F97316','#7C3AED','#E11D48','#0891B2','#64748B'];
    const positive = values.slice(0, 8).map(value => ({ ...value, value: Math.max(0, value.value) }));
    const total = positive.reduce((sum, value) => sum + value.value, 0) || 1;
    let cursor = 0;
    const stops = positive.map((value, index) => { const start = cursor; cursor += value.value * 100 / total; return `${colors[index]} ${start}% ${cursor}%`; });
    return <PieLayout><Pie $gradient={`conic-gradient(${stops.join(',')})`} $donut={result.visualization === 7} /><Legend>{positive.map((value, index) => <div key={`${value.label}-${index}`}><i style={{ background: colors[index] }} /><span>{value.label}</span><b>{display(value.value)}</b></div>)}</Legend></PieLayout>;
  }
  return <ResultBars>{values.map((value, index) => <ResultBar key={`${value.label}-${index}`}>
    <span title={value.label}>{value.label}</span><i style={{ width: `${Math.max(2, Math.abs(value.value) * 100 / max)}%` }} /><b>{display(value.value)}</b>
  </ResultBar>)}</ResultBars>;
}

const KpiResult = styled.div`
  display: grid;
  min-height: 190px;
  place-items: center;
  text-align: center;
  strong { display: block; color: ${({ theme }) => theme.color.brand}; font-size: 42px; }
  span { color: ${({ theme }) => theme.color.textMuted}; font-size: 13.5px; }
`;

export function ReportBuilder({ projectId }: { projectId?: string }) {
  const queryClient = useQueryClient();
  const [reportId, setReportId] = useState<string>();
  const [scopeProjectId, setScopeProjectId] = useState(projectId);
  const [name, setName] = useState('Novo relatório');
  const [source, setSource] = useState(1);
  const [visualization, setVisualization] = useState(1);
  const [definition, setDefinition] = useState<ReportDefinition>(emptyDefinition);
  const [isShared, setIsShared] = useState(false);
  const [result, setResult] = useState<ReportExecution>();

  const catalogQuery = useQuery<ReportCatalog>({
    queryKey: ['report-catalog', scopeProjectId],
    queryFn: () => api.getReportCatalog(scopeProjectId),
  });
  const savedQuery = useQuery<SavedReport[]>({
    queryKey: ['saved-reports', projectId],
    queryFn: () => api.getSavedReports(projectId),
  });
  const selectedSource = catalogQuery.data?.sources.find(item => item.source === source);
  const numericFields = selectedSource?.fields.filter(field => field.numeric) ?? [];

  useEffect(() => {
    if (selectedSource && definition.columns.length === 0 && definition.metrics.length === 0) {
      setDefinition(current => ({ ...current, columns: selectedSource.fields.slice(0, 5).map(field => field.key) }));
    }
  }, [selectedSource, definition.columns.length, definition.metrics.length]);

  const previewMutation = useMutation<ReportExecution>({
    mutationFn: () => api.previewReport({ projectId: scopeProjectId, source, visualization, definition }),
    onSuccess: setResult,
  });
  const saveMutation = useMutation<SavedReport>({
    mutationFn: () => api.saveReport({ projectId: scopeProjectId, name, source, visualization, definition, isShared }, reportId),
    onSuccess: async saved => {
      setReportId(saved.id);
      await queryClient.invalidateQueries({ queryKey: ['saved-reports', projectId] });
    },
  });
  const duplicateMutation = useMutation({
    mutationFn: (id: string) => api.duplicateReport(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['saved-reports', projectId] }),
  });
  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteReport(id),
    onSuccess: async (_, id) => {
      if (reportId === id) newReport();
      await queryClient.invalidateQueries({ queryKey: ['saved-reports', projectId] });
    },
  });
  const runMutation = useMutation<ReportExecution, Error, string>({
    mutationFn: id => api.runSavedReport(id),
    onSuccess: setResult,
  });

  const canRun = definition.columns.length > 0 || definition.metrics.length > 0;
  const mutationError = previewMutation.error ?? saveMutation.error ?? runMutation.error
    ?? duplicateMutation.error ?? deleteMutation.error;

  const setSourceAndReset = (next: number) => {
    setSource(next);
    const fields = catalogQuery.data?.sources.find(item => item.source === next)?.fields ?? [];
    setDefinition({ ...emptyDefinition, columns: fields.slice(0, 5).map(field => field.key) });
    setResult(undefined);
  };
  const toggleColumn = (key: string) => setDefinition(current => ({
    ...current,
    columns: current.columns.includes(key)
      ? current.columns.filter(column => column !== key)
      : [...current.columns, key],
  }));
  const updateFilter = (index: number, changes: Partial<ReportFilter>) => setDefinition(current => ({
    ...current, filters: current.filters.map((filter, currentIndex) => currentIndex === index ? { ...filter, ...changes } : filter),
  }));
  const updateMetric = (index: number, changes: Partial<ReportMetric>) => setDefinition(current => ({
    ...current, metrics: current.metrics.map((metric, currentIndex) => currentIndex === index ? { ...metric, ...changes } : metric),
  }));
  const newReport = () => {
    const first = catalogQuery.data?.sources[0];
    setReportId(undefined); setScopeProjectId(projectId); setName('Novo relatório'); setSource(first?.source ?? 1); setVisualization(1);
    setDefinition({ ...emptyDefinition, columns: first?.fields.slice(0, 5).map(field => field.key) ?? [] });
    setIsShared(false); setResult(undefined);
  };
  const openSaved = (saved: SavedReport) => {
    setReportId(saved.id); setScopeProjectId(saved.projectId); setName(saved.name); setSource(saved.source); setVisualization(saved.visualization);
    setDefinition(saved.definition); setIsShared(saved.isShared); setResult(undefined);
  };
  const exportSaved = async (id: string) => {
    const exported = await api.exportReport(id);
    const url = URL.createObjectURL(exported.blob);
    const anchor = document.createElement('a'); anchor.href = url; anchor.download = exported.fileName; anchor.click();
    URL.revokeObjectURL(url);
  };

  const sourceName = useMemo(() => selectedSource?.name ?? 'Fonte', [selectedSource]);

  return <Layout>
    <div>
      <Panel>
        <h3>Construtor sem código</h3><p>Monte uma consulta usando apenas fontes, campos e operações permitidas.</p>
        <Field>Nome<input maxLength={200} value={name} onChange={event => setName(event.target.value)} /></Field>
        <Inline>
          <Field>Fonte<select value={source} onChange={event => setSourceAndReset(Number(event.target.value))}>
            {catalogQuery.data?.sources.map(item => <option key={item.source} value={item.source}>{item.name}</option>)}
          </select></Field>
          <Field>Visualização<select value={visualization} onChange={event => setVisualization(Number(event.target.value))}>
            {(catalogQuery.data?.visualizations ?? Object.keys(visualizationNames).map(Number)).map(item => <option key={item} value={item}>{visualizationNames[item]}</option>)}
          </select></Field>
        </Inline>
        <SectionTitle>Colunas <span>{definition.columns.length} selecionadas</span></SectionTitle>
        <Columns>{selectedSource?.fields.map(field => <label key={field.key}>
          <input type="checkbox" checked={definition.columns.includes(field.key)} onChange={() => toggleColumn(field.key)} />{field.label}
        </label>)}</Columns>

        <SectionTitle>Filtros <Mini type="button" onClick={() => setDefinition(current => ({ ...current, filters: [...current.filters, { field: selectedSource?.fields[0]?.key ?? '', operator: 'eq', value: '' }] }))}><Plus size={11} />Adicionar</Mini></SectionTitle>
        {definition.filters.map((filter, index) => <RowEditor key={index}>
          <select aria-label="Campo do filtro" value={filter.field} onChange={event => updateFilter(index, { field: event.target.value })}>{selectedSource?.fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}</select>
          <select aria-label="Operador" value={filter.operator} onChange={event => updateFilter(index, { operator: event.target.value })}>{catalogQuery.data?.filterOperators.map(operator => <option key={operator} value={operator}>{operatorNames[operator] ?? operator}</option>)}</select>
          <input aria-label="Valor do filtro" disabled={filter.operator === 'isEmpty' || filter.operator === 'isNotEmpty'} value={filter.value ?? ''} onChange={event => updateFilter(index, { value: event.target.value })} placeholder={filter.operator === 'in' ? 'valor 1, valor 2' : 'valor'} />
          {filter.operator === 'between' && <input aria-label="Valor final do filtro" value={filter.valueTo ?? ''} onChange={event => updateFilter(index, { valueTo: event.target.value })} placeholder="valor final" />}
          <button type="button" aria-label="Remover filtro" onClick={() => setDefinition(current => ({ ...current, filters: current.filters.filter((_, currentIndex) => currentIndex !== index) }))}><Trash2 size={13} /></button>
        </RowEditor>)}

        <SectionTitle>Métricas <Mini type="button" onClick={() => setDefinition(current => ({ ...current, metrics: [...current.metrics, { operation: 1, label: 'Contagem' }] }))}><Plus size={11} />Adicionar</Mini></SectionTitle>
        {definition.metrics.map((metric, index) => <RowEditor key={index}>
          <select aria-label="Operação" value={metric.operation} onChange={event => {
            const operation = Number(event.target.value);
            updateMetric(index, { operation, field: operation === 1 || operation === 7 ? undefined : operation === 4 ? selectedSource?.fields[0]?.key : numericFields[0]?.key, label: metricNames[operation] });
          }}>{catalogQuery.data?.metrics.map(operation => <option key={operation} value={operation}>{metricNames[operation]}</option>)}</select>
          <select aria-label="Campo da métrica" disabled={metric.operation === 1 || metric.operation === 7} value={metric.field ?? ''} onChange={event => updateMetric(index, { field: event.target.value })}>{(metric.operation === 4 ? selectedSource?.fields ?? [] : numericFields).map(field => <option key={field.key} value={field.key}>{field.label}</option>)}</select>
          <input aria-label="Rótulo da métrica" value={metric.label} onChange={event => updateMetric(index, { label: event.target.value })} />
          <button type="button" aria-label="Remover métrica" onClick={() => setDefinition(current => ({ ...current, metrics: current.metrics.filter((_, currentIndex) => currentIndex !== index) }))}><Trash2 size={13} /></button>
        </RowEditor>)}

        <Inline>
          <Field>Agrupar por<select value={definition.groupBy ?? ''} onChange={event => setDefinition(current => ({ ...current, groupBy: event.target.value || undefined }))}><option value="">Sem agrupamento</option>{selectedSource?.fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}</select></Field>
          <Field>Ordenar por<select value={definition.orderBy ?? ''} onChange={event => setDefinition(current => ({ ...current, orderBy: event.target.value || undefined }))}><option value="">Automático</option>{selectedSource?.fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}</select></Field>
        </Inline>
        <Inline>
          <Field>De<input type="date" value={definition.from ?? ''} onChange={event => setDefinition(current => ({ ...current, from: event.target.value || undefined }))} /></Field>
          <Field>Até<input type="date" value={definition.to ?? ''} onChange={event => setDefinition(current => ({ ...current, to: event.target.value || undefined }))} /></Field>
        </Inline>
        <Toggle><input type="checkbox" checked={definition.descending} onChange={event => setDefinition(current => ({ ...current, descending: event.target.checked }))} />Ordem decrescente</Toggle>
        <Toggle><input type="checkbox" checked={isShared} onChange={event => setIsShared(event.target.checked)} />Compartilhar com gestores autorizados</Toggle>
        <Actions>
          <Action type="button" onClick={newReport}><Plus size={13} />Novo</Action>
          <Action type="button" disabled={!canRun || previewMutation.isPending} onClick={() => previewMutation.mutate()}><Eye size={13} />Pré-visualizar</Action>
          <Action $primary type="button" disabled={!canRun || !name.trim() || saveMutation.isPending} onClick={() => saveMutation.mutate()}><Save size={13} />Salvar</Action>
        </Actions>
        {mutationError && <Message $error role="alert">{(mutationError as Error).message}</Message>}
      </Panel>

      <Panel style={{ marginTop: 12 }}><h3>Relatórios salvos</h3><p>Abra, duplique, compartilhe ou exporte definições existentes.</p>
        <SavedList>{savedQuery.data?.map(saved => <Saved key={saved.id}><div><b>{saved.name}</b><span>{saved.isShared ? 'Compartilhado' : 'Privado'} · {new Date(saved.updatedAt).toLocaleDateString('pt-BR')}</span></div><div>
          <button type="button" title="Abrir" onClick={() => { openSaved(saved); runMutation.mutate(saved.id); }}><Eye size={13} /></button>
          <button type="button" title="Duplicar" onClick={() => duplicateMutation.mutate(saved.id)}><Copy size={13} /></button>
          <button type="button" title="Exportar CSV" onClick={() => void exportSaved(saved.id)}><Download size={13} /></button>
          <button type="button" title="Excluir" onClick={() => deleteMutation.mutate(saved.id)}><Trash2 size={13} /></button>
        </div></Saved>)}</SavedList>
        {!savedQuery.isLoading && !savedQuery.data?.length && <Message>Nenhum relatório salvo neste escopo.</Message>}
      </Panel>
    </div>

    <Panel>
      <ResultHeader><div><h3>Resultado · {sourceName}</h3></div>{result && <span>{result.resultRowCount} de {result.sourceRowCount} registros</span>}</ResultHeader>
      {result ? <VisualResult result={result} /> : <Message>Configure o relatório e selecione “Pré-visualizar”.</Message>}
    </Panel>
  </Layout>;
}
