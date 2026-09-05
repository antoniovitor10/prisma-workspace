import styled from 'styled-components'
import { Badge, Button } from './ui'
import type { AcceptanceScenario, Selection, UserStory } from '../types/model'
import { traduzir, traduzirRotuloChave } from '../lib/i18n'

const Shell = styled.aside<{ $open: boolean }>`
  position: fixed;
  inset: 0 0 0 auto;
  z-index: 25;
  width: 366px;
  border-left: 1px solid #d7e0e8;
  background: #fff;
  box-shadow: -8px 0 28px rgba(16, 47, 79, .055);
  overflow-y: auto;
  transform: translateX(${({ $open }) => $open ? '0' : '105%'});
  transition: transform .2s ease;

  @media (max-width: 1280px) {
    width: min(410px, calc(100vw - 32px));
    box-shadow: -14px 0 40px rgba(16, 47, 79, .2);
  }
`

const Header = styled.header`
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 14px;
  padding: 21px 20px 17px;
  border-bottom: 1px solid #e0e6eb;
  background: rgba(255,255,255,.97);
  backdrop-filter: blur(10px);

  .eyebrow { color: #1671b8; font-size: 10px; font-weight: 780; letter-spacing: .1em; text-transform: uppercase; }
  h2 { margin: 5px 0 0; color: #102f4f; font-size: 19px; line-height: 1.3; letter-spacing: -.02em; }
  button { border: 1px solid #d1dbe3; border-radius: 8px; background: #fff; color: #5b6c7e; padding: 6px 9px; cursor: pointer; }
`

const Body = styled.div`
  padding: 19px 20px 30px;
  color: #21374b;
  overflow-wrap: anywhere;

  .summary { margin: 0 0 18px; color: #5d6f82; line-height: 1.6; }
  dl { margin: 0; display: grid; gap: 17px; }
  dt { margin-bottom: 6px; color: #7a8897; font-size: 10px; font-weight: 760; letter-spacing: .08em; text-transform: uppercase; }
  dd { margin: 0; line-height: 1.55; }
  ul { display: grid; gap: 6px; margin: 0; padding-left: 18px; }
  li { color: #4f6376; }
  details { margin-bottom: 18px; border: 1px solid #dbe3ea; border-radius: 10px; background: #f8fafb; }
  summary { padding: 11px 12px; color: #294761; font-weight: 700; cursor: pointer; }
  pre { max-height: 420px; margin: 0; overflow: auto; white-space: pre-wrap; font: 11px/1.55 ui-monospace, SFMono-Regular, Consolas, monospace; padding: 0 12px 12px; color: #40566a; }
  .chips { display: flex; flex-wrap: wrap; gap: 6px; }
  .action { margin-bottom: 18px; }
`

const Empty = styled.div`
  display: grid;
  min-height: 100%;
  align-content: center;
  gap: 10px;
  padding: 32px;
  color: #748495;
  text-align: center;

  .symbol { display: grid; width: 54px; height: 54px; place-items: center; margin: 0 auto; border: 1px solid #d6e0e7; border-radius: 16px; background: #f2f7fa; color: #1671b8; font-weight: 820; }
  h2 { margin: 0; color: #294761; font-size: 17px; }
  p { margin: 0; font-size: 12px; line-height: 1.55; }
`

const StoryList = styled.div`
  display: grid;
  gap: 9px;

  details { margin: 0; background: #fff; }
  summary { display: grid; gap: 6px; padding: 12px; }
  .story-title { color: #173b5c; font-size: 12px; line-height: 1.4; }
  .story-body { display: grid; gap: 10px; padding: 0 12px 13px; }
  .story-body p { margin: 0; color: #5d6f82; font-size: 12px; line-height: 1.5; }
`

const Scenario = styled.div`
  display: grid;
  gap: 7px;

  div { display: grid; grid-template-columns: 58px minmax(0, 1fr); gap: 7px; font-size: 11px; line-height: 1.45; }
  strong { color: #1671b8; font-size: 9px; letter-spacing: .06em; text-transform: uppercase; }
  span { color: #40586d; }
`

function scalar(value: unknown) {
  if (value === null || value === undefined || value === '') return 'Não informado'
  if (typeof value === 'boolean') return value ? 'Sim' : 'Não'
  return traduzir(value)
}

function scenarioView(scenario: AcceptanceScenario) {
  return <Scenario>
    {scenario.given && <div><strong>Dado</strong><span>{scenario.given}</span></div>}
    {scenario.when && <div><strong>Quando</strong><span>{scenario.when}</span></div>}
    <div><strong>Então</strong><span>{scenario.then}</span></div>
  </Scenario>
}

function storiesView(stories: UserStory[]) {
  if (!stories.length) return 'Nenhuma história derivada'
  return <StoryList>{stories.map(story => <details key={story.id}>
    <summary><Badge>{story.id}</Badge><span className="story-title">{story.title}</span></summary>
    <div className="story-body"><p>{story.narrative}</p>{scenarioView(story.scenario)}</div>
  </details>)}</StoryList>
}

function valueView(value: unknown, key?: string) {
  if (key === 'userStories' && Array.isArray(value)) return storiesView(value as UserStory[])
  if (key === 'scenario' && value && typeof value === 'object') return scenarioView(value as AcceptanceScenario)
  if (Array.isArray(value)) {
    if (value.length === 0) return 'Nenhum'
    if (value.every(item => ['string', 'number', 'boolean'].includes(typeof item))) return <div className="chips">{value.map((item, index) => <Badge key={`${String(item)}-${index}`} $tone="neutral">{scalar(item)}</Badge>)}</div>
    return <ul>{value.map((item, index) => <li key={index}>{typeof item === 'object' ? <pre>{JSON.stringify(item, null, 2)}</pre> : scalar(item)}</li>)}</ul>
  }
  if (typeof value === 'object' && value !== null) return <pre>{JSON.stringify(value, null, 2)}</pre>
  return scalar(value)
}

const hidden = new Set(['body', '_kind', '_eyebrow', '_summary', '_action', 'sections'])

export function Inspector({ selection, onClose, onNavigate }: { selection: Selection; onClose: () => void; onNavigate: (view: string) => void }) {
  if (!selection) return <Shell $open={false} aria-hidden="true"><Empty><div className="symbol">i</div><h2>Inspetor</h2><p>Selecione uma especificação, tarefa, nó, verificação ou lacuna para entender sua origem, relações e impacto.</p></Empty></Shell>

  const entries = Object.entries(selection).filter(([key, value]) => !hidden.has(key) && value !== undefined)
  const title = traduzir(selection.title ?? selection.label ?? selection.role ?? selection.id ?? selection.subject ?? 'Detalhes')
  return <Shell $open aria-live="polite">
    <Header><div><span className="eyebrow">{traduzir(selection._eyebrow ?? selection._kind ?? 'Inspetor')}</span><h2>{title}</h2></div><button onClick={onClose} aria-label="Fechar inspetor">Esc</button></Header>
    <Body>
      {selection._summary && <p className="summary">{String(selection._summary)}</p>}
      {selection._action && <div className="action"><Button $primary onClick={() => onNavigate(selection._action!.view)}>{selection._action.label}</Button></div>}
      {selection.body !== undefined && <details open><summary>Documento completo</summary><pre style={{ maxHeight: 600 }}>{scalar(selection.body)}</pre></details>}
      <dl>{entries.map(([key, value]) => <div key={key}><dt>{key === 'userStories' ? 'Histórias para revisar' : traduzirRotuloChave(key)}</dt><dd>{valueView(value, key)}</dd></div>)}</dl>
    </Body>
  </Shell>
}
