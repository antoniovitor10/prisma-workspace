# SPEC-B-001: Subtarefas como filhas no Product Backlog

**Status:** approved

**Estado as-built (2026-08-24):** backlog hierárquico e alternância SPA `Backlog ↔ Quadro` existem. O link
`Projetos` do breadcrumb funciona; o nome do projeto permanece como texto atual com `aria-current`, não como
link. Filtros, item aberto, expansão e rolagem ainda não são serializados/restaurados integralmente pela URL.
Essas limitações são parte explícita do estado atual e mantêm a TASK-025 em andamento.
## Objective
Exibir itens e subtarefas em árvore no Product Backlog, permitindo expansão e colapso conforme a hierarquia funcional definida na D36.

## Context
Atualmente, a visualização do Product Backlog no frontend (`BacklogPlanner.tsx`) filtra explicitamente subtarefas (`kind !== 6`). Embora a entidade `WorkItem` no backend já possua a relação `ParentId` suportando estruturas hierárquicas profundas, o frontend não renderiza árvores e o backend retorna dados como lista flat. Isso impede a navegação e gestão de subtarefas diretamente no backlog.

## Scope
- Exibição de tarefas pai com suas respectivas subtarefas filhas aninhadas no Product Backlog.
- Suporte estrito à hierarquia da D36. Na navegação funcional, a cadeia permanece
  `Organização → Equipe → Projeto → Tarefa → Subtarefa → Checklist`. Em projetos de
  desenvolvimento, os tipos do mesmo `WorkItem` formam
  `Épico → Feature opcional → História de usuário → Tarefa → Subtarefa`; `Feature` pode
  ser omitida, sem introduzir profundidade arbitrária ou novos níveis organizacionais.
- Funcionalidade de expandir e colapsar nós da árvore no frontend.
- Ordenação hierárquica garantida (item pai precede seus itens filhos).
- Preservação dos ancestrais de um item correspondente quando pesquisa ou filtros estiverem ativos,
  para que o resultado nunca seja apresentado fora do seu contexto hierárquico.
- Validação no backend de toda criação ou alteração de `ParentId`, impedindo ciclos, órfãos,
  vínculos entre projetos diferentes e cadeias incompatíveis com a D36.
- Adaptação na renderização do `BacklogPlanner.tsx` e ajustes na query do backend `GetProjectBacklogQuery` se necessário para transporte ou montagem da hierarquia.
- Ação de exclusão acessível em cada linha do Product Backlog, representada visualmente por uma lixeira.
- Confirmação explícita e tratamento atômico da exclusão lógica de itens com descendentes.
- Navegação contextual de mão dupla entre Product Backlog do projeto e Kanban transversal, preservando
  projeto, quadro e tarefa em foco quando aplicáveis.
- Breadcrumb do workspace com segmentos navegáveis e indicação semântica da página atual.
- Product Backlog mostrando por padrão tarefas sem sprint, com filtro explícito para incluir itens em sprint.

## Out of Scope
- Alterações no modelo de dados da entidade `WorkItem` para introduzir novas chaves de hierarquia (utilizar `ParentId` existente).
- Drag-and-drop para reordenar hierarquia entre diferentes pais nesta especificação.
- Alterações na visualização do Kanban geral.

## Functional Requirements
1. Product Backlog exibe tarefas pai com subtarefas aninhadas na estrutura em árvore.
2. O sistema deve respeitar os níveis e tipos da D36, aceitando `Feature` como nível opcional e impedindo cadeias arbitrárias de subtarefas.
3. Cada item com filhos deve permitir expansão e colapso individual do seu nó hierárquico.
4. A ordenação dos itens deve respeitar rigorosamente a hierarquia (tarefa pai exibida imediatamente antes de suas subtarefas).
5. O backend deve fornecer os dados necessários com a relação `ParentId` intacta e o frontend deve construir/renderizar a árvore sem descartar itens por filtro de `kind`.
6. Quando um item corresponder à pesquisa ou aos filtros, todos os seus ancestrais devem permanecer
   visíveis como linhas de contexto, mesmo que não correspondam diretamente aos critérios. O caminho
   até o resultado deve ser expandido automaticamente enquanto o filtro estiver ativo.
