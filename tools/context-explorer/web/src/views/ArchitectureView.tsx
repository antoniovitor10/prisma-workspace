import { GraphCanvas } from '../components/GraphCanvas'
import { Badge, Card, Grid, Kicker, Meta, Page, PageHeader, Panel, Split } from '../components/ui'
import { traduzir } from '../lib/i18n'
import type { ExplorerModel, Selection } from '../types/model'

export function ArchitectureView({model,onSelect}:{model:ExplorerModel;onSelect:(item:Selection)=>void}){
  const nodes=model.architecture.nodes.filter(node=>node.id!=='OMNIROUTE_READINESS').map(node=>({...node,label:traduzir(node.label??node.id),_eyebrow:'Componente da arquitetura'}))
  return <Page><PageHeader><div><Kicker>Visão integrada</Kicker><h1>Arquitetura</h1><p>Como histórias, especificações, Engenharia de Contexto, agentes, testes e homologação humana se conectam sem perder a arquitetura AI-Native.</p></div><Badge $tone="blue">mapa conceitual</Badge></PageHeader><Split $aside={340}><GraphCanvas nodes={nodes} edges={model.architecture.edges} onSelect={onSelect} direction="LR" height={720}/><Panel><h2>Do pedido à entrega</h2><p>Pessoa → história → spec gerada pela IA → G-SPEC → tarefa → contexto → agente → testes → homologação.</p><p>Se houver diferença, o sistema cria uma tarefa, a IA classifica e o fluxo retorna ao código ou à história/spec.</p></Panel></Split><Panel><h2>Componentes de apoio</h2><Grid $columns={3}>{model.architecture.laterals.filter(id=>id!=='OMNIROUTE_READINESS').map(id=>{const node=nodes.find(item=>item.id===id);return <Card key={id} onClick={()=>onSelect(node??{id})}><Meta><Badge $tone="neutral">apoio</Badge></Meta><h3>{node?.label??traduzir(id)}</h3><p>{id==='STATE'?'Estado persistido da execução.':id==='TELEMETRY'?'Sinais operacionais sem segredos.':'Transferência explícita de contexto entre etapas.'}</p></Card>})}</Grid></Panel></Page>
}
