# SPEC-PROJECT-METHODOLOGY-HIDDEN: Metodologia oculta

**Status:** approved
**Baseline:** decisão explícita de PO, formalizada na D48 e revisada em 2026-08-24.

## Propósito

Retirar completamente metodologia ou estrutura de trabalho da experiência funcional do produto enquanto esse conceito não representar comportamentos úteis e validados, preservando temporariamente os dados e contratos internos existentes por compatibilidade.

## Contrato funcional aprovado

1. Metodologia do projeto fica totalmente oculta por enquanto.
2. O produto não exibe, solicita, permite selecionar nem permite editar metodologia em nenhuma tela.
3. Os nomes `Kanban`, `Scrum`, `Scrumban`, `Lista simples`, `XP`, `Metodologia` e `Estrutura de trabalho` não aparecem como classificação de projeto na experiência operacional.
4. `XP` não é opção disponível e permanece totalmente oculto.
5. Metodologia não é filtro, agrupamento, coluna, indicador, campo de relatório, item de busca ou informação de cartão/cabeçalho.
6. Nenhuma funcionalidade visível deve mudar em razão do valor interno de metodologia.
7. Projetos novos usam internamente `ProjectMethodology.Kanban` (`1`) apenas como valor técnico compatível.
8. Projetos existentes preservam o valor persistido quando outros dados forem editados.
9. Enum, coluna SQL, propriedades de domínio, DTOs e contratos internos podem permanecer temporariamente para retrocompatibilidade.
10. Esta decisão não autoriza remover schema, alterar dados existentes ou criar migration.

## Comportamento esperado

### Criação de projeto

- O formulário não apresenta metodologia ou estrutura de trabalho.
- O usuário não escolhe um valor.
- O frontend e/ou backend usa `Kanban = 1` somente como fallback técnico.
- O projeto não recebe funcionalidades extras ou restrições visíveis em razão desse valor.

### Edição de projeto

- Configurações não exibem metodologia.
- Salvar nome, descrição, responsável, datas, status, equipes, membros ou outras configurações preserva o valor interno previamente carregado.
- Usuário não consegue trocar metodologia por ação da interface.

### Demais superfícies

- Listagem e detalhes de projetos não exibem metodologia.
- Kanban, Product Backlog, Sprint, tarefa, dashboards e relatórios não usam metodologia para decidir quais campos visuais mostrar.
- Busca global não indexa nem apresenta metodologia como informação funcional.
- Construtor de relatórios não oferece metodologia como dimensão, métrica ou filtro.

## XP

- `XP` não integra o enum atual e não deve ser adicionado nesta etapa.
- Nenhum controle, label, migration, seed ou regra deve ser criado para XP.
- Eventual inclusão futura exige nova decisão humana e revisão deste contrato.

## Fora do escopo

- Remover `Project.Methodology`.
- Remover ou alterar `ProjectMethodology`.
- Remover a coluna SQL ou seu `HasConversion<int>()`.
- Reescrever valores de projetos existentes.
- Criar migration de metodologia ou XP.
- Definir comportamentos futuros específicos para Kanban, Scrum, Scrumban, Lista simples ou XP.
- Usar metodologia para habilitar ou ocultar recursos do produto.

## Estado atual comprovado no código

- `ProjectMethodology` contém `Kanban = 1`, `Scrum = 2`, `Scrumban = 3` e `SimpleList = 4`; XP não existe.
- `Project.Methodology` permanece persistido e usa `Kanban` como valor inicial compatível.
- O formulário atual de criação não mostra seletor e envia `methodology: 1` internamente.
- As configurações atuais não mostram controle de metodologia e preservam o valor carregado ao salvar outros campos.
- Existem testes React verificando a ausência dos rótulos e controles no cadastro e nas configurações.
- A API e a camada Application ainda recebem, validam, retornam e registram `Methodology` nos contratos de projeto.
- O frontend ainda mantém `methodology` em tipos e payloads para compatibilidade.

## Gaps entre contrato e implementação

1. **Comportamento visual indireto:** `Kanban`, `BacklogPlanner`, `TaskDetailDrawer`, `SprintDashboard` e `ReportsHub` consultam `project.methodology` para decidir se Story Points aparecem. Isso faz um valor que deveria ser apenas técnico ainda alterar a experiência visível.
2. **API mutável:** criação e atualização ainda aceitam `Methodology` do cliente; embora o contrato possa permanecer por compatibilidade, o backend não diferencia claramente valor técnico preservado de escolha funcional suspensa.
3. **DTO exposto:** respostas de projeto ainda incluem `Methodology`, ampliando o risco de novos consumidores voltarem a exibi-lo ou utilizá-lo como regra funcional.
4. **Eventos e auditoria:** criação e atualização registram metodologia nos payloads de eventos administrativos, mesmo ela não sendo informação funcional da experiência.
5. **Testes incompletos:** os testes atuais cobrem cadastro e configurações, mas não comprovam ausência em listagens, tarefas, backlog, sprint, dashboards, relatórios, busca e filtros.
6. **Backlog divergente:** a descrição atual de `TASK-001` ainda fala em validar quatro opções e manter dropdowns, contrariando D48 e esta spec; precisa ser atualizada na consolidação documental.
7. **Homologação manual pendente:** ainda é necessário percorrer todas as superfícies para confirmar que nenhum rótulo, seletor ou comportamento condicionado à metodologia permanece visível.

