import { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { Badge, Button, EmptyState, FilterRow, Kicker, Meta, Page, PageHeader, Panel, Progress, Stat } from '../components/ui'
import { activeSpecs, taskTestGates, tasksForSpec, userStoriesForSpec } from '../lib/insights'
import type { ExplorerModel, Selection, StoryTask, UserStory } from '../types/model'

type Result = 'pending' | 'conform' | 'changes_requested' | 'not_implemented' | 'blocked'
type Entry = { result: Result; observation: string; updatedAt?: string | null }
type Store = Record<string, Entry>

const STORAGE_KEY = 'context-explorer:user-story-validation:v1'
const LEGACY_STORAGE_KEY = 'context-explorer:manual-validation:current-specs:v2'

const Metrics = styled.div`
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 10px;
  @media (max-width: 1180px) { grid-template-columns: repeat(3, 1fr); }
  @media (max-width: 850px) { grid-template-columns: repeat(2, 1fr); }
`
const ProgressLabel = styled.div`
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 8px;
  color: #5d6f82;
  font-size: 12px;
  strong { color: #173b5c; }
`
const Groups = styled.div`display: grid; gap: 12px;`
const Group = styled.details`
  overflow: hidden;
  border: 1px solid #d8e2e9;
  border-radius: 15px;
  background: #fff;

  > summary {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 14px;
    padding: 17px 19px;
    cursor: pointer;
    list-style: none;
  }
  > summary::-webkit-details-marker { display: none; }
  .stories { display: grid; gap: 10px; padding: 0 12px 12px; border-top: 1px solid #e4eaef; background: #f8fafb; }
`
const StoryCard = styled.article`
  display: grid;
  grid-template-columns: minmax(0, 1fr) 220px;
  gap: 16px;
  margin-top: 12px;
  border: 1px solid #dce5ec;
  border-radius: 12px;
  background: #fff;
  padding: 16px;

  h3 { margin: 6px 0 7px; color: #143653; font-size: 15px; }
  .link-button { border: 0; background: transparent; color: inherit; padding: 0; text-align: left; font: inherit; cursor: pointer; }
  .link-button:hover { color: #146ca8; }
  .narrative { margin: 0 0 13px; color: #52677b; line-height: 1.5; }
  .scenario { display: grid; gap: 6px; }
  .scenario div { display: grid; grid-template-columns: 64px minmax(0, 1fr); gap: 8px; color: #38536b; font-size: 12px; line-height: 1.45; }
  .scenario strong { color: #146ca8; text-transform: uppercase; }
  .source { display: block; margin-top: 11px; color: #8491a0; font-size: 10.5px; }
  .control { display: grid; gap: 8px; align-content: start; }
  select, textarea { width: 100%; border: 1px solid #ccd7e0; border-radius: 8px; background: #fff; color: #27445f; padding: 9px; }
  textarea { min-height: 92px; resize: vertical; }
  .save-state { color: #7a8998; font-size: 10.5px; text-align: center; }
  .task-created { border: 1px solid #c8ddeb; border-radius: 8px; background: #f1f8fc; color: #315b78; padding: 9px; font-size: 11px; line-height: 1.45; }

  @media (max-width: 780px) { grid-template-columns: 1fr; }
`

function initialStore(): Store {
  try {
    const current = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}') as Record<string, { result?: string; observation?: string; updatedAt?: string | null }>
    if (Object.keys(current).length) return Object.fromEntries(Object.entries(current).map(([id, entry]) => [id, {
      result: entry.result === 'approved' ? 'conform' : ['pending','conform','changes_requested','not_implemented','blocked'].includes(entry.result ?? '') ? entry.result as Result : 'pending',
      observation: entry.observation ?? '',
      updatedAt: entry.updatedAt
    }]))
    const legacy = JSON.parse(localStorage.getItem(LEGACY_STORAGE_KEY) ?? '{}') as Record<string, { result?: string; evidence?: string }>
    return Object.fromEntries(Object.entries(legacy).map(([id, entry]) => [id, {
      result: entry.result === 'passed' ? 'conform' : entry.result === 'failed' ? 'changes_requested' : entry.result === 'blocked' ? 'blocked' : 'pending',
      observation: entry.evidence ?? ''
    }]))
  } catch {
    return {}
  }
}

function resultTone(result: Result) {
  if (result === 'conform') return 'success' as const
  if (result === 'changes_requested') return 'error' as const
  if (result === 'not_implemented') return 'error' as const
  if (result === 'blocked') return 'warning' as const
  return 'neutral' as const
}

