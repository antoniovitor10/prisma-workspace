import { useMemo, useState } from 'react'
import styled from 'styled-components'
import { Badge, EmptyState, FilterRow, Kicker, Meta, Page, PageHeader, Panel, Stat } from '../components/ui'
import { activeSpecs, userStoriesForSpec } from '../lib/insights'
import type { ExplorerModel, Selection } from '../types/model'

const Metrics = styled.div`display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px;@media(max-width:800px){grid-template-columns:1fr}`
const Groups = styled.div`display:grid;gap:12px`
const Group = styled.details`overflow:hidden;border:1px solid #d8e2e9;border-radius:15px;background:#fff;summary{display:flex;align-items:center;justify-content:space-between;gap:14px;padding:17px 19px;cursor:pointer;list-style:none}summary::-webkit-details-marker{display:none}.items{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;padding:12px;border-top:1px solid #e4eaef;background:#f8fafb}@media(max-width:900px){.items{grid-template-columns:1fr}}`
const Story = styled.button`display:grid;gap:8px;border:1px solid #dce5ec;border-radius:12px;background:#fff;padding:14px;text-align:left;cursor:pointer;h3{margin:0;color:#173b5c;font-size:14px}.narrative{margin:0;color:#5d6f82;font-size:12px;line-height:1.5}.expected{border-left:3px solid #7cb4d7;padding-left:9px;color:#35566f;font-size:11px;line-height:1.45}&:hover{border-color:#7cb4d7;box-shadow:0 8px 22px rgba(16,47,79,.07)}`

export function StoriesView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const [query, setQuery] = useState('')
  const [specId, setSpecId] = useState('all')
  const specs = activeSpecs(model)
  const stories = useMemo(() => (model.userStories ?? []).filter(story => (specId === 'all' || story.specId === specId) && `${story.id} ${story.title} ${story.narrative} ${story.specTitle}`.toLowerCase().includes(query.toLowerCase())), [model, query, specId])
  const groups = specs.map(spec => ({ spec, stories: stories.filter(story => story.specId === spec.id) })).filter(group => group.stories.length)
  const linkedSpecs = new Set((model.userStories ?? []).map(story => story.specId)).size

  return <Page>
    <PageHeader><div><Kicker>Primeira etapa do fluxo</Kicker><h1>Histórias do produto</h1><p>A necessidade e o comportamento esperado são escritos aqui primeiro. Depois, a IA cria ou revisa a especificação técnica relacionada.</p></div><Badge $tone="success">fonte funcional humana</Badge></PageHeader>
    <Panel><strong>Sem aprovação individual:</strong><p style={{marginBottom:0}}>Histórias não recebem gate. O G-SPEC aprova a especificação criada pela IA. Depois da implementação, estas mesmas histórias orientam a homologação.</p></Panel>
    <Metrics><Stat><span>Histórias canônicas</span><strong>{model.userStories.length}</strong><small>em {model.storyCatalog.path}</small></Stat><Stat><span>Specs relacionadas</span><strong>{linkedSpecs}</strong><small>contratos gerados ou migrados</small></Stat><Stat><span>Origem inicial</span><strong>Baseline</strong><small>catálogo atual migrado das specs já aprovadas</small></Stat></Metrics>
    <FilterRow><input value={query} onChange={event=>setQuery(event.target.value)} placeholder="Buscar história ou comportamento"/><select value={specId} onChange={event=>setSpecId(event.target.value)}><option value="all">Todos os módulos</option>{specs.map(spec=><option key={spec.id} value={spec.id}>{spec.title}</option>)}</select><Badge $tone="neutral">{stories.length} histórias</Badge></FilterRow>
    {groups.length===0?<EmptyState>Nenhuma história encontrada.</EmptyState>:<Groups>{groups.map(({spec,stories:items})=><Group key={spec.id}><summary><span><strong>{spec.title}</strong><Meta style={{marginTop:7,marginBottom:0}}><Badge>{spec.id}</Badge><Badge $tone="neutral">{items.length} histórias</Badge></Meta></span><Badge $tone="neutral">abrir</Badge></summary><div className="items">{items.map(story=><Story key={story.id} onClick={()=>onSelect({...story,_kind:'user-story',_eyebrow:'História canônica',_summary:story.narrative,relatedSpec:model.specs.find(item=>item.id===story.specId)})}><Meta><Badge>{story.id}</Badge></Meta><h3>{story.title}</h3><p className="narrative">{story.narrative}</p><div className="expected"><strong>Resultado esperado:</strong> {story.scenario.then}</div></Story>)}</div></Group>)}</Groups>}
  </Page>
}
