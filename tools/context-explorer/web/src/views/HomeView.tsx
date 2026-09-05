import styled from 'styled-components'
import { Badge, Card, Grid, Kicker, Meta, Page, PageHeader, Panel, Stat } from '../components/ui'
import { activeSpecs, openTasks, statusLabel } from '../lib/insights'
import type { ExplorerModel, Selection } from '../types/model'

const Metrics = styled.div`display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:11px;@media(max-width:1050px){grid-template-columns:repeat(2,minmax(0,1fr))}@media(max-width:620px){grid-template-columns:1fr}`
const Flow = styled.div`display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:9px;@media(max-width:1100px){grid-template-columns:repeat(2,minmax(0,1fr))}@media(max-width:700px){grid-template-columns:1fr}.step{position:relative;border:1px solid #dbe4eb;border-radius:12px;background:#fff;padding:14px;text-align:left;cursor:pointer}.number{display:grid;width:24px;height:24px;place-items:center;margin-bottom:9px;border-radius:50%;background:#eaf4fb;color:#146ca8;font-size:11px;font-weight:800}.step strong{display:block;color:#173753;font-size:13px}.step span:last-child{display:block;margin-top:4px;color:#718094;font-size:11px;line-height:1.4}`

const areas = [
  ['stories','Histórias','A necessidade e o comportamento esperado são escritos primeiro.'],
  ['specs','Especificações','A IA transforma histórias em contrato funcional e técnico.'],
  ['gates','G-SPEC','Você aprova a spec; histórias não recebem gate individual.'],
  ['execution','Tarefas e implementação','Agentes planejam, implementam e executam os testes.'],
  ['validation','Homologação','Você confere se cada história está conforme no sistema.'],
  ['gaps','Correção e retorno','Observações geram tarefas e podem voltar à história ou ao código.']
] as const

export function HomeView({ model, onSelect, onNavigate }: { model: ExplorerModel; onSelect: (item: Selection) => void; onNavigate: (view: string) => void }) {
  const specs = activeSpecs(model)
  const stories = model.userStories ?? []
  const plannedTasks = openTasks(model)
  const feedbackTasks = model.storyTasks.filter(task=>['pending_analysis','ready','in_progress','blocked'].includes(task.status))
  const agentTasks = [...plannedTasks, ...feedbackTasks.map(task=>({...task,priority:'Homologação'}))]
  const pendingSpecs = specs.filter(spec=>spec.status!=='approved').length
  return <Page>
    <PageHeader><div><Kicker>Central de controle story-first</Kicker><h1>Visão geral</h1><p>Da necessidade humana à especificação gerada pela IA, implementação, testes e homologação com retorno rastreável.</p></div><Badge $tone={specs.every(spec=>spec.status==='approved')?'success':'warning'}>{specs.filter(spec=>spec.status==='approved').length}/{specs.length} specs ativas aprovadas</Badge></PageHeader>
    <Metrics><Stat><span>Histórias canônicas</span><strong>{stories.length}</strong><small>primeira fonte funcional</small></Stat><Stat><span>Specs com G-SPEC</span><strong>{specs.length-pendingSpecs}/{specs.length}</strong><small>{pendingSpecs===0?'todas aprovadas':'aguardam aprovação'}</small></Stat><Stat><span>Tarefas da homologação</span><strong>{feedbackTasks.length}</strong><small>aguardando análise ou correção</small></Stat><Stat><span>Trabalho dos agentes</span><strong>{agentTasks.length}</strong><small>tarefas abertas no total</small></Stat></Metrics>
    <Panel><h2>Fluxo de trabalho</h2><p>Cada etapa abre a área correspondente. O mapa completo, com responsáveis e retornos, está em Processos.</p><Flow>{areas.map(([id,title,description],index)=><button className="step" key={id} onClick={()=>onNavigate(id)}><span className="number">{index+1}</span><strong>{title}</strong><span>{description}</span></button>)}</Flow></Panel>
    {agentTasks.length>0&&<Panel><div style={{display:'flex',justifyContent:'space-between',gap:14,alignItems:'start',flexWrap:'wrap'}}><div><h2>Trabalho dos agentes</h2><p>Estas tarefas serão executadas por mim ou por outros agentes para aproximar o código das specs. Você pode acompanhar, mas não precisa realizá-las.</p></div><button onClick={()=>onNavigate('execution')}>Ver todas</button></div><Grid $columns={3}>{agentTasks.slice(0,3).map(task=><Card key={task.id} onClick={()=>onSelect({...task,title:task.id,_eyebrow:'Tarefa para os agentes',_summary:task.requirement})}><Meta><Badge>{task.priority}</Badge><Badge $tone={task.status==='in_progress'?'warning':'neutral'}>{statusLabel(task.status)}</Badge></Meta><h3>{task.id}</h3><p>{task.requirement}</p></Card>)}</Grid></Panel>}
    <Grid $columns={2}><Panel><h2>O que precisa da sua atenção</h2><p>{pendingSpecs===0?'Nenhuma spec aguarda G-SPEC. Você pode homologar as histórias já implementadas e registrar diferenças.':`${pendingSpecs} spec(s) ainda precisam de G-SPEC antes da implementação.`}</p><Card onClick={()=>onNavigate('validation')}><Meta><Badge $tone="warning">Sua conferência</Badge></Meta><h3>Continuar homologação</h3><p>Marque se o sistema está conforme. Observações e funcionalidades ausentes criam tarefas automaticamente.</p></Card></Panel><div style={{display:'grid',gap:14}}><Card onClick={()=>onNavigate('process')}><Meta><Badge>Mapa visual</Badge></Meta><h2>Processo story-first completo</h2><p>Veja histórias, specs, tarefas, testes, homologação, triagem da IA e caminhos de retorno.</p></Card><Card onClick={()=>onNavigate('architecture')}><Meta><Badge $tone="success">Preservado</Badge></Meta><h2>Arquitetura de IA</h2><p>Contexto, motor, ciclo de correção, agentes e rastreabilidade permanecem reunidos numa área técnica separada.</p></Card></div></Grid>
  </Page>
}
