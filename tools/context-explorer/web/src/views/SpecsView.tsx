import { useMemo, useState } from 'react'
import { Badge, Card, FilterRow, Grid, Kicker, Meta, Page, PageHeader } from '../components/ui'
import { activeSpecs, declaredGaps, historicalSpecs, specSummary, statusLabel, tasksForSpec, taskTestGates, userStoriesForSpec } from '../lib/insights'
import type { ExplorerModel, Selection } from '../types/model'

export function SpecsView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const [query,setQuery]=useState('')
  const [scope,setScope]=useState<'active'|'history'|'all'>('active')
  const gaps=useMemo(()=>declaredGaps(model),[model])
  const base=scope==='active'?activeSpecs(model):scope==='history'?historicalSpecs(model):model.specs
  const specs=base.filter(spec=>`${spec.id} ${spec.title} ${spec.body}`.toLowerCase().includes(query.toLowerCase()))
  return <Page><PageHeader><div><Kicker>Contrato gerado a partir das histórias</Kicker><h1>Especificações</h1><p>A IA transforma histórias em regras funcionais e técnicas. A aprovação formal acontece aqui pelo G-SPEC, nunca história por história.</p></div><Badge $tone="success">{activeSpecs(model).filter(spec=>spec.status==='approved').length} aprovadas ativas</Badge></PageHeader>
    <FilterRow><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Buscar por ID, título ou conteúdo"/><select value={scope} onChange={e=>setScope(e.target.value as typeof scope)}><option value="active">Somente specs ativas</option><option value="history">Histórico/substituídas</option><option value="all">Todas</option></select><Badge $tone="neutral">{specs.length} resultados</Badge></FilterRow>
    <Grid $columns={2}>{specs.map(spec=>{const tasks=tasksForSpec(model,spec.id);const relatedGaps=gaps.filter(g=>g.specId===spec.id);const stories=userStoriesForSpec(model,spec.id);const testGates=taskTestGates(tasks);return <Card key={spec.id} onClick={()=>onSelect({...spec,_eyebrow:'Especificação canônica',_summary:specSummary(spec),relatedTasks:tasks,declaredGaps:relatedGaps,userStories:stories,requiredTestGates:testGates,contextPlan:model.context.specContexts.find(item=>item.specId===spec.id),_action:{label:'Ver histórias de origem',view:'stories'}})}><Meta><Badge>{spec.id}</Badge><Badge $tone={spec.status==='approved'?'success':spec.status==='superseded'?'neutral':'warning'}>{statusLabel(spec.status)}</Badge></Meta><h2>{spec.title}</h2><p>{specSummary(spec)}</p><Meta style={{marginTop:14,marginBottom:0}}><Badge>{stories.length} histórias</Badge><Badge $tone="neutral">{tasks.length} tarefas</Badge><Badge $tone={relatedGaps.length?'warning':'neutral'}>{relatedGaps.length} lacunas</Badge><Badge $tone="neutral">{testGates.length} verificações exigidas</Badge></Meta></Card>})}</Grid>
  </Page>
}
