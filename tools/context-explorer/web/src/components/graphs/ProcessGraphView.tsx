import { Background, Controls, Handle, MarkerType, Position, ReactFlow, type Edge, type Node, type NodeProps } from '@xyflow/react'
import styled from 'styled-components'
import { traduzir } from '../../lib/i18n'
import type { ExplorerModel, ProcessNode, Selection } from '../../types/model'

type CardData = { entity: ProcessNode; border: string; background: string }

const Shell = styled.section`border:1px solid #d7e0e8;border-radius:16px;background:#fff;overflow:hidden;box-shadow:0 8px 34px rgba(16,47,79,.055);`
const Header = styled.div`display:flex;align-items:center;justify-content:space-between;gap:14px;padding:14px 16px;border-bottom:1px solid #dce4ea;background:#f8fafb;strong{color:#183149}.legend{display:flex;flex-wrap:wrap;gap:12px;color:#64778a;font-size:11px}.item{display:inline-flex;align-items:center;gap:6px}.line{width:20px;height:3px;border-radius:3px;background:#287ba9}.line.human{background:#7655a6}.line.return{background:#bf5a4d}`
const Reading = styled.div`display:flex;flex-wrap:wrap;gap:7px;padding:11px 16px;border-bottom:1px solid #e3e9ee;color:#53677a;font-size:12px;.step{display:inline-flex;align-items:center;gap:7px}.arrow{color:#1671b8;font-weight:800}`

function palette(type?:string){if(type==='human_gate'||type==='human_input'||type==='human_validation'||type==='decision')return{border:'#7655a6',background:'#f6f1fb'};if(type==='human_gate_paused')return{border:'#bf4b40',background:'#fff1ef'};if(type==='terminal'||type==='state')return{border:'#238755',background:'#eef9f3'};return{border:'#287ba9',background:'#f0f7fb'}}

function ProcessCard({data}:NodeProps){const value=data as CardData;const entity=value.entity;return <div style={{position:'relative',minHeight:70,border:`1px solid ${value.border}`,borderLeft:`5px solid ${value.border}`,borderRadius:11,background:value.background,padding:'11px 13px',boxShadow:'0 4px 13px rgba(16,47,79,.06)'}}><small style={{display:'block',marginBottom:5,color:value.border,fontSize:9,fontWeight:800,letterSpacing:'.08em'}}>{traduzir(entity.type??'processo').toUpperCase()}</small><strong style={{display:'block',color:'#183149',fontSize:12,lineHeight:1.35}}>{traduzir(entity.id)}</strong><Handle type="target" id="left" position={Position.Left}/><Handle type="target" id="right" position={Position.Right}/><Handle type="target" id="top" position={Position.Top}/><Handle type="target" id="bottom" position={Position.Bottom}/><Handle type="source" id="s-left" position={Position.Left}/><Handle type="source" id="s-right" position={Position.Right}/><Handle type="source" id="s-top" position={Position.Top}/><Handle type="source" id="s-bottom" position={Position.Bottom}/></div>}

const nodeTypes={processCard:ProcessCard}
const positions:Record<string,[number,number]>={
  story_creation:[0,0],spec_generation:[235,0],spec_review:[470,0],task_planning:[705,0],context_assembly:[940,0],
  implementation:[940,175],check_human_gates:[705,175],gate_execution:[470,175],story_validation:[235,175],validation_decision:[0,175],
  pr_creation:[0,350],completed:[235,350],story_revalidation:[470,350],feedback_task:[235,525],ai_triage:[470,525],paused_blocked:[705,410]
}
const main=['story_creation','spec_generation','spec_review','task_planning','context_assembly','implementation','gate_execution','story_validation','validation_decision']

