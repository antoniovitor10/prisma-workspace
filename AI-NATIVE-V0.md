# Arquitetura AI-Native V0 — Proposta Adaptada ao Project runrun-copia

## 1. Visão Geral

Esta proposta estabelece a arquitetura AI-Native V0 para o projeto `runrun-copia` (Painel Kanban Detran-SE), adaptando princípios modernos de engenharia orientada a agentes (SDD, Context Engineering, Gates, Loop, Graph, Agent Contracts, Human Gates, Telemetry e OmniRoute) à realidade da stack existente.

### Adaptada à Stack Real
- **Backend:** .NET 8 (ASP.NET Core Web API, Clean Architecture: `src/Detran.Kanban.Domain`, `src/Detran.Kanban.Application`, `src/Detran.Kanban.Infrastructure`, `src/Detran.Kanban.Api`, `tests/Detran.Kanban.Tests`)
- **ORM e Banco:** Entity Framework Core + SQL Server
- **Frontend:** React 19 + TypeScript + Vite + Styled Components + dnd-kit (`src/Detran.Kanban.Web`)
- **Histórias Canônicas:** `stories/catalog.json` registra primeiro a necessidade e o comportamento esperado
- **Especificações Canônicas:** `specs/*.md` contém o contrato técnico gerado/revisado pela IA e aprovado por G-SPEC
- **Backlog:** `backlog.md` (raiz) como repositório central de demandas; `specs/backlog.md` é a spec canônica de subtarefas hierárquicas (SPEC-B-001)

### Principais Diferenças da Arquitetura Genérica
1. **Sem Invenção de Comandos:** Mapeia exatamente a CLI .NET (`dotnet build`, `dotnet test`) e npm no diretório Web (`npm run build`, `npm test`, `npm run lint`).
2. **Contexto por Domínio:** Em vez de carregar a solution inteira, a IA opera com índices de contexto focados por módulo funcional.
3. **Respeito à Clean Architecture:** Agent Contracts e Human Gates garantem que a separação de camadas (Domain -> Application -> Infrastructure -> Api -> Web) e CQRS com MediatR não sejam violados.

---

## 2. Context Index

Estrutura de contextualização leve para agentes operarem com janela de contexto reduzida e alta precisão:

```
context/
├── projects/
│   ├── spec.md        -> specs/project-structure.md
│   ├── code.md        -> referencias aos arquivos em Domain, Application, Infrastructure, Api e Web
│   ├── tests.md       -> testes xUnit em tests/Detran.Kanban.Tests e Vitest em Web
│   └── gates.md       -> comandos dotnet build, dotnet test, npm run build, npm test
├── work-items/
│   ├── spec.md        -> specs/work-items.md
│   ├── code.md        -> entidades WorkItem, DTOs, Handlers, Controllers, Componentes React
│   ├── tests.md       -> testes de WorkItem
│   └── gates.md       -> gates de validacao
├── backlog/
│   ├── spec.md        -> specs/backlog.md
│   ├── code.md        -> logica de ordenacao, priorizacao e sprint allocation
│   ├── tests.md       -> testes de backlog
│   └── gates.md       -> gates de validacao
├── workflow/
│   ├── spec.md        -> specs/workflow-status.md
│   ├── code.md        -> regras de transicao de etapas, limitação WIP
│   ├── tests.md       -> testes de workflow
│   └── gates.md       -> gates de validacao
├── history/
│   ├── spec.md        -> specs/task-history.md
│   ├── code.md        -> auditoria, historico imutavel de acoes
│   ├── tests.md       -> testes de historico
│   └── gates.md       -> gates de validacao
├── sprints/
│   ├── spec.md        -> specs/sprints.md
│   ├── code.md        -> iteracoes, datas de inicio/fim, burndown
│   ├── tests.md       -> testes de sprints
│   └── gates.md       -> gates de validacao
├── attachments/
│   ├── spec.md        -> regras de anexos e upload
│   ├── code.md        -> servicos de storage e controllers
│   ├── tests.md       -> testes de integracao de upload
│   └── gates.md       -> gates de validacao
└── dependencies/
    ├── spec.md        -> specs/dependencies.md
    ├── code.md        -> grafo de dependencias entre tarefas e bloqueios
    ├── tests.md       -> testes de bloqueio de ciclo e dependencias
    └── gates.md       -> gates de validacao
```

