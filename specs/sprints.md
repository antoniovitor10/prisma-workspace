# SPEC-S-003: Sprints, planejamento e quadro operacional

**Status:** approved
**Revisão humana:** decisões confirmadas por PO em 2026-08-24
**Natureza:** contrato desejado aprovado; o estado atual e os gaps estão separados abaixo

## Objetivo

Permitir que tarefas de um projeto sejam planejadas em sprints sem perder sua posição no Kanban, com ciclo de
vida calculado pelas datas, planejamento hierárquico, tratamento explícito dos itens abertos no encerramento e
histórico consultável.

## Contrato aprovado pelo PO

### Ciclo de vida

1. A sprint possui os estados funcionais `Planejada`, `Ativa` e `Encerrada`.
2. O estado é calculado automaticamente a partir da data atual, da data inicial e da data final.
3. Não existe ação manual `Iniciar sprint`.
4. Podem existir várias sprints ativas simultaneamente no mesmo projeto quando seus períodos se sobrepõem.
5. Uma sprint encerrada continua visível no histórico e nos filtros.
6. Ao chegar ao encerramento com tarefas abertas, o usuário autorizado deve escolher, para essas tarefas:
   - mover para outra sprint do mesmo projeto; ou
   - remover o vínculo com a sprint, fazendo-as voltar ao Product Backlog padrão.
7. Tarefas concluídas permanecem como evidência histórica da sprint encerrada.
8. Enquanto a escolha sobre tarefas abertas não for confirmada, nenhum vínculo deve ser alterado parcialmente.

### Criação, edição, exclusão e autorização

9. Criar uma sprint exige `nome`, `data inicial` e `data final`.
10. A data final deve ser igual ou posterior à data inicial.
11. Objetivo e equipe são opcionais.
12. Criar, editar ou excluir sprint exige uma permissão configurável por usuário/perfil; não depende de um papel
    fixo como `ScrumMaster`.
13. A API é a autoridade final da autorização, mesmo quando a interface oculta ou desabilita ações.
14. Excluir uma sprint remove somente o vínculo `SprintId` das tarefas vinculadas.
15. A exclusão nunca exclui, arquiva ou movimenta a tarefa no Kanban.
16. Quadro, coluna, posição, conteúdo, hierarquia e histórico da tarefa são preservados.
17. A desvinculação das tarefas e a exclusão da sprint devem ocorrer na mesma transação.

### Relação com Product Backlog, Kanban e tarefas

18. Product Backlog, Kanban e sprint são recortes do mesmo `WorkItem`; não existem cópias da tarefa.
19. O Product Backlog mostra por padrão tarefas sem sprint (`SprintId = null`).
20. O Product Backlog oferece filtro para incluir tarefas que já estejam em uma sprint. O detalhamento visual
    desse filtro pertence à spec de backlog.
21. Uma tarefa pode permanecer no Kanban sem pertencer a uma sprint.
22. Incluir, transferir ou retirar uma tarefa de sprint não altera quadro, coluna ou posição no Kanban.
23. Uma tarefa pertence, no máximo, a uma sprint por vez.
24. Ao excluir uma sprint, suas tarefas ficam sem sprint e voltam a aparecer no Product Backlog padrão.

### Capacidade

25. Não existe cálculo automático de capacidade nesta versão.
26. O sistema não deve inferir capacidade a partir de calendário, equipe, jornada ou quantidade de tarefas.
27. Se valores manuais de disponibilidade e ausência forem mantidos por compatibilidade, devem ser tratados
    apenas como entradas informativas; não podem impedir criação, ativação automática ou encerramento da sprint.

### Planejamento hierárquico

28. Ao planejar uma tarefa pai, todas as descendentes ativas são incluídas na mesma sprint.
29. Quando todas as filhas diretas ativas de um pai forem selecionadas para a mesma sprint, o pai também é
    incluído automaticamente; a regra é aplicada recursivamente aos ancestrais.