const edgeStyle={stroke:'#287ba9',strokeWidth:2.2}
const humanStyle={stroke:'#7655a6',strokeWidth:2.2}
const returnStyle={stroke:'#bf5a4d',strokeWidth:2,strokeDasharray:'7 5'}
function edge(id:string,source:string,target:string,label:string|undefined,sourceHandle:string,targetHandle:string,style:Record<string,unknown>,type='smoothstep'):Edge{return{id,source,target,label,sourceHandle,targetHandle,type,markerEnd:{type:MarkerType.ArrowClosed},style,labelStyle:{fill:'#40566a',fontSize:10,fontWeight:750},labelBgStyle:{fill:'#fff',fillOpacity:.97},labelBgPadding:[6,4]}}

export function ProcessGraph({model,onSelect}:{model:ExplorerModel;onSelect:(item:Selection)=>void}){
  const byId=new Map(model.graphs.processGraph.nodes.map(item=>[item.id,item]))
  const nodes:Node[]=Object.entries(positions).map(([id,[x,y]])=>{const entity=byId.get(id)??{id};const colors=palette(entity.type);return{id,type:'processCard',position:{x,y},data:{entity,...colors},style:{width:205}}})
  const edges:Edge[]=[
    edge('e1','story_creation','spec_generation',undefined,'s-right','left',edgeStyle),
    edge('e2','spec_generation','spec_review','spec pronta','s-right','left',edgeStyle),
    edge('e3','spec_review','task_planning','G-SPEC aprovada','s-right','left',humanStyle),
    edge('e4','spec_review','spec_generation','ajustes na spec','s-top','top',returnStyle),
    edge('e5','task_planning','context_assembly',undefined,'s-right','left',edgeStyle),
    edge('e6','context_assembly','implementation',undefined,'s-bottom','top',edgeStyle),
    edge('e7','implementation','check_human_gates',undefined,'s-left','right',edgeStyle),
    edge('e8','check_human_gates','gate_execution','sem gate especial','s-left','right',edgeStyle),
    edge('e9','check_human_gates','paused_blocked','aprovação necessária','s-bottom','top',humanStyle),
    edge('e10','paused_blocked','gate_execution','humano aprovou','s-left','bottom',humanStyle),
    edge('e11','gate_execution','implementation','teste falhou','s-bottom','bottom',returnStyle),
    edge('e12','gate_execution','story_validation','tudo passou','s-left','right',edgeStyle),
    edge('e13','story_validation','validation_decision','conferência registrada','s-left','right',humanStyle),
    edge('e14','validation_decision','pr_creation','está conforme','s-bottom','top',edgeStyle),
    edge('e15','validation_decision','feedback_task','observação ou ausência','s-bottom','left',returnStyle),
    edge('e16','feedback_task','ai_triage','tarefa criada','s-right','left',edgeStyle),
    edge('e17','ai_triage','task_planning','defeito ou item faltante','s-top','bottom',returnStyle),
    edge('e18','ai_triage','story_creation','mudança de necessidade','s-left','bottom',returnStyle),
    edge('e19','pr_creation','completed','PR integrado','s-right','left',edgeStyle),
    edge('e20','completed','story_revalidation','correção arquivada','s-right','left',edgeStyle),
    edge('e21','story_revalidation','story_validation','revalidar','s-top','bottom',humanStyle)
  ]
  return <Shell><Header><strong>Fluxo completo integrado · 16 etapas / 21 transições</strong><div className="legend"><span className="item"><span className="line"/>caminho normal</span><span className="item"><span className="line human"/>ação humana</span><span className="item"><span className="line return"/>retorno para correção</span></div></Header><Reading>{main.map((id,index)=><span className="step" key={id}>{index>0&&<span className="arrow">→</span>}<span>{traduzir(id)}</span></span>)}</Reading><div style={{height:760}}><ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} fitView fitViewOptions={{padding:.12,maxZoom:1.02}} minZoom={.28} maxZoom={1.7} nodesDraggable={false} onNodeClick={(_,node)=>{const entity=(node.data as CardData).entity;onSelect({...entity,title:traduzir(entity.id),_eyebrow:'Etapa do processo',_summary:entity.description,_kind:'processo'})}}><Background color="#dfe6ec" gap={22} size={1}/><Controls showInteractive={false}/></ReactFlow></div></Shell>
}
