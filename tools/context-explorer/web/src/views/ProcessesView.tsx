import { useState } from 'react'
import styled from 'styled-components'
import { BpmnProcessMap, type ProcessKind } from '../components/graphs/BpmnProcessMap'
import { Badge, Grid, Kicker, Page, PageHeader, Panel, Segmented } from '../components/ui'
import type { ExplorerModel, Selection } from '../types/model'

const Legend = styled.div`display:flex;flex-wrap:wrap;gap:14px;color:#5e7082;font-size:12px;.item{display:flex;align-items:center;gap:7px}.line{width:24px;height:3px;border-radius:3px;background:#7892a8}.line.return{background:#c05a4c}.box{width:16px;height:12px;border:2px solid #7650b1;border-radius:3px;background:#f6f1fd}`
const Journey = styled.div`display:flex;gap:8px;overflow-x:auto;padding:3px 1px 10px;.step{display:grid;grid-template-columns:25px minmax(130px,1fr);gap:8px;min-width:190px;align-items:start;border:1px solid #dbe4eb;border-radius:11px;background:#fff;padding:10px}.number{display:grid;width:25px;height:25px;place-items:center;border-radius:50%;background:#eaf4fb;color:#146ca8;font-size:11px;font-weight:800}.step strong{display:block;color:#173753;font-size:12px}.step span:last-child{display:block;margin-top:3px;color:#738395;font-size:10.5px;line-height:1.35}`

const developmentJourney = [
  ['História','PO descreve a necessidade'],['Spec','A IA gera o contrato'],['G-SPEC','PO aprova a spec'],['Execução','IA planeja e agentes implementam'],['Testes','Build e testes precisam passar'],['Homologação','PO confere pelas histórias'],['Pendência','Observação cria uma tarefa'],['Triagem','IA decide código ou história/spec'],['Retorno','Corrige, arquiva e revalida']
]
const productJourney = [
  ['Organização','Define o tenant institucional'],['Equipes','Pessoas e permissões'],['Projeto','Agrupa o trabalho'],['Tarefa','Unidade canônica'],['Quadro','Forma de visualização'],['Execução','Movimentos, horas e evidências'],['Resultado','Conclusão, histórico e relatórios']
]

export function ProcessesView({ onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const [kind,setKind] = useState<ProcessKind>('development')
  return <Page>
    <PageHeader><div><Kicker>Mapa operacional</Kicker><h1>Processos</h1><p>Uma leitura visual no estilo Bizagi: quem escreve a história, o que a IA produz, onde PO decide e como observações voltam para execução.</p></div><Badge $tone="blue">2 processos documentados</Badge></PageHeader>
    <Panel><div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center',flexWrap:'wrap'}}><Segmented><button aria-pressed={kind==='development'} onClick={()=>setKind('development')}>Desenvolvimento do projeto</button><button aria-pressed={kind==='product'} onClick={()=>setKind('product')}>Operação do produto</button></Segmented><Legend><span className="item"><i className="line"/>fluxo normal</span><span className="item"><i className="line return"/>retorno/correção</span><span className="item"><i className="box"/>decisão humana</span></Legend></div></Panel>
    <Panel><h3>Leitura rápida</h3><Journey>{(kind==='development'?developmentJourney:productJourney).map(([title,description],index)=><div className="step" key={title}><span className="number">{index+1}</span><span><strong>{title}</strong><span>{description}</span></span></div>)}</Journey><small>O diagrama abaixo detalha responsáveis e caminhos de retorno. Arraste horizontalmente para explorar e clique em uma etapa para abrir o Inspector.</small></Panel>
    <BpmnProcessMap kind={kind} onSelect={onSelect}/>
    <Grid $columns={3}><Panel><h3>{kind==='development'?'Como o trabalho começa':'Como o produto se organiza'}</h3><p>{kind==='development'?'Toda mudança nasce como história. A IA gera a spec e só planeja a execução depois do G-SPEC.':'Organização, equipes e projeto dão o contexto. A tarefa é a unidade real de trabalho; o quadro é uma forma de visualizá-la.'}</p></Panel><Panel><h3>Onde há decisão humana</h3><p>{kind==='development'?'PO escreve ou corrige a história, aprova a spec, homologa o comportamento e autoriza o deploy. Histórias não têm aprovação individual.':'Gestores definem estrutura e acesso; participantes executam o trabalho conforme suas permissões e os filtros pessoais.'}</p></Panel><Panel><h3>Como ler as voltas</h3><p>{kind==='development'?'Falha técnica volta ao código. Observação cria tarefa e a IA decide se volta à execução ou à história/spec; depois, PO revalida.':'Uma tarefa concluída pode ser reaberta; o estado acompanha a coluna correspondente e o histórico preserva a mudança.'}</p></Panel></Grid>
  </Page>
}