7. A criação e qualquer alteração de `ParentId` devem validar a cadeia da D36:
   - `Epic` é raiz;
   - `Feature`, quando utilizada, é filha de `Epic`;
   - `UserStory` é filha de `Epic` ou de `Feature`;
   - `Task` é filha de `UserStory`;
   - `Subtask` é filha de `Task`;
   - `Checklist` continua sendo uma coleção da tarefa e não um `WorkItem` adicional.
   Tipos que não participam dessa cadeia não ganham novas relações pai-filho nesta entrega e
   permanecem como itens raiz, preservando os dados existentes.
8. Pai e filho devem pertencer ao mesmo projeto. Cada item possui uma única posição de quadro/coluna conforme
   D52; a árvore nunca atravessa projetos ou organizações.
9. Cada item do Product Backlog deve exibir uma ação de lixeira com nome acessível `Excluir <título do item>`.
10. `Excluir` envia a tarefa à lixeira por 7 dias e é diferente de `Arquivar`; dados relacionados permanecem
    recuperáveis durante o prazo e a política de auditoria continua válida.
11. Ao excluir um item sem descendentes ativos, a interface deve apresentar uma confirmação simples antes
    de enviar a operação.
12. Ao excluir um item pai com descendentes ativos, a interface informa a quantidade e pergunta se o usuário
    deseja incluir também todas as filhas e descendentes.
13. O diálogo oferece `Excluir pai e subtarefas`, `Excluir somente o pai` e `Cancelar`.
14. Ao excluir somente o pai, as filhas são reatribuídas à tarefa avó quando ela existir; sem avó, tornam-se
    itens independentes. O backend descobre a família persistida e executa exclusão/reparenting em transação.
15. Restaurar um pai que foi excluído junto com as subtarefas restaura toda a família na mesma operação.
16. `Arquivar` é ação separada; itens arquivados aparecem em filtro próprio e podem ser restaurados.
17. Após sucesso, Product Backlog, sprint e Kanban devem ser invalidados/recarregados. Em falha, nenhuma
    exclusão, reparenting, restauração ou arquivamento pode permanecer parcial.
18. Linhas da mesma família devem formar um bloco visual contínuo: a primeira filha aparece imediatamente
    abaixo do pai, com vão vertical curto e consistente, recuo progressivo e conector contínuo. Não pode haver
    uma área vazia que faça a subtarefa parecer desvinculada ou pertencente ao item seguinte.
19. O cabeçalho contextual do projeto deve oferecer uma troca visível entre `Product Backlog` e `Quadro`, sem
    obrigar o usuário a retornar à lista de projetos ou usar a sidebar global.
20. Ao sair do Product Backlog para o Quadro, o sistema abre o quadro atual da tarefa em foco ou um quadro
    autorizado escolhido pelo usuário. Não existe quadro principal/padrão por projeto.
21. Ao sair de um Quadro transversal para o Product Backlog, o sistema abre o projeto da tarefa em foco. Sem
    tarefa em foco e havendo vários projetos representados, solicita/permite escolher o projeto sem perder o
    quadro como contexto de retorno.
22. Quando a navegação partir de uma tarefa, o destino deve abrir ou destacar o mesmo `WorkItem`, quando ele
    estiver visível e autorizado, usando parâmetro de rota/query estável em vez de estado apenas em memória.
23. Filtros, expansão da árvore, seleção e posição de rolagem devem ser preservados durante uma ida curta ao
    Quadro e restaurados ao voltar, desde que os dados ainda sejam válidos.
24. A troca deve usar navegação SPA, sem reload completo, e respeitar os botões voltar/avançar do navegador,
    teclado, foco acessível e layout responsivo.
25. No breadcrumb `Projetos > {Projeto}`, o segmento `Projetos` deve ser link/controle real para `/projects`,
    acionável por mouse e teclado, com estado de foco visível e sem reload completo.
