# SPEC-S-003: Sprints, planejamento e quadro operacional (v2 — Sprint ↔ Project N:N)

**Status:** draft

**Versão:** 2 (sucede a v1 `approved` de 2026-08-24)

**Motivo do rebaixamento para `draft`:** o PO determinou em 2026-09-08, textualmente,
*"pode garantir todas as specs como aprovadas exceto isso que passei ai agora"*, referindo-se ao novo
contrato N:N de Sprint. As demais specs ativas permanecem aprovadas; esta volta para `draft` e precisa de
`G-SPEC` humano antes de qualquer implementação.

**Autoridade da mudança:** decisão humana explícita do PO em 2026-09-08. Não é autoaprovação de agente.

**Natureza:** contrato desejado em revisão. O estado atual comprovado no código e os gaps continuam separados
abaixo.

---

## Objetivo

Permitir que **uma sprint reúna trabalho de vários projetos** e que **um projeto participe de várias sprints**,
sem que a sprint deixe de ser um recorte do mesmo `WorkItem` já posicionado no Kanban. O ciclo de vida é
calculado pelas datas, o planejamento é hierárquico e atômico, o encerramento exige destino explícito para as
tarefas abertas e o histórico permanece consultável.

## Contexto e motivação da v2

A v1 modelava `Sprint.ProjectId` como vínculo único e obrigatório. Isso impede o cenário real do PO: um ciclo
de duas semanas que atravessa vários projetos da mesma organização, com métricas consolidadas e segmentáveis.
A v2 introduz a associação N:N `SprintProject` sem transformar a sprint em dona da tarefa e sem tocar em
`Board`, `Stage` ou `Position`.

---

## Contrato aprovado pelo PO — parte determinada em 2026-09-08

### N:N Sprint ↔ Project

1. `Sprint` pertence à **Organization**. `OrganizationId` passa a ser o vínculo de tenant obrigatório.
2. `Team` continua **opcional** na sprint e não define escopo de projeto.
3. Existe a associação `SprintProject`, com chave composta `SprintId` + `ProjectId`.
4. Uma sprint reúne **vários** projetos; um projeto participa de **várias** sprints, inclusive simultâneas.
5. Um `WorkItem` continua pertencendo a **um único** `Project` e a **no máximo uma** sprint por vez.
6. Vincular uma tarefa a uma sprint só é válido se o `Project` da tarefa estiver associado àquela sprint. A API
   é a autoridade dessa validação e rejeita o vínculo caso contrário.
7. Adicionar ou remover uma sprint **nunca** altera `Board`, `Stage` ou `Position` da tarefa.
8. Remover um `Project` de uma sprint exige tratamento atômico das tarefas daquele projeto vinculadas à sprint:
   o usuário autorizado escolhe explicitamente entre remover o `SprintId` dessas tarefas ou mover essas tarefas
   para outra sprint que já contenha aquele projeto. Sem escolha confirmada, a remoção do projeto é cancelada
   integralmente.
9. Métricas da sprint são **consolidadas** (toda a sprint) e **segmentáveis por projeto**, usando o mesmo
   conjunto de tarefas — sem tabelas de resumo paralelas.
10. Toda operação de sprint valida permissões em **todos** os projetos envolvidos. Falta de permissão em
    qualquer projeto participante impede a operação inteira; não existe execução parcial.
11. O backfill de `Sprint.ProjectId` para `SprintProject` é **obrigatório** na migração: cada sprint existente
    gera exatamente uma linha `SprintProject` com o projeto atual, sem perda de vínculo.
12. O estado da sprint é **calculado pelas datas**. Não existe ação `Iniciar sprint`. Períodos sobrepostos são
    permitidos, inclusive várias sprints ativas na mesma organização e no mesmo projeto.
13. Excluir uma sprint remove **somente** o `SprintId` das tarefas. Tarefas e histórico nunca são excluídos.
14. Planejamento hierárquico é **atômico**; tarefas abertas no encerramento exigem **destino explícito**.

