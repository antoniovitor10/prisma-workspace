# SPEC-PROJECT-STRUCTURE: Estrutura de Trabalho e Kanban sem Sprint automática

**Status:** superseded

**Substituída por:** `SPEC-PROJECT-METHODOLOGY-HIDDEN`, por decisão humana registrada na D48.
## Objective
Renomear os rótulos de interface de "Metodologia" para "Estrutura de Trabalho", manter somente as quatro estruturas definidas na D20, validar os valores do enum e garantir que a criação de projetos Kanban não gere sprints automaticamente.

## Context
Atualmente, o sistema utiliza o termo "Metodologia" na interface do usuário (ex: no formulário de criação de projeto em `Projects.tsx` e nas configurações do projeto em `ProjectSettings.tsx`). Para alinhamento conceitual, o rótulo visual deve ser atualizado para "Estrutura de Trabalho", sem alterar identificadores de código backend, banco de dados ou contratos.
O enum `ProjectMethodology` possui os valores Kanban (1), Scrum (2), Scrumban (3) e SimpleList (4), conforme D20. PO decidiu em 2026-08-20 que XP deve permanecer oculto e fora da implementação por enquanto. Também é regra de negócio que projetos Kanban não criem Sprints automaticamente durante a inicialização.

## Scope
- Renomeação visual dos rótulos de "Metodologia" para "Estrutura de Trabalho" nas telas `Projects.tsx` e `ProjectSettings.tsx`.
- Garantia de que a criação de projetos do tipo Kanban não cria Sprints automaticamente.
- Validação explícita de `ProjectMethodology` nos fluxos de criação e atualização para rejeitar valores fora do enum.

## Out of Scope
- Renomear o enum `ProjectMethodology`, colunas no banco de dados, DTOs ou contratos da API REST.
- Adicionar ou exibir XP; a opção fica adiada até nova decisão explícita.
- Proibir ou remover a capacidade manual de criar Sprints em projetos Kanban.
- Adicionar lógicas condicionais complexas ou switch/cases por metodologia no backend (não existem no código atualmente).

## Functional Requirements
1. A interface do usuário deve exibir "Estrutura de Trabalho" em substituição ao termo "Metodologia" nas telas de listagem/criação de projetos (`Projects.tsx`) e configurações (`ProjectSettings.tsx`).
2. As opções devem permanecer Kanban (1), Scrum (2), Scrumban (3) e Lista simples (4); XP não deve ser exibido.
3. A criação de um projeto com estrutura de trabalho Kanban (`ProjectMethodology.Kanban`) não deve acionar a criação automática de nenhuma Sprint.
4. Os validadores de criação e atualização de projeto devem rejeitar valores que não pertençam a `ProjectMethodology`.

## Invariants
- `ProjectMethodology` é persistido como inteiro no banco de dados através da configuração `HasConversion<int>()`.
- Os valores inteiros existentes dos enums (Kanban=1, Scrum=2, Scrumban=3, SimpleList=4) não devem ter seus IDs alterados.
- O contrato das DTOs de projeto permanece aceitando o enum `ProjectMethodology`.

## Acceptance Criteria
- **Given** que um usuário está na tela de criação ou edição de projetos
  **When** visualiza o formulário
  **Then** o rótulo exibido é "Estrutura de Trabalho" e o dropdown contém somente Kanban (1), Scrum (2), Scrumban (3) e Lista simples (4)

- **Given** que um usuário cria um novo projeto com estrutura "Kanban"
  **When** o projeto é inicializado
  **Then** o projeto é criado com sucesso sem nenhuma Sprint vinculada automaticamente

## Data Impact
- Nenhuma alteração de schema, enum persistido ou migration.

## Authorization Impact
- Nenhuma alteração nas regras de permissão ou papéis de acesso existentes.

## Contracts
- As DTOs de requisição/resposta de projetos (`CreateProjectDto`, `UpdateProjectDto`, `ProjectDto`) continuam utilizando a propriedade `Methodology` do tipo `ProjectMethodology` (int).

## Dependencies
- Nenhuma dependência externa adicional.

## Error Cases
- Envio de um valor inteiro fora do enum `ProjectMethodology` (ex: 0 ou 5) deve resultar em erro de validação (HTTP 400 Bad Request) pelo FluentValidation.

## Pending Decisions
- Nenhuma nesta spec. XP permanece adiado e fora do escopo por decisão explícita de PO em 2026-08-20.

## Test Gate Mapping
- `tests/Prisma.Workspace.Tests/ProjectFeatureTests.cs`:
  - Garantir que a criação com `ProjectMethodology.Kanban` não inclui sprints automáticas no repositório/banco.
  - Validar que valores numéricos não definidos em `ProjectMethodology` são rejeitados na criação e na atualização.

## Risks
- Sem validação `IsInEnum`, a API pode aceitar números não definidos; por isso a validação faz parte do escopo e dos testes.

## Rollback
- Reverter as alterações nos arquivos frontend (`Projects.tsx`, `ProjectSettings.tsx`) e nos validadores. Não há migration para reverter.

## Traceability
CAND-P-001, CAND-P-002, CAND-P-003 → SPEC-PROJECT-STRUCTURE → TASK-PROJECT-STRUCTURE → ProjectFeatureTests.cs → [commit]
