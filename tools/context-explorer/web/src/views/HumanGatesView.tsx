import { useState } from 'react'
import styled from 'styled-components'
import { Badge, Card, Grid, Kicker, Meta, Page, PageHeader, Panel, Button, EmptyState } from '../components/ui'
import { useGates } from '../hooks/useGates'
import type { ExplorerModel, Selection, GateDecision, GateStatus } from '../types/model'

/* ------------------------------------------------------------------ */
/*  Styled helpers (modal, timeline, section)                         */
/* ------------------------------------------------------------------ */

const Overlay = styled.div`
  position: fixed;
  inset: 0;
  z-index: 100;
  display: grid;
  place-items: center;
  background: rgba(16, 47, 79, .45);
  backdrop-filter: blur(3px);
  animation: fade-in .18s ease-out;
  @keyframes fade-in { from { opacity: 0; } to { opacity: 1; } }
`

const Dialog = styled.div`
  width: min(460px, calc(100vw - 40px));
  border: 1px solid #c9d9e5;
  border-radius: 16px;
  background: #fff;
  box-shadow: 0 24px 64px rgba(16, 47, 79, .18);
  padding: 26px;
  animation: slide-up .2s ease-out;
  @keyframes slide-up { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: translateY(0); } }

  h3 { margin: 0 0 6px; color: #102f4f; font-size: 18px; }
  p { margin: 0 0 18px; color: #607185; line-height: 1.55; }
`

const Field = styled.label`
  display: grid;
  gap: 6px;
  margin-bottom: 14px;
  color: #3a5068;
  font-size: 12px;
  font-weight: 680;

  input, textarea {
    width: 100%;
    min-height: 38px;
    border: 1px solid #ccd7e0;
    border-radius: 9px;
    background: #fff;
    color: #17283a;
    padding: 8px 12px;
    font: inherit;
    resize: vertical;
  }
  input:focus, textarea:focus { outline: 3px solid rgba(22, 113, 184, .16); border-color: #4b92c4; }
`

const Actions = styled.div`
  display: flex;
  justify-content: flex-end;
  gap: 9px;
  margin-top: 20px;
`

const SectionTitle = styled.h2`
  margin: 8px 0 14px;
  color: #102f4f;
  font-size: 20px;
  font-weight: 720;
  letter-spacing: -.025em;
`

const Timeline = styled.div`
  display: grid;
  gap: 0;
  border-left: 2px solid #d9e1e8;
  margin-left: 12px;
  padding-left: 20px;
`

const TimelineEntry = styled.div`
  position: relative;
  padding: 12px 0 16px;

  &::before {
    content: '';
    position: absolute;
    left: -27px;
    top: 16px;
    width: 12px;
    height: 12px;
    border-radius: 50%;
    border: 2px solid #d9e1e8;
    background: #fff;
  }

  &[data-status='approved']::before { background: #25935a; border-color: #25935a; }
  &[data-status='rejected']::before { background: #a9362b; border-color: #a9362b; }

  .time { color: #718094; font-size: 11px; margin-bottom: 4px; }
  .detail { color: #3a5068; line-height: 1.5; }
  .detail strong { color: #102f4f; }
`

const SpecCard = styled.div`
  border: 1px solid #d9e1e8;
  border-radius: 14px;
  background: #fff;
  padding: 17px;
  box-shadow: 0 1px 2px rgba(16, 47, 79, .03);

  h3 { margin: 0 0 6px; color: #122f4d; font-size: 15px; line-height: 1.35; }
  p { margin: 0 0 12px; color: #607185; font-size: 13px; line-height: 1.5; }
  .path { color: #718094; font-size: 11px; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
`

const TaskList = styled.ul`
  margin: 8px 0 0;
  padding-left: 18px;
  li { color: #4f6376; margin-bottom: 4px; font-size: 13px; }
  li strong { color: #102f4f; }
`

const StatusDot = styled.span<{ $color: string }>`
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: ${({ $color }) => $color};
  margin-right: 6px;
`

/* ------------------------------------------------------------------ */
/*  Modal de confirmacao                                               */
/* ------------------------------------------------------------------ */