### Ciclo de vida (v1 preservada e reafirmada)

15. Estados funcionais: `Planejada`, `Ativa` e `Encerrada`, derivados de `StartDate`, `EndDate` e da data atual.
16. Uma sprint encerrada continua visível em histórico e filtros.
17. Ao encerrar com tarefas abertas, o usuário autorizado escolhe, para essas tarefas: mover para outra sprint
    compatível (que contenha o projeto da tarefa) ou remover o vínculo, devolvendo-as ao Product Backlog padrão.
18. Tarefas concluídas permanecem como evidência histórica da sprint encerrada.
19. Enquanto a escolha sobre tarefas abertas não for confirmada, nenhum vínculo é alterado parcialmente.

### Criação, edição, exclusão e autorização

20. Criar uma sprint exige `nome`, `data inicial`, `data final` e **pelo menos um projeto** participante.
21. A data final deve ser igual ou posterior à data inicial.
22. Objetivo e equipe permanecem opcionais.
23. Criar, editar, excluir e administrar projetos participantes exige **permissão configurável** por
    usuário/perfil, avaliada em todos os projetos envolvidos. Não depende de papel fixo como `ScrumMaster`.
24. A API é a autoridade final da autorização, mesmo quando a interface oculta ou desabilita a ação.
25. A desvinculação das tarefas e a exclusão da sprint ocorrem na mesma transação; falha causa rollback integral.

### Relação com Product Backlog, Kanban e tarefas

26. Product Backlog, Kanban e sprint são recortes do mesmo `WorkItem`; não existem cópias.
27. O Product Backlog mostra por padrão tarefas sem sprint (`SprintId = null`) e oferece filtro para incluir
    tarefas já planejadas.
28. Uma tarefa pode permanecer no Kanban sem pertencer a nenhuma sprint.
29. Incluir, transferir ou retirar uma tarefa de sprint não altera quadro, coluna ou posição.

### Capacidade

30. Não existe cálculo automático de capacidade nesta versão; o sistema não infere capacidade a partir de
    calendário, equipe, jornada ou quantidade de tarefas.
31. Valores manuais preservados por compatibilidade são apenas informativos e não bloqueiam ciclo nem
    encerramento.

### Planejamento hierárquico

32. Ao planejar uma tarefa pai, todas as descendentes ativas entram na mesma sprint. Como toda a família
    pertence ao mesmo `Project` (regra 5), a validação da regra 6 é feita uma vez para a família.
33. Quando todas as filhas diretas ativas de um pai forem selecionadas para a mesma sprint, o pai é incluído
    automaticamente; a regra é recursiva sobre os ancestrais.
34. A interface informa quantos itens foram selecionados diretamente e quantos foram incluídos automaticamente.
35. O backend recalcula a família e persiste de forma atômica; em falha, nenhum item muda de `SprintId`.
36. Sem sprint disponível, a interface oferece `Criar sprint`, preserva a seleção e conclui o planejamento sem
    reselecionar itens.

### Quadro da sprint

37. A aba `Quadro` representa as mesmas tarefas nas posições que ocupam no Kanban.
38. Como a sprint agora atravessa projetos, a tela agrupa por `BoardId` e oferece **segmentação por projeto**,
    sem misturar colunas homônimas de quadros distintos.
39. Cada seção exibe as colunas ativas do quadro na ordem de `Stage.Position`, inclusive vazias.
40. Uma tarefa só pode ser arrastada entre colunas do mesmo quadro nessa tela.
41. O movimento usa `POST /api/WorkItems/move`, preserva `SprintId` e segue as regras operacionais do Kanban.
42. A interface aplica atualização otimista e restaura a posição anterior se a API rejeitar.
43. Sprints encerradas exibem o quadro em modo somente leitura.

---

## Modelo de dados proposto