30. A interface informa quantos itens foram selecionados diretamente e quantos foram incluídos automaticamente.
31. O backend recalcula a família e persiste a alteração de forma atômica.
32. Em falha, nenhum item da família muda de `SprintId`.
33. Se não houver sprint disponível durante o planejamento, a interface oferece `Criar sprint`, preserva a
    seleção atual e, após a criação, permite concluir o planejamento sem selecionar os itens novamente.

### Quadro da sprint

34. A aba `Quadro` representa as mesmas tarefas da sprint nas posições que elas ocupam no Kanban.
35. Como uma sprint pode conter tarefas posicionadas em quadros diferentes, a tela agrupa por `BoardId`, sem
    misturar colunas de mesmo nome pertencentes a quadros distintos.
36. Cada seção exibe as colunas ativas do quadro na ordem de `Stage.Position`, inclusive quando vazias.
37. Uma tarefa pode ser arrastada apenas entre colunas do mesmo quadro nessa tela.
38. O movimento usa `POST /api/WorkItems/move`, preserva `SprintId` e segue as regras operacionais vigentes do
    Kanban.
39. A interface aplica atualização otimista e restaura a posição anterior se a API rejeitar a operação.
40. Sprints encerradas mostram seu quadro em modo somente leitura.

## Critérios de aceite

- **Dado** que hoje é anterior à data inicial, **quando** a sprint é consultada, **então** seu estado é
  `Planejada`.
- **Dado** que hoje está entre as datas inicial e final, inclusive, **quando** a sprint é consultada, **então**
  seu estado é `Ativa`, sem ação manual de início.
- **Dado** que existem períodos sobrepostos, **quando** duas ou mais sprints chegam à data inicial,
  **então** todas podem aparecer como ativas no mesmo projeto.
- **Dado** que a sprint chegou ao encerramento com tarefas abertas, **quando** o responsável trata o
  encerramento, **então** escolhe outra sprint ou Product Backlog antes de confirmar.
- **Dado** que o usuário escolheu outra sprint, **quando** confirma, **então** somente as tarefas abertas são
  transferidas para a sprint de destino do mesmo projeto.
- **Dado** que o usuário escolheu Product Backlog, **quando** confirma, **então** as tarefas abertas recebem
  `SprintId = null` sem mudar de quadro ou coluna.
- **Dado** que uma sprint está encerrada, **quando** o usuário consulta histórico ou filtros, **então** ela
  continua disponível para consulta.
- **Dado** que o usuário não possui a permissão configurável de gestão de sprints, **quando** tenta criar,
  editar ou excluir, **então** a API rejeita a operação e a UI não oferece a ação.
- **Dado** um formulário sem nome, data inicial ou data final, **quando** o usuário tenta criar a sprint,
  **então** a operação é bloqueada com validação por campo.
- **Dado** que uma sprint possui tarefas, **quando** o usuário autorizado confirma a exclusão, **então** a
  sprint é excluída e todas as tarefas ficam sem sprint, preservando Kanban e histórico.
- **Dado** que o Product Backlog foi aberto sem filtros adicionais, **então** mostra somente tarefas sem sprint.
- **Dado** que o filtro para incluir tarefas planejadas foi ativado, **então** tarefas em sprint também aparecem.
- **Dado** uma tarefa pai com descendentes ativos, **quando** ela é planejada, **então** toda a família é
  vinculada atomicamente à sprint.
- **Dado** um card na aba `Quadro` da sprint, **quando** é movido para outra coluna do mesmo quadro,
  **então** a nova posição persiste e o `SprintId` não muda.
- **Dado** um movimento rejeitado por regra operacional ou autorização, **então** a UI restaura o card e
  apresenta mensagem acessível.

## Estado atual comprovado no código

O código atual já possui:

- entidade `Sprint` com nome, objetivo opcional, datas, equipe opcional, estado persistido, capacidades e
  snapshots históricos;
