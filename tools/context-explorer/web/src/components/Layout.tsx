import { useEffect, type ReactNode } from 'react'
import styled from 'styled-components'
import { Sidebar, navigation } from './Sidebar'
import { Inspector } from './Inspector'
import { Button } from './ui'
import type { Selection } from '../types/model'

const Main = styled.main<{ $inspectorOpen: boolean }>`
  min-height: 100vh;
  margin-left: 252px;
  margin-right: ${({ $inspectorOpen }) => $inspectorOpen ? '366px' : '0'};
  padding: 24px clamp(20px, 2.3vw, 38px) 48px;
  transition: margin-right .18s ease;

  @media (max-width: 1280px) { margin-right: 0; }
  @media (max-width: 820px) { margin-left: 82px; padding-inline: 16px; }
`

const Topbar = styled.header`
  display: flex;
  min-height: 38px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 23px;
  color: #718094;
  font-size: 11px;

  .crumb { display: flex; align-items: center; gap: 7px; }
  .crumb strong { color: #294761; font-weight: 720; }
  .snapshot { display: inline-flex; align-items: center; gap: 7px; }
  .dot { width: 7px; height: 7px; border-radius: 50%; background: #25935a; box-shadow: 0 0 0 4px #e8f7ef; }
  .model { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; justify-content: flex-end; }
`

export function Layout({ view, onNavigate, selection, onCloseInspector, generatedAt, modelSource, refreshing, onRefresh, children }: { view: string; onNavigate: (id: string) => void; selection: Selection; onCloseInspector: () => void; generatedAt: string; modelSource: 'live' | 'snapshot'; refreshing: boolean; onRefresh: () => void; children: ReactNode }) {
  useEffect(() => {
    const close = (event: KeyboardEvent) => { if (event.key === 'Escape') onCloseInspector() }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [onCloseInspector])

  const label = navigation.find(([id]) => id === view)?.[1] ?? view
  const snapshot = new Date(generatedAt)
  return <>
    <Sidebar active={view} onNavigate={onNavigate} />
    <Main $inspectorOpen={Boolean(selection)}>
      <Topbar><div className="crumb"><span>AI-Native</span><span>/</span><strong>{label}</strong></div><div className="model"><div className="snapshot"><span className="dot"/>{modelSource === 'live' ? 'Modelo atualizado pela API' : 'Snapshot local'} · {Number.isNaN(snapshot.getTime()) ? generatedAt : snapshot.toLocaleString('pt-BR')}</div><Button onClick={onRefresh} disabled={refreshing}>{refreshing ? 'Atualizando…' : 'Atualizar modelo'}</Button></div></Topbar>
      {children}
    </Main>
    <Inspector selection={selection} onClose={onCloseInspector} onNavigate={onNavigate} />
  </>
}