```
Sprint
  Id                (PK)
  OrganizationId    (obrigatório, novo — tenant raiz conforme D58)
  TeamId            (opcional, preservado)
  Name, Goal, StartDate, EndDate
  CreatedAt
  ProjectId         (legado — preservado somente até o backfill ser validado; depois removido em migration própria)
  Status/CompletedAt/CancelledAt (legado — deixam de governar; ver seção "Estado calculado")

SprintProject
  SprintId          (PK composta, FK Sprint)
  ProjectId         (PK composta, FK Project)
  AddedAt
  AddedByUserId

WorkItem
  SprintId          (opcional, inalterado)
  ProjectId efetivo (derivado de Board.ProjectId / vínculo atual — inalterado por esta spec)
```

### Estado calculado

- O estado funcional é derivado: `hoje < StartDate` → `Planejada`; `StartDate <= hoje <= EndDate` → `Ativa`;
  `hoje > EndDate` → `Encerrada`.
- O fuso é o da organização (`Organization.TimeZone`). A **API é a única fonte do estado funcional**; a UI
  nunca recalcula.
- `Sprint.Status`, `CompletedAt` e `CancelledAt` permanecem como colunas legadas durante a transição, sem
  governar exibição, filtro ou transição. Sua remoção física exige `G-MIGRATION` própria.

### Backfill obrigatório

1. Para cada `Sprint` existente com `ProjectId` não nulo, inserir uma linha em `SprintProject`.
2. Preencher `Sprint.OrganizationId` a partir de `Project.OrganizationId` da sprint de origem.
3. Verificação pós-backfill: `COUNT(Sprint)` com `ProjectId` não nulo == `COUNT(SprintProject)` agrupado por
   sprint com exatamente uma linha; nenhuma sprint sem `OrganizationId`.
4. Nenhuma tarefa muda de `SprintId`, `BoardId`, `StageId` ou `Position` durante o backfill.
5. O backfill é idempotente e reexecutável.

---

## Critérios de aceite

- **Dado** uma sprint com os projetos A e B, **quando** um usuário planeja uma tarefa do projeto A, **então** o
  vínculo é aceito.
- **Dado** uma sprint com os projetos A e B, **quando** um usuário tenta planejar uma tarefa do projeto C,
  **então** a API rejeita com erro de validação explícito e nada é persistido.
- **Dado** um projeto participante de três sprints, **quando** suas sprints são consultadas, **então** todas
  aparecem, inclusive com períodos sobrepostos.
- **Dado** que hoje é anterior à data inicial, **então** o estado é `Planejada`, sem ação manual de início.
- **Dado** que hoje está entre as datas, inclusive, **então** o estado é `Ativa`.
- **Dado** que a sprint contém tarefas do projeto A, **quando** o usuário remove o projeto A da sprint,
  **então** a UI exige escolha explícita entre desvincular essas tarefas ou movê-las para outra sprint
  compatível, e a operação inteira é atômica.
- **Dado** que a escolha do parágrafo anterior não foi confirmada, **então** nem o projeto nem as tarefas são
  alterados.
- **Dado** que o usuário não possui permissão em um dos projetos participantes, **quando** tenta qualquer
  operação da sprint, **então** a API rejeita a operação inteira.
- **Dado** que uma sprint possui tarefas, **quando** o usuário autorizado confirma a exclusão, **então** a
  sprint é excluída, todas as tarefas ficam com `SprintId = null` e quadro, coluna, posição, conteúdo,
  hierarquia e histórico são preservados.
- **Dado** o encerramento com tarefas abertas, **então** o usuário escolhe outra sprint compatível ou o Product
  Backlog antes de confirmar.
- **Dado** uma tarefa pai com descendentes ativos, **quando** é planejada, **então** toda a família é vinculada
  atomicamente; em falha, nenhum item muda.
- **Dado** o dashboard da sprint, **quando** o usuário segmenta por projeto, **então** as métricas mudam de
  recorte sem alterar dados nem criar agregados paralelos.