- validação de nome obrigatório e intervalo de datas válido na entidade e nos validators;
- listagem de todas as sprints do projeto ordenadas por data inicial, incluindo estados terminais;
- criação e edição via API;
- encerramento/cancelamento com escolha entre Product Backlog e outra sprint para itens abertos;
- snapshot dos itens ao concluir ou cancelar;
- planejamento de tarefas pelo mesmo `SprintId`, sem duplicar o `WorkItem`;
- dashboard com planejamento, quadro, métricas e histórico;
- quadro da sprint com movimentação persistente e preservação do vínculo da sprint;
- capacidade informada manualmente por pessoa.

Evidências principais:

- `src/Detran.Kanban.Domain/Entities/Sprint.cs`
- `src/Detran.Kanban.Domain/Enums/SprintStatus.cs`
- `src/Detran.Kanban.Application/Features/Sprints/SprintsFeature.cs`
- `src/Detran.Kanban.Infrastructure/Repositories/SprintRepository.cs`
- `src/Detran.Kanban.Api/Controllers/SprintsController.cs`
- `src/Detran.Kanban.Web/src/features/scrum/SprintDashboard.tsx`
- `src/Detran.Kanban.Web/src/features/scrum/SprintKanbanBoard.tsx`

## Gaps entre o código atual e o contrato aprovado

1. **Estado manual:** `Sprint.Status` é persistido e alterado pelo endpoint
   `PUT /api/sprints/{id}/status`; não é calculado pelas datas.
2. **Botão manual:** a UI ainda exibe `Iniciar sprint`.
3. **Uma ativa por projeto:** domínio, handler e repositório bloqueiam a ativação quando já existe outra sprint
   ativa, contrariando a decisão de permitir períodos simultâneos.
4. **Autorização fixa:** criar, editar, mudar status e capacidade exigem hoje no mínimo `ScrumMaster`, em vez
   de uma permissão configurável por usuário/perfil.
5. **Exclusão ausente:** não existe endpoint `DELETE /api/sprints/{id}` nem fluxo transacional de desvinculação.
6. **Capacidade exposta:** o dashboard mantém editor e KPI de capacidade. Embora os valores sejam manuais,
   essa superfície deve ser homologada para confirmar se continuará apenas informativa ou ficará oculta.
7. **Encerramento automático:** não existe processamento automático por data nem mecanismo definido para
   solicitar a destinação dos itens abertos quando a data final passa.
8. **Contrato histórico:** sprints terminais são retornadas pela API e exibidas na trilha atual, mas falta
   homologação manual dos filtros e do histórico completo.
9. **D25 desatualizada:** a regra antiga de somente uma sprint ativa e transição manual conflita com a decisão
   humana atual. `DECISIONS.md` precisa registrar uma decisão sucessora antes da implementação.

## Impacto de dados e workflow

- A alteração de status persistido para status calculado precisa definir a compatibilidade de sprints já
  concluídas ou canceladas e de `CompletedAt`/`CancelledAt`.
- A remoção da unicidade lógica de sprint ativa altera regra de workflow e exige `G-WORKFLOW`.
- Se índices, constraints ou colunas precisarem mudar, a implementação exige `G-MIGRATION`; esta spec não
  presume migration sem auditoria do modelo físico.
- A exclusão deve preservar `SprintItemSnapshot` conforme a política histórica vigente; se houver mudança da
  estrutura ou semântica imutável, exige `G-HISTORY`.

## Contratos de API esperados

### Consulta

- `GET /api/projects/{projectId}/sprints` retorna datas e o estado funcional derivado, incluindo sprints
  encerradas para histórico/filtros.

### Criação

- `POST /api/projects/{projectId}/sprints`
- Obrigatórios: `name`, `startDate`, `endDate`.
- Opcionais: `goal`, `teamId`.

### Edição

- `PUT /api/sprints/{id}` altera nome, objetivo e datas mediante permissão configurável.

### Exclusão

- `DELETE /api/sprints/{id}` exige confirmação na UI e permissão configurável.
- API remove `SprintId` das tarefas e exclui a sprint na mesma transação.
- Falha em qualquer etapa causa rollback integral.

### Tratamento de itens abertos no encerramento