function Story({ story, entry, storyTask, onUpdate, onSelect }: { story: UserStory; entry: Entry; storyTask?: StoryTask; onUpdate: (patch: Partial<Entry>) => void; onSelect: () => void }) {
  const [draft, setDraft] = useState(entry.observation)
  useEffect(() => setDraft(entry.observation), [entry.observation])
  const changed = draft !== entry.observation
  return <StoryCard>
    <div>
      <Meta><Badge>{story.id}</Badge><Badge $tone={resultTone(entry.result)}>{entry.result === 'conform' ? 'está conforme' : entry.result === 'changes_requested' ? 'ajuste necessário' : entry.result === 'not_implemented' ? 'não implementada' : entry.result === 'blocked' ? 'bloqueada' : 'ainda não testada'}</Badge></Meta>
      <h3><button className="link-button" onClick={onSelect}>{story.title}</button></h3>
      <p className="narrative">{story.narrative}</p>
      <div className="scenario">
        {story.scenario.given && <div><strong>Dado</strong><span>{story.scenario.given}</span></div>}
        {story.scenario.when && <div><strong>Quando</strong><span>{story.scenario.when}</span></div>}
        <div><strong>Então</strong><span>{story.scenario.then}</span></div>
      </div>
      <span className="source">História canônica: stories/catalog.json · contrato relacionado: {story.specPath}</span>
    </div>
    <div className="control">
      <select aria-label={`Resultado de ${story.id}`} value={entry.result} onChange={event => onUpdate({ result: event.target.value as Result })}>
        <option value="pending">Ainda não testei</option>
        <option value="conform">Está conforme</option>
        <option value="changes_requested">Precisa de ajuste</option>
        <option value="not_implemented">Isso não está implementado</option>
        <option value="blocked">Não consegui testar</option>
      </select>
      <textarea aria-label={`Observação de ${story.id}`} placeholder="O que você observou?" value={draft} onChange={event => setDraft(event.target.value)} />
      <Button $primary={changed} disabled={!changed} onClick={() => onUpdate({ observation: draft })}>{changed ? 'Salvar e criar tarefa' : 'Observação salva'}</Button>
      {changed && <span className="save-state">Ao salvar, uma tarefa de análise será criada ou atualizada.</span>}
      {storyTask && <div className="task-created"><strong>{storyTask.id}</strong><br/>Tarefa criada · aguardando análise da IA.</div>}
    </div>
  </StoryCard>
}