- **Dado** um card na aba `Quadro`, **quando** é movido para outra coluna do mesmo quadro, **então** a posição
  persiste e o `SprintId` não muda.
- **Dado** o backfill aplicado, **quando** as sprints legadas são consultadas, **então** cada uma exibe
  exatamente o projeto que possuía antes, sem perda de vínculo.

---

## Estado atual comprovado no código

Evidências verificadas nesta auditoria (arquivo e linha):

- `src/Prisma.Workspace.Domain/Entities/Sprint.cs:10` — `public Guid ProjectId { get; set; }`: vínculo 1:N
  obrigatório com projeto; **não existe** `OrganizationId` nem `SprintProject`.
- `src/Prisma.Workspace.Domain/Entities/Sprint.cs:16` — `Status` persistido com default `Planned`.
- `src/Prisma.Workspace.Domain/Entities/Sprint.cs:74-91` — `ChangeStatus` exige o ciclo manual
  `Planejada → Ativa → Concluída` e bloqueia ativação quando já existe outra sprint ativa
  (`projectHasAnotherActiveSprint`).
- `src/Prisma.Workspace.Domain/Entities/Sprint.cs:54-72` — `CaptureHistory` grava `SprintItemSnapshot`.
- `src/Prisma.Workspace.Domain/Entities/Sprint.cs:104-141` — `SprintItemSnapshot` imutável já implementado.
- `src/Prisma.Workspace.Application/Features/Sprints/SprintsFeature.cs` — criação, edição e mudança de status.
- `src/Prisma.Workspace.Api/Controllers/SprintsController.cs` — endpoints existentes.
- `src/Prisma.Workspace.Web/src/features/scrum/SprintDashboard.tsx:488` — métricas derivadas de
  `item.completedAt`, herdando o defeito de conclusão descrito em `SPEC-WORKFLOW-STATUS`.
- `tests/Prisma.Workspace.Tests/ScrumDomainTests.cs` e `ScrumApplicationTests.cs` — cobertura do modelo atual.

---

## Gaps entre o código atual e o contrato v2

Os nove gaps da v1 permanecem válidos e são renumerados abaixo, seguidos dos gaps novos introduzidos pelo N:N.

### Gaps herdados da v1

1. **Estado manual persistido:** `Sprint.Status` é alterado por `PUT /api/sprints/{id}/status`; não é calculado
   pelas datas (`Sprint.cs:16,74`).
2. **Botão manual:** a UI ainda expõe `Iniciar sprint`.
3. **Uma ativa por projeto:** domínio e handlers bloqueiam a segunda sprint ativa (`Sprint.cs:85`), contrariando
   períodos sobrepostos.
4. **Autorização fixa:** operações exigem no mínimo `ScrumMaster`, e não uma permissão configurável.
5. **Exclusão ausente:** não existe `DELETE /api/sprints/{id}` nem fluxo transacional de desvinculação.
6. **Capacidade exposta:** o dashboard mantém editor e KPI de capacidade; precisa ser homologado como
   informativo ou ocultado.
7. **Encerramento automático:** não há processamento por data nem mecanismo para exigir destino dos itens
   abertos quando a data final passa.
8. **Contrato histórico:** sprints terminais retornam pela API, mas filtros e histórico completo não foram
   homologados.
9. **D25 desatualizada:** a regra de sprint única ativa e transição manual conflita com a decisão vigente;
   `DECISIONS.md` precisa da decisão sucessora antes da implementação.

### Gaps novos do contrato N:N

10. **Sem `SprintProject`:** a entidade, a configuração EF, o índice e a FK não existem.
11. **Sem `Sprint.OrganizationId`:** o tenant da sprint é hoje derivado do projeto; a sprint não é um agregado
    de organização.
12. **Sem validação Projeto-da-tarefa ∈ Sprint:** nada impede hoje vincular uma tarefa a uma sprint de outro
    projeto além do `ProjectId` fixo.
