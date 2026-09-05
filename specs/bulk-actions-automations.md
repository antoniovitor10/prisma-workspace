# SPEC-BULK-ACTIONS-AUTOMATIONS: Ações em massa e automações

**Status:** approved
**Baseline:** contrato revisado por PO em 2026-08-24; diferenças do produto atual estão registradas como gaps e aguardam implementação e homologação manual.

## Objective

### Propósito
Definir ações em massa seguras e compreensíveis sobre tarefas selecionadas, incluindo o comportamento hierárquico entre tarefas e subtarefas. As automações já existentes permanecem preservadas internamente, mas ficam ocultas na interface por enquanto.

## Context

### Contrato aprovado
- As ações em massa disponíveis ao usuário são:
  - mover;
  - atribuir responsável;
  - alterar prioridade;
  - adicionar à sprint;
  - arquivar;
  - excluir.
- Selecionar uma tarefa pai deve perguntar se suas subtarefas também serão incluídas.
- Selecionar todas as subtarefas de uma mesma tarefa deve selecionar automaticamente a tarefa pai.
- Se parte da seleção não puder receber a ação, a interface deve identificar as tarefas inválidas e perguntar se o usuário deseja continuar somente com as válidas.
- Ações destrutivas devem exigir confirmação, informar a quantidade de tarefas afetadas e oferecer a ação `Desfazer` depois da execução.
- Automações ficam ocultas na interface por enquanto. Código, regras e dados internos existentes podem permanecer para compatibilidade e evolução futura.

### Estado atual comprovado no código
- O Kanban possui seleção múltipla de até 200 tarefas visíveis.
- `KanbanBulkToolbar` expõe hoje: mover para etapa, atribuir usuário, remover usuário, alterar prioridade, adicionar/remover tag e definir sprint.
- `POST /api/boards/{boardId}/work-items/bulk` executa a alteração em lote de forma síncrona.
- `BulkWorkItemsCommandValidator` rejeita seleção vazia, mais de 200 itens e IDs duplicados.
- O backend valida pertencimento ao quadro, permissão, destino, workflow, WIP, usuário, tag e sprint antes de persistir as mudanças.
- As alterações aplicadas geram `TaskEvent` com tipo `bulk_action`.
- O Product Backlog possui seleção múltipla própria para adicionar tarefas à sprint ou retorná-las ao backlog.
- A seleção atual do Kanban e do Product Backlog trata cada item individualmente; não implementa as regras aprovadas de pai e subtarefas.
- `AutomationManager` está visível no cabeçalho do Kanban e permite criar, editar, ativar, pausar e excluir regras.
- O backend mantém CRUD de `AutomationRule` e executa regras no gatilho de entrada em coluna.
- `AutomationRule` possui `BoardId`, `TriggerStageId`, `ActionType`, `ActionValue`, `IsActive` e `CreatedAt`.
- As ações internas de automação existentes são `AssignUser`, `MoveToStage`, `SetPriority` e `AddTag`.

## Scope

### Escopo incluído
- Seleção explícita de tarefas no Kanban e no Product Backlog.
- Tratamento hierárquico de tarefa pai e subtarefas durante a seleção.
- Pré-validação das tarefas selecionadas antes de executar a ação.
- Identificação de tarefas válidas e inválidas.
- Confirmação humana antes de uma execução parcial.
- Confirmação com quantidade afetada para arquivamento e exclusão.
- Possibilidade de desfazer ações destrutivas já executadas.
- Registro auditável das alterações realizadas.
- Preservação interna do código e dos dados de automações existentes.
- Ocultação de todos os acessos de interface para gestão de automações.

## Out of Scope

### Escopo excluído por enquanto
- Expor criação, edição, ativação, pausa, exclusão ou diagnóstico de automações na interface.
- Adicionar novos gatilhos, condições, scripts, webhooks ou ações de automação.
- Motor genérico de BPMN ou execução de código arbitrário.
- Execução de ações que ignore permissões, workflow, WIP ou invariantes de domínio.
- Aprovação automática de Human Gates.
- Exclusão física de histórico ou evidências auditáveis.

## Actors and Permissions

### Atores e permissões
- O usuário só pode executar em massa uma ação que poderia executar individualmente nas tarefas envolvidas.
- A autorização deve ser verificada antes da execução e respeitar organização, quadro, projeto e tarefa.
- Tarefas sem autorização ou incompatíveis com a ação devem ser apresentadas como inválidas, sem revelar dados protegidos.
- A interface de automações não deve ser oferecida a nenhum perfil enquanto o módulo estiver oculto.
- Os endpoints e dados internos de automação existentes continuam protegidos pelas permissões atuais enquanto forem preservados.