export function ManualValidationView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const stories = useMemo(() => model.userStories ?? [], [model])
  const [store, setStore] = useState<Store>(initialStore)
  const [query, setQuery] = useState('')
  const [result, setResult] = useState<'all' | Result>('all')
  const [remote, setRemote] = useState<'loading' | 'available' | 'unavailable'>('loading')
  const [hydrated, setHydrated] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    fetch('/api/manual-validation', { signal: controller.signal, cache: 'no-store' })
      .then(response => {
        if (!response.ok) throw new Error('API indisponível')
        return response.json() as Promise<{ entries?: Store }>
      })
      .then(payload => {
        if (payload.entries && Object.keys(payload.entries).length) setStore(payload.entries)
        setRemote('available')
      })
      .catch(error => {
        if (error instanceof Error && error.name === 'AbortError') return
        setRemote('unavailable')
      })
      .finally(() => setHydrated(true))
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!hydrated) return
    localStorage.setItem(STORAGE_KEY, JSON.stringify(store))
    if (remote !== 'available') return
    const timeout = window.setTimeout(() => {
      void fetch('/api/manual-validation', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ entries: store })
      }).then(async response => {
        if (!response.ok) return setRemote('unavailable')
        const payload = await response.json() as { storyTasks?: StoryTask[] }
        if (payload.storyTasks?.length) window.dispatchEvent(new Event('context-explorer:model-changed'))
      }).catch(() => setRemote('unavailable'))
    }, 450)
    return () => window.clearTimeout(timeout)
  }, [store, hydrated, remote])

  const update = (id: string, patch: Partial<Entry>) => setStore(value => ({
    ...value,
    [id]: { ...(value[id] ?? { result: 'pending' as Result, observation: '' }), ...patch, updatedAt: new Date().toISOString() }
  }))
  const stateOf = (id: string): Entry => store[id] ?? { result: 'pending', observation: '' }
  const visible = stories.filter(story => {
    const state = stateOf(story.id)
    return (result === 'all' || state.result === result)
      && `${story.id} ${story.specId} ${story.specTitle} ${story.title} ${story.narrative}`.toLowerCase().includes(query.toLowerCase())
  })
  const grouped = activeSpecs(model)
    .map(spec => ({ spec, stories: visible.filter(story => story.specId === spec.id) }))
    .filter(group => group.stories.length)
  const count = (value: Result) => stories.filter(story => stateOf(story.id).result === value).length
  const conform = count('conform')
  const progress = stories.length ? (conform / stories.length) * 100 : 0

  return <Page>
    <PageHeader>
      <div><Kicker>Conferência do produto pelas histórias</Kicker><h1>Homologação</h1><p>As histórias não são aprovadas individualmente. Você apenas confere o sistema; toda observação ou funcionalidade ausente gera uma tarefa para análise da IA.</p></div>
      <Badge $tone={remote === 'available' ? 'success' : remote === 'loading' ? 'neutral' : 'warning'}>{remote === 'available' ? 'salvo no projeto' : remote === 'loading' ? 'conectando…' : 'rascunho neste navegador'}</Badge>
    </PageHeader>
    <Panel><strong>Como funciona:</strong><p style={{ marginBottom: 12 }}>As histórias registram primeiro a necessidade. A IA transforma esse conteúdo em spec, a spec recebe G-SPEC e os agentes implementam. Aqui você somente marca se o produto está conforme. Observações, ajustes e itens não implementados entram automaticamente em Trabalho dos agentes.</p><ProgressLabel><span>Progresso da homologação</span><strong>{conform} de {stories.length} histórias conformes · {Math.round(progress)}%</strong></ProgressLabel><Progress $value={progress} /></Panel>
    <Metrics>
      <Stat><span>Ainda não testei</span><strong>{count('pending')}</strong></Stat>
      <Stat><span>Estão conformes</span><strong>{conform}</strong></Stat>
      <Stat><span>Precisam de ajuste</span><strong>{count('changes_requested')}</strong></Stat>
      <Stat><span>Não implementadas</span><strong>{count('not_implemented')}</strong></Stat>
      <Stat><span>Não consegui testar</span><strong>{count('blocked')}</strong></Stat>
    </Metrics>
    <FilterRow>
      <input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar história, módulo ou spec" />
      <select value={result} onChange={event => setResult(event.target.value as typeof result)}>
        <option value="all">Todos os resultados</option>
        <option value="pending">Ainda não testei</option>
        <option value="conform">Estão conformes</option>
        <option value="changes_requested">Precisam de ajuste</option>
        <option value="not_implemented">Não implementadas</option>
        <option value="blocked">Não consegui testar</option>
      </select>
      <Badge $tone="neutral">{visible.length} de {stories.length} histórias</Badge>
      <Button onClick={() => { if (confirm('Limpar todas as homologações registradas?')) setStore({}) }}>Limpar resultados</Button>
    </FilterRow>
    {grouped.length === 0 ? <EmptyState>Nenhuma história encontrada para o filtro atual.</EmptyState> : <Groups>{grouped.map(({ spec, stories: items }) => {
      const gates = taskTestGates(tasksForSpec(model, spec.id))
      const passed = items.filter(story => stateOf(story.id).result === 'conform').length
      const allStories = userStoriesForSpec(model, spec.id)
      return <Group key={spec.id}>
        <summary>
          <span><strong>{spec.title}</strong><Meta style={{ marginTop: 7, marginBottom: 0 }}><Badge>{spec.id}</Badge><Badge $tone="neutral">{allStories.length} histórias</Badge><Badge $tone="neutral">{gates.length} testes automatizados exigidos</Badge></Meta></span>
          <Badge $tone={passed === items.length ? 'success' : 'neutral'}>{passed}/{items.length} visíveis conformes</Badge>
        </summary>
        <div className="stories">{items.map(story => <Story key={story.id} story={story} entry={stateOf(story.id)} storyTask={model.storyTasks.find(task => task.storyId === story.id && ['pending_analysis','ready','in_progress','blocked'].includes(task.status))} onUpdate={patch => update(story.id, patch)} onSelect={() => onSelect({ ...story, _kind: 'user-story', _eyebrow: 'História de usuário', _summary: story.narrative })} />)}</div>
      </Group>
    })}</Groups>}
  </Page>
}