---

## 3. Fluxo de Trabalho (Loop AI-Native)

O ciclo de vida de desenvolvimento orientado a IA segue o fluxo story-first definido em D67:

```
1. PO escreve ou ajusta a história de usuário.
2. A IA cria/revisa a spec relacionada (`draft` ou `review`).
3. PO aprova a spec via G-SPEC; histórias não recebem aprovação individual.
4. A IA cria tarefas rastreáveis e monta o contexto mínimo.
5. Agentes implementam e executam build, testes e demais gates.
6. PO homologa o produto pelas histórias.
7. Toda observação, ajuste ou funcionalidade ausente cria/atualiza uma tarefa vinculada à história e à spec.
8. A IA classifica a pendência: defeito/item faltante volta à execução; mudança de requisito volta à história/spec e exige novo G-SPEC.
9. Após a correção, a tarefa é arquivada, a observação ativa é limpa e a história volta para revalidação, preservando o histórico.
```

---

## 4. Agent Contracts

Contrato formal que define a interface entre o agente planejador (ou usuario humano) e o agente executor.

```yaml
input:
  task_id: "TASK-XXX"
  spec_path: "specs/XXX.md"
  context_path: "context/XXX/"
  target_layer: ["Domain", "Application", "Infrastructure", "Api", "Web"]

output:
  implementation:
    modified_files: []
    created_files: []
  tests:
    added_unit_tests: []
    added_component_tests: []
  gates_result:
    backend_build: "passed | failed"
    backend_tests: "passed | failed"
    frontend_build: "passed | failed"
    frontend_tests: "passed | failed"
    frontend_lint: "passed | failed"
  pr_details:
    title: "feat(domain): descricao concisa"
    branch: "feature/TASK-XXX"

validation:
  gates_passing: true
  human_gate_required: false
  human_gate_reason: null
```

---

## 5. Gates Mapeados

Apenas comandos reais e existentes no repositorio sao utilizados nos gates de integracao:

### Backend (.NET 8 Clean Architecture)
- **Compilacao:** `dotnet build` (Executar na raiz da solução `Detran.Kanban.sln`)
- **Testes Unitarios e de Integracao:** `dotnet test`
- **Validacao de Migrations EF Core:** `dotnet ef migrations list` / `dotnet build` em `src/Detran.Kanban.Infrastructure`

### Frontend (React 19 + TypeScript)
- **Compilacao / Typecheck:** `npm run build` (Executar em `src/Detran.Kanban.Web`)
- **Testes de Componente / Unitarios:** `npm test` (Executar em `src/Detran.Kanban.Web`)
- **Análise Estática / Linter:** `npm run lint` (Executar em `src/Detran.Kanban.Web`)

### Gates disponíveis e lacunas restantes
- **E2E (End-to-End):** Playwright configurado em `src/Detran.Kanban.Web`; execução obrigatória por D40 para mudanças de frontend, endpoint ou schema. O profile ainda precisa de G-WORKFLOW para representar sua aplicação condicional sem executar E2E indevidamente em tarefas isentas.
- **Backend Linter / Formatting Strict Gate:** Inexistente como script separado (`dotnet format` não configurado em CI).
- **SAST / Análise de Segurança:** Inexistente na pipeline local.

---

## 6. Human Gates

Etapas onde a atuacao do agente é pausada aguardando aprovacao ou revisao humana explicita:

1. **Migrations de Banco de Dados:**
   - Criação ou alteração de tabelas no SQL Server via EF Core Migrations.
   - Scripts de carga ou alteração de esquemas legados.