26. O nome do projeto também deve ser um link real para `/projects/{projectId}`. Na visão geral ele usa
    `aria-current="page"`; em Backlog, Sprints, Quadros ou Relatórios, o último segmento da seção é que recebe
    `aria-current`, enquanto o projeto continua sendo o atalho para a visão geral.
27. A área clicável do breadcrumb deve abranger o rótulo completo, não somente o separador. O retorno preserva
    filtros válidos da lista de projetos e respeita voltar/avançar do navegador.
28. O Product Backlog lista por padrão somente tarefas com `SprintId = null`.
29. Um filtro explícito permite incluir também tarefas já vinculadas a sprint, sem criar cópias.
30. Kanban e sprint são recortes do mesmo `WorkItem`; uma tarefa pode permanecer no Kanban sem pertencer a
    qualquer sprint.
31. Excluir uma sprint remove somente `SprintId`; quadro, coluna, conteúdo e histórico permanecem, e a tarefa
    volta a aparecer no Product Backlog padrão.

## Invariants
- Uma subtarefa (`ParentId != null`) deve sempre estar associada a um `WorkItem` pai válido do mesmo projeto.
- Toda cadeia nova ou alterada deve corresponder aos níveis da D36 e permanecer acíclica.
- O colapso visual de um nó pai não altera nenhum dado persistido.
- Exclusão para lixeira e arquivamento são ações distintas e preservam auditoria.
- Excluir somente o pai nunca deixa órfãos: as filhas são reatribuídas à avó ou se tornam independentes.
- Exclusão/restauração de família, reparenting e arquivamento são atômicos.
- Nenhum `WorkItem` com `kind == 6` (Subtask) deve ser omitido da listagem do backlog caso seu pai esteja ativo.
- O filtro padrão `Sem sprint` não altera dados; apenas seleciona itens com `SprintId = null`.

## Acceptance Criteria
- **Given** um projeto com uma tarefa pai (kind WorkItem) e duas subtarefas (kind Subtask, ParentId apontando para o pai)
  **When** o usuário acessa a tela de Product Backlog
  **Then** a tarefa pai é exibida no backlog e possui um controle de expansão exibindo as 2 subtarefas aninhadas abaixo dela.

- **Given** uma árvore de tarefas com 3 níveis de profundidade (Pai -> Filho -> Neto)
  **When** o usuário clica para expandir o nó Filho
  **Then** o nó Neto é exibido recuado e alinhado abaixo do nó Filho.

- **Given** um nó pai expandido no backlog
  **When** o usuário clica no botão de colapsar do nó pai
  **Then** todas as subtarefas filhas e descendentes são ocultadas na visualização.

- **Given** que somente uma subtarefa corresponde à pesquisa ou aos filtros ativos
  **When** o Product Backlog apresenta os resultados
  **Then** a subtarefa e todos os seus ancestrais são exibidos, o caminho é expandido e os
  ancestrais são identificáveis como contexto do resultado.

- **Given** uma tentativa de criar ou alterar um vínculo pai-filho entre projetos/organizações diferentes,
  com tipos incompatíveis com a D36 ou que feche um ciclo
  **When** a operação é enviada ao backend
  **Then** a operação é rejeitada com erro de regra de negócio e nenhum `ParentId` é persistido.

- **Given** uma tarefa sem filhos no Product Backlog
  **When** o usuário aciona a lixeira, confirma e possui permissão
  **Then** a tarefa vai para a lixeira por 7 dias, desaparece das visões ativas e pode ser restaurada no prazo.

- **Given** uma tarefa pai com filhas e descendentes ativos
  **When** o usuário aciona a lixeira
  **Then** o diálogo informa a quantidade e permite excluir pai e subtarefas, excluir somente o pai ou cancelar.

- **Given** a exclusão somente do pai
  **When** ele possui filhas ativas
  **Then** as filhas são reatribuídas à avó existente ou se tornam independentes, sem órfãos ou perda de dados.