function ConfirmDialog({
  title, description, action, actionLabel, onConfirm, onCancel, loading: submitting
}: {
  title: string
  description: string
  action: 'approve' | 'reject'
  actionLabel: string
  onConfirm: (by: string, reason: string) => void
  onCancel: () => void
  loading: boolean
}) {
  const [by, setBy] = useState('PO')
  const [reason, setReason] = useState('')

  return (
    <Overlay onClick={e => { if (e.target === e.currentTarget) onCancel() }}>
      <Dialog>
        <h3>{title}</h3>
        <p>{description}</p>
        <Field>
          {action === 'approve' ? 'Aprovado por' : 'Rejeitado por'}
          <input value={by} onChange={e => setBy(e.target.value)} placeholder="Nome" />
        </Field>
        <Field>
          Motivo (opcional)
          <textarea value={reason} onChange={e => setReason(e.target.value)} rows={3} placeholder="Descreva o motivo da decisao..." />
        </Field>
        <Actions>
          <Button onClick={onCancel} disabled={submitting}>Cancelar</Button>
          {action === 'approve'
            ? <Button $primary onClick={() => onConfirm(by, reason)} disabled={!by.trim() || submitting}>{submitting ? 'Processando...' : actionLabel}</Button>
            : <Button $danger onClick={() => onConfirm(by, reason)} disabled={!by.trim() || submitting}>{submitting ? 'Processando...' : actionLabel}</Button>
          }
        </Actions>
      </Dialog>
    </Overlay>
  )
}

/* ------------------------------------------------------------------ */
/*  Componente principal                                               */
/* ------------------------------------------------------------------ */

