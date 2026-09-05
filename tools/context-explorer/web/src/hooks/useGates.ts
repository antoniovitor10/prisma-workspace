import { useCallback, useEffect, useRef, useState } from 'react'
import type { GateDecision, GatesResponse, GateStatus, SpecGateInfo } from '../types/model'

export function useGates() {
  const [gates, setGates] = useState<GateStatus[]>([])
  const [specs, setSpecs] = useState<SpecGateInfo[]>([])
  const [decisions, setDecisions] = useState<GateDecision[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const retries = useRef(0)

  const refresh = useCallback(() => {
    setError(null)
    setLoading(true)
    fetch('/api/gates', { cache: 'no-store' })
      .then(res => {
        if (!res.ok) throw new Error(`Falha ao carregar gates (${res.status})`)
        return res.json() as Promise<GatesResponse>
      })
      .then(data => {
        retries.current = 0
        setGates(data.gates)
        setSpecs(data.specs)
        setDecisions(data.decisions)
        setLoading(false)
      })
      .catch(err => {
        // A API pode ainda estar subindo junto com o Vite: tenta de novo antes de desistir.
        if (retries.current < 2) {
          retries.current += 1
          setTimeout(refresh, 1200)
          return
        }
        setError(err.message)
        setLoading(false)
      })
  }, [])

  useEffect(() => { refresh() }, [refresh])

  const approveGate = useCallback(async (gateId: string, targetId: string, approvedBy: string, reason?: string) => {
    const res = await fetch('/api/gates/approve', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gateId, targetId, approvedBy, reason: reason || null })
    })
    const data = await res.json()
    if (!res.ok) throw new Error(data.message || 'Falha ao aprovar gate')
    refresh()
    return data
  }, [refresh])

  const rejectGate = useCallback(async (gateId: string, targetId: string, rejectedBy: string, reason?: string) => {
    const res = await fetch('/api/gates/reject', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gateId, targetId, rejectedBy, reason: reason || null })
    })
    const data = await res.json()
    if (!res.ok) throw new Error(data.message || 'Falha ao rejeitar gate')
    refresh()
    return data
  }, [refresh])

  return { gates, specs, decisions, loading, error, refresh, approveGate, rejectGate }
}
