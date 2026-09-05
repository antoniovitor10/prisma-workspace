const exact: Record<string, string> = {
  home: 'visão geral', specs: 'especificações', context: 'contexto', backlog: 'demandas',
  traceability: 'rastreabilidade', process: 'processo', engine: 'motor', loop: 'ciclo',
  gates: 'verificações', gaps: 'lacunas', agents: 'agentes', architecture: 'arquitetura',
  domain: 'domínio', spec: 'especificação', task: 'tarefa', code: 'código', test: 'teste',
  gate: 'verificação', automated: 'automação', human_input: 'entrada humana', human_validation: 'homologação humana', human_gate: 'aprovação humana', human_gate_paused: 'aprovação pendente',
  decision: 'decisão', terminal: 'concluído', failure: 'falha', external_failure: 'falha externa',
  blocked: 'bloqueado', actor: 'ator', artifact: 'artefato', validation: 'validação',
  state: 'estado', lateral: 'componente lateral', selected: 'selecionado', 'NOT SELECTED': 'não selecionado',
  organizations: 'organizações', projects: 'projetos', work_items: 'itens de trabalho', workflow: 'fluxo de trabalho', history: 'histórico',
  sprints: 'ciclos de trabalho', dependencies: 'dependências', access: 'acesso e permissões',
  draft: 'rascunho', approved: 'aprovada', review: 'revisão', active: 'ativo', missing: 'ausente',
  error: 'erro', warning: 'alerta', info: 'informação', high: 'alta', medium: 'média', low: 'baixa', balanced: 'equilibrada',
  broken_path: 'caminho ausente', missing_test: 'teste ausente', verification_note: 'verificação manual',
  idle: 'ocioso', 'spec-draft': 'especificação em rascunho', 'spec-review': 'revisão da especificação',
  'context-loading': 'carregando contexto', implementing: 'implementando', gating: 'executando verificações',
  'pr-created': 'PR criado', 'human-review': 'revisão humana', merged: 'integrado', failed: 'falhou',
  story_creation: 'escrever ou revisar história', spec_generation: 'IA gera a especificação', spec_creation: 'criação da especificação', spec_review: 'aprovação G-SPEC', spec_approved: 'especificação aprovada',
  task_planning: 'IA planeja as tarefas', task_ingestion: 'entrada da tarefa', context_assembly: 'montagem do contexto', implementation: 'implementação',
  check_human_gates: 'verificar aprovações humanas', paused_blocked: 'pausado / bloqueado',
  gate_execution: 'build e testes', story_validation: 'homologação pelas histórias', validation_decision: 'resultado da homologação', feedback_task: 'criar tarefa da observação', ai_triage: 'triagem da IA', story_revalidation: 'aguardando nova homologação', pr_creation: 'criação do PR', completed: 'concluído', PAUSED: 'pausado',
  defines: 'define', implemented_by: 'implementado por', tested_by: 'testado por',
  validated_by: 'validado por', governed_by: 'governado por', depends_on: 'depende de',
  Story: 'Histórias', 'Source Code': 'Código-fonte', Tests: 'Testes', Gates: 'Verificações', Contracts: 'Contratos',
  Dependencies: 'Dependências', Database: 'Banco de dados', Docs: 'Documentação', History: 'Histórico',
  Bootstrap: 'Inicialização', 'Pass / Review': 'Aprovado / Revisão', Pass: 'Aprovado',
  Human: 'Pessoa responsável', 'User Story': 'História de usuário', Task: 'Tarefa', Spec: 'Especificação', Homologation: 'Homologação', 'Feedback Task': 'Tarefa de correção', 'AI Triage': 'Triagem da IA', 'Context Engineering': 'Engenharia de contexto',
  'Context Index': 'Índice de contexto', 'Context Graph': 'Grafo de contexto', 'Process Graph': 'Grafo do processo',
  Agent: 'Agente', 'Loop Engine': 'Motor de execução', Review: 'Revisão', 'Human Gate': 'Aprovação humana',
  Done: 'Concluído', Fail: 'Falha', State: 'Estado', Telemetry: 'Telemetria', Handoff: 'Transferência de contexto',
  'OmniRoute Readiness': 'Prontidão do OmniRoute'
}

const keyLabels: Record<string, string> = {
  id: 'Identificador', title: 'Título', label: 'Nome', role: 'Papel', description: 'Descrição', status: 'Status',
  state: 'Estado', path: 'Caminho', source: 'Origem', reason: 'Motivo', explanation: 'Explicação', severity: 'Severidade',
  domain: 'Domínio', type: 'Tipo', risk: 'Risco', priority: 'Prioridade', requirement: 'Requisito', requirements: 'Requisitos',
  dependencies: 'Dependências', affected_areas: 'Áreas afetadas', gates: 'Verificações', human_gate: 'Aprovação humana',
  specId: 'Especificação', specPath: 'Caminho da especificação', relatedTasks: 'Tarefas relacionadas', detectedGaps: 'Lacunas detectadas',
  testGates: 'Verificações de teste', pendingDecisions: 'Decisões pendentes', contextPlan: 'Plano de contexto',
  readiness: 'Prontidão', review: 'Revisão', evidence: 'Evidências', summary: 'Resumo', category: 'Categoria',
  capability: 'Capacidade', tools: 'Ferramentas', outputs: 'Saídas', constraints: 'Restrições', context: 'Contexto',
  auto_approval: 'Aprovação automática', always_required: 'Sempre obrigatório', condition: 'Condição',
  enforced_by: 'Aplicado por', declared_in_node: 'Declarado no nó', appliesWhen: 'Quando se aplica', enforcement: 'Aplicação',
  resolvedPath: 'Caminho resolvido', acceptanceCriteria: 'Critérios de aceite', contextCandidates: 'Contexto candidato',
  relatedSpec: 'Especificação relacionada', simulated: 'Simulado', note: 'Observação', prompt: 'Pergunta'
}

export function traduzir(valor: unknown): string {
  const texto = String(valor ?? '')
  return exact[texto] ?? exact[texto.toLowerCase()] ?? texto.split('_').join(' ')
}

export function traduzirRotuloChave(chave: string): string {
  return keyLabels[chave] ?? chave.replace(/([a-z])([A-Z])/g, '$1 $2').split('_').join(' ')
}

export function traduzirPapelAgente(id: string, papel: string): string {
  return ({ discovery: 'Agente de descoberta', planner: 'Agente de planejamento', executor: 'Agente executor', reviewer: 'Agente revisor' } as Record<string, string>)[id] ?? traduzir(papel)
}

export function traduzirNomeVerificacao(id: string, nome: unknown): string {
  return ({
    backend_build: 'Compilação do backend', backend_test: 'Testes do backend', frontend_build: 'Compilação do frontend',
    frontend_test: 'Testes do frontend', frontend_lint: 'Análise estática do frontend', missing_e2e: 'Testes ponta a ponta',
    missing_migration_validation: 'Validação das migrations do EF Core', missing_ci_pipeline: 'Pipeline de build, testes e lint',
    missing_backend_analyzer: 'Análise estática rigorosa do backend'
  } as Record<string, string>)[id] ?? traduzir(nome)
}