export function HumanGatesView({ model, onSelect }: { model: ExplorerModel; onSelect: (item: Selection) => void }) {
  const { gates, specs, decisions, loading, error, approveGate, rejectGate, refresh } = useGates()
  const [modal, setModal] = useState<{
    action: 'approve' | 'reject'
    gateId: string
    targetId: string
    title: string
    description: string
  } | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  const gSpec = gates.find(g => g.id === 'G-SPEC')
  const operationalGates = gates.filter(g => g.id !== 'G-SPEC')
  const pendingSpecs = specs.filter(s => ['draft', 'review'].includes(s.status.toLowerCase()))
  const approvedSpecs = specs.filter(s => s.status === 'approved')
  const supersededSpecs = specs.filter(s => s.status === 'superseded')

  async function handleConfirm(by: string, reason: string) {
    if (!modal) return
    setSubmitting(true)
    setFeedback(null)
    try {
      if (modal.action === 'approve') {
        await approveGate(modal.gateId, modal.targetId, by, reason || undefined)
        setFeedback({ type: 'success', message: `${modal.gateId} aprovado para ${modal.targetId}` })
      } else {
        await rejectGate(modal.gateId, modal.targetId, by, reason || undefined)
        setFeedback({ type: 'success', message: `${modal.gateId} rejeitado para ${modal.targetId}` })
      }
      setModal(null)
    } catch (err) {
      setFeedback({ type: 'error', message: err instanceof Error ? err.message : 'Erro desconhecido' })
    } finally {
      setSubmitting(false)
    }
  }

  function formatDate(iso: string) {
    try {
      return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
    } catch { return iso }
  }

  if (loading) return <Page><EmptyState>Carregando gates...</EmptyState></Page>
  if (error) return <Page><Panel><p style={{ color: '#a9362b' }}>Erro ao carregar gates: {error}. Verifique se o servidor esta rodando (node server.js na porta 3847).</p><Button onClick={refresh}>Tentar novamente</Button></Panel></Page>

  return (
    <Page>
      <PageHeader>
        <div>
          <Kicker>Autoridade humana</Kicker>
          <h1>Aprovacoes humanas</h1>
          <p>
            Pontos de parada obrigatórios. O motor pode detectar e pausar; somente uma pessoa pode liberar a transição.
            Selecione uma spec ou gate para aprovar ou rejeitar diretamente.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <Badge $tone={pendingSpecs.length > 0 ? 'warning' : 'success'}>
            {pendingSpecs.length > 0 ? `${pendingSpecs.length} spec(s) pendente(s)` : 'Tudo aprovado'}
          </Badge>
          <Button onClick={refresh}>Atualizar</Button>
        </div>
      </PageHeader>

      {feedback && (
        <Panel style={{ borderColor: feedback.type === 'success' ? '#bce6cd' : '#f3c0bb', background: feedback.type === 'success' ? '#edf9f2' : '#fff1ef' }}>
          <p style={{ margin: 0, color: feedback.type === 'success' ? '#147144' : '#a9362b' }}>
            {feedback.message}
          </p>
        </Panel>
      )}

      {/* ---- SECAO 1: G-SPEC ---- */}
      <SectionTitle>Aprovacoes de especificacao (G-SPEC)</SectionTitle>
      <Panel>
        <p style={{ margin: '0 0 4px' }}>
          <strong>Regra:</strong> toda alteracao em codigo de produto, entidades ou APIs exige uma especificacao aprovada.
          Specs em status <code>draft</code> ou <code>review</code> bloqueiam a implementacao e exigem o Human Gate G-SPEC.
        </p>
      </Panel>

      {pendingSpecs.length === 0 && approvedSpecs.length === 0 && supersededSpecs.length === 0 && (
        <EmptyState>Nenhuma especificacao encontrada no diretorio specs/</EmptyState>
      )}

      {pendingSpecs.length > 0 && (
        <Grid $columns={2}>
          {pendingSpecs.map(spec => (
            <SpecCard key={spec.id}>
              <Meta>
                <Badge $tone="warning">{spec.status}</Badge>
                <Badge>{spec.id}</Badge>
              </Meta>
              <h3
                style={{ cursor: 'pointer' }}
                onClick={() => onSelect({
                  id: spec.id,
                  title: spec.title,
                  _eyebrow: 'Especificacao aguardando aprovacao',
                  _summary: `Status atual: ${spec.status}. Leia o documento completo abaixo antes de aprovar.`,
                  status: spec.status,
                  path: spec.path,
                  body: spec.body
                })}
              >
                {spec.title}
              </h3>
              <p className="path">{spec.path}</p>
              <Actions style={{ justifyContent: 'flex-start' }}>
                <Button onClick={() => onSelect({
                  id: spec.id,
                  title: spec.title,
                  _eyebrow: 'Especificacao aguardando aprovacao',
                  _summary: `Status atual: ${spec.status}. Leia o documento completo abaixo antes de aprovar.`,
                  status: spec.status,
                  path: spec.path,
                  body: spec.body
                })}>
                  Ler spec
                </Button>
                <Button $primary onClick={() => setModal({
                  action: 'approve',
                  gateId: 'G-SPEC',
                  targetId: spec.path,
                  title: `Aprovar ${spec.id}`,
                  description: `Tem certeza que deseja aprovar a especificacao "${spec.title}"? O status sera alterado de "${spec.status}" para "approved" diretamente no arquivo ${spec.path}.`
                })}>
                  Aprovar
                </Button>
                <Button $danger onClick={() => setModal({
                  action: 'reject',
                  gateId: 'G-SPEC',
                  targetId: spec.path,
                  title: `Rejeitar ${spec.id}`,
                  description: `Rejeitar a especificacao "${spec.title}". O arquivo permanecera com status "${spec.status}" e a rejeicao sera registrada no historico.`
                })}>
                  Rejeitar
                </Button>
              </Actions>
            </SpecCard>
          ))}
        </Grid>
      )}

      {approvedSpecs.length > 0 && (
        <details style={{ marginTop: 16 }}>
          <summary style={{ cursor: 'pointer', color: '#607185', fontSize: 13, fontWeight: 680 }}>
            Especificações já aprovadas ({approvedSpecs.length}) — somente consulta
          </summary>
          <Grid $columns={3} style={{ marginTop: 10 }}>
            {approvedSpecs.map(spec => (
              <Card key={spec.id} onClick={() => onSelect({
                id: spec.id,
                title: spec.title,
                _eyebrow: 'Especificacao aprovada',
                _summary: `Status: approved | Arquivo: ${spec.path}`,
                status: spec.status,
                path: spec.path,
                body: spec.body
              })}>
                <Meta>
                  <Badge $tone="success">aprovada</Badge>
                  <Badge $tone="neutral">{spec.id}</Badge>
                </Meta>
                <h3>{spec.title}</h3>
              </Card>
            ))}
          </Grid>
        </details>
      )}

      {supersededSpecs.length > 0 && (
        <details style={{ marginTop: 16 }}>
          <summary style={{ cursor: 'pointer', color: '#607185', fontSize: 13, fontWeight: 680 }}>
            Especificações substituídas ({supersededSpecs.length}) — não exigem aprovação
          </summary>
          <Grid $columns={3} style={{ marginTop: 10 }}>
            {supersededSpecs.map(spec => (
              <Card key={spec.id} onClick={() => onSelect({
                id: spec.id,
                title: spec.title,
                _eyebrow: 'Especificação substituída',
                _summary: `Status: superseded | Arquivo: ${spec.path}`,
                status: spec.status,
                path: spec.path,
                body: spec.body
              })}>
                <Meta><Badge $tone="neutral">substituída</Badge><Badge $tone="neutral">{spec.id}</Badge></Meta>
                <h3>{spec.title}</h3>
              </Card>
            ))}
          </Grid>
        </details>
      )}

      {/* ---- SECAO 2: Gates operacionais ---- */}
      <SectionTitle style={{ marginTop: 32 }}>Decisões especiais</SectionTitle>
      <Panel style={{ borderColor: '#bce6cd', background: '#edf9f2' }}>
        <p style={{ margin: 0 }}>
          <strong>Nenhuma decisão especial está registrada como pendente agora.</strong> Quando uma implementação precisar alterar banco, workflow, histórico, escopo ou entrar em produção, o agente pausa e informa exatamente qual autorização você precisa conceder.
        </p>
      </Panel>
      <details style={{ marginTop: 12 }}>
      <summary style={{ cursor: 'pointer', color: '#607185', fontSize: 13, fontWeight: 680 }}>Entender ou registrar um gate operacional</summary>
      <Panel style={{ marginTop: 10 }}><p style={{ margin: 0 }}><strong>Em linguagem simples:</strong> estes gates são travas de segurança. Eles não são tarefas nem testes; impedem que agentes tomem sozinhos decisões de alto impacto.</p></Panel>
      <Grid $columns={2} style={{ marginTop: 10 }}>
        {operationalGates.map(gate => {
          const approved = gate.decisions.filter(d => d.status === 'approved')
          const rejected = gate.decisions.filter(d => d.status === 'rejected')

          return (
            <SpecCard key={gate.id} role="button" tabIndex={0} onClick={() => onSelect({
              id: gate.id,
              title: gate.id,
              _eyebrow: 'Gate operacional',
              _summary: gate.description,
              condition: gate.condition,
              always_required: gate.always_required,
              status: gate.status,
              tasks: gate.tasks,
              decisions: gate.decisions
            })}>
              <Meta>
                <Badge $tone="warning">{gate.id}</Badge>
                <Badge $tone={gate.always_required ? 'error' : 'blue'}>
                  {gate.always_required ? 'Sempre obrigatorio' : 'Condicional'}
                </Badge>
                {approved.length > 0 && <Badge $tone="success">{approved.length} aprovacao(oes)</Badge>}
                {rejected.length > 0 && <Badge $tone="error">{rejected.length} rejeicao(oes)</Badge>}
              </Meta>
              <h3>{gate.description}</h3>
              <p style={{ fontSize: 12, color: '#718094', marginBottom: 8 }}>{gate.condition}</p>

              {gate.tasks.length > 0 && (
                <TaskList>
                  {gate.tasks.map(t => (
                    <li key={t.id}><strong>{t.id}</strong> ({t.priority}) - {t.requirement?.slice(0, 80)}{(t.requirement?.length || 0) > 80 ? '...' : ''}</li>
                  ))}
                </TaskList>
              )}

              <Actions style={{ justifyContent: 'flex-start', marginTop: 12 }}>
                <Button $primary onClick={e => {
                  e.stopPropagation()
                  setModal({
                    action: 'approve',
                    gateId: gate.id,
                    targetId: gate.tasks[0]?.id || gate.id,
                    title: `Aprovar ${gate.id}`,
                    description: `Registrar aprovacao para o gate ${gate.id}. Especifique o alvo (ex: TASK-001) e o motivo.`
                  })
                }}>
                  Registrar aprovacao
                </Button>
                <Button $danger onClick={e => {
                  e.stopPropagation()
                  setModal({
                    action: 'reject',
                    gateId: gate.id,
                    targetId: gate.tasks[0]?.id || gate.id,
                    title: `Rejeitar ${gate.id}`,
                    description: `Registrar rejeicao para o gate ${gate.id}. Especifique o alvo e o motivo.`
                  })
                }}>
                  Registrar rejeicao
                </Button>
              </Actions>
            </SpecCard>
          )
        })}
      </Grid>
      </details>

      {/* ---- SECAO 3: Historico de decisoes ---- */}
      <SectionTitle style={{ marginTop: 32 }}>Historico de decisoes</SectionTitle>

      {decisions.length === 0 ? (
        <EmptyState>Nenhuma decisao registrada ainda. Aprove ou rejeite um gate para iniciar o historico.</EmptyState>
      ) : (
        <Panel>
          <Timeline>
            {[...decisions].reverse().map((d, i) => (
              <TimelineEntry key={i} data-status={d.status}>
                <div className="time">{formatDate(d.timestamp)}</div>
                <div className="detail">
                  <Meta style={{ marginBottom: 6 }}>
                    <Badge $tone={d.status === 'approved' ? 'success' : 'error'}>
                      <StatusDot $color={d.status === 'approved' ? '#25935a' : '#a9362b'} />
                      {d.status === 'approved' ? 'Aprovado' : 'Rejeitado'}
                    </Badge>
                    <Badge>{d.gateId}</Badge>
                    <Badge $tone="neutral">{d.targetId}</Badge>
                  </Meta>
                  <strong>Por:</strong> {d.approvedBy}
                  {d.reason && <> | <strong>Motivo:</strong> {d.reason}</>}
                </div>
              </TimelineEntry>
            ))}
          </Timeline>
        </Panel>
      )}

      {/* ---- Modal ---- */}
      {modal && (
        <ConfirmDialog
          title={modal.title}
          description={modal.description}
          action={modal.action}
          actionLabel={modal.action === 'approve' ? 'Confirmar aprovacao' : 'Confirmar rejeicao'}
          onConfirm={handleConfirm}
          onCancel={() => setModal(null)}
          loading={submitting}
        />
      )}
    </Page>
  )
}
