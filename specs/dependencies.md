# SPEC-F-009: Dependências de Tarefas Ocultas

**Status:** approved

**Revisão funcional:** aprovada explicitamente por PO em 2026-08-24

**Homologação manual:** pendente

**Natureza:** contrato-alvo de ocultação com preservação interna temporária

## Propósito

Manter dependências, pré-requisitos e bloqueios entre tarefas fora da experiência e da operação atual do produto.
Dados e estruturas internas já existentes podem ser preservados para compatibilidade, auditoria e eventual decisão
futura, mas não devem governar tela, workflow, automação, relatório ou comportamento operacional nesta fase.

Esta decisão sucede o objetivo visível anterior de `SPEC-F-009`, preservando a mesma spec canônica e seu histórico.

## Estado funcional aprovado

- Dependências e pré-requisitos entre tarefas ficam **ocultos por enquanto**.
- Usuários não criam, editam, removem, pesquisam nem visualizam dependências na interface.
- Dependência não bloqueia criação, edição, movimentação, conclusão, sprint, apontamento de horas ou arquivamento.
- Dependência não dispara automação, notificação, aprovação, alerta, SLA ou transição de workflow.
- Dependência não participa de filtros, agrupamentos, ordenações, badges, indicadores, dashboards ou relatórios.
- Dados internos existentes podem permanecer no banco e no domínio, sem exclusão ou migration nesta revisão.
- Nenhum dado de dependência deve ser inventado, completado ou inferido durante importações.

## Superfícies que devem permanecer ocultas

- detalhe/modal da tarefa;
- criação rápida e edição completa;
- Kanban, Product Backlog, sprint e Meu Trabalho;
- busca global e autocomplete;
- filtros pessoais ou compartilhados;
- cards, tooltips e contadores;
- dashboards, relatórios e exportações operacionais;
- notificações e central de alertas;
- portal externo e solicitações;
- ações em massa e automações.

Não devem aparecer termos funcionais como `Dependência`, `Pré-requisito`, `Bloqueia`, `Bloqueado por` ou equivalentes
nessas superfícies enquanto este contrato estiver vigente.

## Preservação interna

Podem permanecer temporariamente, sem uso funcional:

- entidade/tabela `WorkItemLink` e os tipos `DependsOn` e `Blocks`;
- referências já persistidas entre tarefas;
- `DependencyGraphService` e validações defensivas existentes;
- DTOs, repositórios e código de compatibilidade necessário para leitura interna;
- histórico técnico relacionado a vínculos já gravados.

Preservar não significa disponibilizar. Endpoints de busca ou mutação específicos de dependência não integram o
contrato operacional e devem ficar inacessíveis aos fluxos normais quando a ocultação for implementada.

## Regras e invariantes

1. Nenhuma tarefa muda de estado ou capacidade por possuir dependência interna.
2. Dados preservados não podem gerar `IsBlocked`, prioridade, atraso, impedimento ou qualquer resultado derivado.
3. Uma dependência interna não impede a conclusão de tarefa ou de suas subtarefas.
4. Nenhuma automação pode usar dependência como condição, gatilho ou ação.
5. Nenhum relatório ou filtro funcional pode consultar dependência para compor resultado.
6. Nenhuma API operacional deve exigir dependência ou pré-requisito em seu request/response público.
7. Isolamento por organização continua obrigatório para qualquer dado interno preservado.
8. Não haverá remoção de schema, backfill ou limpeza destrutiva nesta fase.
9. Uma reativação futura da funcionalidade exige nova decisão humana, revisão desta spec e `G-SCOPE`.
10. Remover ou transformar schema/dados exige auditoria, backup e `G-MIGRATION`.

## Fora do escopo

- Autocomplete por código ou título para criar dependência.
- Exibição de chaves humanas ou GUIDs de tarefas relacionadas.
- Criação ou remoção funcional de links `DependsOn`/`Blocks`.
- Detecção de ciclos como requisito operacional do produto.
- Grafo visual de dependências.
- Planejamento automático por caminho crítico.
- Bloqueio automático de tarefa, coluna, sprint ou entrega.
- Exclusão de dados internos existentes.
- Alteração de schema ou remoção imediata de código legado.

## Estado atual comprovado no código

- `WorkItemLink` e `WorkItemLinkType` representam `DependsOn`, `Blocks`, `Related` e `Duplicate`.
- `DependencyGraphService` canonicaliza arestas e detecta ciclos.
- handlers permitem criar/remover links e há testes de grafo.
- `TaskDetailDrawer.tsx` exibe seção “Dependências e bloqueios”, lista vínculos e permite incluir/remover.
- `DependencyAutocomplete.tsx` pesquisa tarefas para vinculação.
- Product Backlog oferece filtro por bloqueio/dependência e exibe badges/contadores.
- consultas de quadro calculam `IsBlocked` a partir de `DependsOn`.
- Meu Trabalho e relatórios usam dependências para classificar itens bloqueados.
- tarefas históricas `TASK-006`, `TASK-007` e `TASK-014` registram a implementação anterior.