## Functional Requirements

### Seleção hierárquica
1. Ao selecionar uma tarefa pai, a interface deve perguntar se o usuário deseja incluir suas subtarefas.
2. Se o usuário não incluir as subtarefas, somente a tarefa pai permanece selecionada.
3. Quando todas as subtarefas diretas de uma tarefa forem selecionadas, a tarefa pai deve ser selecionada automaticamente.
4. A quantidade exibida na barra de ações deve refletir a seleção final após aplicar as regras hierárquicas.
5. A seleção não pode ultrapassar o limite técnico de 200 tarefas por execução enquanto esse limite existir no backend.

### Catálogo de ações
6. `Mover` altera a coluna das tarefas selecionadas e respeita workflow e limite WIP aplicáveis.
7. `Atribuir responsável` define o responsável escolhido nas tarefas válidas.
8. `Alterar prioridade` aplica a prioridade escolhida nas tarefas válidas.
9. `Adicionar à sprint` vincula as tarefas válidas à sprint escolhida.
10. `Arquivar` usa o mesmo comportamento e as mesmas permissões da ação individual de arquivamento.
11. `Excluir` usa o mesmo comportamento da ação individual de exclusão, incluindo lixeira e preservação histórica definidos na spec de tarefas.

### Validação e execução parcial
12. Antes da execução, o sistema deve validar cada tarefa individualmente.
13. Se todas forem válidas, a ação pode prosseguir normalmente.
14. Se existirem tarefas inválidas, nenhuma alteração deve ocorrer antes da confirmação do usuário.
15. A confirmação deve mostrar a quantidade de tarefas válidas e inválidas e identificar as inválidas de forma segura.
16. O usuário pode cancelar toda a operação ou confirmar a execução somente sobre as tarefas válidas.
17. O resultado deve informar quantas tarefas foram alteradas, ignoradas ou falharam.
18. Falhas não podem ser ocultadas por uma mensagem genérica de sucesso do lote.

### Confirmação e desfazer
19. Arquivar e excluir em massa sempre exigem confirmação explícita.
20. A confirmação deve exibir a ação e a quantidade total de tarefas afetadas, considerando a inclusão de subtarefas.
21. Após arquivar ou excluir, a interface deve oferecer `Desfazer`.
22. `Desfazer` deve restaurar as tarefas e seus relacionamentos abrangidos pela ação original, sem apagar o histórico da operação.
23. O prazo e o mecanismo técnico de `Desfazer` devem ser definidos antes da implementação, sem reduzir a exigência funcional de oferecê-lo.

### Automações ocultas
24. A interface não deve exibir botões, menus, formulários ou páginas de automações.
25. Ocultar o módulo não exige remover `AutomationRule`, endpoints, migrations ou dados existentes.
26. Regras ativas já persistidas não podem continuar produzindo efeitos silenciosos sem uma decisão explícita de operação. Antes da implementação, deve ser definido se elas serão pausadas em lote ou apenas deixarão de ser editáveis pela interface.
27. Qualquer reativação visual futura do módulo exige nova revisão da spec e homologação manual.

## Invariants

- Nenhuma ação em massa amplia as permissões do executor.
- Todas as tarefas passam pelas mesmas validações essenciais da ação individual correspondente.
- Uma operação parcial só começa depois de confirmação explícita.
- Arquivamento e exclusão em massa nunca são executados sem confirmação.
- O histórico original não é apagado ao desfazer uma ação.
- Seleção automática do pai ocorre somente quando todas as suas subtarefas aplicáveis estão selecionadas.
- Automações não autoaprovam gates humanos.

## Data Impact

### Persistência atual
- Ações em massa são executadas diretamente sobre `WorkItem` pelo endpoint bulk e registram eventos `bulk_action`.
- Não existe uma entidade persistida de execução em massa, prévia ou desfazer.
- Automações usam a entidade existente `AutomationRule`.
- Não existem `AutomationRuleVersion`, `AutomationExecution` ou `AutomationExecutionItem`.

### Impacto futuro
- Implementar `Desfazer` e um resultado durável por item pode exigir persistência adicional ou payload de compensação.
- Qualquer alteração de schema exige `G-MIGRATION` antes da implementação.
- Alterações na estrutura imutável do histórico exigem `G-HISTORY`.
- A spec não autoriza remover dados ou migrations de automações já existentes.

