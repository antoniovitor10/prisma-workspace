import { useEffect, useMemo, useState } from 'react'
import styled from 'styled-components'
import { buildDecisionQuestions, type DecisionQuestion } from '../lib/analysis'
import type { ExplorerModel, Selection } from '../types/model'
import { Badge, Button, FilterRow, Panel } from './ui'

const STORAGE_KEY = 'context-explorer:decision-answers:v1'
type Answer = { resolved: boolean; note: string }

const List = styled.div`display: grid; gap: 10px; margin-top: 14px;`
const Question = styled.article<{ $resolved: boolean }>`
  border: 1px solid ${({ $resolved }) => $resolved ? '#c9e5d5' : '#d9e1e8'};
  border-radius: 11px;
  background: ${({ $resolved }) => $resolved ? '#f3fbf6' : '#fff'};
  padding: 14px;

  .head { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-bottom: 9px; }
  h3 { margin: 0 0 6px; color: #183149; font-size: 13.5px; line-height: 1.45; }
  p { margin: 0; color: #6a7a8b; font-size: 12px; line-height: 1.5; }
  textarea { width: 100%; min-height: 66px; margin-top: 11px; resize: vertical; border: 1px solid #ccd7e0; border-radius: 8px; padding: 9px 10px; color: #263e54; font: inherit; font-size: 12px; }
  .actions { display: flex; justify-content: space-between; gap: 8px; margin-top: 9px; }
  .link { border: 0; background: transparent; color: #1671b8; padding: 0; font-size: 11px; font-weight: 700; cursor: pointer; }
`

function loadAnswers(): Record<string, Answer> {
  try { return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}') as Record<string, Answer> }
  catch { return {} }
}

function tone(question: DecisionQuestion) {
  return question.severity === 'high' ? 'error' : question.severity === 'medium' ? 'warning' : 'blue'
}

export function QuestionQueue({ model, onSelect, compact = false }: { model: ExplorerModel; onSelect: (selection: Selection) => void; compact?: boolean }) {
  const questions = useMemo(() => buildDecisionQuestions(model), [model])
  const [answers, setAnswers] = useState<Record<string, Answer>>(loadAnswers)
  const [status, setStatus] = useState<'open' | 'resolved' | 'all'>('open')
  const visible = questions.filter(question => status === 'all' || Boolean(answers[question.id]?.resolved) === (status === 'resolved')).slice(0, compact ? 4 : undefined)
  const openCount = questions.filter(question => !answers[question.id]?.resolved).length

  useEffect(() => localStorage.setItem(STORAGE_KEY, JSON.stringify(answers)), [answers])
  const update = (id: string, patch: Partial<Answer>) => setAnswers(current => ({ ...current, [id]: { resolved: current[id]?.resolved ?? false, note: current[id]?.note ?? '', ...patch } }))

  return <Panel>
    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 14 }}><div><h2 style={{ marginBottom: 5 }}>Perguntas para fechar</h2><p style={{ margin: 0 }}>{openCount} decisões abertas, derivadas das specs, gaps e gates.</p></div><Badge $tone={openCount > 0 ? 'warning' : 'success'}>{openCount} abertas</Badge></div>
    {!compact && <FilterRow style={{ marginTop: 14 }}><select value={status} onChange={event => setStatus(event.target.value as typeof status)}><option value="open">Somente abertas</option><option value="resolved">Resolvidas</option><option value="all">Todas</option></select></FilterRow>}
    <List>{visible.map(question => { const answer = answers[question.id] ?? { resolved: false, note: '' }; return <Question key={question.id} $resolved={answer.resolved}>
      <div className="head"><div style={{ display: 'flex', gap: 6 }}><Badge $tone={tone(question)}>{question.category}</Badge>{question.specId && <Badge $tone="neutral">{question.specId}</Badge>}</div><Badge $tone={answer.resolved ? 'success' : 'neutral'}>{answer.resolved ? 'Resolvida' : 'Aberta'}</Badge></div>
      <h3>{question.prompt}</h3><p>{question.context}</p>
      {!compact && <textarea aria-label={`Resposta para ${question.prompt}`} value={answer.note} onChange={event => update(question.id, { note: event.target.value })} placeholder="Registre a decisão ou evidência aqui…"/>}
      <div className="actions"><button className="link" onClick={() => onSelect({ ...question, title: question.prompt, _kind: 'decision-question', _eyebrow: 'Pergunta dinâmica', _summary: question.context })}>Ver contexto</button>{!compact && <Button onClick={() => update(question.id, { resolved: !answer.resolved })}>{answer.resolved ? 'Reabrir' : 'Marcar resolvida'}</Button>}</div>
    </Question> })}</List>
    {visible.length === 0 && <p>Nenhuma pergunta neste filtro.</p>}
  </Panel>
}
