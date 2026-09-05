export interface Section { heading: string; level: number; body: string }
export interface Spec { id: string; title: string; status: string; path: string; body: string; sections: Section[]; [key: string]: unknown }
export interface AcceptanceScenario { given: string; when: string; then: string; raw: string }
export interface UserStory { id: string; specId: string; specTitle: string; specPath: string; sourceHeading: string; title: string; narrative: string; actor: string; scenario: AcceptanceScenario; status?: string; origin?: string }
export interface StoryTask { id: string; storyId: string; specId: string; specPath: string; title: string; requirement: string; observation: string; sourceResult: string; classification: string; status: string; createdAt: string; updatedAt: string; archivedAt?: string | null; resolution?: string }
export interface Task { id: string; specId: string; specPath: string; domain: string; type: string; risk: string; dependencies: string[]; affected_areas: string[]; gates: string[]; human_gate: string; status: string; priority: string; requirement: string; [key: string]: unknown }
export interface Agent { id: string; path: string; role: string; description: string; capability: Record<string, unknown>; context: Record<string, unknown>; tools: Record<string, unknown>; outputs: Record<string, unknown>; constraints: Record<string, unknown>; [key: string]: unknown }
export interface Gap { type: string; subject: string; detail: string; source: string; severity: string; path: string | null; resolvedPath: string | null; reason: string; explanation: string; [key: string]: unknown }
export interface HumanGate { id: string; description: string; declared_in_node: string; auto_approval: boolean; always_required: boolean; condition?: string; enforced_by?: string; note?: string }
export interface GraphNode { id: string; type?: string; label?: string; description?: string; [key: string]: unknown }
export interface GraphEdge { from: string; to: string; relation?: string; condition?: string; label?: string }
export interface ProcessNode extends GraphNode { status?: string }
export interface EngineNode extends GraphNode { state?: string }
export interface ContextArtifact { path: string; category: string; domain: string | null; source: string; reason: string; [key: string]: unknown }
export interface ContextPlan { id: string; domain: string; minimum: ContextArtifact[]; expanded: ContextArtifact[]; categoryStates: Record<string, string> }
export interface SpecContextPlan extends ContextPlan { specId: string }
export interface TaskContextPlan extends ContextPlan { taskId: string; spec: string | null }
export interface Selections { specs: Array<{id: string; label: string}>; tasks: Array<{id: string; label: string}>; domains: Array<{id: string; label: string}> }
export interface ActualContext { [key: string]: unknown }
export interface TaskPreview extends TaskContextPlan { simulated: boolean }
export interface InspectorAction { label: string; view: string }
export interface InspectorSelection extends Record<string, unknown> {
  id?: string
  title?: string
  label?: string
  _kind?: string
  _eyebrow?: string
  _summary?: string
  _action?: InspectorAction
}
export interface ExplorerModel {
  generatedAt: string
  canonical: Record<string, unknown>
  artifactsSummary: Record<string, unknown>
  selections: Selections
  agents: Agent[]
  specs: Spec[]
  specDetails?: Array<Record<string, unknown>>
  userStories: UserStory[]
  storyCatalog: { path: string; exists: boolean; version: number | null; migrationNote: string | null }
  storyTasks: StoryTask[]
  tasks: Task[]
  humanGates: Record<string, Omit<HumanGate, 'id'>>
  context: {
    domainContexts: ContextPlan[]
    specContexts: SpecContextPlan[]
    taskContexts: TaskContextPlan[]
  }
  previews: { tasks: TaskPreview[] }
  graphs: {
    specGraph: { nodes: GraphNode[]; edges: GraphEdge[] }
    processGraph: { nodes: ProcessNode[]; edges: GraphEdge[] }
    engineGraph: { states: string[]; edges: GraphEdge[]; mapping: Record<string, unknown> }
    loopGraph: { nodes: GraphNode[]; edges: GraphEdge[] }
  }
  contextGraph: { nodes: GraphNode[]; edges: GraphEdge[] }
  loop: Record<string, unknown>
  architecture: { conceptual: boolean; description: string; nodes: GraphNode[]; edges: GraphEdge[]; laterals: string[] }
  readiness: {
    omniroute: { path: string; exists: boolean; status: string | null; configExists: boolean; text: string }
  }
  gaps: Gap[]
}
export type Selection = InspectorSelection | null

// --- Human Gates API Types ---

export interface GateDecision {
  gateId: string
  targetId: string
  status: 'approved' | 'rejected'
  approvedBy: string
  reason: string | null
  timestamp: string
}

export interface SpecGateInfo {
  id: string
  title: string
  status: string
  path: string
  fileName: string
  body?: string
}

export interface RelatedTask {
  id: string
  requirement: string
  priority: string
}

export interface GateStatus {
  id: string
  description: string
  always_required: boolean
  condition: string
  status: string
  specs?: SpecGateInfo[]
  tasks: RelatedTask[]
  decisions: GateDecision[]
}

export interface GatesResponse {
  gates: GateStatus[]
  specs: SpecGateInfo[]
  decisions: GateDecision[]
}