## Contracts

### API atual
- `POST /api/boards/{boardId}/work-items/bulk`: executa uma ação em massa síncrona, com limite de 200 IDs.
- `GET /api/boards/{boardId}/automations`: lista regras internas.
- `POST /api/boards/{boardId}/automations`: cria regra interna.
- `PUT /api/boards/{boardId}/automations/{id}`: atualiza e ativa/pausa regra interna por `IsActive`.
- `DELETE /api/boards/{boardId}/automations/{id}`: exclui regra interna.

### Contratos ainda necessários
- Pré-validação por item com separação entre válidos e inválidos.
- Confirmação de execução parcial sem alterar dados antes da resposta do usuário.
- Ações bulk de arquivar e excluir.
- Contrato seguro para desfazer arquivamento ou exclusão em massa.
- Resultado estruturado com totais de sucesso, itens ignorados e falhas.

### Interface desejada
- Barra de seleção com quantidade final e ação escolhida.
- Pergunta contextual ao selecionar tarefa pai.
- Seleção automática do pai quando todas as subtarefas forem selecionadas.
- Resumo de itens válidos e inválidos antes de execução parcial.
- Modal de confirmação para arquivamento e exclusão, com quantidade afetada.
- Feedback de conclusão com ação `Desfazer`.
- Nenhum acesso visual a automações enquanto estiverem ocultas.

## Error Cases

### Validações e erros
- Seleção vazia ou acima de 200 tarefas.
- IDs repetidos ou tarefas inexistentes.
- Tarefa fora do escopo autorizado.
- Destino, responsável, prioridade ou sprint inválidos.
- Movimento incompatível com workflow ou limite WIP.
- Tarefa que não pode ser arquivada ou excluída conforme as regras individuais.
- Falha ao desfazer porque a restauração viola uma regra atual deve ser informada claramente e não pode resultar em restauração parcial silenciosa.
- Falha de uma tarefa não deve ser apresentada como sucesso total.

## Authorization Impact

### Segurança
- Reavaliar autorização na execução; a pré-validação não reserva permissão.
- Não expor detalhes protegidos das tarefas inválidas.
- Não aceitar código, SQL, nomes de handlers ou expressões executáveis vindas do cliente.
- Segredos não podem existir nos eventos, definições ou mensagens de erro.
- Preservar isolamento por organização em todas as ações.

## Acceptance Criteria

- **Given** uma tarefa pai com subtarefas
  **When** o usuário seleciona a tarefa pai
  **Then** o sistema pergunta se deve incluir as subtarefas e respeita a escolha.
- **Given** todas as subtarefas diretas de uma tarefa selecionadas
  **When** a seleção é atualizada
  **Then** a tarefa pai também é selecionada automaticamente.
- **Given** uma seleção parcialmente inválida
  **When** o usuário solicita uma ação em massa
  **Then** nada é alterado até que sejam mostradas as inválidas e o usuário confirme continuar somente com as válidas.
- **Given** uma ação em massa de mover, atribuir responsável, alterar prioridade ou adicionar à sprint
  **When** a seleção válida é confirmada
  **Then** a alteração é aplicada somente às tarefas autorizadas e o resultado informa o total processado.
- **Given** arquivamento ou exclusão em massa
  **When** o usuário inicia a operação
  **Then** o sistema mostra confirmação com a quantidade total afetada.
- **Given** uma ação destrutiva concluída
  **When** o feedback é apresentado
  **Then** a ação `Desfazer` fica disponível e restaura o conjunto afetado conforme o contrato aprovado.
- **Given** o módulo de automações preservado internamente
  **When** o usuário navega pela aplicação
  **Then** nenhum acesso de interface para automações é exibido.

## Test Gate Mapping

### Testes necessários
- Testes unitários da seleção pai/subtarefas.
- Testes de componente para pergunta de inclusão, seleção automática do pai e contagem final.
- Testes de handler e integração para cada ação do catálogo aprovado.
- Testes de autorização e validação individual por tarefa.
- Testes de execução parcial garantindo ausência de alteração antes da confirmação.
- Testes de confirmação e desfazer para arquivamento e exclusão.
- Teste de regressão garantindo que a interface de automações está oculta.
- Playwright E2E cobrindo seleção hierárquica, lote totalmente válido, lote parcialmente inválido, cancelamento, confirmação destrutiva e desfazer.