- **Given** pai e subtarefas excluídos juntos ainda dentro de 7 dias
  **When** o usuário autorizado restaura o pai
  **Then** a família inteira é restaurada atomicamente.

- **Given** uma tarefa pai com descendentes ativos
  **When** o usuário cancela ou a operação falha durante o arquivamento
  **Then** pai e descendentes continuam ativos, sem exclusão/reparenting parcial e sem órfãos.

- **Given** uma tarefa pai expandida com uma ou mais subtarefas
  **When** a árvore é renderizada em desktop ou mobile
  **Then** as filhas ficam visualmente agrupadas logo abaixo do pai, com recuo e conector legíveis e sem
  espaço vertical excessivo entre as linhas da família.

- **Given** que o usuário está no Product Backlog filtrado por um quadro
  **When** aciona `Ir para o quadro`
  **Then** o Kanban correspondente abre sem reload e oferece retorno direto ao mesmo backlog.

- **Given** que o usuário abriu o Quadro a partir de uma tarefa do backlog
  **When** retorna pelo atalho contextual ou pelo botão voltar do navegador
  **Then** o projeto, filtro de quadro, expansão, seleção e posição anterior são restaurados quando válidos.

- **Given** que o usuário está visualizando uma tarefa no Kanban
  **When** aciona `Ver no Product Backlog`
  **Then** o backlog do mesmo projeto abre, preserva o quadro como contexto e destaca a tarefa/ancestrais.

- **Given** que o usuário está no workspace `Projetos > Projeto Modelo`
  **When** aciona `Projetos` por mouse ou teclado
  **Then** a lista de projetos abre por navegação SPA; ao acionar `Projeto Modelo`, a visão geral do projeto
  abre pela rota canônica, e o segmento terminal permanece identificado com `aria-current`.

- **Given** tarefas com e sem sprint no mesmo projeto
  **When** o Product Backlog abre
  **Then** mostra somente as sem sprint; ao ativar `Incluir tarefas em sprint`, mostra também as planejadas.

- **Given** uma tarefa sem sprint posicionada no Kanban
  **When** o usuário consulta quadro e Product Backlog
  **Then** ela permanece no Kanban e aparece no Product Backlog padrão como o mesmo `WorkItem`.

- **Given** uma sprint planejada excluída
  **When** a operação termina
  **Then** seus itens recebem `SprintId = null`, preservam quadro/coluna e voltam ao Product Backlog.

## Data Impact
Nenhum schema de banco de dados alterado. O atributo `ParentId` já existe na tabela/entidade `WorkItem`.

## Authorization Impact
As permissões existentes de leitura e escrita do projeto se aplicam. Usuários com acesso de leitura ao projeto visualizam a hierarquia completa.

## Contracts
- `GetProjectBacklogQuery` / `GetProjectBacklogQueryHandler`:
  - Retorno DTO deve incluir `ParentId` preenchido para todos os itens do backlog.
  - Certificar que itens do tipo `Subtask` (`kind == 6`) não sejam filtrados na consulta SQL/EF Core.
- `CreateWorkItemCommandHandler` e qualquer comando que altere `ParentId`:
  - validar pai existente no mesmo projeto;
  - validar a combinação de tipos da D36;
  - rejeitar autorreferência e ciclos diretos ou indiretos antes de persistir.
- `DELETE /api/WorkItems/{id}` / `SetWorkItemArchivedCommandHandler`:
  - separar exclusão para lixeira de arquivamento;
  - aceitar decisão explícita de incluir subtarefas quando existirem descendentes ativos;
  - recalcular a árvore afetada no servidor;
  - excluir/restaurar família ou reatribuir filhas em transação única;
  - aplicar expiração automática da lixeira após 7 dias;
  - permitir consulta/restauração de arquivadas por filtro/ação próprios.