13. **Sem gestão de projetos participantes:** não existem endpoints de adicionar/remover projeto na sprint nem
    o fluxo atômico de tratamento das tarefas ao remover um projeto.
14. **Sem segmentação por projeto nas métricas:** o dashboard consolida por sprint única de um projeto só.
15. **Sem autorização multiprojeto:** a checagem atual assume um projeto; falta avaliar todos os participantes.
16. **Sem backfill:** nenhuma migration converte `Sprint.ProjectId` em `SprintProject`.
17. **Quadro da sprint monoprojeto:** o agrupamento por `BoardId` existe, mas não há recorte por projeto.

---

## Impacto de dados e workflow

- Introduzir `SprintProject` e `Sprint.OrganizationId` é alteração de schema: exige `G-MIGRATION`.
- A cadeia de migrations é **incremental** e deve permanecer assim: a base real de produção possui dados e um
  histórico de migrations aplicadas. Consolidação de migrations só ocorre no lote final `migrations-consolidation`.
- Trocar status persistido por status calculado e remover a exclusividade de sprint ativa altera regra de
  workflow: exige `G-WORKFLOW`.
- Se a implementação alterar a estrutura ou a semântica imutável de `SprintItemSnapshot`, exige `G-HISTORY`.
  Apenas ler os snapshots existentes não aciona esse gate.
- `SprintItemSnapshot` deve passar a registrar também o `ProjectId` do item quando a sprint for multiprojeto;
  como isso altera a estrutura do snapshot, esse ponto específico **aciona `G-HISTORY`**.

---

## Contratos de API esperados

### Consulta

- `GET /api/sprints?organizationId=...&projectId=...` retorna sprints da organização, filtráveis por projeto
  participante, com estado funcional derivado e a lista de projetos participantes.
- `GET /api/sprints/{id}` inclui `projects[]` e métricas consolidadas.
- `GET /api/sprints/{id}/metrics?projectId=...` retorna a mesma métrica segmentada.

### Criação

- `POST /api/sprints` — obrigatórios `name`, `startDate`, `endDate`, `projectIds[]` (pelo menos um).
  Opcionais `goal`, `teamId`.

### Edição

- `PUT /api/sprints/{id}` altera nome, objetivo e datas mediante permissão configurável em todos os projetos.

### Projetos participantes

- `POST /api/sprints/{id}/projects` adiciona um projeto.
- `DELETE /api/sprints/{id}/projects/{projectId}` exige `openItemsAction` = `Unlink` ou `MoveToSprint` com
  `destinationSprintId` compatível. Sem esse parâmetro, responde `400` sem alterar nada.

### Exclusão

- `DELETE /api/sprints/{id}` remove `SprintId` das tarefas e exclui a sprint na mesma transação; falha em
  qualquer etapa causa rollback integral.

### Encerramento com itens abertos

- Comando recebe `ReturnToBacklog` ou `MoveToSprint`; `MoveToSprint` exige sprint destino diferente que contenha
  o projeto de cada item movido.

### Movimentação no quadro

- `POST /api/WorkItems/move` recebe `workItemId`, `destinationStageId` e `position`; não altera `SprintId` nem
  `BacklogRank`.

---

## Regras de concorrência e erro

- Planejamento, transferência de itens abertos, remoção de projeto e exclusão da sprint são transacionais.
- `409 concurrency_conflict` provoca releitura dos dados afetados, sem recarga manual da página.
- Se a intenção já estiver refletida no servidor, a UI a trata como concluída.
- Se ainda for válida, a UI preserva o diálogo/seleção e pede nova confirmação com dados atuais.
- Se deixar de ser válida, a UI encerra ou desabilita a ação e explica o motivo.
- A sprint de destino não pode ser a própria sprint e deve conter o projeto de cada tarefa transferida.

---

