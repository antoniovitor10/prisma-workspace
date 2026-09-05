import { memo, useMemo } from 'react'
import { Background, Controls, Handle, MarkerType, Position, ReactFlow, type Edge, type Node, type NodeProps } from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import styled from 'styled-components'
import { GraphShell } from '../ui'
import type { Selection } from '../../types/model'

export type ProcessKind = 'development' | 'product'

interface StepData extends Record<string, unknown> {
  label: string
  kind: 'event' | 'activity' | 'human' | 'gateway' | 'system'
  owner: string
  description: string
  output: string
  reference?: string
}

const Step = styled.div<{ $kind: StepData['kind'] }>`
  display:grid;align-content:center;width:${({$kind})=>$kind === 'event' ? '86px' : $kind === 'gateway' ? '112px' : '190px'};min-height:${({$kind})=>$kind === 'event' ? '86px' : '76px'};
  border:${({$kind})=>$kind === 'event' ? '3px' : '1px'} solid ${({$kind})=>$kind === 'human' ? '#7650b1' : $kind === 'system' ? '#21865b' : $kind === 'gateway' ? '#c58518' : '#287cb6'};
  border-radius:${({$kind})=>$kind === 'event' ? '50%' : $kind === 'gateway' ? '18px' : '12px'};
  background:${({$kind})=>$kind === 'human' ? '#f6f1fd' : $kind === 'system' ? '#edf8f2' : $kind === 'gateway' ? '#fff8e8' : '#fff'};
  color:#14324e;padding:${({$kind})=>$kind === 'event' ? '8px' : '12px 14px'};box-shadow:0 4px 13px rgba(16,47,79,.09);text-align:center;
  .type{margin-bottom:5px;color:#6c7c8d;font-size:9px;font-weight:800;letter-spacing:.09em;text-transform:uppercase}.label{font-size:13px;font-weight:760;line-height:1.25}
`

const Lane = styled.div`
  width:3540px;height:132px;border:1px solid #d8e2ea;border-left:7px solid #98b4c9;border-radius:14px;background:rgba(255,255,255,.66);pointer-events:none;
  .name{position:absolute;inset:0 auto 0 0;display:grid;width:126px;place-items:center;border-right:1px solid #d8e2ea;background:#edf4f8;color:#21435e;font-size:12px;font-weight:780;text-align:center;padding:12px}
`

const StepNode = memo(({ data }: NodeProps<Node<StepData>>) => <Step $kind={data.kind}><Handle type="target" position={Position.Left}/><div className="type">{data.kind === 'human' ? 'decisão humana' : data.kind === 'gateway' ? 'decisão' : data.kind === 'system' ? 'automação' : data.kind === 'event' ? 'evento' : 'atividade'}</div><div className="label">{data.label}</div><Handle type="source" position={Position.Right}/></Step>)
const LaneNode = memo(({ data }: NodeProps<Node<{ label: string }>>) => <Lane><div className="name">{data.label}</div></Lane>)

const nodeTypes = { step: StepNode, lane: LaneNode }

type StepDefinition = [string,string,StepData['kind'],number,number,string,string,string,string?]

const developmentSteps: StepDefinition[] = [
  ['story','Escrever história','human',170,20,'PO','Registra necessidade, ator e comportamento esperado antes da solução técnica.','História canônica'],
  ['spec','IA gera a espcificação','system',410,160,'IA / Agentes','Transforma histórias em contrato técnico verificável.','Spec em revisão'],
  ['gspec','Aprovar especificação','human',650,20,'PO','Aprova somente a especificação; a história não recebe gate individual.','Spec aprovada','G-SPEC'],
  ['tasks','IA planeja tarefas','system',890,160,'IA / Agentes','Decompõe a spec aprovada em trabalho rastreável.','Tarefas e dependências'],
  ['context','Montar contexto','system',1130,160,'IA / Agentes','Seleciona história, spec, código, testes e gates necessários.','Plano de contexto'],
  ['implement','Implementar','activity',1370,160,'IA / Agentes','Executa somente o escopo aprovado.','Código e evidências'],
  ['gates','Checar gates especiais','gateway',1610,160,'IA / Agentes','Pausa quando migration, workflow, histórico, escopo ou deploy exigem decisão humana.','Gate liberado ou pausa'],
  ['test','Build e testes','system',1850,300,'CI / Testes','Executa verificações automatizadas e retorna falhas à implementação.','Resultado técnico'],
  ['manual','Homologar as histórias','human',2090,20,'PO','Confere o produto e registra se está conforme, ausente ou diferente.','Resultado da homologação'],
  ['decision','Está conforme?','gateway',2330,20,'PO','Separa o aceite de observações e funcionalidades ausentes.','Aceite ou pendência'],
  ['feedback','Criar tarefa de ajuste','system',2570,160,'Sistema','Cria ou atualiza uma tarefa vinculada à história e à spec.','Tarefa de homologação'],
  ['triage','IA faz a triagem','system',2810,160,'IA / Agentes','Classifica defeito, item faltante, mudança de história/spec ou bloqueio de teste.','Destino da correção'],
  ['release','Preparar entrega','activity',3050,440,'IA / Agentes','Reúne rastreabilidade, PR e evidências.','Release candidato'],
  ['deploy','Aprovar DEPLOY','human',2810,20,'PO','Autoriza promoção para ambiente institucional.','Liberação','G-DEPLOY'],
  ['production','Produção','event',3290,440,'Produção','Publica com backup, ponto de retorno e validação.','Versão disponível'],
]

