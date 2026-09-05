import { useEffect, useState } from 'react'
import type { ExplorerModel } from '../types/model'

export function useModel() {
  const [model, setModel] = useState<ExplorerModel | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [refreshing, setRefreshing] = useState(false)
  const [source, setSource] = useState<'live' | 'snapshot'>('snapshot')
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setRefreshing(true)
    setError(null)

    async function load() {
      try {
        const live = await fetch('/api/model', { signal: controller.signal, cache: 'no-store' })
        const contentType = live.headers.get('content-type') ?? ''
        if (!live.ok || !contentType.includes('application/json')) throw new Error('API local indisponível')
        setModel(await live.json() as ExplorerModel)
        setSource('live')
      } catch (reason) {
        if (reason instanceof Error && reason.name === 'AbortError') return
        try {
          const snapshot = await fetch(`${import.meta.env.BASE_URL}model.json`, { signal: controller.signal, cache: 'no-store' })
          if (!snapshot.ok) throw new Error(`Falha ao carregar model.json (${snapshot.status})`)
          setModel(await snapshot.json() as ExplorerModel)
          setSource('snapshot')
        } catch (fallbackReason) {
          if (fallbackReason instanceof Error && fallbackReason.name !== 'AbortError') setError(fallbackReason.message)
        }
      } finally {
        if (!controller.signal.aborted) setRefreshing(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [revision])

  useEffect(() => {
    const refreshFromChange = () => setRevision(value => value + 1)
    window.addEventListener('context-explorer:model-changed', refreshFromChange)
    return () => window.removeEventListener('context-explorer:model-changed', refreshFromChange)
  }, [])

  return {
    model,
    loading: !model && !error,
    refreshing,
    source,
    error,
    refresh: () => setRevision(value => value + 1)
  }
}