2. **Mudanças de Arquitetura:**
   - Alteracao nas camadas Clean Architecture (Domain, Application, Infrastructure, Api).
   - Adicao de novas bibliotecas NuGet ou pacotes npm.
   - Modificacoes no pipeline de Auth/JWT/Identity.
3. **Mudanças no Modelo de Workflow:**
   - Regras de transicao de status de tarefas, restricoes WIP e calculo de SLA.
   - Mudancas na estrutura de autorizacao por papel/organizacao.
4. **UX Complexa:**
   - Alteracoes em interacoes drag-and-drop (`dnd-kit`), paineis Kanban complexos e construtor de relatorios.
5. **Deploy e Homologacao:**
   - Promocao de código para ambiente de homologacao ou produção do Detran-SE.

---

## 7. Telemetry

Métricas coletadas durante cada ciclo de execucao dos agentes para auditoria e otimização contínua:

### Métricas Coletadas
- **Tempo por Task:** Minutos decorridos entre o recebimento da task e a geracao dos gates aprovados.
- **Coverage por Spec:** Percentual de cenarios descritos na spec que possuem testes automatizados correspondentes.
- **Gates Passing Rate:** Taxa de sucesso na primeira tentativa de execucao dos gates por agente.
- **Human Gates Acionados:** Frequencia e motivos pelos quais a intervencao humana foi solicitada.

### Formato de Exportação (JSON / OpenTelemetry compatible)
```json
{
  "taskId": "TASK-001",
  "spec": "specs/work-items.md",
  "executionTimeMs": 142000,
  "gates": {
    "backendBuild": true,
    "backendTest": true,
    "frontendBuild": true,
    "frontendTest": true,
    "frontendLint": true
  },
  "humanGateTriggered": false,
  "metrics": {
    "coveragePercentage": 92.5,
    "filesModified": 4
  }
}
```

### Integração com OmniRoute
- Exportacao automatica do payload de telemetria ao final de cada pipeline.
- Padronizacao de headers e contratos para interoperabilidade entre diferentes agentes orquestradores.

---

## 8. Roadmap de Adoção

### Fase 1: Specs + Backlog + Context Index (Atual)
- Consolidar todas as especificacoes canonicas em `specs/`.
- Manter `backlog.md` sincronizado com tarefas atomicas.
- Mapear a estrutura de diretórios em `context/` para consumo dos agentes.

### Fase 2: Gates Automatizados (executor local concluído; CI pendente)
- O comando `tools/agent-loop/cli.js run-gates` executa sequencialmente somente os product gates ativos e allowlisted do profile, no estado `gating`, sem Human Gates pendentes.
- Resultados, duração e código de saída são registrados no envelope/telemetria; stdout/stderr não são persistidos.
- Permanece pendente integrar a mesma política à CI e representar o E2E condicional de D40 no profile após G-WORKFLOW.
- Impedir merges sem 100% de sucesso nos gates continua pendente de CI.

### Fase 3: Agent Contracts
- Formalizar o input/output YAML/JSON em prompts de agentes.
- Garantir que a IA valide os contratos antes de sinalizar conclusao de tarefa.

### Fase 4: Telemetry + OmniRoute
- Habilitar registro automatico de metricas por ciclo.
- Integrar com orquestrador OmniRoute para monitoramento de eficiencia e taxa de aprovação humana.

---

## 9. Requisito Ausente / SOURCE_MISSING

- **Status:** SOURCE_MISSING
- **Descrição:** Requisito complementar referente a PDF adicional / documentação complementar externa não fornecida na entrada original.
- **Domínio Potencial:** `project-structure` (relatórios e exportações de estrutura de projeto).
- **Specs Independentes:** `specs/project-structure.md`, `specs/work-items.md`, `specs/backlog.md`, `specs/dependencies.md`, `specs/sprints.md`, `specs/workflow-status.md`, `specs/task-history.md` foram construídas de forma independente e funcionalmente autocontidas.
- **Decisões a Reabrir se o PDF for Fornecido:** Formatos de exportação de relatórios de projeto, templates visuais de PDF e regras de layout de relatórios institucionais.
