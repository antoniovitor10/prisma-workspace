import { useMemo, useState } from 'react'
import styled from 'styled-components'
import { Badge, Card, EmptyState, FilterRow, Grid, Kicker, Meta, Page, PageHeader, Panel } from '../components/ui'
import { declaredGaps } from '../lib/insights'
import type { ExplorerModel, Selection } from '../types/model'

const Sections=styled.div`display:grid;gap:22px`

export function GapsView({model,onSelect}:{model:ExplorerModel;onSelect:(item:Selection)=>void}){
 const [query,setQuery]=useState('');const [kind,setKind]=useState<'functional'|'scanner'>('functional')
 const functional=useMemo(()=>declaredGaps(model),[model]).filter(gap=>`${gap.specId} ${gap.heading} ${gap.detail}`.toLowerCase().includes(query.toLowerCase()))
 const scanner=model.gaps.filter(gap=>`${gap.type} ${gap.subject} ${gap.detail} ${gap.path??''}`.toLowerCase().includes(query.toLowerCase()))
 const functionalGroups=Object.values(functional.reduce<Record<string,{specId:string;title:string;items:typeof functional}>>((groups,item)=>{const group=groups[item.specId]??={specId:item.specId,title:item.specTitle,items:[]};group.items.push(item);return groups},{}))
 const scannerGroups=Object.values(scanner.reduce<Record<string,{type:string;items:typeof scanner}>>((groups,item)=>{const group=groups[item.type]??={type:item.type,items:[]};group.items.push(item);return groups},{}))
 return <Page><PageHeader><div><Kicker>Pendências sem mistura</Kicker><h1>Lacunas e decisões</h1><p>Lacunas funcionais vêm dos próprios contratos. Alertas do scanner indicam vínculos, caminhos ou artefatos inconsistentes; uma coisa não é prova da outra.</p></div><Badge $tone={kind==='functional'?'warning':'neutral'}>{kind==='functional'?functional.length:scanner.length} itens</Badge></PageHeader>
 <FilterRow><button onClick={()=>setKind('functional')} aria-pressed={kind==='functional'}>Lacunas declaradas nas specs</button><button onClick={()=>setKind('scanner')} aria-pressed={kind==='scanner'}>Alertas técnicos do scanner</button><input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Buscar nesta lista"/></FilterRow>
 <Sections>{kind==='functional'?<><Panel><h2>Regras ou comportamentos ainda abertos</h2><p>Itens extraídos somente de seções que declaram gap, lacuna ou divergência nas specs ativas. Abra um módulo para conferir a lista e sua fonte.</p></Panel>{functionalGroups.length===0?<EmptyState>Nenhuma lacuna funcional declarada corresponde ao filtro.</EmptyState>:<Grid $columns={2}>{functionalGroups.map(group=><Card key={group.specId} onClick={()=>onSelect({id:group.specId,title:group.title,_eyebrow:'Lacunas declaradas na spec',_summary:`${group.items.length} item(ns) declarado(s)`,items:group.items,source:group.items[0]?.specPath})}><Meta><Badge>{group.specId}</Badge><Badge $tone="warning">{group.items.length} lacunas</Badge></Meta><h3>{group.title}</h3><p>{group.items.slice(0,2).map(item=>item.detail).join(' · ')}{group.items.length>2?'…':''}</p></Card>)}</Grid>}</>:<><Panel><h2>Integridade do modelo</h2><p>Alertas produzidos pelo scanner, agrupados por tipo. Eles localizam referências ou metadados inconsistentes, mas não comprovam implementação funcional.</p></Panel>{scannerGroups.length===0?<EmptyState>Nenhum alerta técnico corresponde ao filtro.</EmptyState>:<Grid $columns={2}>{scannerGroups.map(group=><Card key={group.type} onClick={()=>onSelect({id:group.type,title:group.type,_eyebrow:'Grupo de alertas do scanner',_summary:`${group.items.length} alerta(s)`,items:group.items})}><Meta><Badge $tone={group.items.some(item=>item.severity==='error')?'error':'warning'}>{group.items.length} alertas</Badge></Meta><h3>{group.type}</h3><p>{group.items.slice(0,2).map(item=>item.explanation||item.detail).join(' · ')}{group.items.length>2?'…':''}</p></Card>)}</Grid>}</>}</Sections>
 </Page>
}