## Regras de compatibilidade

1. Novos projetos persistem `Kanban = 1` como valor técnico até remoção futura aprovada.
2. Edição de projeto legado não pode substituir silenciosamente seu valor interno por `Kanban`.
3. Nenhum valor existente deve ser normalizado ou migrado nesta etapa.
4. Consumidores internos podem ler o campo somente para compatibilidade, não para variar a experiência funcional.
5. Contratos que ainda recebem o campo devem validar enum para evitar corrupção, mas a interface não o oferece ao usuário.
6. Remover o campo de contratos ou persistência exige auditoria de consumidores e, se houver schema, `G-MIGRATION`.

## Critérios de aceite

- **Dado** o cadastro de projeto, **quando** o formulário abrir, **então** não existe texto nem controle relacionado a metodologia, estrutura de trabalho ou XP.
- **Dado** um projeto novo, **quando** for criado, **então** o valor técnico compatível é `Kanban = 1` sem escolha do usuário.
- **Dado** um projeto legado com outro valor interno, **quando** seus demais dados forem editados, **então** a metodologia persistida é preservada.
- **Dado** qualquer projeto, **quando** o usuário navegar por projeto, Kanban, backlog, sprint, tarefa, dashboard, relatório, busca ou filtro, **então** metodologia e XP não aparecem.
- **Dado** dois projetos com valores internos diferentes, **quando** as mesmas telas forem abertas, **então** a disponibilidade e exibição dos recursos não variam apenas por metodologia.
- **Dado** o construtor de relatórios, **quando** listar campos, dimensões e filtros, **então** metodologia e XP não são oferecidos.
- **Dado** esta revisão, **quando** aplicada documentalmente, **então** nenhuma migration ou remoção de código/schema é executada.

## Testes e homologação

### Evidência automatizada necessária

- Testes React de ausência no cadastro e configurações.
- Testes React de ausência em listagem, cabeçalhos, detalhes, Kanban, Product Backlog, Sprint e tarefa.
- Testes de dashboard, relatórios, busca e filtros garantindo que metodologia/XP não são opções nem dimensões.
- Teste de criação confirmando o fallback interno `Kanban = 1`.
- Teste de atualização confirmando preservação do valor legado.
- Testes comparando projetos com valores internos diferentes e comprovando que a UI não muda por metodologia.
- Playwright percorrendo as superfícies principais e garantindo ausência dos rótulos e controles.

### Homologação manual pendente

1. Criar projeto e verificar que metodologia/XP não aparecem.
2. Editar projeto e verificar que metodologia/XP não aparecem.
3. Abrir projeto, quadro, backlog, sprint e tarefa em projetos legados de valores diferentes.
4. Confirmar que Story Points ou outros recursos não são exibidos/ocultados por metodologia.
5. Abrir relatórios, dashboards, filtros e busca global e verificar ausência total.

## Riscos

- Sobrescrever valores legados ao editar outro campo do projeto.
- Manter regras visuais ocultamente acopladas à metodologia e gerar experiências diferentes sem explicação.
- Novos consumidores tratarem o campo de compatibilidade como requisito funcional.
- Remover coluna ou enum sem mapear integrações, relatórios, importações e dados existentes.

## Decisão futura

Metodologia somente poderá voltar a ser funcional após definição explícita de comportamentos distintos, revisão desta spec e aprovação humana. Até lá, não deve haver nova pergunta de seleção nem implementação de XP.

## Referências

- `DECISIONS.md` — D20, D42 e D48
- `specs/project-management.md`
- `specs/project-structure.md` — superseded
- `src/Detran.Kanban.Domain/Enums/ProjectMethodology.cs`
- `src/Detran.Kanban.Domain/Entities/Project.cs`
- `src/Detran.Kanban.Application/Features/Projects/ProjectsFeature.cs`
- `src/Detran.Kanban.Application/Features/Projects/ProjectManagementFeature.cs`
- `src/Detran.Kanban.Api/Controllers/ProjectsController.cs`
- `src/Detran.Kanban.Web/src/pages/Projects.tsx`
- `src/Detran.Kanban.Web/src/pages/ProjectSettings.tsx`

## Rollback

Esta revisão altera somente documentação. Schema, dados, enum e contratos existentes permanecem intactos.

## Rastreabilidade

D48 → `SPEC-PROJECT-METHODOLOGY-HIDDEN` → implementação parcial atual → gaps documentados → testes automatizados → homologação manual.