- O comando recebe `ReturnToBacklog` ou `MoveToSprint`.
- `MoveToSprint` exige uma sprint de destino diferente, pertencente ao mesmo projeto.
- A operação registra o resultado histórico e altera somente o vínculo de sprint dos itens abertos.

### Movimentação no quadro

- `POST /api/WorkItems/move` recebe `workItemId`, `destinationStageId` e `position`.
- O movimento não altera `SprintId` nem `BacklogRank`.

## Regras de concorrência e erro

- Planejamento, transferência de itens abertos e exclusão da sprint são transacionais.
- Uma resposta `409 concurrency_conflict` deve provocar nova leitura dos dados afetados, sem exigir recarga
  manual da página.
- Se a intenção já estiver refletida no servidor, a UI a trata como concluída.
- Se ainda for válida, a UI preserva o diálogo/seleção e solicita nova confirmação com dados atuais.
- Se deixar de ser válida, a UI encerra ou desabilita a ação e explica o motivo.
- Falha ao desvincular ou transferir qualquer item causa rollback integral.
- A sprint de destino não pode ser a própria sprint nem pertencer a outro projeto.

## Homologação manual pendente

- Criar sprint somente com nome, data inicial e data final.
- Confirmar mudança automática entre Planejada, Ativa e Encerrada conforme as datas.
- Confirmar coexistência de várias sprints ativas no mesmo projeto.
- Confirmar ausência de `Iniciar sprint`.
- Confirmar permissão configurável em criação, edição e exclusão.
- Excluir sprint com tarefas e verificar que elas continuam no mesmo quadro/coluna e ficam sem sprint.
- Encerrar sprint com itens abertos escolhendo outra sprint e Product Backlog em testes separados.
- Confirmar filtro do Product Backlog para incluir tarefas em sprint.
- Confirmar que sprints encerradas continuam disponíveis em histórico e filtros.
- Confirmar que capacidade não é calculada automaticamente nem bloqueia o ciclo da sprint.
- Confirmar planejamento hierárquico e rollback em falha.
- Confirmar movimentação e persistência no quadro da sprint.

## Test Gate Mapping

- **.NET:** cálculo do estado por datas; sobreposição de sprints; autorização configurável; validação da
  criação; exclusão transacional; preservação de quadro/coluna/histórico; destinação atômica de itens abertos;
  planejamento hierárquico; concorrência.
- **React/Vitest:** ausência do botão iniciar; formulários; permissões; diálogo de exclusão; escolha de destino;
  estados históricos; capacidade apenas informativa; rollback acessível.
- **Playwright:** criar sprint; observar transição por data; manter duas ativas; excluir com tarefas; encerrar
  para backlog e para outra sprint; filtrar backlog; consultar encerradas; mover card no quadro e recarregar.

## Human Gates para implementação

- `G-SPEC`: já aprovado pelo PO para este contrato.
- `G-WORKFLOW`: obrigatório para substituir o ciclo manual e a exclusividade de sprint ativa.
- `G-MIGRATION`: obrigatório somente se a auditoria comprovar alteração real de schema/constraint.
- `G-HISTORY`: obrigatório somente se a implementação alterar a estrutura ou a semântica imutável dos
  snapshots existentes.
- `G-COMPLETION`: conforme a classificação da tarefa de implementação.

## Riscos

- Datas e fuso horário inconsistentes podem produzir estados diferentes entre cliente e servidor; a API deve
  ser a fonte do estado funcional.
- Encerramento automático sem resolução dos itens abertos pode deixar vínculos ambíguos; a escolha deve ser
  explícita e transacional.
- Excluir sprint sem preservar snapshots pode apagar evidência histórica.
- Remover a regra de uma sprint ativa exige revisar métricas, notificações e seletores que hoje priorizam uma
  única sprint ativa.

## Rastreabilidade

`D25` (a suceder parcialmente) + `D52` + `D54` → `SPEC-S-003` → `TASK-008` / `TASK-022` / `TASK-023` →
testes .NET, React e Playwright → homologação manual
