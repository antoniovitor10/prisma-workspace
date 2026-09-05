import { useEffect, useState } from 'react'
import { Layout } from './components/Layout'
import { useModel } from './hooks/useModel'
import type { Selection } from './types/model'
import { BacklogView } from './views/BacklogView'
import { GapsView } from './views/GapsView'
import { HomeView } from './views/HomeView'
import { HumanGatesView } from './views/HumanGatesView'
import { AIArchitectureView } from './views/AIArchitectureView'
import { ManualValidationView } from './views/ManualValidationView'
import { ProcessesView } from './views/ProcessesView'
import { SpecsView } from './views/SpecsView'
import { StoriesView } from './views/StoriesView'

const routeAliases: Record<string, string> = {
  backlog: 'execution',
  context: 'architecture/context',
  workflow: 'architecture/workflow',
  traceability: 'architecture/traceability',
  engine: 'architecture/engine',
  loop: 'architecture/loop',
  agents: 'architecture/agents'
}

function normalizeRoute(value: string) {
  const clean = value.replace(/^#?\/?/, '') || 'home'
  return routeAliases[clean] ?? clean
}

export default function App() {
  const { model, loading, refreshing, source, error, refresh } = useModel()
  const [route, setRoute] = useState(normalizeRoute(window.location.hash))
  const [selection, setSelection] = useState<Selection>(null)

  useEffect(() => {
    const sync = () => setRoute(normalizeRoute(window.location.hash))
    window.addEventListener('hashchange', sync)
    return () => window.removeEventListener('hashchange', sync)
  }, [])

  if (loading) return <div className="status">Carregando model.json...</div>
  if (error || !model) return <div className="status error">{error ?? 'Modelo indisponível'}</div>

  const navigate = (id: string) => {
    const target = normalizeRoute(id)
    window.location.hash = `/${target}`
    setRoute(target)
    setSelection(null)
  }
  const [view, subview = 'overview'] = route.split('/')
  const views = {
    home: <HomeView model={model} onSelect={setSelection} onNavigate={navigate} />,
    stories: <StoriesView model={model} onSelect={setSelection} />,
    specs: <SpecsView model={model} onSelect={setSelection} />,
    execution: <BacklogView model={model} onSelect={setSelection} />,
    process: <ProcessesView model={model} onSelect={setSelection} />,
    validation: <ManualValidationView model={model} onSelect={setSelection} />,
    gates: <HumanGatesView model={model} onSelect={setSelection} />,
    gaps: <GapsView model={model} onSelect={setSelection} />,
    architecture: <AIArchitectureView model={model} onSelect={setSelection} activeTab={subview} onTabChange={tab => navigate(`architecture/${tab}`)} />
  }

  const content = views[view as keyof typeof views] ?? views.home
  return <Layout view={view in views ? view : 'home'} onNavigate={navigate} selection={selection} onCloseInspector={() => setSelection(null)} generatedAt={model.generatedAt} modelSource={source} refreshing={refreshing} onRefresh={refresh}>{content}</Layout>
}
