import { Badge, Card, Grid, Kicker, Meta, Page, PageHeader, Panel } from '../components/ui'
import { traduzirPapelAgente } from '../lib/i18n'
import type { ExplorerModel, Selection } from '../types/model'

const descriptions: Record<string, string> = {
  discovery: 'Descobre o contexto do sistema, lê a estrutura e reúne dependências e especificações sem modificar o código.',
  planner: 'Transforma uma especificação aprovada em plano de execução, contexto necessário, verificações e dependências.',
  executor: 'Implementa somente o escopo autorizado e produz as evidências técnicas da alteração.',
  reviewer: 'Revisa aderência, testes, riscos e verificações sem reescrever silenciosamente o trabalho.'
}

export function AgentsView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  return <Page><PageHeader><div><Kicker>Papéis delimitados</Kicker><h1>Agentes</h1><p>Quatro contratos com entrada, ferramentas, saída e proibições explícitas. O poder vem do papel; nenhum agente recebe autoridade humana.</p></div><Badge>{model.agents.length} contratos</Badge></PageHeader><Grid $columns={2}>{model.agents.map((agent,index) => { const allowed = agent.tools.allowed as unknown[] ?? []; const outputs = agent.outputs.produces as unknown[] ?? []; const role = traduzirPapelAgente(agent.id, agent.role); const description = descriptions[agent.id] ?? agent.description; return <Card key={agent.id} onClick={() => onSelect({ ...agent, title: role, description, _eyebrow: `Agente ${index + 1}`, _summary: description })}><Meta><Badge>{String(index + 1).padStart(2,'0')}</Badge><Badge $tone="success">{String(agent.capability.strength)}</Badge></Meta><h2>{role}</h2><p>{description}</p><Meta style={{marginTop:14,marginBottom:0}}><Badge $tone="neutral">{allowed.length} ferramentas</Badge><Badge $tone="neutral">{outputs.length} saídas</Badge></Meta></Card>})}</Grid><Panel><h2>Separação de responsabilidade</h2><p><strong>Descoberta</strong> encontra o contexto. <strong>Planejamento</strong> transforma a especificação aprovada em plano. <strong>Execução</strong> altera somente o escopo autorizado. <strong>Revisão</strong> confere evidências e verificações sem reescrever o trabalho.</p></Panel></Page>
}