## Gaps entre contrato e implementação

1. **Detalhe da tarefa:** a seção visível e seus comandos contradizem a ocultação aprovada.
2. **Autocomplete:** busca e seleção de dependência continuam disponíveis.
3. **Backlog:** filtro, badges, contadores e tooltips expõem dependências.
4. **Kanban/API:** `IsBlocked` ainda deriva de vínculos internos.
5. **Meu Trabalho:** dependências influenciam alertas e priorização.
6. **Relatórios:** itens bloqueados ainda são calculados por `DependsOn`.
7. **Contratos operacionais:** endpoints de pesquisa/criação/remoção ainda podem tornar a função utilizável sem UI.
8. **Automações e notificações:** consumidores devem ser auditados para eliminar qualquer efeito atual ou futuro.
9. **Importação:** precisa preservar somente dados comprovadamente existentes, sem inferir vínculos ausentes.
10. **Testes/homologação:** falta comprovar ausência completa nas superfícies e inexistência de efeito operacional.

## Critérios de aceite da ocultação futura

- **Dado** qualquer perfil interno ou externo, **quando** abre ou edita uma tarefa, **então** não vê seção, campo,
  badge ou ação de dependência/pré-requisito.
- **Dado** uma tarefa com links internos preservados, **quando** é movida, concluída ou planejada, **então** esses
  links não bloqueiam nem alteram a operação.
- **Dado** Kanban, Backlog, sprint, Meu Trabalho, busca ou filtros, **quando** a tarefa possui dependência interna,
  **então** nenhum indicador ou critério derivado aparece.
- **Dado** dashboard, relatório ou exportação, **quando** contém tarefas com links preservados, **então** dependência
  não vira dimensão, métrica, filtro ou classificação.
- **Dado** configuração de automação/notificação, **quando** o usuário escolhe condições e ações, **então** não há
  opção baseada em dependência, bloqueio ou pré-requisito.
- **Dado** acesso direto a contrato legado específico de dependência, **quando** o usuário tenta usá-lo como fluxo
  operacional, **então** a função não fica disponível e nenhum vínculo é criado ou alterado.
- **Dado** o deploy da ocultação, **quando** o banco é auditado antes e depois, **então** os dados internos existentes
  permanecem íntegros e nenhuma migration destrutiva foi aplicada.

## Testes e homologação necessários

- React/Playwright: ausência da seção, autocomplete, badges, filtros e comandos em desktop/mobile.
- API: contratos funcionais não expõem nem aceitam dependência e rotas legadas não permitem mutação operacional.
- Aplicação: criar, mover, concluir, planejar e apontar horas ignoram links preservados.
- Relatórios/Meu Trabalho: nenhum indicador ou recorte derivado de dependência.
- Automação/notificação: catálogo sem gatilho, condição ou ação de dependência.
- Persistência: contagem e integridade dos dados internos preservadas, sem migration.
- Homologação manual por PO antes de promoção.

## Human Gates

- `G-SPEC`: aprovado explicitamente por PO para ocultação nesta fase.
- `G-SCOPE`: decisão registrada na D59; exigido para reativar ou expandir a função no futuro.
- `G-MIGRATION`: não aplicável à ocultação; obrigatório antes de remover ou transformar schema/dados.
- `G-WORKFLOW`: obrigatório se uma decisão futura voltar a usar dependência em transições/bloqueios.
- `G-DEPLOY`: obrigatório antes de promover a ocultação.

## Referências

- `AGENTS.md`.
- `DECISIONS.md` — D35, D53, D54 e D59.
- `ROADMAP.md` — Fase 8.
- `src/Prisma.Workspace.Domain/Entities/WorkItemCollaboration.cs` (`WorkItemLink`).
- `src/Prisma.Workspace.Domain/Enums/WorkItemLinkType.cs`.
- `src/Prisma.Workspace.Domain/Services/DependencyGraphService.cs`.
- `src/Prisma.Workspace.Application/Features/WorkItems/WorkItemManagementFeature.cs`.
- `src/Prisma.Workspace.Application/Features/Backlog/BacklogFeature.cs`.
- `src/Prisma.Workspace.Application/Features/Me/MyWorkDashboardFeature.cs`.
- `src/Prisma.Workspace.Application/Features/Reports/ReportBuilderFeature.cs`.
- `src/Prisma.Workspace.Web/src/components/TaskDetailDrawer.tsx`.
- `src/Prisma.Workspace.Web/src/features/task/DependencyAutocomplete.tsx`.
- `src/Prisma.Workspace.Web/src/features/scrum/BacklogPlanner.tsx`.
- `tests/Prisma.Workspace.Tests/DependencyGraphTests.cs`.

## Rollback documental

Esta revisão não remove código nem dados. Reativar a experiência anterior exige decisão humana e revisão desta spec;
não deve ocorrer por simples rollback de interface.

## Rastreabilidade

Decisão humana de PO -> D59 -> `SPEC-F-009` (`approved`) -> gap documental -> futura ocultação de
produto sem migration -> testes -> homologação manual.