## Homologação manual pendente

- Criar sprint com nome, datas e dois projetos.
- Planejar tarefas de ambos os projetos e confirmar que quadro, coluna e posição não mudam.
- Tentar planejar tarefa de um terceiro projeto e confirmar a rejeição.
- Consultar métricas consolidadas e segmentadas por projeto.
- Remover um projeto com tarefas vinculadas e exercitar as duas destinações.
- Confirmar transição automática entre Planejada, Ativa e Encerrada pelas datas e ausência de `Iniciar sprint`.
- Confirmar coexistência de sprints com períodos sobrepostos.
- Excluir sprint com tarefas e verificar preservação integral do Kanban e do histórico.
- Confirmar permissão configurável avaliada em todos os projetos participantes.
- Confirmar o backfill em cópia da base real antes de aplicar em produção.

## Test Gate Mapping

- **.NET:** estado calculado por datas e fuso da organização; sobreposição; N:N `SprintProject`; rejeição de
  tarefa cujo projeto não participa; remoção de projeto com as duas destinações e rollback; autorização em
  todos os projetos; exclusão transacional preservando quadro/coluna/histórico; planejamento hierárquico
  atômico; backfill idempotente e verificável; concorrência `409`.
- **React/Vitest:** ausência do botão iniciar; seletor de projetos participantes; diálogo de remoção de projeto
  com destino obrigatório; segmentação de métricas por projeto; rollback acessível.
- **Playwright:** criar sprint multiprojeto; planejar itens de dois projetos; observar transição por data;
  manter duas sprints ativas; remover projeto com tarefas; excluir sprint com tarefas; consultar encerradas;
  mover card no quadro e recarregar.

## Human Gates para implementação

- **`G-SPEC`** — **pendente**. Esta v2 está em `draft` e não pode ser implementada antes da aprovação humana.
- **`G-MIGRATION`** — **obrigatório**. `SprintProject`, `Sprint.OrganizationId` e o backfill alteram schema.
- **`G-WORKFLOW`** — **obrigatório**. Estado calculado, fim da exclusividade de sprint ativa e a nova regra de
  elegibilidade de tarefa alteram regras de workflow.
- **`G-HISTORY`** — **obrigatório apenas** se `SprintItemSnapshot` passar a persistir `ProjectId` ou qualquer
  outro campo novo. Somente ler snapshots existentes não aciona o gate.
- **`G-SCOPE`** — necessário para qualquer requisito não coberto aqui nem em `DECISIONS.md`.
- **`G-COMPLETION`** — conforme a classificação da tarefa de implementação.

## Perguntas abertas para o PO (não decidir sem resposta)

1. **Escopo da sprint sem projeto:** uma sprint pode existir temporariamente sem nenhum projeto associado
   (ex.: recém-criada) ou o mínimo de um projeto é invariante permanente? A spec assume invariante permanente.
2. **Sprint × equipe:** com a sprint pertencendo à organização, `TeamId` continua útil como rótulo informativo
   ou deve ser removido da experiência?
3. **`Sprint.ProjectId` legado:** manter a coluna por uma release inteira como rede de segurança, ou removê-la
   na mesma migration após a verificação do backfill?
4. **Snapshot multiprojeto:** aceitar acionar `G-HISTORY` para incluir `ProjectId` em `SprintItemSnapshot`, ou
   derivar o projeto por join no `WorkItem` e manter o snapshot intacto?
5. **Segmentação de capacidade:** a capacidade manual passa a ser por pessoa e por projeto, ou permanece por
   pessoa na sprint inteira?

## Rastreabilidade

`D25` (a suceder) + `D52` + `D54` + `D58` + decisão do PO de 2026-09-08 → **decisão sucessora proposta D81 (não aprovada)**
→ `SPEC-S-003 v2` → lote `sprint-planning-v2` (`TASK-100`..`TASK-107`) → testes .NET, React e Playwright →
homologação manual.