const productSteps: StepDefinition[] = [
  ['org','Organização','event',170,20,'Administração','Define o ambiente institucional e seus membros.','Organização ativa'],
  ['teams','Equipes e pessoas','activity',410,20,'Gestão','Organiza disponibilidade, acesso e alocação.','Equipe disponível'],
  ['project','Projeto','activity',650,160,'Gestão','Agrupa objetivo, trabalho, quadros e acompanhamento.','Projeto ativo'],
  ['task','Criar tarefa','activity',890,160,'Equipe','Registra a unidade canônica de trabalho.','Tarefa no backlog'],
  ['board','Exibir em quadro','activity',1130,300,'Equipe','Projeta a mesma tarefa em uma visão de trabalho.','Tarefa visível'],
  ['column','Mover na coluna','activity',1370,300,'Equipe','A posição da coluna representa o estado da tarefa.','Estado atualizado'],
  ['work','Executar trabalho','activity',1610,160,'Participantes','Aponta horas, subtarefas, responsáveis e anexos.','Evidências do trabalho'],
  ['done','Concluir','gateway',1850,160,'Equipe','Confirma que o trabalho atingiu a condição de conclusão.','Tarefa concluída'],
  ['history','Histórico','system',2070,300,'Sistema','Registra mudanças relevantes e movimentações.','Trilha de auditoria'],
  ['reports','Relatórios','event',2070,20,'Gestão','Consolida trabalho, pessoas, tempo e andamento.','Visão gerencial']
]

function makeGraph(kind: ProcessKind) {
  const lanes = kind === 'development'
    ? [['PO',0],['OmniRoute / Agentes',140],['CI / Testes',280],['Produção',420]] as const
    : [['Gestão',0],['Planejamento',140],['Execução',280]] as const
  const steps = kind === 'development' ? developmentSteps : productSteps
  const nodes: Node[] = [
    ...lanes.map(([label,y],index) => ({ id:`lane:${index}`, type:'lane', position:{x:0,y}, data:{label}, selectable:false, draggable:false, connectable:false, zIndex:-1 })),
    ...steps.map(([id,label,stepKind,x,y,owner,description,output,reference]) => ({ id, type:'step', position:{x,y:y+23}, data:{label,kind:stepKind,owner,description,output,reference}, draggable:false }))
  ]
  const pairs = kind === 'development'
    ? [['story','spec'],['spec','gspec'],['gspec','tasks'],['tasks','context'],['context','implement'],['implement','gates'],['gates','test'],['test','manual'],['manual','decision'],['decision','deploy'],['deploy','release'],['release','production'],['decision','feedback'],['feedback','triage'],['production','revalidate']]
    : [['org','teams'],['teams','project'],['project','task'],['task','board'],['board','column'],['column','work'],['work','done'],['done','history'],['history','reports']]
  const edges: Edge[] = pairs.map(([source,target]) => ({ id:`${source}-${target}`, source,target,type:'smoothstep',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#7892a8',strokeWidth:2} }))
  if (kind === 'development') {
    edges.push(
      {id:'spec-fix',source:'gspec',target:'spec',type:'smoothstep',label:'ajustar spec',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#c05a4c',strokeWidth:2}},
      {id:'test-fix',source:'test',target:'implement',type:'smoothstep',label:'corrigir código',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#c05a4c',strokeWidth:2}},
      {id:'triage-code',source:'triage',target:'tasks',type:'smoothstep',label:'defeito ou item faltante',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#c05a4c',strokeWidth:2}},
      {id:'triage-story',source:'triage',target:'story',type:'smoothstep',label:'mudança de necessidade',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#c05a4c',strokeWidth:2}},
      {id:'revalidate-manual',source:'revalidate',target:'manual',type:'smoothstep',label:'conferir novamente',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#7650b1',strokeWidth:2}}
    )
  } else {
    edges.push({id:'reopen',source:'done',target:'column',type:'smoothstep',label:'reabrir',markerEnd:{type:MarkerType.ArrowClosed},style:{stroke:'#c58518',strokeWidth:2}})
  }
  return { nodes, edges }
}

export function BpmnProcessMap({ kind, onSelect }: { kind: ProcessKind; onSelect: (item: Selection) => void }) {
  const graph = useMemo(() => makeGraph(kind), [kind])
  return <GraphShell $height={660}><ReactFlow nodes={graph.nodes} edges={graph.edges} nodeTypes={nodeTypes} defaultViewport={{x:12,y:18,zoom:kind==='development'?.52:.72}} minZoom={.28} maxZoom={1.35} nodesDraggable={false} onNodeClick={(_,node) => { if (node.type !== 'lane') onSelect({ id:node.id,title:String(node.data.label),_eyebrow:`Processo · ${String(node.data.owner)}`,_summary:String(node.data.description),output:node.data.output,gate:node.data.reference }) }}><Background color="#dbe5ec" gap={22}/><Controls showInteractive={false}/></ReactFlow></GraphShell>
}