### Homologação manual pendente
- Selecionar uma tarefa pai e aceitar/recusar a inclusão das subtarefas.
- Selecionar todas as subtarefas e confirmar a seleção automática do pai.
- Executar cada uma das seis ações aprovadas.
- Validar a lista de itens inválidos e cancelar uma execução parcial.
- Confirmar uma execução parcial somente com itens válidos.
- Arquivar e excluir em massa, conferindo quantidade e `Desfazer`.
- Confirmar que automações não aparecem em nenhuma superfície da interface.

## Implementation Gaps

1. O catálogo atual não possui arquivar ou excluir em massa.
2. O catálogo atual expõe ações fora do conjunto aprovado: remover usuário, adicionar tag e remover tag.
3. A ação atual atribui participantes (`WorkItemAssignee`); a decisão exige atribuir o responsável principal e precisa de adequação explícita.
4. Não existe pergunta ao selecionar tarefa pai nem inclusão opcional das subtarefas.
5. Selecionar todas as subtarefas não seleciona automaticamente a tarefa pai.
6. O backend atual valida o lote como conjunto e falha antes da persistência quando encontra incompatibilidade; não retorna prévia separando tarefas válidas e inválidas.
7. Não há confirmação antes de execução parcial porque execução parcial ainda não existe.
8. Não há confirmação destrutiva com quantidade para arquivamento/exclusão em massa.
9. Não existe ação `Desfazer` nem contrato de compensação persistido.
10. O botão e o modal `Automações` continuam visíveis no cabeçalho do Kanban, contrariando a decisão de ocultá-los.
11. É necessário decidir operacionalmente o destino das regras de automação já ativas antes de ocultar o módulo, para evitar execução silenciosa sem gestão visível.
12. A homologação manual deste contrato ainda não foi executada.

## Dependencies

- `specs/work-item-management.md`.
- `specs/workflow-status.md`.
- `specs/search-saved-filters.md`.
- `specs/audit-leadtime-history.md`.
- Alteração de schema exige `G-MIGRATION`.
- Alteração de workflow exige `G-WORKFLOW`.
- Alteração de histórico imutável exige `G-HISTORY`.

## Risks

- Uma seleção hierárquica incorreta pode afetar mais tarefas do que o usuário percebe.
- A execução parcial pode produzir estado inesperado se as tarefas inválidas não forem apresentadas com clareza.
- Um `Desfazer` incompleto pode restaurar apenas parte do lote.
- Regras de automação ativas e ocultas podem continuar alterando tarefas sem um ponto visível de administração.
- Alterações amplas incorretas podem comprometer workflow, sprint, responsáveis e auditoria.

## Pending Decisions

### Decisões ainda necessárias antes da implementação
1. Por quanto tempo a ação `Desfazer` ficará disponível.
2. Se `Desfazer` será uma compensação registrada no histórico ou uma restauração baseada em snapshot da execução.
3. Se regras de automação atualmente ativas serão pausadas em lote ao ocultar a interface ou continuarão executando internamente.
4. Como a seleção hierárquica trata descendentes além das subtarefas diretas.

## Rollback

- Desabilitar temporariamente as novas ações em massa sem apagar o histórico já registrado.
- Se o novo contrato de execução parcial falhar, retornar ao comportamento atômico anterior e informar indisponibilidade da execução parcial.
- Preservar dados de automações existentes; ocultação da interface não autoriza exclusão.
- Reverter schema somente após os gates aplicáveis e sem apagar evidência auditável.

## Traceability

### Referências
- `AGENTS.md`.
- `DECISIONS.md` — D18, D21, D22, D34, D37, D52 e D53.
- `src/Detran.Kanban.Web/src/features/board/KanbanBulkToolbar.tsx`.
- `src/Detran.Kanban.Web/src/features/board/AutomationManager.tsx`.
- `src/Detran.Kanban.Web/src/pages/Kanban.tsx`.
- `src/Detran.Kanban.Application/Features/Productivity/ProductivityFeature.cs`.
- `src/Detran.Kanban.Api/Controllers/ProductivityController.cs`.
- `tests/Detran.Kanban.Tests/ProductivityFeatureTests.cs`.
- CAND-BULK-ACTIONS-AUTOMATIONS -> SPEC-BULK-ACTIONS-AUTOMATIONS -> task -> tests -> commit.