## Dependencies
- Entidade `WorkItem` com propriedade `ParentId`.
- Componente `BacklogPlanner.tsx` no frontend React.
- Rotas `/projects/:projectId/backlog` e `/boards/:boardId`.
- `ProjectWorkspace.tsx`, `Kanban.tsx` e parâmetros de rota/query do React Router.
- `SPEC-BOARDS-STAGES-WIP` para posição singular e quadro transversal.
- `SPEC-MULTI-BOARD-VIEWS` apenas como histórico superseded do modelo implantado.
- `SPEC-TOP-NAVIGATION-SHELL` para hospedar breadcrumbs e troca contextual sem sidebar.
- D54 e `SPEC-S-003` para a relação entre Product Backlog, Kanban e sprint.

## Error Cases
- **Ciclo na hierarquia (ex: A é pai de B, B é pai de A):** A montagem da árvore no frontend deve prevenir recursão infinita lançando exceção tratada ou interrompendo a renderização cíclica.
- **Subtarefa com ParentId inexistente:** rejeitar a gravação no backend; ocorrência em dado legado é corrupção de integridade a ser registrada e corrigida, nunca silenciosamente promovida a raiz pela UI.
- **Pai em outro projeto ou organização:** rejeitar a gravação, mesmo que identificadores sejam conhecidos.
- **Combinação incompatível com a D36:** rejeitar a gravação sem alterar a hierarquia anterior.
- **Exclusão de pai sem decisão sobre subtarefas:** responder com conflito/regra de negócio, incluindo a
  quantidade de descendentes ativos, sem modificar nenhum item.
- **Falha durante a cascata:** rollback integral e mensagem acionável na interface.

## Decisions Resolved
- A implementação preserva a D36 e monta a árvore no frontend a partir da lista flat já retornada com `ParentId`; a API não precisa introduzir um segundo contrato de árvore aninhada.
- Registros órfãos são inválidos e devem ser impedidos pelo backend/relacionamento persistente; não serão mascarados na UI.
- Pesquisa e filtros preservam e expandem os ancestrais dos resultados para manter o contexto hierárquico.
- A hierarquia é limitada ao projeto; pai e filho podem ocupar quadros diferentes, mas cada item possui apenas
  uma posição atual conforme D52, sem inventar projeções simultâneas.
- A validação da D36 aplica-se em todas as entradas que criem ou alterem `ParentId`, e não somente na renderização do backlog.
- `Excluir` envia para lixeira por 7 dias; `Arquivar` é ação separada. Ao excluir somente o pai, o backend
  preserva a D36 reatribuindo filhas à avó ou tornando-as independentes.

## Test Gate Mapping
- `BacklogFeatureTests`: Adicionar testes para validar que a query do backlog retorna subtarefas com seus respectivos `ParentId`s.
- Testes de backend: validar mesmo projeto, combinações permitidas da D36, rejeição de cadeia inválida e prevenção de ciclo.
- `features/scrum/BacklogPlanner.test.tsx`: Adicionar testes de componente React validando a renderização hierárquica, ações de expandir/colapsar, agrupamento visual pai/filhas, ancestrais preservados em filtros e tratamento defensivo de órfãos/ciclos.
- Testes .NET: lixeira de folha, expiração de 7 dias, restauração, descoberta recursiva, reparenting,
  arquivamento separado, autorização e rollback atômico.
- React/Playwright: lixeira por linha, diálogo com quantidade/opções, restauração, arquivadas e atualização
  das visões sem recarregar a página.
- React/Playwright: navegação Backlog ↔ Quadro no mesmo projeto, preservação de quadro/item/filtros, histórico
  do navegador, foco e comportamento responsivo.
- React/Playwright: breadcrumb com link `Projetos`, `aria-current` no projeto, foco visível e navegação SPA.

## Risks
- Queda de performance na renderização da árvore em projetos com milhares de `WorkItems` se a montagem recursiva não for otimizada (usar memoização).

## Rollback
- Reverter o componente `BacklogPlanner.tsx` para voltar a aplicar o filtro `kind !== 6` e remover a exibição hierárquica.

## Traceability
CAND-B-001 -> SPEC-B-001 -> TASK-005/TASK-021/TASK-025 -> testes .NET, React e Playwright -> commit
